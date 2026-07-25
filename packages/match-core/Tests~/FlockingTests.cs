using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §5 / docs/27 Phase C acceptance bar: separation
/// min-distance holds, and the blocked-hex clamp is never violated.
/// Alignment/cohesion have no tests here because they aren't implemented
/// yet (see Flocking.cs's own header) -- nothing to verify.</summary>
public class FlockingTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    [Fact]
    public void Separate_pushes_directly_away_from_a_single_overlapping_neighbor_by_half_the_overlap()
    {
        // self at origin, neighbour 1m away on +X, both radius 1.5,
        // spacing 1.0 -> combined minDist 4.0, overlap 3.0, half-push 1.5,
        // straight along -X (away from the neighbour) -- the exact
        // MonsterSteeringController.SeparationForce formula, ported.
        var neighbors = new List<Flocking.Neighbor> { new Flocking.Neighbor(1.0, 0.0, 1.5) };
        var (dx, dz) = Flocking.Separate(0.0, 0.0, 1.5, neighbors, 1.0);

        Assert.Equal(-1.5, dx, 9);
        Assert.Equal(0.0, dz, 9);
    }

    [Fact]
    public void Separate_is_a_no_op_once_clear_of_every_neighbor()
    {
        // 10m apart, combined minDist only 4.0 -- already clear.
        var neighbors = new List<Flocking.Neighbor> { new Flocking.Neighbor(10.0, 0.0, 1.5) };
        var (dx, dz) = Flocking.Separate(0.0, 0.0, 1.5, neighbors, 1.0);

        Assert.Equal(0.0, dx, 9);
        Assert.Equal(0.0, dz, 9);
    }

    [Fact]
    public void Separate_accumulates_cumulatively_across_multiple_neighbors_in_list_order()
    {
        // two neighbours, both dead-overlapping from opposite sides --
        // each neighbour's push is computed against the position already
        // nudged by the earlier one in this same call (the cumulative
        // idiom SeparationForce's own header documents), not the original
        // start position independently for each.
        var neighbors = new List<Flocking.Neighbor>
        {
            new Flocking.Neighbor(1.0, 0.0, 1.5),
            new Flocking.Neighbor(-1.0, 0.0, 1.5),
        };
        var (dx, dz) = Flocking.Separate(0.0, 0.0, 1.5, neighbors, 1.0);

        // first neighbour pushes self to (-1.5, 0); second neighbour is
        // now 0.5m away (not 1m), so its own push uses THAT distance.
        var afterFirst = -1.5;
        var distToSecond = Math.Abs(afterFirst - (-1.0));   // 0.5
        var pushSecond = (4.0 - distToSecond) * 0.5;         // (4.0-0.5)*0.5 = 1.75, direction -X again (away from -1,0)
        var expectedDx = afterFirst - pushSecond;
        Assert.Equal(expectedDx, dx, 9);
        Assert.Equal(0.0, dz, 9);
    }

    [Fact]
    public void MatchState_separation_pass_converges_two_overlapping_units_toward_the_combined_radius()
    {
        // two DISTINCT (nonzero-distance) spawn hexes -- Flocking.Separate
        // deliberately has no push for an EXACTLY coincident pair (same
        // singularity MonsterSteeringController.SeparationForce's own
        // `dist > 1e-3f` guard has: no well-defined "away" direction at
        // zero distance), so a meaningful convergence test needs a real,
        // if small, starting gap. Radius is sized off the ACTUAL spawn
        // distance (not guessed) so this test is robust to whatever the
        // hex grid's real neighbour spacing happens to be.
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbors = new List<HexCoord>(start.Neighbors());
        var hexA = neighbors[0];
        var hexB = neighbors[1];
        var (ax0, az0) = hexA.ToWorld();
        var (bx0, bz0) = hexB.ToWorld();
        var spawnDist = Math.Sqrt((ax0 - bx0) * (ax0 - bx0) + (az0 - bz0) * (az0 - bz0));

        const double spacing = 1.0;   // matches MatchState's own private SeparationSpacing
        var radius = spawnDist / 2.0 + 2.0;   // guarantees real overlap regardless of exact hex geometry
        var minDist = radius * 2.0 + spacing;

        var idA = m.SpawnUnit(0, hexA, speed: 3.0, radius);
        var idB = m.SpawnUnit(1, hexB, speed: 3.0, radius);

        for (var i = 0; i < 200; i++) m.Tick(null);

        var a = m.FindUnit(idA)!;
        var b = m.FindUnit(idB)!;
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        var dist = Math.Sqrt(dx * dx + dz * dz);

        // converges asymptotically toward minDist from below -- never
        // overshoots (each unit's own push only ever closes HALF its
        // current deficit), so a healthy margin below minDist plus a
        // reasonable ceiling both hold after 200 ticks of runway.
        Assert.True(dist > minDist - 0.1, $"two overlapping units should separate close to the combined radius+spacing (got {dist}, expected close to {minDist})");
        Assert.True(dist <= minDist + 1e-6, $"separation must never overshoot past minDist (got {dist}, minDist {minDist})");
    }

    [Fact]
    public void MatchState_separation_never_pushes_a_unit_into_a_blocked_or_off_map_hex()
    {
        // docs/23 §5 acceptance: "blocked-hex clamp never violated across
        // 10k random steps." match-core's own no-ambient-randomness
        // discipline (docs/23 §0) extends naturally to test setups too --
        // this substitutes a deterministic, maximally adversarial setup
        // for literal RNG: 20 units on 20 DISTINCT nearby open hexes
        // (nonzero pairwise distance -- see the convergence test above for
        // why exact coincidence is a no-op singularity, not a stress
        // case), each given a generously large radius so nearly every
        // pair genuinely overlaps and separation has real, sustained
        // pressure to push someone into a neighbouring hex -- checked
        // every tick for every unit: 20 units * 500 ticks = 10,000 checks.
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var start = city.CenterHex;
        var spots = OpenHexesNear(city, start, 20);

        var ids = new List<uint>();
        for (var i = 0; i < spots.Count; i++) ids.Add(m.SpawnUnit(i % 2, spots[i], speed: 3.0, radius: 20.0));

        for (var tick = 0; tick < 500; tick++)
        {
            m.Tick(null);
            for (var i = 0; i < ids.Count; i++)
            {
                var u = m.FindUnit(ids[i])!;
                var hex = NearestHex(u.X, u.Z);
                Assert.True(city.Contains(hex) && !blocked.Contains(hex),
                    $"unit {ids[i]} landed in a blocked/off-map hex at tick {tick} (pos {u.X},{u.Z} -> hex {hex.Q},{hex.R})");
            }
        }
    }

    [Fact]
    public void MatchState_separation_does_not_disturb_widely_spaced_units()
    {
        // sanity: units that were never close together shouldn't drift at
        // all -- separation only ever fires once bodies actually overlap
        // the combined radius+spacing envelope.
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var spots = OpenHexesNear(city, city.CenterHex, 30);
        var far = spots[spots.Count - 1];   // last ring scanned -- farthest from the cluster
        var id = m.SpawnUnit(0, far, speed: 3.0);
        var (ex, ez) = far.ToWorld();

        for (var i = 0; i < 10; i++) m.Tick(null);

        var u = m.FindUnit(id)!;
        Assert.Equal(ex, u.X, 9);
        Assert.Equal(ez, u.Z, 9);
    }

    [Fact]
    public void Same_seed_same_city_same_orders_hashes_identically_with_separation_in_play()
    {
        // docs/23 §13-A's determinism acceptance, re-proven with several
        // units spawned close enough together that separation actually
        // engages every tick -- separation must be exactly as
        // reproducible as pathing/movement already are.
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xF10Cu, TwoPlayers(), city);
            var start = city.CenterHex;
            var ids = new List<uint>();
            for (var i = 0; i < 8; i++) ids.Add(m.SpawnUnit(i % 2, start, speed: 3.0 + (i % 3)));

            var spots = OpenHexesNear(city, start, 8);
            var commands = new List<Command>();
            for (var i = 0; i < ids.Count; i++)
            {
                var goal = spots[i];
                commands.Add(new Command(i % 2, CommandKind.MoveTo, targetEntity: ids[i], argA: goal.Q, argB: goal.R));
            }
            m.Tick(commands);
            for (var i = 0; i < 400; i++) m.Tick(null);
            return m.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    // ---- deterministic hex-picking helpers (no RNG -- fixed scans, matching UnitMovementTests) ----

    private static List<HexCoord> OpenHexesNear(CityModel city, HexCoord center, int count)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var result = new List<HexCoord>();
        for (var r = 0; r <= 60 && result.Count < count; r++)
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

    /// <summary>Test-only mirror of MatchState's own private HexAt --
    /// nearest hex to a world position, same formula.</summary>
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
}
