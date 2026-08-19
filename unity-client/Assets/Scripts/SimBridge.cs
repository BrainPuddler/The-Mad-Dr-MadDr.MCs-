using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/27 Phase A: the one place a Unity scene owns a `match-core`
/// `MatchState` and pumps it at its fixed 10 ticks/s, independent of
/// Unity's own variable frame rate -- the standard fixed-timestep-with-
/// interpolation accumulator (same shape Unity's own FixedUpdate uses
/// internally, capped so a stalled frame can't spiral into running
/// dozens of catch-up ticks).
///
/// Absent from every existing scene until a dev/test scene explicitly
/// creates one: no current gameplay depends on this file existing.
/// `MonsterAgent` only reads sim state through here when it was
/// explicitly opted in (docs/27 §6.3) -- everything else keeps running
/// its legacy `Time.deltaTime`-driven `TickX` methods completely
/// unaffected, per docs/27's whole point ("leave every not-yet-ported
/// order kind exactly as correct as it is today").
///
/// `Alpha` is the ONE piece of per-frame interpolation state every
/// sim-driven unit's view shares -- "how far between the last completed
/// tick and the next are we, right now" is a property of the MATCH, not
/// of any one unit, so it lives here once rather than being duplicated
/// (and risking drifting apart) per <see cref="SimUnitView"/>.
/// </summary>
public class SimBridge : MonoBehaviour
{
    private const double TickInterval = 1.0 / MatchState.TicksPerSecond;

    [Tooltip("Safety cap on ticks run in one Unity frame -- a stalled frame drops the remainder rather than trying to catch up all at once (the classic fixed-timestep spiral-of-death guard).")]
    public int MaxTicksPerFrame = 4;

    private MatchState _match;
    private readonly List<Command> _pending = new List<Command>();
    private readonly Dictionary<uint, SimUnitView> _views = new Dictionary<uint, SimUnitView>();
    private double _tickAccumulator;

    /// <summary>docs/30 (selectable races + AI opponents): non-null only
    /// when <see cref="StartMatch(uint,IReadOnlyList{PlayerSetup},CityModel)"/>
    /// was used and at least one slot was AI-controlled -- every existing
    /// call site keeps using the <see cref="FactionId"/>-list overload
    /// below, which never sets this, so <see cref="Pump"/>'s AI-decision
    /// step is skipped entirely for every scene that doesn't opt in.</summary>
    private AiMatchDriver _aiDriver;

    /// <summary>How far between the last completed sim tick and the next
    /// one this render frame falls, in [0, 1]. Every sim-driven unit's
    /// view lerps its own prev/curr snapshot by this SAME value.</summary>
    public float Alpha { get; private set; }

    public bool HasMatch => _match != null;

    /// <summary>Start a fresh sim for this scene, all-human. Call once,
    /// before spawning any sim-driven unit. Forwards to the <see
    /// cref="PlayerSetup"/> overload below with every slot Human -- kept as
    /// its own overload (not just call-site sugar) so every existing
    /// caller compiles and behaves byte-identically, same "old overload
    /// wraps the new one" shape <see cref="MatchState.Create(uint,IReadOnlyList{FactionId},CityModel)"/>
    /// itself already uses.</summary>
    public void StartMatch(uint seed, IReadOnlyList<FactionId> factions, CityModel city, int? timeCapTicks = MatchState.DefaultTimeCapTicks)
    {
        var setups = new PlayerSetup[factions.Count];
        for (var i = 0; i < factions.Count; i++) setups[i] = PlayerSetup.Human(factions[i]);
        StartMatch(seed, setups, city, timeCapTicks);
    }

