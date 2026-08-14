using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// 2026-08 (creator report: "same problem as the monsters, walking
/// directly toward each other and not wandering. Why can't you use the
/// monster navigation system? advantages cost or is there a reason?"):
/// there was no real cost/architecture reason -- this is a standalone,
/// reusable ground-unit path follower giving any non-flying unit the
/// SAME real navigation `MonsterAgent`'s own private `FollowPath`
/// already has (docs/25's "Hybrid Steering + Deadlock Recovery" plan,
/// four shipped phases): `HexPathfinder` A* routing, so a route actually
/// goes around solid buildings instead of a straight line through them,
/// PLUS `MonsterSteeringController`'s local blended separation and
/// time-to-collision predictive avoidance via <see
/// cref="RuntimeCityBuilder.SteerFollowPath"/> -- instead of the naive
/// "walk in a straight line toward the target, sort out overlaps
/// afterward with a hard push-apart" every non-Monster unit in this
/// codebase (<see cref="Worker"/> included, until this) used.
///
/// Deliberately a STANDALONE helper, not a refactor of MonsterAgent's own
/// private FollowPath/ComputePath/RecomputeIfCityChanged into a shared
/// utility both classes call through: that method is combat-critical,
/// already tuned across four real engineering phases with real bugs
/// found and fixed against it (docs/12), and carries flying-unit
/// branches (altitude tiers, carve-vs-strafe banking) this ground-only
/// helper has no use for at all. This class mirrors its GROUND-unit path
/// exactly -- same static `MonsterSteeringController` calls, same
/// `RuntimeCityBuilder.SteerFollowPath` -- reusing the identical
/// underlying systems (including, for free, `DeadlockManager`'s stall
/// recovery: `SteerFollowPath` already reads any `UnitCombat`'s
/// `YieldUntil`/`YieldTarget`, and a Worker's `UnitCombat` is already
/// registered in the same `RuntimeCityBuilder.Combatants` pool
/// `DeadlockManager` scans) without touching that already-working file.
///
/// One instance per moving unit (a plain field, not a MonoBehaviour --
/// same "no unnecessary components" idiom this codebase already follows
/// for per-unit animation state, e.g. `HumanCharacterAnimState`).
/// </summary>
public class GroundPathFollower
{
    private List<Vector3> _path;
    private int _pathIndex;
    private HexCoord _pathGoalHex;
    private int _pathCityVersion = -1;
    private MonsterSteeringController.WaypointProgress _waypointProgress;
    private bool _contested;

    // A goal SetGoal already tried and failed to route to (unreachable --
    // distinct from "blocked at that exact hex," which is handled by
    // substituting a neighbour before ever calling HexPathfinder). Without
    // this, a caller that keeps calling SetGoal every frame with the same
    // unreachable goal (every Tick* method here does) would re-run a full
    // A* search -- expensive specifically for a search that has to
    // exhaust its space to conclude "no route" -- every single frame
    // until something else changes the goal. Cleared automatically the
    // instant the goal or city version actually changes.
    private HexCoord? _failedGoalHex;
    private int _failedGoalCityVersion = -1;

    /// <summary>How far `Tick` actually moved `self` last call -- feed
    /// this straight into a caller's own "distance moved this frame"
    /// bookkeeping (e.g. `Worker._frameMoveDistance`, which the
    /// character-system animator needs for its own distance-synced gait,
    /// docs/34) instead of re-deriving it from a raw target/speed
    /// calculation that no longer reflects what steering actually did.</summary>
    public float LastStepDistance { get; private set; }

