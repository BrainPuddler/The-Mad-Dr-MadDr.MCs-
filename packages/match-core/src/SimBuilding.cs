using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §2 Phase 2: a building's lifecycle state --
    /// "ghost → under-construction → complete → Damaged → Destroyed."
    /// `Ghost` (the pre-commit placement cursor) is deliberately NOT a
    /// sim-side state: it's a Unity-only preview before a
    /// `CommandKind.BuildStructure` is ever issued, so the sim never
    /// observes it -- a building doesn't exist here until the command
    /// lands. `Damaged` is likewise not a distinct persisted state
    /// (docs/18 §3: "Intact → Damaged (≤50% HP)... a visual state") --
    /// derived from <see cref="SimBuilding.IsDamaged"/> instead of forking
    /// the state machine for a value that's a pure function of HP.</summary>
    public enum BuildingState
    {
        UnderConstruction = 0,
        Complete = 1,
        Destroyed = 2,
    }

    /// <summary>One deterministic base/structure entity (docs/23 §2's
    /// Phase 2 tasks: "placement validation... construction lifecycle").
    /// Sim-side twin of Unity's eventual `BaseDresser`-driven building
    /// instance, the same relationship <see cref="SimUnit"/> has to
    /// `MonsterAgent`. Footprint is a single hex for this v0.1 slice --
    /// docs/18's generated buildings have real multi-hex footprints, but
    /// matching that here is real added scope (footprint-aware placement
    /// validation, multi-hex blocking) deliberately deferred, flagged
    /// rather than guessed at.</summary>
    public sealed class SimBuilding
    {
        public uint EntityId { get; }
        public int PlayerIndex { get; }
        public BuildingKind Kind { get; }
        public HexCoord Hex { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public BuildingState State { get; private set; }

        private int _ticksUntilComplete;

        /// <summary>docs/18 §3: ≤50% HP reads as Damaged -- a pure
        /// function of current HP, not its own state, and only
        /// meaningful once actually built (a building under construction
        /// isn't "damaged," it's unfinished).</summary>
        public bool IsDamaged => State == BuildingState.Complete && Hp * 2 <= MaxHp;

        internal SimBuilding(uint entityId, int playerIndex, BuildingKind kind, HexCoord hex,
            int maxHp, int buildTimeTicks, bool completeImmediately)
        {
            EntityId = entityId;
            PlayerIndex = playerIndex;
            Kind = kind;
            Hex = hex;
            MaxHp = maxHp;
            Hp = maxHp;
            if (completeImmediately)
            {
                State = BuildingState.Complete;
                _ticksUntilComplete = 0;
            }
            else
            {
                State = BuildingState.UnderConstruction;
                _ticksUntilComplete = buildTimeTicks;
            }
        }

        /// <summary>One tick's worth of construction progress. A no-op
        /// once Complete or Destroyed -- construction never resumes after
        /// destruction (matching docs/23 §2's staging: destruction is
        /// terminal for this building instance, a replacement is a fresh
        /// BuildStructure command, not a repair of this one).</summary>
        internal void Tick()
        {
            if (State != BuildingState.UnderConstruction) return;
            if (_ticksUntilComplete > 0) _ticksUntilComplete--;
            if (_ticksUntilComplete <= 0) State = BuildingState.Complete;
        }

        /// <summary>Apply damage (a forward-looking hook for the combat
        /// phase docs/23 §13-C hasn't landed sim-side yet -- directly
        /// callable now so Phase 2's own "destruction reopens the hex"
        /// acceptance bar is testable without waiting on full combat
        /// resolution, the same "expose the entry point early, wire the
        /// caller later" precedent <see cref="MatchState.SpawnUnit"/> set
        /// for pre-command setup APIs). Clamped at 0, never negative;
        /// reaching 0 is terminal (Destroyed), regardless of construction
        /// state -- an unfinished building can be destroyed too.</summary>
        internal void ApplyDamage(int amount)
        {
            if (State == BuildingState.Destroyed || amount <= 0) return;
            Hp -= amount;
            if (Hp <= 0)
            {
                Hp = 0;
                State = BuildingState.Destroyed;
            }
        }

        /// <summary>Append this building's canonical bytes, FIXED field
        /// order (docs/23 §13-J), matching <see cref="SimUnit.WriteTo"/>'s
        /// own convention exactly.</summary>
        public void WriteTo(FnvHash h)
        {
            h.Add(EntityId);
            h.Add(PlayerIndex);
            h.Add((int)Kind);
            h.Add(Hex.Q);
            h.Add(Hex.R);
            h.Add(MaxHp);
            h.Add(Hp);
            h.Add((int)State);
            h.Add(_ticksUntilComplete);
        }
    }
}
