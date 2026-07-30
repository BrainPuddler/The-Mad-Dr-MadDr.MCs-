using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §2 Phase 2 acceptance: placement legality (roads/
/// water/occupied rejected), cost debit, construction lifecycle,
/// destruction reopens the hex, determinism of build queues. Wallet-CAP
/// enforcement is deliberately NOT tested here -- docs/23 §3 lists
/// "storage caps from buildings" as Phase 3's own task, not Phase 2's; see
/// BuildingDef.cs's header for why.</summary>
public class BuildingTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var h in center.Ring(0)) if (city.Contains(h) && !blocked.Contains(h)) return h;
        for (var r = 1; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) return h;
        throw new InvalidOperationException("no open hex found");
    }

    private static HexCoord FindBlockedHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 0; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && blocked.Contains(h)) return h;
        throw new InvalidOperationException("no blocked hex found -- city too sparse for this test");
    }

    [Fact]
    public void SpawnHqForPlayer_isCompleteImmediatelyAndBlocksItsHex()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var hqHex = FindOpenHex(city, city.CenterHex);

        var id = m.SpawnHqForPlayer(0, hqHex);

        var hq = m.FindBuilding(id)!;
        Assert.Equal(BuildingState.Complete, hq.State);
        Assert.Equal(BuildingKind.Hq, hq.Kind);
        Assert.Equal(BuildingDef.Get(BuildingKind.Hq).MaxHp, hq.MaxHp);

        // the HQ's own hex is now blocked -- a build attempt there fails
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hqHex.Q, argB: hqHex.R) });
        Assert.Equal(1, m.BuildingCount);   // still just the HQ -- the second build was rejected
    }

    [Fact]
    public void SpawnFactoryForPlayer_isCompleteImmediatelyAndBlocksItsHex()
    {
        // 2026-07 amendment: the starting-Factory bootstrap grant (docs/12
        // "give the player one fully functional factory on startup"),
        // same shape as SpawnHqForPlayer above.
        var city = SmallCity();
        var m = MatchState.Create(11u, TwoPlayers(), city);
        var factoryHex = FindOpenHex(city, city.CenterHex);

        var id = m.SpawnFactoryForPlayer(0, factoryHex);

        var factory = m.FindBuilding(id)!;
        Assert.Equal(BuildingState.Complete, factory.State);
        Assert.Equal(BuildingKind.Factory, factory.Kind);
        Assert.Equal(BuildingDef.Get(BuildingKind.Factory).MaxHp, factory.MaxHp);
        Assert.Equal(0, factory.PlayerIndex);

        // free -- no wallet debit for the starting grant
        Assert.Equal(0, m.Player(0).Wallet(ResourceKind.Bones));
        Assert.Equal(0, m.Player(0).Wallet(ResourceKind.Blood));

        // Complete (not UnderConstruction) the instant it's spawned --
        // e.g. CanTrainUnit's own readiness gate is already satisfied,
        // unlike a player-built Factory which starts UnderConstruction.
        Assert.False(m.CanTrainUnit(0, id, RosterUnitKind.Rifleman));   // player 0 here is MadDoctor -- wrong roster kind, not wrong readiness; TrainUnitTests covers the readiness gate itself

        // the Factory's own hex is now blocked -- a second build there fails
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: factoryHex.Q, argB: factoryHex.R) });
        Assert.Equal(1, m.BuildingCount);   // still just the Factory -- the second build was rejected
    }

    [Fact]
    public void BuildStructure_debitsExactCostAndStartsUnderConstruction()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 50);
        player.Grant(ResourceKind.Blood, 50);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });

        Assert.Equal(1, m.BuildingCount);
        var b = m.BuildingAt(0);
        Assert.Equal(BuildingKind.BloodStorage, b.Kind);
        Assert.Equal(BuildingState.UnderConstruction, b.State);
        Assert.Equal(hex.Q, b.Hex.Q);
        Assert.Equal(hex.R, b.Hex.R);

        // docs/22's real Blood Bank cost: 20 Bones + 10 Blood
        Assert.Equal(50 - 20, player.Wallet(ResourceKind.Bones));
        Assert.Equal(50 - 10, player.Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void BuildStructure_unaffordable_isASilentNoOpThatSpendsNothing()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 5);   // BloodStorage needs 20

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });

        Assert.Equal(0, m.BuildingCount);
        Assert.Equal(5, player.Wallet(ResourceKind.Bones));   // untouched -- all-or-nothing, never a partial spend
        Assert.Equal(0, player.Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void BuildStructure_onAnAlreadyBlockedHex_isANoOp()
    {
        // docs/23 §2 acceptance: "placement legality (roads/water/occupied rejected)"
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var blockedHex = FindBlockedHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: blockedHex.Q, argB: blockedHex.R) });

        Assert.Equal(0, m.BuildingCount);
        Assert.Equal(100, player.Wallet(ResourceKind.Bones));
    }

    [Fact]
    public void BuildStructure_onAnOffMapHex_isANoOp()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: 99999, argB: 99999) });

        Assert.Equal(0, m.BuildingCount);
    }

    [Fact]
    public void BuildStructure_targetingHqKind_isANoOp()
    {
        // the HQ is generator-placed only (SpawnHqForPlayer) -- never a
        // valid BuildStructure target, per CommandKind.BuildStructure's
        // own doc comment.
        var city = SmallCity();
        var m = MatchState.Create(6u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.Hq, argA: hex.Q, argB: hex.R) });

        Assert.Equal(0, m.BuildingCount);
        Assert.Equal(1000, player.Wallet(ResourceKind.Bones));
    }

    [Fact]
    public void Construction_completesExactlyAtItsBuildTimeTicks_notBeforeOrAfter()
    {
        var city = SmallCity();
        var m = MatchState.Create(7u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });

        var buildTime = BuildingDef.Get(BuildingKind.BloodStorage).BuildTimeTicks;
        var b = m.BuildingAt(0);

        // the Tick() that issued the BuildStructure command already ran
        // this building's own construction Tick() once in that SAME call
        // (ApplyCommand runs before the per-building Tick loop, both
        // inside one Tick() invocation) -- so buildTime-1 ticks remain,
        // not buildTime.
        for (var i = 0; i < buildTime - 2; i++) m.Tick(null);
        Assert.Equal(BuildingState.UnderConstruction, b.State);

        m.Tick(null);   // the exact tick that finishes it
        Assert.Equal(BuildingState.Complete, b.State);
    }

    [Fact]
    public void ApplyBuildingDamage_destroysAtZeroHpAndReopensTheHexForRebuilding()
    {
        var city = SmallCity();
        var m = MatchState.Create(8u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });
        var id = m.BuildingAt(0).EntityId;

        var maxHp = BuildingDef.Get(BuildingKind.BloodStorage).MaxHp;
        m.ApplyBuildingDamage(id, maxHp);   // exactly lethal

        var building = m.FindBuilding(id)!;
        Assert.Equal(BuildingState.Destroyed, building.State);
        Assert.Equal(0, building.Hp);

        // the hex is open again -- a second structure can now be built there
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.FuelStorage, argA: hex.Q, argB: hex.R) });
        Assert.Equal(2, m.BuildingCount);
        Assert.Equal(BuildingKind.FuelStorage, m.BuildingAt(1).Kind);
    }

    [Fact]
    public void ApplyBuildingDamage_clampsAtZeroAndIsANoOpOnceAlreadyDestroyed()
    {
        var city = SmallCity();
        var m = MatchState.Create(9u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });
        var id = m.BuildingAt(0).EntityId;

        m.ApplyBuildingDamage(id, 99999);   // wildly overkill
        var building = m.FindBuilding(id)!;
        Assert.Equal(0, building.Hp);
        Assert.Equal(BuildingState.Destroyed, building.State);

        m.ApplyBuildingDamage(id, 50);   // must not throw or go negative
        Assert.Equal(0, building.Hp);
        Assert.Equal(BuildingState.Destroyed, building.State);
    }

    [Fact]
    public void IsDamaged_becomesTrueAtOrBelowHalfMaxHpOnlyOnceComplete()
    {
        var city = SmallCity();
        var m = MatchState.Create(10u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });
        var id = m.BuildingAt(0).EntityId;
        var building = m.FindBuilding(id)!;

        // still under construction: never reads as "damaged" even at partial HP semantics N/A here (HP is full)
        Assert.False(building.IsDamaged);

        var buildTime = BuildingDef.Get(BuildingKind.BloodStorage).BuildTimeTicks;
        for (var i = 0; i < buildTime; i++) m.Tick(null);
        Assert.Equal(BuildingState.Complete, building.State);
        Assert.False(building.IsDamaged);   // full HP, complete

        m.ApplyBuildingDamage(id, building.MaxHp / 2);   // exactly half gone
        Assert.True(building.IsDamaged);
    }

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_buildings_and_units_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xB01Du, TwoPlayers(), city);
            var hqHexP0 = FindOpenHex(city, city.CenterHex);
            m.SpawnHqForPlayer(0, hqHexP0);

            var spots = new List<HexCoord>();
            var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
            for (var r = 1; r <= 30 && spots.Count < 6; r++)
                foreach (var h in city.CenterHex.Ring(r))
                    if (city.Contains(h) && !blocked.Contains(h)) { spots.Add(h); if (spots.Count >= 6) break; }

            var p0 = m.Player(0);
            var p1 = m.Player(1);
            p0.Grant(ResourceKind.Bones, 200);
            p0.Grant(ResourceKind.Blood, 200);
            p1.Grant(ResourceKind.Bones, 200);
            p1.Grant(ResourceKind.Fuel, 200);

            var commands = new List<Command>
            {
                new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: spots[0].Q, argB: spots[0].R),
                new Command(1, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.FuelStorage, argA: spots[1].Q, argB: spots[1].R),
                new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.Factory, argA: spots[2].Q, argB: spots[2].R),
            };
            m.Tick(commands);

            var id0 = m.SpawnUnit(0, spots[3], speed: 4.0);
            var id1 = m.SpawnUnit(1, spots[4], speed: 5.0);
            m.Tick(new List<Command>
            {
                new Command(0, CommandKind.MoveTo, targetEntity: id0, argA: spots[5].Q, argB: spots[5].R),
                new Command(1, CommandKind.MoveTo, targetEntity: id1, argA: spots[0].Q, argB: spots[0].R),
            });

            for (var i = 0; i < 200; i++) m.Tick(null);
            return m.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Headless_harness_builds_the_full_tech_tree_from_a_scripted_command_list_deterministically_twice()
    {
        // docs/23 §2's own literal Phase 2 acceptance bar: "headless
        // harness builds each faction's full tech tree from a scripted
        // command list, deterministically, twice, identical hashes."
        // Every buildable kind (all of BuildingKind except Hq, which is
        // generator-placed, never commanded) queued in one scripted
        // command list.
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0x7EC4u, TwoPlayers(), city);
            var player = m.Player(0);
            foreach (ResourceKind r in Enum.GetValues(typeof(ResourceKind))) player.Grant(r, 1000);

            var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
            var spots = new List<HexCoord>();
            for (var r = 1; r <= 30 && spots.Count < 7; r++)
                foreach (var h in city.CenterHex.Ring(r))
                    if (city.Contains(h) && !blocked.Contains(h)) { spots.Add(h); if (spots.Count >= 7) break; }

            var buildableKinds = new[]
            {
                BuildingKind.BloodStorage, BuildingKind.FuelPump, BuildingKind.FuelStorage,
                BuildingKind.PartsStorage, BuildingKind.HarvestPost, BuildingKind.Factory, BuildingKind.Defense,
            };
            Assert.Equal(spots.Count, buildableKinds.Length);   // sanity: one open hex per kind

            var commands = new List<Command>();
            for (var i = 0; i < buildableKinds.Length; i++)
                commands.Add(new Command(0, CommandKind.BuildStructure, targetEntity: (uint)buildableKinds[i], argA: spots[i].Q, argB: spots[i].R));
            m.Tick(commands);

            // run past the slowest build time so every structure completes
            var maxBuildTime = 0;
            foreach (var k in buildableKinds) maxBuildTime = Math.Max(maxBuildTime, BuildingDef.Get(k).BuildTimeTicks);
            for (var i = 0; i < maxBuildTime; i++) m.Tick(null);

            return m.Hash();
        }

        var first = Run();
        var second = Run();
        Assert.Equal(first, second);
    }

    // ---- CanPlaceBuilding: the read-only preview query a Unity ghost-
    // placement cursor calls every frame, sharing ApplyBuildStructure's
    // own validation so the two can never disagree ----

    [Fact]
    public void CanPlaceBuilding_isTrueForAnAffordableOpenHex()
    {
        var city = SmallCity();
        var m = MatchState.Create(11u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.Player(0).Grant(ResourceKind.Bones, 20);
        m.Player(0).Grant(ResourceKind.Blood, 10);

        Assert.True(m.CanPlaceBuilding(0, BuildingKind.BloodStorage, hex));
    }

    [Fact]
    public void CanPlaceBuilding_isFalseWhenUnaffordable()
    {
        var city = SmallCity();
        var m = MatchState.Create(12u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.Player(0).Grant(ResourceKind.Bones, 5);   // BloodStorage needs 20

        Assert.False(m.CanPlaceBuilding(0, BuildingKind.BloodStorage, hex));
    }

    [Fact]
    public void CanPlaceBuilding_isFalseOnABlockedHex()
    {
        var city = SmallCity();
        var m = MatchState.Create(13u, TwoPlayers(), city);
        var blockedHex = FindBlockedHex(city, city.CenterHex);
        m.Player(0).Grant(ResourceKind.Bones, 1000);
        m.Player(0).Grant(ResourceKind.Blood, 1000);

        Assert.False(m.CanPlaceBuilding(0, BuildingKind.BloodStorage, blockedHex));
    }

    [Fact]
    public void CanPlaceBuilding_isFalseOffMapAndForHqKind()
    {
        var city = SmallCity();
        var m = MatchState.Create(14u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.Player(0).Grant(ResourceKind.Bones, 1000);
        m.Player(0).Grant(ResourceKind.Blood, 1000);

        Assert.False(m.CanPlaceBuilding(0, BuildingKind.BloodStorage, new HexCoord(99999, 99999)));
        Assert.False(m.CanPlaceBuilding(0, BuildingKind.Hq, hex));
    }

    [Fact]
    public void CanPlaceBuilding_reflectsAHexBecomingOccupiedAfterAPriorBuild()
    {
        // the exact "preview before AND after another build lands" case a
        // live ghost cursor depends on: CanPlaceBuilding must track the
        // SAME _blockedToGround set ApplyBuildStructure itself mutates.
        var city = SmallCity();
        var m = MatchState.Create(15u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.Player(0).Grant(ResourceKind.Bones, 1000);
        m.Player(0).Grant(ResourceKind.Blood, 1000);

        Assert.True(m.CanPlaceBuilding(0, BuildingKind.FuelStorage, hex));
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });
        Assert.False(m.CanPlaceBuilding(0, BuildingKind.FuelStorage, hex));   // now occupied
    }

    // ---- TicksUntilComplete: the construction-progress readout a
    // BaseDresser scaffold-percent visual reads ----

    [Fact]
    public void TicksUntilComplete_countsDownToZeroAndStaysZeroOnceComplete()
    {
        var city = SmallCity();
        var m = MatchState.Create(16u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.Player(0).Grant(ResourceKind.Bones, 100);
        m.Player(0).Grant(ResourceKind.Blood, 100);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });

        var buildTime = BuildingDef.Get(BuildingKind.BloodStorage).BuildTimeTicks;
        var b = m.BuildingAt(0);
        Assert.Equal(buildTime - 1, b.TicksUntilComplete);   // this Tick() already ran the building's own first Tick()

        for (var i = 0; i < buildTime - 1; i++) m.Tick(null);
        Assert.Equal(BuildingState.Complete, b.State);
        Assert.Equal(0, b.TicksUntilComplete);
    }

    [Fact]
    public void TicksUntilComplete_isZeroForAnHqCompletedImmediately()
    {
        var city = SmallCity();
        var m = MatchState.Create(17u, TwoPlayers(), city);
        var hqHex = FindOpenHex(city, city.CenterHex);
        var id = m.SpawnHqForPlayer(0, hqHex);

        Assert.Equal(0, m.FindBuilding(id)!.TicksUntilComplete);
    }
}