    /// <summary>(Re)computes a real A* route to `goalHex` if this
    /// follower doesn't already have a live one heading there -- cheap
    /// to call every frame from a Tick* method exactly like MonsterAgent's
    /// own `ComputePath`/`RecomputeIfCityChanged` call sites do, since
    /// `HexPathfinder` only actually runs when the goal or the city's
    /// blocked-hex set (destruction opening/closing routes) has changed.
    /// If `goalHex` itself is blocked (a building's own hex, most
    /// callers' natural target), routes to its nearest open neighbour
    /// instead -- same "approach the rim, not the solid center" fallback
    /// `MonsterAgent.FindOwnFactoryApproachHex`-style code already uses,
    /// generalized here so every caller gets it for free instead of each
    /// one having to pre-sanitize its own goal.</summary>
    public void SetGoal(RuntimeCityBuilder builder, Vector3 fromPos, HexCoord goalHex)
    {
        if (_path != null && _pathGoalHex.Equals(goalHex) && _pathCityVersion == builder.CityVersion) return;
        if (_failedGoalHex.HasValue && _failedGoalHex.Value.Equals(goalHex) && _failedGoalCityVersion == builder.CityVersion) return;

        var blocked = builder.BlockedFor(false);
        var actualGoal = goalHex;
        if (blocked.Contains(actualGoal))
        {
            HexCoord? best = null;
            var bestSq = float.MaxValue;
            foreach (var n in actualGoal.Neighbors())
            {
                if (!builder.City.Contains(n) || blocked.Contains(n)) continue;
                var d = (builder.WorldOf(n) - fromPos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = n; }
            }
            if (best.HasValue) actualGoal = best.Value;
        }

        var start = builder.HexAt(fromPos);
        var hexPath = HexPathfinder.FindPath(start, actualGoal, builder.City, blocked);
        _pathGoalHex = goalHex;   // the ORIGINAL goal, so "already routed there" checks match future callers regardless of the approach-hex substitution above
        _pathCityVersion = builder.CityVersion;
        if (hexPath == null)
        {
            _path = null;
            _failedGoalHex = goalHex;
            _failedGoalCityVersion = builder.CityVersion;
            return;
        }

        _failedGoalHex = null;
        _pathIndex = 0;
        MonsterSteeringController.ResetProgress(ref _waypointProgress);
        _contested = false;
        _path = new List<Vector3>(hexPath.Count);
        foreach (var hex in hexPath) _path.Add(builder.WorldOf(hex));
    }

    /// <summary>Drop the current route -- e.g. a caller switching to a
    /// wholly different kind of order (combat, a player move command)
    /// that will call `SetGoal` with a fresh goal of its own anyway, so
    /// this is a minor courtesy against `SetGoal`'s cache momentarily
    /// matching a stale goal by coincidence, not load-bearing.</summary>
    public void Clear() { _path = null; }

    /// <summary>Advances one frame along the current path, blending real
    /// obstacle-avoiding local steering on top of the A* route -- the
    /// exact same per-frame shape MonsterAgent's own FollowPath uses
    /// (waypoint-advance via `ShouldAdvanceWaypoint`, direction+speed via
    /// `SteerFollowPath`), scoped to ground movement only (no flying
    /// branch -- nothing built on this class ever flies). Moves `self`
    /// directly, matching every other Worker movement method's existing
    /// "the Tick method just mutates transform.position itself" style,
    /// rather than returning a velocity for a caller to apply. Returns
    /// true once the path is fully walked (caller should treat the unit
    /// as arrived) or if there's no path at all (caller forgot to
    /// `SetGoal` first, or `SetGoal` found no route -- either way,
    /// nothing to walk).</summary>
    public bool Tick(RuntimeCityBuilder builder, Transform self, UnitCombat fighter, float dt, float speed)
    {
        LastStepDistance = 0f;
        if (_path == null) return true;

        if (_pathCityVersion != builder.CityVersion)
        {
            // destruction changed passability mid-route -- reroute
            // against the new blocked set, same trigger MonsterAgent's
            // own RecomputeIfCityChanged uses.
            var wasGoal = _pathGoalHex;
            _path = null;
            SetGoal(builder, self.position, wasGoal);
            if (_path == null) return true;
        }

        var radius = fighter != null ? fighter.Radius : 0.6f;
        var arriveDist = MonsterSteeringController.ArriveDistFor(radius);
        var legDir = LegDirection();
        while (_pathIndex < _path.Count
               && MonsterSteeringController.ShouldAdvanceWaypoint(ref _waypointProgress,
                   self.position, _path[_pathIndex], legDir, arriveDist, _contested, dt))
        {
            _pathIndex++;
            legDir = LegDirection();
        }
        if (_pathIndex >= _path.Count) { _path = null; return true; }

        var target = _path[_pathIndex];
        var to = target - self.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < 1e-4f) return false;
        var dir = to / dist;

        var steer = dir;
        var speedScale = 1f;
        if (builder != null && fighter != null)
        {
            var result = builder.SteerFollowPath(fighter, dir, speed);
            steer = result.Direction;
            speedScale = result.SpeedScale;
            _contested = result.Contested;
        }

        self.rotation = Quaternion.Slerp(self.rotation, Quaternion.LookRotation(steer, Vector3.up), dt * 5f);
        var stepLen = speed * speedScale * dt;
        self.position += steer * stepLen;
        LastStepDistance = stepLen;
        return false;
    }

    private Vector3 LegDirection()
    {
        if (_path == null || _pathIndex <= 0 || _pathIndex >= _path.Count) return Vector3.zero;
        var leg = _path[_pathIndex] - _path[_pathIndex - 1];
        leg.y = 0f;
        return leg.sqrMagnitude < 1e-6f ? Vector3.zero : leg.normalized;
    }
}
