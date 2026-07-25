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