    /// <summary>docs/30 (selectable races + AI opponents): start a fresh
    /// sim with per-player AI wiring. Builds an <see cref="AiMatchDriver"/>
    /// alongside the match itself so <see cref="Pump"/> can start asking it
    /// for commands from the very first tick -- an all-human `players` list
    /// still builds one (cheap: <see cref="AiMatchDriver.HasAnyAi"/> is
    /// false, so <see cref="Pump"/>'s per-tick check is a single bool
    /// read). <paramref name="timeCapTicks"/> (2026-08, creator direction:
    /// "add a game duration selector 15,30,45 minutes or unlimited")
    /// passes straight through to <see cref="MatchState.Create(uint,IReadOnlyList{PlayerSetup},CityModel,int?)"/>
    /// -- defaults to the original 15-minute cap, null for Unlimited.</summary>
    public void StartMatch(uint seed, IReadOnlyList<PlayerSetup> players, CityModel city, int? timeCapTicks = MatchState.DefaultTimeCapTicks)
    {
        _match = MatchState.Create(seed, players, city, timeCapTicks);
        _aiDriver = new AiMatchDriver(_match, seed);
        _pending.Clear();
        _views.Clear();
        _tickAccumulator = 0.0;
        Alpha = 0f;
    }

    /// <summary>docs/30: bulk-add commands generated OUTSIDE the normal
    /// per-input `QueueXCommand` wrappers below -- specifically, <see
    /// cref="AiMatchDriver.DecideCommands"/>'s own already-built <see
    /// cref="Command"/> structs, which don't need re-deriving from raw
    /// args. Same pending buffer, same one-tick latency, same lockstep
    /// contract as every human-issued command -- an AI's orders are
    /// indistinguishable from a human's once queued.</summary>
    public void QueueCommands(IReadOnlyList<Command> commands)
    {
        for (var i = 0; i < commands.Count; i++) _pending.Add(commands[i]);
    }

    /// <summary>Spawn a sim-side unit and register the view that will
    /// receive its tick snapshots. Returns the new entity ID.
    /// <paramref name="radius"/> (docs/27 Phase C) feeds match-core's own
    /// sim-side separation (<see cref="MatchState.SpawnUnit"/>) -- defaults
    /// to <see cref="MatchState.DefaultUnitRadius"/> so existing call
    /// sites keep compiling unchanged.</summary>
    public uint SpawnUnit(int playerIndex, HexCoord atHex, double speed, SimUnitView view, double radius = MatchState.DefaultUnitRadius)
    {
        var id = _match.SpawnUnit(playerIndex, atHex, speed, radius);
        _views[id] = view;
        var u = _match.FindUnit(id);
        if (u != null) view.Prime(u.X, u.Z);   // no interpolation FROM nowhere on the very first frame
        return id;
    }

    /// <summary>2026-07 amendment: setup-time pass-through to <see
    /// cref="MatchState.SpawnHqForPlayer"/> -- same direct-call contract
    /// as <see cref="SpawnUnit"/> (never queued; Complete the instant it's
    /// called), not a Command. No-op returning 0 if no match is
    /// running.</summary>
    public uint SpawnHqForPlayer(int playerIndex, HexCoord atHex)
        => _match?.SpawnHqForPlayer(playerIndex, atHex) ?? 0;

    /// <summary>2026-07 amendment (docs/12 "give the player one fully
    /// functional factory on startup"): setup-time pass-through to <see
    /// cref="MatchState.SpawnFactoryForPlayer"/> -- same contract as <see
    /// cref="SpawnHqForPlayer"/> above. No-op returning 0 if no match is
    /// running.</summary>
    public uint SpawnFactoryForPlayer(int playerIndex, HexCoord atHex)
        => _match?.SpawnFactoryForPlayer(playerIndex, atHex) ?? 0;

    /// <summary>2026-08 (creator direction: "Human Army is from army
    /// barracks -- part of the basic kit for Human army"): setup-time
    /// pass-through to <see cref="MatchState.SpawnBarracksForPlayer"/>,
    /// same contract as <see cref="SpawnFactoryForPlayer"/> above.</summary>
    public uint SpawnBarracksForPlayer(int playerIndex, HexCoord atHex)
        => _match?.SpawnBarracksForPlayer(playerIndex, atHex) ?? 0;

