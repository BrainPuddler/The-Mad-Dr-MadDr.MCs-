using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §9 (the hardening audit phase): the creator's law
/// verbatim -- "Must adhere to the physical boundaries of the playfield,
/// buildings are solid and cannot be walked through unless they are
/// destroyed." This is match-core's own slice of that audit: the
/// "headless fuzz harness [that] drives 200 units with random orders for
/// 100k ticks and asserts zero frames where any unit's hex is in the
/// blocked set or off-map," for the ONE mover this package owns
/// (`SimUnit.Tick` + `MatchState.ApplySeparationPass`, driven by
/// `CommandKind.MoveTo`/`MoveQueue`/`BuildStructure`). The OTHER movers
/// docs/23 §9 names (MonsterAgent walk/fly, Tank steer, TrafficCar,
/// Citizen, flocking output, minimap-ordered moves, fusion channel drift,
/// anomaly wander) are Unity-side code this package has no access to at
/// all -- audited separately, by reading, in docs/12's Phase 9 entry
/// (no Editor exists in this environment to runtime-fuzz them the same
/// way).
///
/// Random here means "deterministic, seed-driven" (docs/23 §0: never
/// `Math.Random`/`System.Random`) -- the SAME discipline
/// `FlockingTests.cs`'s own 10k-check stress test already uses, extended
/// from a fixed adversarial setup to genuinely varied per-tick
/// decisions.</summary>
public class ContainmentFuzzTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel FuzzCity() => CityGenerator.Generate(90210u, CityPreset.SmallTown());

    private static List<HexCoord> OpenHexesNear(CityModel city, HexCoord center, int count)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var result = new List<HexCoord>();
        for (var r = 0; r <= 80 && result.Count < count; r++)
        {
            foreach (var h in center.Ring(r))
            {
                if (!city.Contains(h) || blocked.Contains(h)) continue;
                result.Add(h);
                if (result.Count >= count) break;
            }
        }
        if (result.Count < count)
            throw new InvalidOperationException($"only found {result.Count} open hexes, needed {count}");
        return result;
    }

    private static HexCoord NearestHex(double x, double z)
    {
        var size = HexCoord.HexMeters / Math.Sqrt(3);
        var approxR = z / (1.5 * size);
        var rGuess = (int)Math.Round(approxR);
        var best = new HexCoord(0, 0);
        var bestDistSq = double.MaxValue;
        for (var r = rGuess - 1; r <= rGuess + 1; r++)
        {
            var approxQ = (x / size - Math.Sqrt(3) / 2 * r) / Math.Sqrt(3);
            var qGuess = (int)Math.Round(approxQ);
            for (var q = qGuess - 1; q <= qGuess + 1; q++)
            {
                var candidate = new HexCoord(q, r);
                var (cx, cz) = candidate.ToWorld();
                var dx = cx - x;
                var dz = cz - z;
                var distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq) { bestDistSq = distSq; best = candidate; }
            }
        }
        return best;
    }

    private static void AssertContained(MatchState m, CityModel city, HashSet<HexCoord> blocked, IReadOnlyList<uint> ids, int tick)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var u = m.FindUnit(ids[i]);
            if (u == null || !u.IsAlive) continue;
            var hex = NearestHex(u.X, u.Z);
            Assert.True(city.Contains(hex), $"unit {ids[i]} is OFF-MAP at tick {tick} (pos {u.X},{u.Z} -> hex {hex.Q},{hex.R})");
            Assert.False(blocked.Contains(hex), $"unit {ids[i]} is INSIDE a blocked hex at tick {tick} (pos {u.X},{u.Z} -> hex {hex.Q},{hex.R})");
        }
    }

    [Fact]
    public void Fuzz_200_units_random_moves_never_leave_a_blocked_or_off_map_hex()
    {
        const int unitCount = 200;
        const int totalTicks = 100_000;
        const int reissueEveryTicks = 47;   // prime, so units don't all reissue in lockstep

        var city = FuzzCity();
        var m = MatchState.Create(0xFEED5EEDu, TwoPlayers(), city);
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var spawnSpots = OpenHexesNear(city, city.CenterHex, unitCount);
        var candidateSpots = OpenHexesNear(city, city.CenterHex, 400);   // richer destination pool than spawn points alone

        var rng = new SimRng(24601u);
        var ids = new List<uint>(unitCount);
        for (var i = 0; i < unitCount; i++)
        {
            // Varied speeds (including a few very fast movers) -- docs/23
            // §9's own "no-tunneling at speed" concern is sharpest for the
            // fastest bodies on the field.
            var speed = 2.0 + rng.IntRange(20);
            ids.Add(m.SpawnUnit(i % 2, spawnSpots[i], speed, radius: 1.0 + rng.IntRange(3)));
        }

        AssertContained(m, city, blocked, ids, 0);

        for (var tick = 0; tick < totalTicks; tick++)
        {
            List<Command>? orders = null;
            if (tick % reissueEveryTicks == 0)
            {
                // A handful of units (not all 200 -- keeps per-tick
                // pathfinding cost bounded) get a fresh random order this
                // tick: mostly a plain MoveTo, sometimes a queued
                // waypoint appended behind whatever's already in flight.
                orders = new List<Command>();
                var reissueCount = 1 + rng.IntRange(6);
                for (var k = 0; k < reissueCount; k++)
                {
                    var unitIndex = rng.IntRange(unitCount);
                    var dest = candidateSpots[rng.IntRange(candidateSpots.Count)];
                    var kind = rng.IntRange(4) == 0 ? CommandKind.MoveQueue : CommandKind.MoveTo;
                    orders.Add(new Command(unitIndex % 2, kind, ids[unitIndex], dest.Q, dest.R));
                }
            }

            m.Tick(orders);
            AssertContained(m, city, blocked, ids, tick + 1);
        }
    }

    [Fact]
    public void Fuzz_with_building_churn_preserves_containment_and_exact_footprint_reopening()
    {
        // A smaller-scale companion to the movement-only fuzz above,
        // exercising docs/23 §9's OTHER acceptance line: "destroyed
        // buildings must reopen exactly their footprint... new §2 player
        // buildings must close/open identically." Real BuildStructure +
        // ApplyBuildingDamage calls mutate match-core's OWN blocked set
        // live, mid-fuzz, while units keep moving through the same space
        // -- containment must hold across that mutation, not just around
        // a static map.
        const int unitCount = 40;
        const int totalTicks = 6_000;

        var city = FuzzCity();
        var m = MatchState.Create(0xB0BAu, TwoPlayers(), city);
        var initialBlocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var spawnSpots = OpenHexesNear(city, city.CenterHex, unitCount);
        var buildSpots = OpenHexesNear(city, city.CenterHex, unitCount + 60);
        buildSpots.RemoveRange(0, unitCount);   // don't build directly on a spawned unit's own hex

        var rng = new SimRng(31337u);
        var ids = new List<uint>(unitCount);
        for (var i = 0; i < unitCount; i++)
            ids.Add(m.SpawnUnit(i % 2, spawnSpots[i], speed: 2.0 + rng.IntRange(10)));

        m.Player(0).Grant(ResourceKind.Bones, 100000);
        m.Player(0).Grant(ResourceKind.Blood, 100000);
        m.Player(0).Grant(ResourceKind.Fuel, 100000);
        m.Player(0).Grant(ResourceKind.Parts, 100000);

        // Track hexes we've deliberately built on and later destroyed, to
        // spot-check exact-footprint reopening at the end (BloodStorage
        // is single-hex today -- docs/12's Phase 2 entry already flags
        // multi-hex player footprints as a later slice, not invented
        // here).
        var everBuiltOn = new List<HexCoord>();
        var everDestroyedIds = new List<uint>();

        // The "currently blocked" set is derived FRESH from live,
        // queryable state each time -- the city's own static blocked-to-
        // ground set (never changes) plus every not-yet-destroyed
        // building's hex, read directly off `m`. Deliberately NOT a
        // hand-maintained mirror updated ad hoc on every build/destroy
        // event: a first draft did that and it silently drifted out of
        // sync with match-core's own real internal state the moment a
        // BuildStructure command silently no-op'd for a reason this test
        // wasn't tracking (funds, an already-occupied hex...) -- exactly
        // the class of bug this whole audit phase exists to catch, just
        // in the TEST harness rather than production this time.
        HashSet<HexCoord> CurrentlyBlocked()
        {
            var s = new HashSet<HexCoord>(initialBlocked);
            for (var i = 0; i < m.BuildingCount; i++)
            {
                var b = m.BuildingAt(i);
                if (b.State != BuildingState.Destroyed) s.Add(b.Hex);
            }
            return s;
        }

        for (var tick = 0; tick < totalTicks; tick++)
        {
            List<Command>? orders = null;
            if (tick % 53 == 0)
            {
                orders = new List<Command>();
                var unitIndex = rng.IntRange(unitCount);
                var dest = spawnSpots[rng.IntRange(spawnSpots.Count)];
                orders.Add(new Command(unitIndex % 2, CommandKind.MoveTo, ids[unitIndex], dest.Q, dest.R));
            }

            if (tick % 211 == 0 && buildSpots.Count > 0)
            {
                var hex = buildSpots[buildSpots.Count - 1];
                buildSpots.RemoveAt(buildSpots.Count - 1);
                (orders ??= new List<Command>()).Add(
                    new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.FuelStorage, argA: hex.Q, argB: hex.R));
                everBuiltOn.Add(hex);
            }

            m.Tick(orders);

            // Destroy a fully-constructed building every so often.
            if (tick % 307 == 0)
            {
                for (var i = 0; i < m.BuildingCount; i++)
                {
                    var b = m.BuildingAt(i);
                    if (b.State == BuildingState.Complete && !everDestroyedIds.Contains(b.EntityId))
                    {
                        m.ApplyBuildingDamage(b.EntityId, b.MaxHp);
                        everDestroyedIds.Add(b.EntityId);
                        break;
                    }
                }
            }

            AssertContained(m, city, CurrentlyBlocked(), ids, tick + 1);
        }

        Assert.NotEmpty(everBuiltOn);
        Assert.NotEmpty(everDestroyedIds);

        // Exact-footprint close/open: every destroyed building's OWN hex
        // (and only that hex -- single-hex player footprints) must be
        // walkable again, while any building still standing keeps its
        // hex solid.
        var finalBlocked = CurrentlyBlocked();
        foreach (var id in everDestroyedIds)
        {
            var b = m.FindBuilding(id)!;
            Assert.Equal(BuildingState.Destroyed, b.State);
            Assert.DoesNotContain(b.Hex, finalBlocked);

            // A unit can actually be ORDERED there and arrive -- not just
            // "not in the blocked set" in the abstract. Spawned directly
            // on an OPEN neighbour (never full cross-city pathing, which
            // could legitimately fail for an unrelated reason -- e.g. a
            // reopened hex boxed in by OTHER still-standing buildings on
            // every other side, a real property of the random layout,
            // not a containment bug) so this isolates exactly the one
            // claim being tested: this specific hex is steppable now.
            HexCoord? openNeighbor = null;
            foreach (var n in b.Hex.Neighbors())
                if (city.Contains(n) && !finalBlocked.Contains(n)) { openNeighbor = n; break; }
            Assert.True(openNeighbor.HasValue, $"destroyed building {b.Hex} has no open neighbour to approach from");

            // speed 3.0 m/s at a 0.1s tick is only 0.3 m/tick -- crossing
            // one real 20m hex (HexCoord.HexMeters) takes ~67 ticks, not
            // the handful a first draft assumed (that mismatch was this
            // test's own bug, not a movement bug: a much faster walker
            // and a generous tick ceiling below both just avoid re-
            // tripping over the same arithmetic).
            var walkerId = m.SpawnUnit(0, openNeighbor!.Value, speed: 30.0);
            m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, walkerId, b.Hex.Q, b.Hex.R) });
            for (var i = 0; i < 200 && m.FindUnit(walkerId)!.Order != UnitOrderKind.Idle; i++) m.Tick(null);
            var landedHex = NearestHex(m.FindUnit(walkerId)!.X, m.FindUnit(walkerId)!.Z);
            Assert.Equal(b.Hex, landedHex);
        }

        for (var i = 0; i < m.BuildingCount; i++)
        {
            var b = m.BuildingAt(i);
            if (b.State != BuildingState.Destroyed) Assert.Contains(b.Hex, finalBlocked);
        }
    }

    [Fact]
    public void An_extremely_fast_unit_never_tunnels_through_intermediate_path_hexes()
    {
        // docs/23 §9: "no-tunneling at speed (step length vs hex size at
        // max speed)." SimUnit.Tick's movement loop is tunnel-proof BY
        // CONSTRUCTION, not by luck: it consumes budget by snapping
        // EXACTLY onto each successive `_path[_pathIndex]` node (an
        // already-adjacent, already-blocked-set-validated hex from
        // HexPathfinder) one at a time, no matter how large a single
        // tick's budget is -- there is no code path that advances
        // position by a raw distance without first checking whether that
        // distance reaches a real path node. This test pins the
        // ARITHMETIC side of that claim (a very large speed still lands
        // EXACTLY on the correct path node, not somewhere drifted past
        // it) since the geometric side (every path node is itself
        // unblocked) is already HexPathfinder's own job/tests.
        var city = FuzzCity();
        var m = MatchState.Create(0x51EEDu, TwoPlayers(), city);
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var start = city.CenterHex;
        var far = OpenHexesNear(city, start, 40)[39];   // a real, far, reachable destination

        // Speed high enough to cross the ENTIRE map in a single tick if
        // movement were naively continuous (0.1s tick, so >2000 m/s here
        // against a ~2km map) -- an intentionally absurd, adversarial
        // value, not a realistic gameplay one.
        var id = m.SpawnUnit(0, start, speed: 50000.0);
        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, id, far.Q, far.R) });

        var u = m.FindUnit(id)!;
        m.Tick(null);   // the one tick this absurd speed needs to finish the whole path

        Assert.Equal(UnitOrderKind.Idle, u.Order);   // arrived, not stuck mid-path
        var landedHex = NearestHex(u.X, u.Z);
        Assert.Equal(far, landedHex);
        Assert.DoesNotContain(landedHex, blocked);
        Assert.True(city.Contains(landedHex));
    }
}
