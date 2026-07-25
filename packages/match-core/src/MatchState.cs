using System;
using System.Collections.Generic;
using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>
    /// The whole match's deterministic simulation state and its fixed-tick
    /// advance function -- the pure `(seed, command-stream) → state` object
    /// docs/23 §11 lockstep 4v4 is built on. Phase 1 shipped the SKELETON
    /// (players, frame counter, entity-ID allocator, RNG state); Phase 1.5
    /// (docs/23 §13-A) adds the first sim-ported gameplay slice --
    /// deterministic unit movement (<see cref="SimUnit"/>) driven by the
    /// SAME <c>HexPathfinder</c>/<c>BattlefieldState</c> citygen-core
    /// already uses, so pathing behaves identically to the (still
    /// Unity-side) live game. Buildings, economy income, and combat are
    /// still ported in by later phases. What matters throughout: one
    /// seeded stream, integer/bitwise-hashed state, a canonical
    /// <see cref="Hash"/>, and a tick that is a pure function of its
    /// inputs.
    ///
    /// Sim rate is fixed (docs/23 §11: 10 ticks/s); this object is
    /// oblivious to wall-clock -- a caller (the relay-driven NetDriver, or
    /// a headless test harness) calls Tick once per simulated tick with
    /// that tick's merged command bundle.
    /// </summary>
    public sealed class MatchState
    {
        public const int TicksPerSecond = 10;
        public const int DefaultSupplyCap = 60;   // docs/23 §13-E
        private const double DtSeconds = 1.0 / TicksPerSecond;

        /// <summary>docs/27 Phase C: fallback body radius for a spawn call
        /// that doesn't supply one -- matches Unity's own
        /// <c>UnitCombat.Radius</c> default (1.5f) so a caller that never
        /// widens its own call site (every existing test, `Ping`-only
        /// callers) gets the same separation behavior it would have gotten
        /// with an explicit "generic monster" radius.</summary>
        public const double DefaultUnitRadius = 1.5;

        /// <summary>docs/27 Phase C: extra clearance (meters) separation
        /// keeps on top of two units' own combined radii -- matches
        /// Unity's `RuntimeCityBuilder.groupSpacing` Inspector default
        /// (1f), a v0.1 placeholder like every other tuning number in this
        /// project (docs/11).</summary>
        private const double SeparationSpacing = 1.0;

        private readonly PlayerState[] _players;
        private readonly SimRng _rng;

        /// <summary>The city units path over, or null for a match that
        /// hasn't wired one up yet (e.g. the Phase 1 empty-match tests).
        /// Generation is citygen-core's own deterministic responsibility
        /// (docs/18); this class only consumes it for pathfinding.</summary>
        private readonly CityModel? _city;

        /// <summary>Ground-blocked hexes, computed once at Create from a
        /// freshly-intact <see cref="BattlefieldState"/> (docs/23 Phase 2
        /// will need this to change as buildings are built/destroyed --
        /// out of scope for the movement-only Phase 1.5 slice).</summary>
        private readonly HashSet<HexCoord>? _blockedToGround;

        /// <summary>Units in entity-ID allocation order -- the ONLY order
        /// this class ever iterates them in for Tick/Hash (docs/23 §0:
        /// "never by object reference or hash-set order"). A parallel
        /// dictionary gives O(1) lookup by ID for command dispatch without
        /// affecting iteration order.</summary>
        private readonly List<SimUnit> _unitsInOrder = new List<SimUnit>();
        private readonly Dictionary<uint, SimUnit> _unitsById = new Dictionary<uint, SimUnit>();

        /// <summary>The tick this state is AT -- 0 before the first Tick.</summary>
        public int Frame { get; private set; }

        /// <summary>Monotonic entity-ID source. IDs are never reused within
        /// a match, are allocated only inside Tick (so allocation order is
        /// part of the deterministic stream), and start at 1 -- 0 is the
        /// "no entity" sentinel (Command.TargetEntity == 0).</summary>
        private uint _nextEntityId = 1;

        /// <summary>Total commands the sim has consumed -- a cheap witness
        /// that the command pipeline is being driven identically on two
        /// clients (part of the hash).</summary>
        public long CommandsProcessed { get; private set; }

        private MatchState(PlayerState[] players, SimRng rng, CityModel? city)
        {
            _players = players;
            _rng = rng;
            _city = city;
            _blockedToGround = city != null ? BattlefieldState.FreshFrom(city).BlockedToGround() : null;
        }

        /// <summary>Start a fresh match. <paramref name="factions"/> is one
        /// entry per player slot (2..8 for 1v1..4v4). Seed drives the whole
        /// match's RNG. <paramref name="city"/> is optional -- a match with
        /// no city (e.g. Phase 1's empty-match determinism proof) simply
        /// can't spawn/path units yet.</summary>
        public static MatchState Create(uint seed, IReadOnlyList<FactionId> factions, CityModel? city = null)
        {
            if (factions == null) throw new ArgumentNullException(nameof(factions));
            if (factions.Count < 2 || factions.Count > 8)
                throw new ArgumentOutOfRangeException(nameof(factions), "2..8 players (1v1..4v4)");

            var players = new PlayerState[factions.Count];
            for (var i = 0; i < factions.Count; i++)
                players[i] = new PlayerState(i, factions[i], DefaultSupplyCap);

            return new MatchState(players, new SimRng(seed), city);
        }

        public int PlayerCount => _players.Length;
        public PlayerState Player(int index) => _players[index];

        /// <summary>Allocate the next entity ID. Sim-internal only (later
        /// phases call this when spawning); exposed for the Phase-1 test
        /// that pins allocation determinism.</summary>
        public uint AllocateEntityId() => _nextEntityId++;

        /// <summary>Spawn a unit at a hex, deterministically. Setup-time
        /// API (matches <see cref="AllocateEntityId"/>'s existing
        /// direct-call precedent) rather than a Command -- mid-match
        /// production (Factories, docs/23 §2/§6) becomes its own command
        /// kind when that phase lands; spawning at match start is not a
        /// player order to replay. Requires a city (see
        /// <see cref="Create"/>). <paramref name="radius"/> defaults to
        /// <see cref="DefaultUnitRadius"/> (docs/27 Phase C) -- every call
        /// site that predates separation keeps compiling and gets a
        /// reasonable generic body size rather than being forced to widen
        /// itself just to keep building. Returns the new unit's entity
        /// ID.</summary>
        public uint SpawnUnit(int playerIndex, HexCoord atHex, double speed, double radius = DefaultUnitRadius)
        {
            if (_city == null) throw new InvalidOperationException("MatchState has no city -- cannot spawn units");
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (speed <= 0.0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (radius <= 0.0) throw new ArgumentOutOfRangeException(nameof(radius));

            var id = AllocateEntityId();
            var (x, z) = atHex.ToWorld();
            var unit = new SimUnit(id, playerIndex, x, z, speed, radius);
            _unitsInOrder.Add(unit);
            _unitsById[id] = unit;
            return id;
        }

        public int UnitCount => _unitsInOrder.Count;

        /// <summary>Read-only unit access in canonical (entity-ID) order --
        /// for tests/harnesses/the future Unity render layer. Never expose
        /// a mutable reference chain gameplay could use to bypass the
        /// Command pipeline (docs/23 §13-J: orders target entity IDs).</summary>
        public SimUnit UnitAt(int index) => _unitsInOrder[index];

        public SimUnit? FindUnit(uint entityId) => _unitsById.TryGetValue(entityId, out var u) ? u : null;

        /// <summary>Advance the simulation by exactly one tick, applying
        /// this tick's commands. Pure function of (current state,
        /// commands): no wall-clock, no ambient randomness -- every draw
        /// comes from <see cref="_rng"/>. Command order within the bundle
        /// is the caller's responsibility to make canonical (the relay
        /// sorts by player index); this method consumes them in the given
        /// order.</summary>
        public void Tick(IReadOnlyList<Command>? commands)
        {
            if (commands != null)
            {
                foreach (var cmd in commands)
                {
                    ApplyCommand(cmd);
                    CommandsProcessed++;
                }
            }

            // docs/23 §13-A: unit movement is the first per-tick system.
            // Iterated strictly in entity-ID order -- see _unitsInOrder's
            // own doc comment for why that's load-bearing, not cosmetic.
            for (var i = 0; i < _unitsInOrder.Count; i++)
            {
                var u = _unitsInOrder[i];
                u.Tick(DtSeconds);
                // docs/27 Phase B: a leg just finished (Idle) with more
                // waypoints queued behind it -- compute the next leg's
                // path immediately, in the SAME tick, so a multi-waypoint
                // walk never idles for even one tick between legs.
                if (u.Order == UnitOrderKind.Idle && u.HasQueuedWaypoints) AdvanceToNextWaypoint(u);
            }

            // docs/23 §5 / docs/27 Phase C: separation, restoring the same
            // "never actually overlap" guarantee legacy (non-sim-driven)
            // units already have via RuntimeCityBuilder.ApplySeparation.
            // Runs on every unit regardless of Order (Idle included),
            // matching that existing call's own unconditional behavior.
            ApplySeparationPass();

            // Economy income, combat resolution, and everything else
            // arrive with their own phases (docs/23 §13-A porting
            // workstream). The frame advance itself is the deterministic
            // heartbeat every system hangs off.
            Frame++;
        }

        private void ApplyCommand(Command cmd)
        {
            switch (cmd.Kind)
            {
                case CommandKind.Ping:
                    // Phase-1 placeholder: proves the command path is driven
                    // deterministically. CommandsProcessed (bumped by the
                    // caller loop) is the only observable effect.
                    break;
                case CommandKind.MoveTo:
                    ApplyMoveTo(cmd);
                    break;
                case CommandKind.MoveQueue:
                    ApplyMoveQueue(cmd);
                    break;
                case CommandKind.None:
                default:
                    break;
            }
        }

        /// <summary>docs/23 §13-A: order TargetEntity to walk to hex
        /// (ArgA, ArgB). Silently a no-op for an unknown entity, an
        /// off-map/blocked destination, or an unreachable one -- exactly
        /// like Unity's `ComputePath` returning null today (docs/12: "an
        /// unreachable destination... wait rather than pretend"), not an
        /// exception, since a desynced/late command must never crash the
        /// sim on one client and not another.</summary>
        private void ApplyMoveTo(Command cmd)
        {
            if (_city == null || _blockedToGround == null) return;
            var unit = FindUnit(cmd.TargetEntity);
            if (unit == null) return;

            // docs/27 Phase B: a REPLACE-style move drops any waypoints
            // already queued, matching Unity's own `!queue` OrderMove
            // clearing `_waypoints` -- a plain (non-shift) click always
            // means "forget what I was doing, go here instead."
            unit.ClearWaypoints();
            var start = HexAt(unit.X, unit.Z);
            var goal = new HexCoord(cmd.ArgA, cmd.ArgB);
            var path = HexPathfinder.FindPath(start, goal, _city, _blockedToGround);
            unit.SetPath(path);
        }

        /// <summary>docs/27 Phase B: append hex (ArgA, ArgB) to
        /// TargetEntity's waypoint queue -- the sim-side twin of a SHIFT
        /// ground-click. If the unit is currently Idle, starts walking
        /// there immediately (nothing to queue BEHIND, since nothing's in
        /// flight); otherwise the waypoint waits for
        /// <see cref="AdvanceToNextWaypoint"/> to pick it up once the
        /// current leg (and everything queued ahead of it) finishes.
        /// Same silent-no-op-on-bad-input contract as
        /// <see cref="ApplyMoveTo"/>.</summary>
        private void ApplyMoveQueue(Command cmd)
        {
            if (_city == null || _blockedToGround == null) return;
            var unit = FindUnit(cmd.TargetEntity);
            if (unit == null) return;

            var hex = new HexCoord(cmd.ArgA, cmd.ArgB);
            if (unit.Order == UnitOrderKind.Idle)
            {
                var start = HexAt(unit.X, unit.Z);
                var path = HexPathfinder.FindPath(start, hex, _city, _blockedToGround);
                unit.SetPath(path);
            }
            else
            {
                unit.EnqueueWaypoint(hex);
            }
        }

        /// <summary>docs/27 Phase B: a unit just went Idle with more
        /// waypoints queued -- dequeue the next one and start walking to
        /// it, in the SAME tick the previous leg finished (never an idle
        /// tick between legs). If the computed path is null (unreachable/
        /// off-map), the unit simply stays Idle and the NEXT queued
        /// waypoint (if any) is tried on a LATER tick's own Idle+queued
        /// check -- one bad waypoint in a queue doesn't stall the rest of
        /// it forever, since this runs again every tick the unit is Idle
        /// with a non-empty queue.</summary>
        private void AdvanceToNextWaypoint(SimUnit unit)
        {
            if (_city == null || _blockedToGround == null) return;
            var next = unit.DequeueWaypoint();
            var start = HexAt(unit.X, unit.Z);
            var path = HexPathfinder.FindPath(start, next, _city, _blockedToGround);
            unit.SetPath(path);
        }

        /// <summary>docs/23 §5 / docs/27 Phase C: one tick's separation
        /// correction across every spawned unit, entity-ID order (the ONLY
        /// order this class ever iterates in -- see
        /// <see cref="_unitsInOrder"/>'s own doc comment). For each unit,
        /// <see cref="Flocking.Separate"/> computes the net push against
        /// every OTHER unit's CURRENT position (already-nudged, for units
        /// earlier in this same pass -- the identical cumulative-per-call
        /// idiom <c>MonsterSteeringController.SeparationForce</c> uses
        /// within a single unit's own neighbour loop, extended here across
        /// units within one tick). A nudge that would land the unit in an
        /// off-map or blocked hex is rejected outright rather than
        /// clamped to the boundary (docs/23 §5's acceptance bar: "blocked-
        /// hex clamp never violated across 10k random steps") -- simpler
        /// and just as correct as a partial slide, since separation gets
        /// another full attempt next tick regardless.</summary>
        private void ApplySeparationPass()
        {
            if (_city == null || _blockedToGround == null || _unitsInOrder.Count < 2) return;

            var neighbors = new List<Flocking.Neighbor>(_unitsInOrder.Count - 1);
            for (var i = 0; i < _unitsInOrder.Count; i++)
            {
                var self = _unitsInOrder[i];
                neighbors.Clear();
                for (var j = 0; j < _unitsInOrder.Count; j++)
                {
                    if (j == i) continue;
                    var other = _unitsInOrder[j];
                    neighbors.Add(new Flocking.Neighbor(other.X, other.Z, other.Radius));
                }

                var (dx, dz) = Flocking.Separate(self.X, self.Z, self.Radius, neighbors, SeparationSpacing);
                if (dx == 0.0 && dz == 0.0) continue;

                var nx = self.X + dx;
                var nz = self.Z + dz;
                var hex = HexAt(nx, nz);
                if (!_city.Contains(hex) || _blockedToGround.Contains(hex)) continue;   // reject: would clip a building/off-map

                self.ApplySeparationOffset(dx, dz);
            }
        }

        /// <summary>Nearest hex to a world position -- inverse of
        /// <see cref="HexCoord.ToWorld"/>. Good enough for "which hex is
        /// this unit standing in right now" (a handful of candidates
        /// around the flat-index guess); Phase 1.5 doesn't need this to be
        /// more than correct, since it only runs once per MoveTo command,
        /// never per tick.</summary>
        private static HexCoord HexAt(double x, double z)
        {
            var size = HexCoord.HexMeters / Math.Sqrt(3);
            var approxR = z / (1.5 * size);
            var rGuess = (int)Math.Round(approxR);
            var best = new HexCoord(0, 0);
            var bestDistSq = double.MaxValue;
            for (var r = rGuess - 1; r <= rGuess + 1; r++)
            {
                var approxQ = (x / size - Math.Sqrt(3) / 2 * r) / Math.Sqrt(3);
                var qGuess = (int)Math.Round(approxQ);
                for (var q = qGuess - 1; q <= qGuess + 1; q++)
                {
                    var candidate = new HexCoord(q, r);
                    var (cx, cz) = candidate.ToWorld();
                    var dx = cx - x;
                    var dz = cz - z;
                    var distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq) { bestDistSq = distSq; best = candidate; }
                }
            }
            return best;
        }

        /// <summary>Canonical 64-bit digest of the entire simulation state,
        /// in a FIXED field order (docs/23 §13-J). Two clients in the same
        /// state produce the same value; the relay compares these every N
        /// ticks to detect desync (docs/23 §11).</summary>
        public ulong Hash()
        {
            var h = new FnvHash();
            h.Add(Frame);
            h.Add((long)CommandsProcessed);
            h.Add(_nextEntityId);
            // RNG position is state -- two clients that have drawn a
            // different number of values MUST hash differently
            h.Add(_rng.StateA);
            h.Add(_rng.StateB);
            h.Add(_rng.StateC);
            h.Add(_rng.StateD);
            h.Add(_players.Length);
            foreach (var p in _players) p.WriteTo(h);
            h.Add(_unitsInOrder.Count);
            for (var i = 0; i < _unitsInOrder.Count; i++) _unitsInOrder[i].WriteTo(h);
            return h.Value;
        }
    }
}
