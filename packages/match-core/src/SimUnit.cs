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

        public double X { get; private set; }
        public double Z { get; private set; }
        public UnitOrderKind Order { get; private set; } = UnitOrderKind.Idle;

        private List<HexCoord>? _path;
        private int _pathIndex;

        internal SimUnit(uint entityId, int playerIndex, double x, double z, double speed)
        {
            EntityId = entityId;
            PlayerIndex = playerIndex;
            X = x;
            Z = z;
            Speed = speed;
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

        /// <summary>Append this unit's canonical bytes, FIXED field order
        /// (docs/23 §13-J), floats bitwise.</summary>
        public void WriteTo(FnvHash h)
        {
            h.Add(EntityId);
            h.Add(PlayerIndex);
            h.AddBits(X);
            h.AddBits(Z);
            h.AddBits(Speed);
            h.Add((int)Order);
            h.Add(_pathIndex);
        }
    }
}
