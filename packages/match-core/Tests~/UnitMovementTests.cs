using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

public class UnitMovementTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    [Fact]
    public void Spawned_unit_starts_idle_at_its_hex()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var here = city.CenterHex;
        var id = m.SpawnUnit(0, here, speed: 5.0);

        var u = m.FindUnit(id);
        Assert.NotNull(u);
        Assert.Equal(UnitOrderKind.Idle, u!.Order);
        var (ex, ez) = here.ToWorld();
        Assert.Equal(ex, u.X, 9);
        Assert.Equal(ez, u.Z, 9);
    }

    [Fact]
    public void MoveTo_command_walks_the_unit_to_an_open_neighbor_and_goes_idle()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var start = city.CenterHex;
        var goal = FindOpenNeighbor(city, start);
        var id = m.SpawnUnit(0, start, speed: 5.0);

        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: id, argA: goal.Q, argB: goal.R) });

        // 20m apart at worst, 5 m/s -> well within 200 ticks (20s)
        for (var i = 0; i < 200 && m.FindUnit(id)!.Order != UnitOrderKind.Idle; i++) m.Tick(null);

        var u = m.FindUnit(id)!;
        Assert.Equal(UnitOrderKind.Idle, u.Order);
        var (gx, gz) = goal.ToWorld();
        Assert.Equal(gx, u.X, 6);
        Assert.Equal(gz, u.Z, 6);
    }

    [Fact]
    public void Unreachable_or_unknown_destination_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var start = city.CenterHex;
        var id = m.SpawnUnit(0, start, speed: 5.0);

        // way off the generated map -- CityModel.Contains(goal) is false
        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: id, argA: 99999, argB: 99999) });
        var u = m.FindUnit(id)!;
        Assert.Equal(UnitOrderKind.Idle, u.Order);
        var (sx, sz) = start.ToWorld();
        Assert.Equal(sx, u.X, 9);
        Assert.Equal(sz, u.Z, 9);

        // an entity ID nobody spawned
        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: 987654, argA: start.Q, argB: start.R) });
        // must not throw, must not create a phantom unit
        Assert.Equal(1, m.UnitCount);
    }

    [Fact]
    public void Same_seed_same_city_same_orders_hashes_identically_across_two_runs()
    {
        // docs/23 §13-A Phase 1.5 acceptance: 100 units, scripted orders,
        // ticked twice, identical MatchState hash.
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xB16u, TwoPlayers(), city);

            var spots = OpenHexesNear(city, city.CenterHex, 100);
            var ids = new List<uint>();
            for (var i = 0; i < spots.Count; i++)
                ids.Add(m.SpawnUnit(i % 2, spots[i], speed: 4.0 + (i % 5)));

            // scripted order stream: unit i walks to spots[(i + 37) % N]
            var commands = new List<Command>();
            for (var i = 0; i < ids.Count; i++)
            {
                var goal = spots[(i + 37) % spots.Count];
                commands.Add(new Command((int)(i % 2), CommandKind.MoveTo, targetEntity: ids[i], argA: goal.Q, argB: goal.R));
            }
            m.Tick(commands);

            for (var i = 0; i < 3000; i++) m.Tick(null);
            return m.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Redirecting_a_mid_path_unit_recomputes_from_its_current_position()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var start = city.CenterHex;
        var far = FarOpenHex(city, start);
        var id = m.SpawnUnit(0, start, speed: 4.0);

        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: id, argA: far.Q, argB: far.R) });
        for (var i = 0; i < 5; i++) m.Tick(null);   // partway there, NOT at a hex center anymore
        var mid = m.FindUnit(id)!;
        Assert.Equal(UnitOrderKind.MoveTo, mid.Order);

        var newGoal = FindOpenNeighbor(city, start);
        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: id, argA: newGoal.Q, argB: newGoal.R) });
        for (var i = 0; i < 200 && m.FindUnit(id)!.Order != UnitOrderKind.Idle; i++) m.Tick(null);

        var u = m.FindUnit(id)!;
        Assert.Equal(UnitOrderKind.Idle, u.Order);
        var (gx, gz) = newGoal.ToWorld();
        Assert.Equal(gx, u.X, 6);
        Assert.Equal(gz, u.Z, 6);
    }

    [Fact]
    public void MoveQueue_from_idle_starts_walking_immediately()
    {
        // docs/27 Phase B: nothing in flight -- a queued waypoint on an
        // Idle unit starts it moving right away, same as MoveTo.
        var city = SmallCity();
        var m = MatchState.Create(7u, TwoPlayers(), city);
        var start = city.CenterHex;
        var goal = FindOpenNeighbor(city, start);
        var id = m.SpawnUnit(0, start, speed: 5.0);

        m.Tick(new List<Command> { new Command(0, CommandKind.MoveQueue, targetEntity: id, argA: goal.Q, argB: goal.R) });
        Assert.Equal(UnitOrderKind.MoveTo, m.FindUnit(id)!.Order);
    }

    [Fact]
    public void MoveQueue_walks_multiple_waypoints_in_order_with_no_idle_tick_between_legs()
    {
        var city = SmallCity();
        var m = MatchState.Create(8u, TwoPlayers(), city);
        var start = city.CenterHex;
        var spots = OpenHexesNear(city, start, 4);   // spots[0] is `start` itself (ring 0)
        var waypointA = spots[1];
        var waypointB = spots[2];
        var id = m.SpawnUnit(0, start, speed: 5.0);

        // MoveTo starts the first leg; MoveQueue appends the second --
        // exactly what a plain-click-then-shift-click sequence produces.
        m.Tick(new List<Command>
        {
            new Command(0, CommandKind.MoveTo, targetEntity: id, argA: waypointA.Q, argB: waypointA.R),
            new Command(0, CommandKind.MoveQueue, targetEntity: id, argA: waypointB.Q, argB: waypointB.R),
        });

        var (ax, az) = waypointA.ToWorld();
        var sawWaypointA = false;
        for (var i = 0; i < 400 && m.FindUnit(id)!.Order != UnitOrderKind.Idle; i++)
        {
            m.Tick(null);
            var u = m.FindUnit(id)!;
            if (Math.Abs(u.X - ax) < 0.01 && Math.Abs(u.Z - az) < 0.01) sawWaypointA = true;
        }

        Assert.True(sawWaypointA, "the unit actually passed through waypoint A on the way to B, not a straight line to B");
        var final = m.FindUnit(id)!;
        Assert.Equal(UnitOrderKind.Idle, final.Order);
        var (bx, bz) = waypointB.ToWorld();
        Assert.Equal(bx, final.X, 6);
        Assert.Equal(bz, final.Z, 6);
    }

    [Fact]
    public void MoveTo_after_MoveQueue_drops_the_previously_queued_waypoint()
    {
        // docs/27 Phase B: a plain (non-shift, REPLACE) move always wins
        // over whatever was queued -- "forget what I was doing, go here
        // instead," matching Unity's own !queue OrderMove semantics.
        var city = SmallCity();
        var m = MatchState.Create(9u, TwoPlayers(), city);
        var start = city.CenterHex;
        var spots = OpenHexesNear(city, start, 4);
        var queuedButDropped = spots[1];
        var replacementGoal = spots[2];
        var id = m.SpawnUnit(0, start, speed: 5.0);

        m.Tick(new List<Command>
        {
            new Command(0, CommandKind.MoveTo, targetEntity: id, argA: start.Q, argB: start.R), // no-op-ish: already there
            new Command(0, CommandKind.MoveQueue, targetEntity: id, argA: queuedButDropped.Q, argB: queuedButDropped.R),
        });
        // now issue a REPLACE move before the queued waypoint is ever reached
        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: id, argA: replacementGoal.Q, argB: replacementGoal.R) });

        for (var i = 0; i < 400 && m.FindUnit(id)!.Order != UnitOrderKind.Idle; i++) m.Tick(null);

        var final = m.FindUnit(id)!;
        Assert.Equal(UnitOrderKind.Idle, final.Order);
        var (gx, gz) = replacementGoal.ToWorld();
        Assert.Equal(gx, final.X, 6);
        Assert.Equal(gz, final.Z, 6);
        // and it must NOT have ended up walking on to the dropped waypoint afterward
        for (var i = 0; i < 50; i++) m.Tick(null);
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(id)!.Order);
        Assert.Equal(gx, m.FindUnit(id)!.X, 6);
    }

    [Fact]
    public void Same_seed_same_city_same_queued_orders_hashes_identically_across_two_runs()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xFEEDu, TwoPlayers(), city);
            var start = city.CenterHex;
            var spots = OpenHexesNear(city, start, 50);
            var ids = new List<uint>();
            for (var i = 0; i < spots.Count; i++)
                ids.Add(m.SpawnUnit(i % 2, spots[i], speed: 3.0 + (i % 4)));

            var commands = new List<Command>();
            for (var i = 0; i < ids.Count; i++)
            {
                var legA = spots[(i + 11) % spots.Count];
                var legB = spots[(i + 23) % spots.Count];
                commands.Add(new Command(i % 2, CommandKind.MoveTo, targetEntity: ids[i], argA: legA.Q, argB: legA.R));
                commands.Add(new Command(i % 2, CommandKind.MoveQueue, targetEntity: ids[i], argA: legB.Q, argB: legB.R));
            }
            m.Tick(commands);
            for (var i = 0; i < 3000; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Units_iterate_and_hash_in_entity_id_order_not_insertion_coincidence()
    {
        var city = SmallCity();
        var m = MatchState.Create(6u, TwoPlayers(), city);
        var spots = OpenHexesNear(city, city.CenterHex, 5);
        var ids = new List<uint>();
        foreach (var s in spots) ids.Add(m.SpawnUnit(0, s, 3.0));

        for (var i = 0; i < ids.Count; i++)
            Assert.Equal(ids[i], m.UnitAt(i).EntityId);
    }

    // ---- deterministic hex-picking helpers (no RNG -- fixed scans) ----

    private static HexCoord FindOpenNeighbor(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n)) return n;
        throw new InvalidOperationException("no open neighbor found -- city too dense for this test");
    }

    private static HexCoord FarOpenHex(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 8; r >= 1; r--)
        {
            foreach (var h in from.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) return h;
        }
        throw new InvalidOperationException("no far open hex found");
    }

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
            throw new InvalidOperationException($"only found {result.Count} open hexes, needed {count} -- widen the search or shrink the test");
        return result;
    }
}
