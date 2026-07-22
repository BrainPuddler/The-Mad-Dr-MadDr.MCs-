using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// docs/25 Phase D: the deadlock-recovery fallback the migration plan's
/// approved architecture describes as "rare-path only ... never becomes
/// the primary mover." Layer 1 steering (Phases B/C) resolves ordinary
/// congestion; this exists for the case that can't self-resolve locally --
/// a corridor jam where every unit's own steering keeps everyone mutually
/// blocked with no unit individually able to see a way clear.
///
/// Polled periodically (NOT every frame -- "rare-path," see
/// `RuntimeCityBuilder.Update()`'s call site) rather than driven by an
/// event: for each unit that currently `WantsToMove` (docs/25's "wants to
/// move + has a valid destination"), tracks how far it's actually moved
/// since the last poll. Once a unit has made under `ProgressEpsilon` of
/// progress for `StallWindow` seconds, it's stalled -- every OTHER unit
/// within `YieldRadius` gets a temporary sidestep target (a neighbouring
/// hex that is NOT in the blocked set -- "solid buildings are never
/// entered by a sidestepping unit" is enforced right here, at the
/// candidate-selection step, not downstream) that increases its distance
/// from the stalled unit, held for `YieldDuration` seconds.
///
/// Deliberately does NOT move, path, or re-order the stalled unit itself
/// -- "never becomes the primary mover" is satisfied by construction: this
/// class only ever writes a blocker's `UnitCombat.YieldTarget`/
/// `YieldUntil`, which `RuntimeCityBuilder.SteerFollowPath` reads and
/// steers toward for units already calling FollowPath that frame (see that
/// method). A blocker that ISN'T currently pathing (idle standing directly
/// in the way, holding weapon range) doesn't get nudged by this phase --
/// an honest scope limit, not an oversight: forcing a combat-holding or
/// idle-parked unit into motion would be a bigger behavioural change than
/// "give ground to someone stuck," and the corridor-jam acceptance
/// scenario docs/25 specifies is itself made of units that are all already
/// trying to walk through the same gap.
/// </summary>
public sealed class DeadlockManager
{
    public const float StallWindow = 2.5f;      // T: how long with < ProgressEpsilon progress counts as "stalled"
    public const float ProgressEpsilon = 1f;    // meters of movement since the last poll that counts as "making progress"
    public const float YieldDuration = 3f;      // how long a granted sidestep target holds before a blocker resumes its own order
    public const float YieldRadius = 6f;        // how close to the stalled unit a neighbour has to be to be asked to yield

    private sealed class Track
    {
        public Vector3 LastCheckedPosition;
        public bool HasBaseline;
        public float StalledFor;
    }

    private readonly Dictionary<MonsterAgent, Track> _tracks = new Dictionary<MonsterAgent, Track>();

    // Rotates which monster gets scanned FIRST each poll -- same fairness
    // pattern RuntimeCityBuilder's traffic-wake cursor already uses. A
    // standalone harness caught why this matters: without it, a two-unit
    // head-on jam resolves by ALWAYS granting yields to whichever unit
    // happens to sit earlier in the roster list, since it's always the
    // first one scanned that "wins" a given poll's single grant (see
    // Poll's own comment) -- that unit's partner never gets its own turn
    // and gets shoved further and further off its own route every time the
    // early-indexed unit re-stalls, forever. Rotating the scan start each
    // poll means a starved unit eventually gets scanned first too.
    private int _scanCursor;

    /// <summary>One poll pass -- O(N) over `monsters` for stall detection,
    /// plus an O(k) neighbour scan (k = units within `YieldRadius`, not the
    /// whole roster) only for whichever handful are actually stalled this
    /// pass, which the plan's own "rare-path only" framing expects to
    /// usually be zero. `pollInterval` is the caller's own timer period
    /// (used as the progress-accumulation step, not measured internally --
    /// this class has no Unity Time dependency, so it stays testable in a
    /// standalone harness the same way SpatialGrid/MonsterSteeringController
    /// are); `now` is the caller's current clock, stamped onto any yield
    /// grants' `YieldUntil`.</summary>
    public void Poll(List<MonsterAgent> monsters, float pollInterval, float now, IHexObstacleQuery hexQuery)
    {
        // Every unit's own progress tracker updates regardless (so nobody's
        // baseline goes stale while some OTHER unit is being acted on), but
        // at most ONE unit is granted yields per poll pass -- matching the
        // plan's "grants ONE temporary priority" wording literally. This
        // matters, not just for fidelity to the doc: a standalone harness
        // caught that granting every simultaneously-stalled unit its own
        // round of yields in the SAME pass (a head-on pair both qualify at
        // once) makes each back away from the OTHER'S retreat in lockstep
        // -- mutual retreat, forever, with neither ever gaining ground.
        // Resolving one at a time breaks that symmetry: only the blocker
        // yields, the stalled unit advances into the space it opened, and
        // whichever of them is still short of its goal next poll gets its
        // turn -- rotated via `_scanCursor` (see its own comment) so that
        // "its turn" doesn't always mean the same unit.
        var n = monsters.Count;
        MonsterAgent stalledThisPoll = null;
        for (var i = 0; i < n; i++)
        {
            var idx = (_scanCursor + i) % n;
            var m = monsters[idx];
            if (m == null) continue;
            Track track;
            if (!_tracks.TryGetValue(m, out track))
            {
                track = new Track();
                _tracks[m] = track;
            }

            if (!m.WantsToMove)
            {
                track.HasBaseline = false;
                track.StalledFor = 0f;
                continue;
            }

            var pos = m.transform.position;
            if (!track.HasBaseline)
            {
                track.LastCheckedPosition = pos;
                track.HasBaseline = true;
                track.StalledFor = 0f;
                continue;
            }

            var moved = (pos - track.LastCheckedPosition).magnitude;
            track.LastCheckedPosition = pos;
            if (!IsNowStalled(ref track.StalledFor, moved, pollInterval)) continue;

            // first stalled unit found from THIS poll's rotated start wins
            // the grant; the loop keeps going (not breaking) so every
            // remaining unit's own tracker still updates this pass.
            if (stalledThisPoll == null) stalledThisPoll = m;
        }
        if (n > 0) _scanCursor = (_scanCursor + 1) % n;

        if (stalledThisPoll != null) GrantYields(stalledThisPoll, monsters, now, hexQuery);
    }