    /// <summary>2026-08 (creator direction: "Create a faction based army
    /// generator. To start making opponents for the game"): setup-time
    /// pass-through to <see cref="MatchState.SpawnRosterUnit"/> -- same
    /// direct-call, no-op-if-no-match contract as <see
    /// cref="SpawnHqForPlayer"/>/<see cref="SpawnFactoryForPlayer"/>
    /// above. Used by <c>RuntimeCityBuilder.SpawnStartingBases</c> to
    /// field the units <see cref="ArmyGenerator"/> generates for an
    /// AI opponent. **These units have no Unity-side visual yet** -- no
    /// mesh/prefab pipeline exists for any <see cref="RosterUnitKind"/>
    /// (only genome-bred creatures render today, via `MonsterAgent`/
    /// `MonsterBody`); they exist in the simulation (queryable, fightable,
    /// salvageable) but are invisible in the 3D scene until that separate
    /// docs/23 §6 Unity task is done.</summary>
    public uint SpawnRosterUnit(int playerIndex, HexCoord atHex, RosterUnitKind kind)
        => _match?.SpawnRosterUnit(playerIndex, atHex, kind) ?? 0;

    /// <summary>Queue a REPLACE move order for the NEXT tick boundary --
    /// never applied immediately (docs/27 §5: one-tick input latency is
    /// correct lockstep behavior, not a bug). Drops any waypoints already
    /// queued on this unit (match-core's `ApplyMoveTo`), same as a plain
    /// (non-shift) ground-click.</summary>
    public void QueueMoveCommand(int playerIndex, uint entityId, HexCoord destination)
    {
        _pending.Add(new Command(playerIndex, CommandKind.MoveTo, targetEntity: entityId, argA: destination.Q, argB: destination.R));
    }

    /// <summary>docs/27 Phase B: queue an APPEND waypoint order -- the
    /// sim-side twin of a SHIFT ground-click. Same one-tick latency as
    /// <see cref="QueueMoveCommand"/>.</summary>
    public void QueueWaypointCommand(int playerIndex, uint entityId, HexCoord destination)
    {
        _pending.Add(new Command(playerIndex, CommandKind.MoveQueue, targetEntity: entityId, argA: destination.Q, argB: destination.R));
    }

    /// <summary>Current sim-side order state for a unit, or Idle if this
    /// entity is unknown (defensive default -- never throws).</summary>
    public UnitOrderKind OrderOf(uint entityId)
    {
        var u = _match?.FindUnit(entityId);
        return u?.Order ?? UnitOrderKind.Idle;
    }

    /// <summary>Current sim frame, or 0 if no match is running yet.
    /// Callers that care about the distinction should check
    /// <see cref="HasMatch"/> rather than treat 0 as meaningful.</summary>
    public int CurrentFrame => _match?.Frame ?? 0;

    /// <summary>2026-08 (win/loss states, docs/02 "Victory conditions"):
    /// true once <see cref="MatchState.CheckMatchEnd"/> has found
    /// Elimination, Dominion, or the time cap. False (never true) if no
    /// match is running.</summary>
    public bool IsMatchOver => _match?.IsMatchOver ?? false;

    /// <summary>Which player won, or null for a draw -- meaningless while
    /// <see cref="IsMatchOver"/> is false.</summary>
    public int? WinnerPlayerIndex => _match?.WinnerPlayerIndex;

    /// <summary><see cref="MatchEndReason.None"/> if no match is running
    /// or it hasn't ended yet.</summary>
    public MatchEndReason EndReason => _match?.EndReason ?? MatchEndReason.None;

    /// <summary>docs/02's time-cap tiebreak score, live -- see <see
    /// cref="MatchState.TerritoryScore"/> for the exact (flagged
    /// placeholder) weighting. 0 if no match is running.</summary>
    public int TerritoryScore(int playerIndex) => _match?.TerritoryScore(playerIndex) ?? 0;

    /// <summary>2026-08 (win-progress HUD): how many player slots this
    /// match has -- lets a caller loop every opponent to find e.g. the
    /// strongest one, the same way <see cref="PlayerFaction"/>'s own
    /// bounds check already reads this internally. 0 if no match is
    /// running.</summary>
    public int PlayerCount => _match?.PlayerCount ?? 0;

