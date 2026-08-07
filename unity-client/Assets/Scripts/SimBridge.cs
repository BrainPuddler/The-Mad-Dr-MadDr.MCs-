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

    /// <summary>How far between the last completed sim tick and the next
    /// one this render frame falls, in [0, 1]. Every sim-driven unit's
    /// view lerps its own prev/curr snapshot by this SAME value.</summary>
    public float Alpha { get; private set; }

    public bool HasMatch => _match != null;

    /// <summary>Start a fresh sim for this scene. Call once, before
    /// spawning any sim-driven unit.</summary>
    public void StartMatch(uint seed, IReadOnlyList<FactionId> factions, CityModel city)
    {
        _match = MatchState.Create(seed, factions, city);
        _pending.Clear();
        _views.Clear();
        _tickAccumulator = 0.0;
        Alpha = 0f;
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

        _tickAccumulator += dt;
        var ticksRun = 0;
        while (_tickAccumulator >= TickInterval && ticksRun < MaxTicksPerFrame)
        {
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
