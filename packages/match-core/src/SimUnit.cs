using System;
using System.Collections.Generic;
using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §13 amendment A (Phase 1.5): a unit's order state.
    /// Deliberately just the two states the "movement + order state
    /// machine" acceptance bar needs -- Idle and MoveTo. Every later phase
    /// adds the order kinds its own slice needs (AttackUnit, Harvest,
    /// SpecialAttack, ...), the same way Unity's MonsterAgent.OrderKind
    /// grew one enum value per feature; this is that enum's sim-side
    /// twin, ported one state at a time rather than all at once.</summary>
    public enum UnitOrderKind
    {
        Idle = 0,
        MoveTo = 1,
    }

    /// <summary>
    /// One deterministic battlefield entity's movement + order state
    /// (docs/23 §13-A: the first slice of the sim-porting workstream).
    /// Position is a double (X, Z) world-space pair -- not fixed-point --
    /// but every operation on it is restricted to the §0 float-discipline
    /// allow-list (+, -, *, /, sqrt; no Sin/Cos/Atan2/Exp in the tick
    /// path), and it is hashed bitwise via <see cref="FnvHash.AddBits"/>,
    /// never through ToString/JSON.
    ///
    /// This is the sim-side analog of Unity's <c>MonsterAgent</c> order
    /// state machine (docs/23 CLAUDE.md: "the unit sim is being ported
    /// here out of the frame-driven Unity MonoBehaviours... do not add
    /// gameplay decisions to MonsterAgent.Update()") -- but it does NOT
    /// yet replace MonsterAgent in Unity. That view-layer rewrite
    /// ("MonsterAgent renders interpolated sim state only, no
    /// Time.deltaTime-driven gameplay left") is Phase 1.5's other half
    /// and is deliberately NOT attempted in this same pass: gutting a
    /// ~950-line file that ten already-shipped phases of combat/economy/
    /// special-attacks logic depend on, with no Unity Editor available to
    /// visually verify the result, is exactly the kind of large,
    /// hard-to-verify change this project's "never claim visual
    /// verification" discipline warns against doing blind. Flagged here
    /// and in docs/12/docs/23, not hidden.
    /// </summary>
    public sealed class SimUnit
    {
        public uint EntityId { get; }
        public int PlayerIndex { get; }
        public double Speed { get; }   // m/s, IEEE-exact ops only

        /// <summary>docs/27 Phase C: body half-width for
        /// <see cref="Flocking.Separate"/> -- the sim-side twin of
        /// Unity's <c>UnitCombat.Radius</c>. Fixed for the unit's whole
        /// lifetime (no genome/growth system sim-side yet), so it's set
        /// once at spawn, not mutated by Tick.</summary>
        public double Radius { get; }

        public double X { get; private set; }
        public double Z { get; private set; }
        public UnitOrderKind Order { get; private set; } = UnitOrderKind.Idle;

        private List<HexCoord>? _path;
        private int _pathIndex;

        /// <summary>docs/27 Phase B: waypoints queued behind the CURRENT
        /// path, walked one at a time in FIFO order once each leg
        /// completes -- the sim-side twin of Unity's `_waypoints` queue
        /// on `MonsterAgent`. `Queue&lt;T&gt;` enumerates in insertion
        /// order (a documented guarantee, unlike Dictionary/HashSet), so
        /// hashing it is safe and deterministic. `MatchState` (not
        /// `SimUnit`) computes the actual path for a dequeued waypoint --
        /// this class stays pathfinding-agnostic, matching how it never
        /// pathfound for `SetPath` either.</summary>
        private readonly Queue<HexCoord> _waypointQueue = new Queue<HexCoord>();

        internal SimUnit(uint entityId, int playerIndex, double x, double z, double speed, double radius)
        {
            EntityId = entityId;
            PlayerIndex = playerIndex;
            X = x;
            Z = z;
            Speed = speed;
            Radius = radius;
        }

        /// <summary>Begin walking a precomputed path (HexPathfinder output,
        /// already deterministic and blocked-set-aware). An empty/null
        /// path is treated as "arrived already" -- Idle, no-op.</summary>
        internal void SetPath(List<HexCoord>? path)
        {
            _path = path != null && path.Count > 0 ? path : null;
            _pathIndex = 0;
            Order = _path != null ? UnitOrderKind.MoveTo : UnitOrderKind.Idle;
        }

        /// <summary>Advance one fixed tick's worth of movement. Consumes
        /// this tick's whole speed*dt budget across as many path nodes as
        /// it covers (never leaves a fractional-tick's motion on the
        /// table) so results don't depend on how finely a path happens to
        /// be subdivided relative to speed -- same idiom as Unity's
        /// FollowPath, ported to fixed dt instead of Time.deltaTime.</summary>
        internal void Tick(double dt)
        {
            if (Order != UnitOrderKind.MoveTo || _path == null) return;

            var budget = Speed * dt;
            while (budget > 0.0 && Order == UnitOrderKind.MoveTo)
            {
                if (_path == null || _pathIndex >= _path.Count)
                {
                    Order = UnitOrderKind.Idle;
                    _path = null;
                    break;
                }

                var (tx, tz) = _path[_pathIndex].ToWorld();
                var dx = tx - X;
                var dz = tz - Z;
                var dist = Math.Sqrt(dx * dx + dz * dz);

                if (dist <= budget)
                {
                    X = tx;
                    Z = tz;
                    budget -= dist;
                    _pathIndex++;
                    if (_pathIndex >= _path.Count)
                    {
                        Order = UnitOrderKind.Idle;
                        _path = null;
                    }
                }
                else
                {
                    if (dist > 1e-12)
                    {
                        X += dx / dist * budget;
                        Z += dz / dist * budget;
                    }
                    budget = 0.0;
                }
            }
        }

        /// <summary>Append a waypoint to walk once the current path (and
        /// every waypoint already queued ahead of it) is finished --
        /// never replaces anything already in flight. If this unit is
        /// currently Idle, it does NOT start moving on its own: the
        /// caller (`MatchState.ApplyMoveQueue`) is responsible for
        /// dequeuing immediately in that case, since starting a path
        /// requires pathfinding this class deliberately doesn't
        /// do.</summary>
        internal void EnqueueWaypoint(HexCoord hex) => _waypointQueue.Enqueue(hex);

        /// <summary>Drop every not-yet-started queued waypoint -- called
        /// when a REPLACE-style move (MoveTo) overrides whatever this
        /// unit was doing, same as Unity's `_waypoints.Clear()` on a
        /// non-queued OrderMove.</summary>
        internal void ClearWaypoints() => _waypointQueue.Clear();

        internal bool HasQueuedWaypoints => _waypointQueue.Count > 0;

        internal HexCoord DequeueWaypoint() => _waypointQueue.Dequeue();

        /// <summary>docs/27 Phase C: apply one tick's worth of separation
        /// nudge, computed by <see cref="MatchState"/> (which owns the
        /// blocked-hex clamp and the other units' current positions) via
        /// <see cref="Flocking.Separate"/>. A plain position add -- this
        /// class stays unaware of neighbours, radii of OTHER units, or the
        /// blocked-hex set, matching how it's never known about
        /// pathfinding either (`MatchState` computes, `SimUnit`
        /// applies).</summary>
        internal void ApplySeparationOffset(double dx, double dz)
        {
            X += dx;
            Z += dz;
        }

        /// <summary>Append this unit's canonical bytes, FIXED field order
        /// (docs/23 §13-J), floats bitwise. The waypoint queue is part of
        /// this unit's real state -- two clients that disagree on what's
        /// still queued (even before any of it has been walked) must
        /// hash differently, or a desync there would go undetected until
        /// it visibly manifested many ticks later.</summary>
        public void WriteTo(FnvHash h)
        {
            h.Add(EntityId);
            h.Add(PlayerIndex);
            h.AddBits(X);
            h.AddBits(Z);
            h.AddBits(Speed);
            h.AddBits(Radius);
            h.Add((int)Order);
            h.Add(_pathIndex);
            h.Add(_waypointQueue.Count);
            foreach (var w in _waypointQueue) { h.Add(w.Q); h.Add(w.R); }
        }
    }
}