    /// <summary>docs/02/docs/03 Dominion -- consecutive ticks this
    /// player has held &gt;=60% of the map's emitters right now (see
    /// <see cref="PlayerState.DominionStreakTicks"/>'s own doc comment).
    /// 0 if no match is running.</summary>
    public int PlayerDominionStreakTicks(int playerIndex)
    {
        var p = _match?.Player(playerIndex);
        return p?.DominionStreakTicks ?? 0;
    }

    /// <summary>The live Lumen phase (docs/03), or Dawn -- the match's own
    /// start phase -- if no match is running yet.</summary>
    public LumenPhase CurrentLumenPhase => _match?.CurrentLumenPhase ?? LumenPhase.Dawn;

    /// <summary>Ticks remaining before the Lumen phase changes, 0 if no
    /// match is running. For a moon-dial HUD's pre-transition warning.</summary>
    public int TicksUntilNextLumenPhase => _match != null ? LumenClock.TicksUntilNextPhase(_match.Frame) : 0;

    /// <summary>A player's current mana balance (docs/03/23 Phase 3.5), 0
    /// if no match is running.</summary>
    public int PlayerMana(int playerIndex)
    {
        var p = _match?.Player(playerIndex);
        return p?.Mana ?? 0;
    }

    /// <summary>2026-08 (per-faction Factory/Control Centre visual
    /// treatment): the accessor BaseDresser.OwnerBaseColor's own doc
    /// comment already flagged as missing -- "SimBridge has no player-
    /// faction accessor yet," forcing that method to approximate faction
    /// from PLAYER INDEX instead. match-core already tracks each
    /// player's real FactionId (PlayerState.Faction, set once at
    /// StartMatch and never reassigned) -- this is a pure passthrough,
    /// no match-core changes needed. `Player(int)` has no bounds check of
    /// its own (`_players[index]`, straight array indexing), so the range
    /// guard lives here rather than risking an IndexOutOfRangeException
    /// from a stale/bad index; falls back to FactionId.Mixed (this
    /// project's own established "don't guess a specific faction's look"
    /// bucket) rather than a made-up default.</summary>
    public FactionId PlayerFaction(int playerIndex)
    {
        if (_match == null || playerIndex < 0 || playerIndex >= _match.PlayerCount) return FactionId.Mixed;
        return _match.Player(playerIndex).Faction;
    }

    /// <summary>Live emitter count, 0 if no match is running -- iterate
    /// with <see cref="EmitterAt"/> for a capture-progress HUD.</summary>
    public int EmitterCount => _match?.EmitterCount ?? 0;

    /// <summary>Live emitter state by index. Only valid when
    /// <paramref name="index"/> &lt; <see cref="EmitterCount"/> (i.e. a
    /// match is running) -- same "caller already knows a match exists"
    /// contract as <see cref="SpawnUnit"/>.</summary>
    public SimEmitter EmitterAt(int index) => _match.EmitterAt(index);

    /// <summary>docs/23 §2 Phase 2's Unity half: queue a BuildStructure
    /// command for the NEXT tick boundary (same one-tick-latency contract
    /// as <see cref="QueueMoveCommand"/>). This method itself never
    /// validates -- callers should check <see cref="CanPlaceBuilding"/>
    /// first for a live ghost-cursor preview; an invalid placement is a
    /// silent sim-side no-op either way, never an exception.</summary>
    public void QueueBuildCommand(int playerIndex, BuildingKind kind, HexCoord hex)
    {
        _pending.Add(new Command(playerIndex, CommandKind.BuildStructure, targetEntity: (uint)kind, argA: hex.Q, argB: hex.R));
    }

    /// <summary>Read-only placement-validity preview, false if no match
    /// is running. Wraps <see cref="MatchState.CanPlaceBuilding"/> --
    /// the EXACT check the sim applies when a queued command actually
    /// lands, so a ghost cursor can never show green for a placement
    /// that then silently fails.</summary>
    public bool CanPlaceBuilding(int playerIndex, BuildingKind kind, HexCoord hex)
        => _match != null && _match.CanPlaceBuilding(playerIndex, kind, hex);

