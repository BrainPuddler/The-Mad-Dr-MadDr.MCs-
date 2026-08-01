namespace MadDr.MatchCore
{
    /// <summary>
    /// The command kinds a player can issue. Phase 1 defines the envelope
    /// and one no-op-ish kind (Ping, used to prove the command pipeline
    /// hashes deterministically); real orders (Move/Attack/Build/...) are
    /// added by the phases that implement them, each targeting ENTITY IDs,
    /// never object references (docs/23 §13-J).
    /// </summary>
    public enum CommandKind
    {
        None = 0,
        Ping = 1,   // Phase 1 placeholder: carries data through the pipeline, affects only the ack counter
        /// <summary>docs/23 Phase 1.5: order the unit at TargetEntity to
        /// walk to the hex (ArgA=Q, ArgB=R). The canonical, replayable way
        /// to move a unit -- never a direct method call from outside the
        /// sim, so a real lockstep command log can reconstruct the exact
        /// same order stream. REPLACES whatever the unit was doing,
        /// including dropping any queued waypoints (<see cref="MoveQueue"/>
        /// below) -- the sim-side twin of a non-shift ground-click.</summary>
        MoveTo = 2,
        /// <summary>docs/27 Phase B: append hex (ArgA=Q, ArgB=R) to
        /// TargetEntity's waypoint queue instead of replacing its current
        /// path -- the sim-side twin of a SHIFT ground-click. If the unit
        /// is currently Idle (nothing in flight), starts walking to this
        /// waypoint immediately, matching Unity's own
        /// `OrderMove(hex, queue: true)` semantics on an idle unit.</summary>
        MoveQueue = 3,
        /// <summary>docs/23 §2 Phase 2: place a new building. No existing
        /// entity to target, so TargetEntity is repurposed to carry the
        /// <see cref="BuildingKind"/> (cast to uint) instead -- exactly
        /// the "generic arg slots are interpreted per Kind" contract this
        /// struct's own header already establishes, not a new liberty.
        /// ArgA=Q, ArgB=R (the hex to build on). Silently a no-op for
        /// <see cref="BuildingKind.Hq"/> (generator-placed only, never
        /// player-built -- see <see cref="MatchState.SpawnHqForPlayer"/>),
        /// an occupied/off-map hex, or an unaffordable cost, matching
        /// every other command kind's "bad input is a no-op, never an
        /// exception" contract.</summary>
        BuildStructure = 4,
        /// <summary>docs/23 Phase 4 (combat core, docs/04): TargetEntity
        /// (the attacker) begins channeling an attack against ArgA (the
        /// defender's entity ID, cast to int -- entity IDs never
        /// realistically exceed int range in a single match, same
        /// "reinterpret the generic slots" contract `BuildStructure`
        /// already uses). Silently a no-op unless BOTH units exist, are
        /// alive, the attacker has combat stats at all, and the two are
        /// already within the attacker's Reach -- there is no
        /// chase-to-range movement yet (docs/12's Phase 4 entry), so an
        /// out-of-range order is rejected rather than queued.</summary>
        AttackUnit = 5,
        /// <summary>docs/23 Phase 6a (docs/04): TargetEntity (the
        /// harvester) begins a 3-second channel looting ArgA (the corpse's
        /// entity ID, cast to int -- same reinterpreted-slot contract as
        /// <see cref="AttackUnit"/>). Silently a no-op unless the harvester
        /// is alive, the target actually IS a corpse still inside its 15s
        /// salvage window with something left to loot, and the two are
        /// within salvage range -- same "bad input never queues, just
        /// rejects" contract as every other command kind.</summary>
        SalvageCorpse = 6,
        /// <summary>docs/23 §6: TargetEntity (the attacker) begins
        /// attacking ArgA (a <see cref="SimAnomaly"/>'s entity ID, cast to
        /// int -- same reinterpreted-slot contract as every other command
        /// with two entity operands). Same reach/existence/alive
        /// preconditions as <see cref="AttackUnit"/>, resolved by
        /// <see cref="MatchState.TickAnomalyCombat"/> instead of
        /// <see cref="MatchState.TickCombat"/> (a deliberately separate
        /// resolution loop -- an anomaly has no facing/arc/Armor of its
        /// own, unlike a real combatant).</summary>
        AttackAnomaly = 7,
        /// <summary>2026-07 worker-economy epic, Phase 4: TargetEntity (a
        /// Complete building) begins training the roster unit named by
        /// ArgA (a <see cref="RosterUnitKind"/>, cast to int -- same
        /// reinterpreted-slot contract as every other command with a data
        /// operand instead of a second entity). Silently a no-op unless
        /// the building exists, is Complete, belongs to the issuing
        /// player, isn't already mid-training (v0.1: one in-progress slot
        /// per building, not a deep queue), the kind belongs to the
        /// player's own faction, and every line of <see
        /// cref="UnitRosterDef.Cost"/> is affordable -- same "bad input
        /// never queues, just rejects" contract as every other command
        /// kind. See <see cref="MatchState.ApplyTrainUnit"/>'s own doc
        /// comment for the full validation list.</summary>
        TrainUnit = 8,
        /// <summary>2026-07 (creator direction: "Building need decent
        /// amount of HPs and should show damage and some low-poly fire
        /// when being attacked"): TargetEntity (the attacker) begins
        /// attacking ArgA (a <see cref="SimBuilding"/>'s entity ID, cast
        /// to int -- same reinterpreted-slot contract as <see
        /// cref="AttackUnit"/>/<see cref="AttackAnomaly"/>). Same
        /// existence/alive/Reach preconditions as those two, resolved by
        /// <see cref="MatchState.TickBuildingCombat"/> instead -- a
        /// building has no facing/arc/Level/XP of its own, the same
        /// reasoning <see cref="AttackAnomaly"/> already established for
        /// anomalies. Silently a no-op for a Destroyed building, matching
        /// every other command kind's "bad input is a no-op" contract.</summary>
        AttackBuilding = 9,
        /// <summary>2026-07 (harvester-to-Factory delivery loop, docs/22):
        /// grant PlayerIndex's own wallet ArgA Blood -- a harvester
        /// monster isn't itself a <see cref="SimUnit"/> (its movement/AI
        /// is still Unity-side, not yet ported into the sim per docs/27's
        /// migration plan), so unlike every other command kind there is
        /// no source entity to validate against; TargetEntity/ArgB are
        /// unused. Bad input (ArgA &lt;= 0) is a silent no-op, same
        /// contract as every other command kind.</summary>
        BankHarvestLoad = 10,
    }

    /// <summary>
    /// One player command, scheduled for a specific tick (docs/23 §11:
    /// commands land N ticks ahead). A flat value type -- all fields
    /// integer, entity references are IDs -- so a command stream serializes
    /// and hashes byte-for-byte identically on every client. The generic
    /// arg slots (<see cref="TargetEntity"/>, <see cref="ArgA"/>,
    /// <see cref="ArgB"/>) are interpreted per <see cref="Kind"/>; later
    /// phases give them meaning without changing the envelope.
    /// </summary>
    public readonly struct Command
    {
        public readonly int PlayerIndex;
        public readonly CommandKind Kind;
        public readonly uint TargetEntity;   // an EntityId value, or 0 for none
        public readonly int ArgA;
        public readonly int ArgB;

        public Command(int playerIndex, CommandKind kind, uint targetEntity = 0, int argA = 0, int argB = 0)
        {
            PlayerIndex = playerIndex;
            Kind = kind;
            TargetEntity = targetEntity;
            ArgA = argA;
            ArgB = argB;
        }

        public void WriteTo(FnvHash h)
        {
            h.Add(PlayerIndex);
            h.Add((int)Kind);
            h.Add(TargetEntity);
            h.Add(ArgA);
            h.Add(ArgB);
        }
    }
}