    /// <summary>The stall-decision arithmetic, pulled out of `Poll` so a
    /// standalone harness can drive it directly against plain floats --
    /// no `MonsterAgent`/`UnitCombat` (both MonoBehaviours, not
    /// constructible outside Unity) required to verify it. `stalledFor` is
    /// the running accumulator (mutated in place, same field `Poll` uses on
    /// its `Track`); returns true (and resets the accumulator) exactly once
    /// the window closes, so a caller sees one grant per stall, not one per
    /// poll tick for as long as the unit stays stuck.</summary>
    public static bool IsNowStalled(ref float stalledFor, float movedSinceLastCheck, float pollInterval)
    {
        if (movedSinceLastCheck >= ProgressEpsilon) { stalledFor = 0f; return false; }
        stalledFor += pollInterval;
        if (stalledFor < StallWindow) return false;
        stalledFor = 0f;
        return true;
    }

    /// <summary>Every OTHER living unit within YieldRadius of the stalled
    /// unit gets a sidestep target, if one exists (a unit already boxed in
    /// on every neighbouring hex is left alone rather than forced
    /// somewhere unsafe).</summary>
    private static void GrantYields(MonsterAgent stalled, List<MonsterAgent> monsters, float now, IHexObstacleQuery hexQuery)
    {
        var pos = stalled.transform.position;
        foreach (var blocker in monsters)
        {
            if (blocker == null || blocker == stalled) continue;
            var fighter = blocker.Fighter;
            if (fighter == null || !fighter.Alive) continue;

            var d = blocker.transform.position - pos;
            d.y = 0f;
            if (d.magnitude > YieldRadius) continue;

            var sidestep = PickSidestepHex(blocker.transform.position, pos, hexQuery);
            if (!sidestep.HasValue) continue;

            fighter.YieldTarget = sidestep;
            fighter.YieldUntil = now + YieldDuration;
        }
    }

    /// <summary>Picks whichever of the blocker's six neighbouring hexes is
    /// passable (in the city, not in the blocked-to-ground set -- ground
    /// blocked, not amphibious/flight, on purpose: a deliberately
    /// conservative choice so a sidestep target is never water, regardless
    /// of whether this particular blocker could actually cross it) AND
    /// increases distance from the stalled unit the most. Returns null if
    /// every neighbour is either blocked or would move the blocker CLOSER
    /// (i.e. every actual escape is a worse direction than standing
    /// still) -- "solid buildings are never entered by a sidestepping
    /// unit" holds because a blocked hex is never even a candidate here,
    /// not because of a check afterward. Public (not just internal to
    /// GrantYields) so a standalone harness can exercise it directly
    /// against a real CityModel/HexCoord (both plain, engine-agnostic
    /// citygen-core types) without needing a live MonsterAgent.</summary>
    public static Vector3? PickSidestepHex(Vector3 blockerPos, Vector3 stalledPos, IHexObstacleQuery hexQuery)
    {
        var home = hexQuery.HexAt(blockerPos);
        var bestDist = (blockerPos - stalledPos).sqrMagnitude;
        HexCoord? best = null;
        foreach (var n in home.Neighbors())
        {
            if (!hexQuery.CityContains(n) || hexQuery.IsBlocked(n)) continue;
            var w = hexQuery.WorldOf(n);
            var dist = (w - stalledPos).sqrMagnitude;
            if (dist > bestDist) { bestDist = dist; best = n; }
        }
        return best.HasValue ? hexQuery.WorldOf(best.Value) : (Vector3?)null;
    }
}

/// <summary>The narrow slice of hex-grid queries DeadlockManager needs to
/// pick a safe sidestep target -- kept as an interface (rather than a
/// direct RuntimeCityBuilder reference) so this stays testable with a
/// small fake in a standalone harness, matching the dependency-light style
/// SpatialGrid/MonsterSteeringController already use.
/// RuntimeCityBuilder implements it directly against its own
/// City/BlockedFor/HexAt/WorldOf.</summary>
public interface IHexObstacleQuery
{
    bool CityContains(HexCoord hex);
    bool IsBlocked(HexCoord hex);
    HexCoord HexAt(Vector3 world);
    Vector3 WorldOf(HexCoord hex);
}