    /// <summary>2026-08 bugfix: unblocks a hex in the sim's own build-
    /// placement gate, for the PROCEDURAL building destruction path
    /// (<see cref="RuntimeCityBuilder.ApplyBuildingDamage(Building, int, HexCoord)"/>),
    /// which has no <see cref="SimBuilding"/> entity of its own to route
    /// through <see cref="MatchState.ApplyBuildingDamage(uint, int)"/>'s
    /// own automatic unblock. Silent no-op if no match is running, same
    /// contract as every other wrapper here.</summary>
    public void UnblockProceduralBuildingHex(HexCoord hex) => _match?.UnblockProceduralBuildingHex(hex);

    /// <summary>Live building count, 0 if no match is running -- iterate
    /// with <see cref="BuildingAt"/> for a BaseDresser to sync visuals.</summary>
    public int BuildingCount => _match?.BuildingCount ?? 0;

    /// <summary>Live building state by index. Only valid when
    /// <paramref name="index"/> &lt; <see cref="BuildingCount"/> (i.e. a
    /// match is running) -- same "caller already knows a match exists"
    /// contract as <see cref="EmitterAt"/>.</summary>
    public SimBuilding BuildingAt(int index) => _match.BuildingAt(index);

    /// <summary>2026-08 (Barracks/infantry roster pass): the unit-side
    /// twin of <see cref="BuildingCount"/>/<see cref="BuildingAt"/> --
    /// same "iterate for a view layer to sync visuals" contract, now that
    /// <see cref="RosterInfantryView"/> is the first real consumer (every
    /// prior reader of <see cref="MatchState.UnitAt"/> stayed sim-internal
    /// -- see that method's own doc comment on why no Unity visual has
    /// ever existed for a <see cref="RosterUnitKind"/> before this).</summary>
    public int UnitCount => _match?.UnitCount ?? 0;

    /// <summary>Live unit state by index. Only valid when <paramref
    /// name="index"/> &lt; <see cref="UnitCount"/>, same contract as
    /// <see cref="BuildingAt"/>.</summary>
    public SimUnit UnitAt(int index) => _match.UnitAt(index);

    /// <summary>A player's current balance of one resource, 0 if no match
    /// is running -- for a build menu to gray out unaffordable options.</summary>
    public int PlayerWallet(int playerIndex, ResourceKind kind)
    {
        var p = _match?.Player(playerIndex);
        return p?.Wallet(kind) ?? 0;
    }

    /// <summary>A player's current cap for one resource, or
    /// <see cref="int.MaxValue"/> (PlayerState's own "uncapped" sentinel,
    /// unchanged) if no match is running -- for a HUD to render "no cap
    /// yet" the same way a running match itself would, rather than
    /// reading as a real cap of 0.</summary>
    public int PlayerWalletCap(int playerIndex, ResourceKind kind)
    {
        var p = _match?.Player(playerIndex);
        return p?.WalletCap(kind) ?? int.MaxValue;
    }

    /// <summary>A player's current supply used/cap, 0 if no match is
    /// running.</summary>
    public int PlayerSupplyUsed(int playerIndex)
    {
        var p = _match?.Player(playerIndex);
        return p?.SupplyUsed ?? 0;
    }

    public int PlayerSupplyCap(int playerIndex)
    {
        var p = _match?.Player(playerIndex);
        return p?.SupplyCap ?? 0;
    }

    /// <summary>2026-07 worker-economy epic, Phase 4: queue a TrainUnit
    /// command for the NEXT tick boundary -- same one-tick-latency
    /// contract as <see cref="QueueBuildCommand"/>. Never validates
    /// itself; callers should check <see cref="CanTrainUnit"/> first for
    /// a live UI preview.</summary>
    public void QueueTrainCommand(int playerIndex, uint buildingEntityId, RosterUnitKind kind)
    {
        _pending.Add(new Command(playerIndex, CommandKind.TrainUnit, targetEntity: buildingEntityId, argA: (int)kind));
    }

