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

        /// <summary>Ground-blocked hexes, seeded once at Create from a
        /// freshly-intact <see cref="BattlefieldState"/> (the generated
        /// city's own buildings/water) and MUTATED from Phase 2 onward as
        /// player-built structures go up (<see cref="ApplyBuildStructure"/>)
        /// or come down (<see cref="ApplyBuildingDamage"/>) -- the field
        /// itself stays `readonly` (never reassigned), only its CONTENTS
        /// change. Known gap, flagged rather than silently ignored: a
        /// unit's already-computed path is not currently invalidated when
        /// a building newly blocks a hex mid-path (match-core has no
        /// reactive "city changed, recompute" pass yet at all -- Unity's
        /// own `RecomputeIfCityChanged` hasn't been ported here either;
        /// this extends an existing Phase 1.5 limitation, not a new
        /// one).</summary>
        private readonly HashSet<HexCoord>? _blockedToGround;

        /// <summary>Units in entity-ID allocation order -- the ONLY order
        /// this class ever iterates them in for Tick/Hash (docs/23 §0:
        /// "never by object reference or hash-set order"). A parallel
        /// dictionary gives O(1) lookup by ID for command dispatch without
        /// affecting iteration order.</summary>
        private readonly List<SimUnit> _unitsInOrder = new List<SimUnit>();
        private readonly Dictionary<uint, SimUnit> _unitsById = new Dictionary<uint, SimUnit>();

        /// <summary>Buildings in entity-ID allocation order -- same
        /// iteration-order law as <see cref="_unitsInOrder"/>, and the
        /// SAME entity-ID counter (<see cref="AllocateEntityId"/>): one
        /// unified ID space across every entity kind, not a separate
        /// counter per kind.</summary>
        private readonly List<SimBuilding> _buildingsInOrder = new List<SimBuilding>();
        private readonly Dictionary<uint, SimBuilding> _buildingsById = new Dictionary<uint, SimBuilding>();

        /// <summary>docs/23 Phase 3.5: one runtime entry per
        /// <see cref="LandmarkKind.Emitter"/> in the generated city,
        /// seeded once at <see cref="Create"/> and in a FIXED order
        /// (generation order -- <see cref="CityModel.Landmarks"/>'s own
        /// list order, which is itself deterministic) for the same
        /// never-hash-order reason every other collection in this class
        /// follows it. Emitters are pre-existing map features, not
        /// player-built, so unlike units/buildings they get no entity ID
        /// and are never looked up by one.</summary>
        private readonly List<SimEmitter> _emitters = new List<SimEmitter>();

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

            if (city != null)
                foreach (var landmark in city.Landmarks)
                    if (landmark.Kind == LandmarkKind.Emitter)
                        _emitters.Add(new SimEmitter(landmark.Site, landmark.Polarity!.Value));
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
        /// itself just to keep building. <paramref name="combat"/> (docs/23
        /// Phase 4) defaults to null -- a pure-movement unit, exactly like
        /// every unit before this phase; the caller supplies the
        /// genome-derived stat block whole (same pattern as speed/radius),
        /// match-core never derives it. Returns the new unit's entity
        /// ID.</summary>
        public uint SpawnUnit(int playerIndex, HexCoord atHex, double speed, double radius = DefaultUnitRadius, CombatStats? combat = null)
        {
            if (_city == null) throw new InvalidOperationException("MatchState has no city -- cannot spawn units");
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (speed <= 0.0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (radius <= 0.0) throw new ArgumentOutOfRangeException(nameof(radius));

            var id = AllocateEntityId();
            var (x, z) = atHex.ToWorld();
            var unit = new SimUnit(id, playerIndex, x, z, speed, radius, combat);
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

        /// <summary>docs/23 §2: place a player's HQ, Complete immediately
        /// (no build time, no cost) -- "every player starts with a themed
        /// HQ placed by the generator," not something a player commands
        /// mid-match. Setup-time API, same direct-call precedent as
        /// <see cref="SpawnUnit"/>; the caller (Unity/CityGen) picks the
        /// actual faction-appropriate landmark hex -- match-core only
        /// needs to be handed one, it doesn't do landmark selection
        /// itself. Blocks the hex like any other building.</summary>
        public uint SpawnHqForPlayer(int playerIndex, HexCoord atHex)
        {
            if (_city == null) throw new InvalidOperationException("MatchState has no city -- cannot spawn buildings");
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));

            var def = BuildingDef.Get(BuildingKind.Hq);
            var id = AllocateEntityId();
            var building = new SimBuilding(id, playerIndex, BuildingKind.Hq, atHex, def.MaxHp, def.BuildTimeTicks, completeImmediately: true);
            _buildingsInOrder.Add(building);
            _buildingsById[id] = building;
            _blockedToGround?.Add(atHex);
            return id;
        }

        public int BuildingCount => _buildingsInOrder.Count;

        /// <summary>Read-only building access in canonical (entity-ID)
        /// order -- same contract as <see cref="UnitAt"/>.</summary>
        public SimBuilding BuildingAt(int index) => _buildingsInOrder[index];

        public SimBuilding? FindBuilding(uint entityId) => _buildingsById.TryGetValue(entityId, out var b) ? b : null;

        /// <summary>docs/03: the current Lumen Cycle phase -- a pure
        /// function of <see cref="Frame"/>, nothing to seed or drift out
        /// of sync (see <see cref="LumenClock"/>'s own doc comment).</summary>
        public LumenPhase CurrentLumenPhase => LumenClock.PhaseAt(Frame);

        public int EmitterCount => _emitters.Count;

        /// <summary>Read-only emitter access in generation order -- same
        /// "fixed, never hash-order" law as <see cref="UnitAt"/>/
        /// <see cref="BuildingAt"/>.</summary>
        public SimEmitter EmitterAt(int index) => _emitters[index];

        /// <summary>Apply damage to a building (docs/23 §2's "Damaged →
        /// Destroyed (rubble hexes reopen)" staging) -- a forward-looking
        /// entry point for the combat phase that hasn't landed sim-side
        /// yet (see <see cref="SimBuilding.ApplyDamage"/>'s own doc
        /// comment). Reopens the hex the instant the building is
        /// Destroyed. Silent no-op for an unknown entity, matching every
        /// other bad-input contract in this class.</summary>
        public void ApplyBuildingDamage(uint entityId, int amount)
        {
            var building = FindBuilding(entityId);
            if (building == null) return;
            building.ApplyDamage(amount);
            if (building.State == BuildingState.Destroyed) _blockedToGround?.Remove(building.Hex);
        }

        /// <summary>docs/23 §3 Phase 3: a storage building just completed
        /// -- raise its owner's wallet cap for whatever resource
        /// `BuildingDef.StorageCapBonus` names (a no-op for every OTHER
        /// building kind, which have no bonus). Raise-only; whether
        /// destroying this building later should lower the cap back down
        /// is docs/22 §6's own unresolved Q28, not decided here (see
        /// `PlayerState.RaiseWalletCap`'s own doc comment).</summary>
        private void ApplyStorageCapBonus(SimBuilding building)
        {
            var def = BuildingDef.Get(building.Kind);
            if (def.StorageCapBonus == null) return;
            var bonus = def.StorageCapBonus.Value;
            _players[building.PlayerIndex].RaiseWalletCap(bonus.Resource, bonus.Amount);
        }

        /// <summary>docs/03 / Phase 3.5: one tick of capture logic for
        /// every emitter, reading live unit positions fresh (no cached
        /// occupancy state -- a unit that moved off the hex this very
        /// tick is already gone). For each emitter: `soleOccupant` is the
        /// one player with a unit standing on its EXACT hex, or null if
        /// the hex is empty OR units from 2+ different players are on it
        /// simultaneously (no valid single claimant either way);
        /// `contested` is true if any unit belonging to a DIFFERENT
        /// player than `soleOccupant` is anywhere within the 3-hex aura
        /// (docs/03's own radius, reused from <see cref="Landmark.
        /// EmitterAuraRadiusHexes"/> rather than a second hardcoded
        /// constant).</summary>
        private void TickEmitters()
        {
            for (var e = 0; e < _emitters.Count; e++)
            {
                var emitter = _emitters[e];
                int? soleOccupant = null;
                var multipleClaimants = false;

                for (var i = 0; i < _unitsInOrder.Count; i++)
                {
                    var u = _unitsInOrder[i];
                    var hex = HexAt(u.X, u.Z);
                    if (hex.Q != emitter.Hex.Q || hex.R != emitter.Hex.R) continue;
                    if (soleOccupant == null) soleOccupant = u.PlayerIndex;
                    else if (soleOccupant.Value != u.PlayerIndex) multipleClaimants = true;
                }
                if (multipleClaimants) soleOccupant = null;

                var contested = false;
                if (soleOccupant != null)
                {
                    for (var i = 0; i < _unitsInOrder.Count; i++)
                    {
                        var u = _unitsInOrder[i];
                        if (u.PlayerIndex == soleOccupant.Value) continue;
                        var hex = HexAt(u.X, u.Z);
                        if (hex.DistanceTo(emitter.Hex) <= Landmark.EmitterAuraRadiusHexes) { contested = true; break; }
                    }
                }

                emitter.Tick(soleOccupant, contested);
            }
        }

        /// <summary>docs/03's polarity/phase output table, granted once
        /// per simulated second to each owned emitter's controller.
        /// Un-owned emitters (docs/03: emitters start uncaptured) produce
        /// nothing until someone actually holds them.</summary>
        private void GrantEmitterManaIncome()
        {
            var phase = CurrentLumenPhase;
            for (var e = 0; e < _emitters.Count; e++)
            {
                var emitter = _emitters[e];
                if (emitter.Owner == null) continue;
                _players[emitter.Owner.Value].GrantMana(EmitterOutput(emitter.Polarity, phase));
            }
        }

        /// <summary>docs/03's "Emitter polarities &amp; output" table,
        /// verbatim: Solar peaks Day (5), Lunar peaks Night (5), Twilight
        /// peaks the transitions (6) and is otherwise flat (3) -- Solar/
        /// Lunar are flat 3 during the transition they don't favor.</summary>
        private static int EmitterOutput(EmitterPolarity polarity, LumenPhase phase)
        {
            switch (polarity)
            {
                case EmitterPolarity.Solar:
                    return phase == LumenPhase.Day ? 5 : phase == LumenPhase.Night ? 1 : 3;
                case EmitterPolarity.Lunar:
                    return phase == LumenPhase.Night ? 5 : phase == LumenPhase.Day ? 1 : 3;
                case EmitterPolarity.Twilight:
                    return phase == LumenPhase.Dusk || phase == LumenPhase.Dawn ? 6 : 3;
                default:
                    return 0;
            }
        }

        /// <summary>docs/23 Phase 4 (combat core): resolve one tick's
        /// worth of attacks, entity-ID order. A unit whose target died,
        /// wandered out of range, or isn't ready yet (cooldown) simply
        /// waits -- no chase-to-range movement this pass (see
        /// <see cref="ApplyAttackUnit"/>'s own doc comment), and a target
        /// that's out of range is NOT an error, just nothing to resolve
        /// this tick.</summary>
        private void TickCombat()
        {
            for (var i = 0; i < _unitsInOrder.Count; i++)
            {
                var attacker = _unitsInOrder[i];
                if (attacker.Order != UnitOrderKind.AttackUnit || !attacker.IsAlive) continue;
                if (!attacker.AttackTargetId.HasValue || attacker.Combat == null) continue;

                var defender = FindUnit(attacker.AttackTargetId.Value);
                if (defender == null || !defender.IsAlive) continue;   // target gone -- unit just waits, doesn't auto-idle
                if (!attacker.CanAttackNow) continue;

                var attackerHex = HexAt(attacker.X, attacker.Z);
                var defenderHex = HexAt(defender.X, defender.Z);
                var combat = attacker.Combat.Value;
                if (attackerHex.DistanceTo(defenderHex) > combat.Reach) continue;

                // posMod: melee/adjacent attacks get the real docs/04 arc
                // classification (Facing.ArcOf requires exact adjacency);
                // a Reach>=2 attacker gets a flat front-equivalent 100 --
                // docs/04 says reach>=2 attackers ignore THEIR OWN arc
                // constraint when choosing a target, but the full "still
                // classify by the target's facing at range" geometry
                // needs widening ArcOf beyond adjacency, a documented,
                // deferred gap (see SimUnit.cs's header).
                var posMod = attackerHex.DistanceTo(defenderHex) == 1
                    ? CombatMath.PosModForArc(Facing.ArcOf(attackerHex, defenderHex, defender.FacingEdge))
                    : 100;

                var inAura = IsWithinAnyEmitterAura(attackerHex);
                var emitterMod = CombatMath.EmitterModPercent(combat.Affinity, CurrentLumenPhase, inAura);

                var isCrit = CombatMath.RollCrit(_rng, combat.CunningPercent);
                var luckOrCrit = isCrit ? 150 : CombatMath.RollLuckPercent(_rng);

                var damage = CombatMath.ResolveDamage(combat.Power, posMod, emitterMod, luckOrCrit, defender.Combat!.Value.Armor);
                defender.ApplyDamage(damage, Frame);
                if (attackerHex.DistanceTo(defenderHex) == 1) attacker.FaceToward(ApproachEdgeFromTo(attackerHex, defenderHex));
                attacker.ResetAttackCooldown();
            }
        }

        /// <summary>Which of `from`'s 6 edges `to` lies beyond -- `to` must
        /// be exactly adjacent (same precondition as
        /// <see cref="Facing.ArcOf"/>, which this mirrors for the
        /// attacker's OWN post-attack facing update rather than the
        /// defender's arc classification).</summary>
        private static HexEdge ApproachEdgeFromTo(HexCoord from, HexCoord to)
        {
            for (var e = 0; e < 6; e++)
                if (from.Neighbor((HexEdge)e).Equals(to)) return (HexEdge)e;
            return HexEdge.E;   // unreachable given the adjacency check above
        }

        /// <summary>True if `hex` is within ANY emitter's aura radius,
        /// regardless of that emitter's owner (docs/03: "auras affect all
        /// monsters by phase/affinity regardless of owner"). Auras don't
        /// stack (docs/03), and since <see cref="CombatMath.
        /// EmitterModPercent"/>'s output only depends on (affinity, phase,
        /// in-any-aura) -- never on WHICH aura -- a plain boolean is the
        /// whole answer; there's nothing to pick the "strongest" among.</summary>
        private bool IsWithinAnyEmitterAura(HexCoord hex)
        {
            for (var e = 0; e < _emitters.Count; e++)
                if (hex.DistanceTo(_emitters[e].Hex) <= Landmark.EmitterAuraRadiusHexes) return true;
            return false;
        }

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

            // docs/23 Phase 4 (combat core, docs/04): attack resolution,
            // entity-ID order, same law as movement above.
            TickCombat();

            // docs/23 §5 / docs/27 Phase C: separation, restoring the same
            // "never actually overlap" guarantee legacy (non-sim-driven)
            // units already have via RuntimeCityBuilder.ApplySeparation.
            // Runs on every unit regardless of Order (Idle included),
            // matching that existing call's own unconditional behavior.
            ApplySeparationPass();

            // docs/23 §2 Phase 2: construction progress. Entity-ID order,
            // same law as the unit loop above. docs/23 §3 Phase 3: the
            // instant a building finishes (UnderConstruction -> Complete,
            // detected here rather than inside SimBuilding itself, which
            // stays unaware of economy concerns the same way SimUnit stays
            // unaware of pathfinding), its wallet-cap bonus (if any)
            // applies -- once, not every tick it stays Complete.
            for (var i = 0; i < _buildingsInOrder.Count; i++)
            {
                var b = _buildingsInOrder[i];
                var wasComplete = b.State == BuildingState.Complete;
                b.Tick();
                if (!wasComplete && b.State == BuildingState.Complete) ApplyStorageCapBonus(b);
            }

            // docs/23 §13 amendment B (Phase 3.5): emitter capture, entity
            // order (generation order, fixed -- same law as everything
            // else). Reads live unit positions fresh every tick; capture
            // is automatic (docs/03), never a Command.
            TickEmitters();

            // Economy income (Blood/Fuel/Ichor/etc.) and upkeep drains,
            // combat resolution, and everything else arrive with their
            // own phases (docs/23 §13-A porting workstream) -- gated on
            // prerequisites that haven't landed yet (Citizens as sim
            // entities, genome-linked per-unit cost data, the FuelNodes
            // generator; see docs/12's Phase 3 entry). The frame advance
            // itself is the deterministic heartbeat every system hangs
            // off.
            Frame++;

            // docs/03 / Phase 3.5 mana income: once per simulated SECOND
            // (docs/03's table is already whole mana/second, so granting
            // it once every 10 completed ticks is exact, not a
            // fractional approximation), checked on the POST-increment
            // Frame so it fires on the 10th/20th/30th... tick.
            if (Frame % TicksPerSecond == 0) GrantEmitterManaIncome();
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
                case CommandKind.BuildStructure:
                    ApplyBuildStructure(cmd);
                    break;
                case CommandKind.AttackUnit:
                    ApplyAttackUnit(cmd);
                    break;
                case CommandKind.None:
                default:
                    break;
            }
        }

        /// <summary>docs/23 Phase 4: TargetEntity begins attacking ArgA.
        /// No chase-to-range movement (docs/12's Phase 4 entry) -- both
        /// units must already exist, be alive, and be within the
        /// attacker's Reach hexes of each other, or this is a silent
        /// no-op, matching every other command's bad-input contract.</summary>
        private void ApplyAttackUnit(Command cmd)
        {
            var attacker = FindUnit(cmd.TargetEntity);
            var defender = FindUnit(unchecked((uint)cmd.ArgA));
            if (attacker == null || defender == null) return;
            // a unit with no Combat stats at all isn't a combatant --
            // can't attack, and can't BE attacked either (there's no
            // Armor/Vitality to resolve damage against).
            if (attacker.Combat == null || defender.Combat == null) return;
            if (!attacker.IsAlive || !defender.IsAlive) return;
            if (ReferenceEquals(attacker, defender)) return;

            var fromHex = HexAt(attacker.X, attacker.Z);
            var toHex = HexAt(defender.X, defender.Z);
            if (fromHex.DistanceTo(toHex) > attacker.Combat.Value.Reach) return;

            attacker.BeginAttacking(defender.EntityId);
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

        /// <summary>docs/23 §2 Phase 2: place a new building at hex
        /// (ArgA, ArgB), building kind decoded from TargetEntity (see
        /// <see cref="CommandKind.BuildStructure"/>'s own doc comment for
        /// why that slot is repurposed here). Validates, in order: a real
        /// buildable kind (never <see cref="BuildingKind.Hq"/>), an
        /// on-map and currently-unblocked hex, and full affordability of
        /// EVERY resource line in the building's cost -- checked BEFORE
        /// debiting any of them, so a multi-resource cost is all-or-
        /// nothing (never a partial spend on an unaffordable build).
        /// Silent no-op on any failure, matching every other command's
        /// bad-input contract.</summary>
        private void ApplyBuildStructure(Command cmd)
        {
            if (_city == null || _blockedToGround == null) return;

            var kind = (BuildingKind)cmd.TargetEntity;
            if (kind == BuildingKind.Hq || (int)kind < 0 || (int)kind >= BuildingDef.AllDefs.Count) return;
            if (cmd.PlayerIndex < 0 || cmd.PlayerIndex >= _players.Length) return;

            var hex = new HexCoord(cmd.ArgA, cmd.ArgB);
            if (!_city.Contains(hex) || _blockedToGround.Contains(hex)) return;

            var def = BuildingDef.Get(kind);
            var player = _players[cmd.PlayerIndex];
            foreach (var (resource, amount) in def.Cost)
                if (player.Wallet(resource) < amount) return;   // unaffordable -- reject before spending anything

            foreach (var (resource, amount) in def.Cost) player.TrySpend(resource, amount);

            var id = AllocateEntityId();
            var building = new SimBuilding(id, cmd.PlayerIndex, kind, hex, def.MaxHp, def.BuildTimeTicks, completeImmediately: false);
            _buildingsInOrder.Add(building);
            _buildingsById[id] = building;
            _blockedToGround.Add(hex);
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
            h.Add(_buildingsInOrder.Count);
            for (var i = 0; i < _buildingsInOrder.Count; i++) _buildingsInOrder[i].WriteTo(h);
            h.Add(_emitters.Count);
            for (var i = 0; i < _emitters.Count; i++) _emitters[i].WriteTo(h);
            return h.Value;
        }
    }
}
