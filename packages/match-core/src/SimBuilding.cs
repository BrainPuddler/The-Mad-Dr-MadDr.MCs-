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

        /// <summary>2026-08 (creator direction: "once a building is
        /// destroyed and after 20 seconds, its area becomes clear and we
        /// can build on it"): the Frame this building's HP first hit 0,
        /// or null while still standing. Lets `MatchState.CanPlaceBuilding`
        /// gate NEW construction on this hex behind a rubble-clearing
        /// delay -- deliberately separate from (and unrelated to) whether
        /// the hex blocks MOVEMENT, which still reopens the instant this
        /// building is Destroyed (`MatchState.ApplyBuildingDamage`
        /// removes it from `_blockedToGround` immediately, unchanged):
        /// walking through fresh rubble and dropping a new building on
        /// top of it are different questions with different answers.</summary>
        public int? DestroyedAtFrame { get; private set; }

        private int _ticksUntilComplete;

        /// <summary>Ticks left in <see cref="BuildingState.UnderConstruction"/>,
        /// 0 once Complete/Destroyed. For a construction-progress visual
        /// (docs/23 §2's own "ghost -> under-construction (scaffold %) ->
        /// complete" lifecycle) -- combine with the same kind's
        /// <see cref="BuildingDef.BuildTimeTicks"/> for a 0-1 fraction;
        /// not stored here itself since it's already public data on the
        /// def, not per-instance state worth duplicating.</summary>
        public int TicksUntilComplete => _ticksUntilComplete;

        /// <summary>docs/18 §3: ≤50% HP reads as Damaged -- a pure
        /// function of current HP, not its own state, and only
        /// meaningful once actually built (a building under construction
        /// isn't "damaged," it's unfinished).</summary>
        public bool IsDamaged => State == BuildingState.Complete && Hp * 2 <= MaxHp;

        /// <summary>2026-07 worker-economy epic, Phase 4: the roster unit
        /// currently mid-training here, or null if idle. v0.1: ONE
        /// in-progress slot per building, not a deep queue (docs/22 §7's
        /// Stitchworks 5-deep queue is a real, larger design this
        /// deliberately doesn't attempt yet) -- a second <see
        /// cref="CommandKind.TrainUnit"/> while this is non-null is a
        /// silent no-op, same "bad input never queues" contract as every
        /// other command.</summary>
        public RosterUnitKind? TrainingKind { get; private set; }

        /// <summary>Ticks left on the current training slot, meaningless
        /// while <see cref="TrainingKind"/> is null -- same relationship
        /// <see cref="TicksUntilComplete"/> has to construction.</summary>
        public int TrainTicksRemaining { get; private set; }

        /// <summary>Begins training `kind` if this building's single slot
        /// is free; returns false (silent no-op) if it's already
        /// occupied. Cost/legality (faction match, affordability,
        /// Complete state) are <see cref="MatchState.ApplyTrainUnit"/>'s
        /// job, same split as every other command handler -- this class
        /// only owns the slot itself, matching how it stays unaware of
        /// economy concerns elsewhere in this file.</summary>
        internal bool BeginTraining(RosterUnitKind kind, int ticks)
        {
            if (TrainingKind != null) return false;
            TrainingKind = kind;
            TrainTicksRemaining = ticks;
            return true;
        }

        /// <summary>One tick's worth of training progress. Returns the
        /// completed kind (and clears the slot) the instant it finishes,
        /// null every other tick -- the caller (<see
        /// cref="MatchState.Tick"/>) is what actually spawns the unit,
        /// same "this class owns state, MatchState owns the economy/
        /// entity-creation side effects" split <see cref="Tick"/>'s own
        /// construction-completion handling already uses.</summary>
        internal RosterUnitKind? TickTraining()
        {
            if (TrainingKind == null) return null;
            if (TrainTicksRemaining > 0) TrainTicksRemaining--;
            if (TrainTicksRemaining <= 0)
            {
                var done = TrainingKind;
                TrainingKind = null;
                return done;
            }
            return null;
        }

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
        /// state -- an unfinished building can be destroyed too. `frame`
        /// stamps <see cref="DestroyedAtFrame"/> the instant that
        /// happens, same "pass the caller's own Frame in" idiom
        /// <see cref="SimUnit.ApplyDamage"/> already uses.</summary>
        internal void ApplyDamage(int amount, int frame)
        {
            if (State == BuildingState.Destroyed || amount <= 0) return;
            Hp -= amount;
            if (Hp <= 0)
            {
                Hp = 0;
                State = BuildingState.Destroyed;
                DestroyedAtFrame = frame;
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
            h.Add(DestroyedAtFrame ?? -1);
            h.Add(_ticksUntilComplete);
            // 2026-07 epic: -1 is not a valid RosterUnitKind value (the
            // enum starts at 0), so it unambiguously encodes "idle slot"
            // without an extra bool field.
            h.Add(TrainingKind.HasValue ? (int)TrainingKind.Value : -1);
            h.Add(TrainTicksRemaining);
        }
    }
}