    /// <summary>Read-only preview, false if no match is running. Wraps
    /// <see cref="MatchState.CanTrainUnit"/> -- same "the ghost/UI check
    /// can never disagree with what actually happens" contract as <see
    /// cref="CanPlaceBuilding"/>.</summary>
    public bool CanTrainUnit(int playerIndex, uint buildingEntityId, RosterUnitKind kind)
        => _match != null && _match.CanTrainUnit(playerIndex, buildingEntityId, kind);

    /// <summary>2026-07 (creator direction: "Building need decent amount
    /// of HPs and should show damage and some low-poly fire when being
    /// attacked"): queue an AttackBuilding command for the NEXT tick
    /// boundary -- same one-tick-latency contract as <see
    /// cref="QueueTrainCommand"/>. Never validates itself (there is no
    /// separate "CanAttackBuilding" preview -- see <see cref="MatchState.
    /// ApplyAttackBuilding"/>'s own doc comment for its full
    /// existence/alive/Reach/Destroyed precondition list, silently a
    /// no-op if any fail, the same contract every other queued command
    /// in this class already follows).</summary>
    public void QueueAttackBuildingCommand(int playerIndex, uint attackerEntityId, uint buildingEntityId)
    {
        _pending.Add(new Command(playerIndex, CommandKind.AttackBuilding, targetEntity: attackerEntityId, argA: unchecked((int)buildingEntityId)));
    }

    /// <summary>2026-07 (harvester-to-Factory delivery loop, docs/22):
    /// queue a BankHarvestLoad command for the NEXT tick boundary -- same
    /// one-tick-latency contract as every other queued command here.
    /// Unlike the others there's no source entity to validate against (a
    /// harvester monster isn't itself a SimUnit yet -- see <see
    /// cref="MadDr.MatchCore.CommandKind.BankHarvestLoad"/>'s own doc
    /// comment), so this is the correct wallet-mutating path rather than
    /// a Unity-side-only counter: it's what makes a delivered load
    /// actually show up in <see cref="PlayerWallet"/> (and therefore in
    /// ResourceHud), not just in a log line.
    ///
    /// 2026-08: `resource` selects which wallet lane grows (defaults to
    /// Blood, matching every call site before this parameter existed --
    /// `ResourceKind.Blood` is enum value 0, the same value ArgB carried
    /// implicitly when it was unused). `RuntimeCityBuilder.
    /// BankHarvestLoad` now calls this once per nonzero resource lane a
    /// harvester actually carried, instead of only ever banking
    /// Blood.</summary>
    public void QueueBankHarvestLoadCommand(int playerIndex, int amount, ResourceKind resource = ResourceKind.Blood)
    {
        _pending.Add(new Command(playerIndex, CommandKind.BankHarvestLoad, argA: amount, argB: (int)resource));
    }

    /// <summary>2026-08 (Collector Lab battalion training, docs/12): debit
    /// twin of <see cref="QueueBankHarvestLoadCommand"/> -- lets
    /// <see cref="RuntimeCityBuilder.BeginCollectorBattalion"/> spend from
    /// the REAL match-core wallet (the same one <see cref="PlayerWallet"/>
    /// reports, which <see cref="ResourceHud"/> displays) instead of a
    /// disconnected client-side counter -- see <see
    /// cref="CommandKind.SpendResource"/>'s own doc comment.</summary>
    public void QueueSpendResourceCommand(int playerIndex, int amount, ResourceKind resource)
    {
        _pending.Add(new Command(playerIndex, CommandKind.SpendResource, argA: amount, argB: (int)resource));
    }

    /// <summary>2026-08 (Zombie/SCV-style construction, docs/12): toggles
    /// `buildingEntityId`'s <see cref="SimBuilding.IsStaffed"/> -- see
    /// <see cref="CommandKind.SetBuildingStaffed"/>'s own doc comment.
    /// TargetEntity carries the building id (the "generic slots
    /// reinterpreted per Kind" contract every command already uses);
    /// ArgA is the staffed bool as 0/1.</summary>
    public void QueueSetBuildingStaffedCommand(int playerIndex, uint buildingEntityId, bool staffed)
    {
        _pending.Add(new Command(playerIndex, CommandKind.SetBuildingStaffed, targetEntity: buildingEntityId, argA: staffed ? 1 : 0));
    }

    /// <summary>2026-08 (docs/12 tech-wing epic, Phase 1): queue a
    /// RegisterWorker command for the NEXT tick boundary -- same "no
    /// source entity to validate" shape as <see
    /// cref="QueueBankHarvestLoadCommand"/> (a Worker isn't a SimUnit
    /// either). Called the instant a Worker actually exists in Unity
    /// (<see cref="RuntimeCityBuilder.OnCitizenPossessed"/>, and the
    /// starting-Worker bootstrap grant) so match-core's own <see
    /// cref="PlayerState.AvailableWorkers"/> -- what <see
    /// cref="MatchState.CanPlaceBuilding"/> now actually gates
    /// construction on -- stays in sync with Unity's own <see
    /// cref="RuntimeCityBuilder.Workers"/> list.</summary>
    public void QueueRegisterWorkerCommand(int playerIndex)
    {
        _pending.Add(new Command(playerIndex, CommandKind.RegisterWorker));
    }

    /// <summary>Sibling of <see cref="QueueRegisterWorkerCommand"/> for a
    /// Worker's death (<see cref="Worker.OnDied"/>).</summary>
    public void QueueUnregisterWorkerCommand(int playerIndex)
    {
        _pending.Add(new Command(playerIndex, CommandKind.UnregisterWorker));
    }

    private void Update() => Pump(Time.deltaTime);

    /// <summary>The actual fixed-timestep accumulator logic, taking `dt`
    /// as a parameter rather than reading `Time.deltaTime` internally --
    /// the same convention every `MonsterAgent.TickX(dt)` method already
    /// follows (`Update()` reads the engine clock once, everything below
    /// it takes `dt` as data). Exposed `public` specifically so a
    /// standalone harness can drive this with controlled `dt` values with
    /// no live Unity loop -- `Time.deltaTime` itself can't be faked
    /// outside the Editor/Player, so this seam is what makes the
    /// accumulator's own correctness (monotonic alpha, no negative
    /// accumulator, the catch-up cap) testable at all.</summary>
    public void Pump(float dt)
    {
        if (_match == null) return;
        // 2026-08 (win/loss states): MatchState.Tick itself already no-ops
        // once the match is over (checked first thing, before even
        // processing commands), so this early return isn't required for
        // correctness -- just avoids burning the fixed-timestep loop below
        // on ticks that would do nothing anyway, every frame, forever
        // after game-over.
        if (_match.IsMatchOver) return;

        _tickAccumulator += dt;
        var ticksRun = 0;
        while (_tickAccumulator >= TickInterval && ticksRun < MaxTicksPerFrame)
        {
            // docs/30: AI players' commands join the SAME pending bundle a
            // human's QueueXCommand calls already fill -- queued BEFORE
            // this tick's Tick() call, same one-tick latency contract as
            // everything else, so an AI's orders are indistinguishable
            // from a human's once queued. Skipped entirely (a single bool
            // read) for every scene that never opted into an AI player.
            if (_aiDriver != null && _aiDriver.HasAnyAi)
                QueueCommands(_aiDriver.DecideCommands(_match));

            _match.Tick(_pending.Count > 0 ? _pending.ToArray() : null);
            _pending.Clear();
            NotifyViews();
            _tickAccumulator -= TickInterval;
            ticksRun++;
        }
        // spiral-of-death guard: a frame so slow it can't catch up drops
        // the remainder rather than trying to run more ticks next frame
        // too -- a visible one-time stutter beats an ever-growing queue.
        if (ticksRun >= MaxTicksPerFrame) _tickAccumulator = 0.0;

        Alpha = Mathf.Clamp01((float)(_tickAccumulator / TickInterval));
    }

    private void NotifyViews()
    {
        for (var i = 0; i < _match.UnitCount; i++)
        {
            var u = _match.UnitAt(i);
            if (_views.TryGetValue(u.EntityId, out var view)) view.OnTick(u.X, u.Z);
        }
    }
}
