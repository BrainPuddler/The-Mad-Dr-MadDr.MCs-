using System;
using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-07 worker-economy epic, Phase 4 acceptance:
/// CommandKind.TrainUnit's full cycle (validation, all-or-nothing cost
/// debit, single in-progress slot, completion spawn) for both faction
/// rosters, plus BuildingKind.BigBrain's supply-cap-on-complete
/// mechanic. Docs/12's Phase 4 entry has the full design rationale.</summary>
public class TrainUnitTests
{
    private static List<FactionId> ArmyVsHive() => new() { FactionId.HumanArmy, FactionId.AlienHive };
    private static List<FactionId> DoctorVsArmy() => new() { FactionId.MadDoctor, FactionId.HumanArmy };
    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        // 2026-07: CanPlaceBuilding now rejects road hexes too -- see
        // BuildingTests.FindOpenHex's own comment for the full story.
        var roads = new HashSet<HexCoord>(city.Roads);
        foreach (var h in center.Ring(0)) if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        for (var r = 1; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new InvalidOperationException("no open hex found");
    }

    private static uint BuildCompleteFactory(MatchState m, CityModel city, int playerIndex)
        => BuildCompleteBuilding(m, city, playerIndex, BuildingKind.Factory, city.CenterHex);

    /// <summary>2026-08 (Barracks/infantry roster pass): same shape as
    /// <see cref="BuildCompleteFactory"/>, for tests that specifically
    /// need a real infantry producer -- Rifleman/FlamethrowerTrooper now
    /// train from Barracks, not Factory (see
    /// <see cref="UnitRosterDef.Producer"/>). <paramref name="searchCenter"/>
    /// defaults to a point well away from <see cref="CityModel.CenterHex"/>
    /// (FindOpenHex is purely city-derived -- it has no idea a Factory
    /// might already sit at CenterHex from an earlier call in the SAME
    /// match/test -- so a caller building BOTH a Factory and a Barracks
    /// in one test must pick disjoint search areas itself, same as this
    /// default already does).</summary>
    private static uint BuildCompleteBarracks(MatchState m, CityModel city, int playerIndex, HexCoord? searchCenter = null)
        => BuildCompleteBuilding(m, city, playerIndex, BuildingKind.Barracks, searchCenter ?? FarFromCenter(city));

    /// <summary>A search-start point far enough from <see
    /// cref="CityModel.CenterHex"/> (8 hex-rings out) that <see
    /// cref="FindOpenHex"/>'s own outward ring search can never land on
    /// the exact hex a Factory built at CenterHex already occupies --
    /// simpler than teaching FindOpenHex about match-placed buildings
    /// (which it deliberately knows nothing about -- see its own
    /// city-only BlockedToGround comment) just for this one two-building
    /// test scenario.</summary>
    private static HexCoord FarFromCenter(CityModel city)
    {
        foreach (var h in city.CenterHex.Ring(8)) if (city.Contains(h)) return h;
        return city.CenterHex;   // pathological tiny-map fallback -- never hit by CityPreset.Village()
    }

    private static uint BuildCompleteBuilding(MatchState m, CityModel city, int playerIndex, BuildingKind kind, HexCoord searchCenter)
    {
        var hex = FindOpenHex(city, searchCenter);
        var player = m.Player(playerIndex);
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);
        player.Grant(ResourceKind.Fuel, 1000);
        player.Grant(ResourceKind.Parts, 1000);
        m.Tick(new List<Command> { new Command(playerIndex, CommandKind.BuildStructure, targetEntity: (uint)kind, argA: hex.Q, argB: hex.R) });
        // BuildingAt(BuildingCount - 1), not BuildingAt(0) -- this helper
        // is now called more than once in a single match by tests that
        // need both a Factory AND a Barracks present (see
        // CanTrainUnit_falseAtFactory_trueAtBarracks_forRifleman), so
        // "the first building ever placed" is no longer necessarily "the
        // one this call just placed."
        var id = m.BuildingAt(m.BuildingCount - 1).EntityId;
        var buildTime = BuildingDef.Get(kind).BuildTimeTicks;
        for (var i = 0; i < buildTime; i++) m.Tick(null);
        Assert.Equal(BuildingState.Complete, m.FindBuilding(id)!.State);
        return id;
    }

    [Fact]
    public void CanTrainUnit_falseUntilBuildingComplete()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, ArmyVsHive(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);
        player.Grant(ResourceKind.Fuel, 1000);
        player.Grant(ResourceKind.Parts, 1000);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.Factory, argA: hex.Q, argB: hex.R) });
        var id = m.BuildingAt(0).EntityId;

        Assert.False(m.CanTrainUnit(0, id, RosterUnitKind.Rifleman));   // still UnderConstruction
    }

    [Fact]
    public void TrainUnit_debitsCostAllOrNothingAndSpawnsOnCompletion()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, ArmyVsHive(), city);
        var barracksId = BuildCompleteBarracks(m, city, 0);
        var player = m.Player(0);

        var def = UnitRosterDef.Get(RosterUnitKind.Rifleman);
        var beforeTraining = new Dictionary<ResourceKind, int>();
        foreach (var (resource, amount) in def.Cost)
        {
            player.Grant(resource, amount);
            beforeTraining[resource] = player.Wallet(resource);
        }

        Assert.True(m.CanTrainUnit(0, barracksId, RosterUnitKind.Rifleman));
        Assert.Equal(0, m.UnitCount);

        m.Tick(new List<Command> { new Command(0, CommandKind.TrainUnit, targetEntity: barracksId, argA: (int)RosterUnitKind.Rifleman) });

        // paid in full immediately, before the unit itself exists -- check
        // the DELTA, not an absolute 0 (BuildCompleteBarracks's own Bones/
        // Fuel grant leaves leftover balance on those resource kinds).
        foreach (var (resource, amount) in def.Cost)
            Assert.Equal(beforeTraining[resource] - amount, player.Wallet(resource));
        Assert.Equal(0, m.UnitCount);   // still training, not spawned yet
        Assert.Equal(RosterUnitKind.Rifleman, m.FindBuilding(barracksId)!.TrainingKind);

        // the Tick() that issued TrainUnit already ran this building's own
        // TickTraining() once in that same call (ApplyCommand runs before
        // the per-building tick loop), same off-by-one shape
        // Construction_completesExactlyAtItsBuildTimeTicks_notBeforeOrAfter
        // already documents for BuildStructure.
        for (var i = 0; i < def.TrainTimeTicks - 2; i++) m.Tick(null);
        Assert.Equal(0, m.UnitCount);

        m.Tick(null);   // the exact tick training finishes
        Assert.Equal(1, m.UnitCount);
        Assert.Null(m.FindBuilding(barracksId)!.TrainingKind);
        Assert.Equal(0, m.UnitAt(0).PlayerIndex);
        Assert.Equal(RosterUnitKind.Rifleman, m.UnitAt(0).SourceRosterKind);
    }

    [Fact]
    public void CanTrainUnit_falseAtFactory_trueAtBarracks_forRifleman()
    {
        // 2026-08 (creator direction: "Human Army is from army barracks
        // -- part of the basic kit for Human army"): a Factory can no
        // longer train infantry, even fully funded and Complete -- see
        // UnitRosterDef.Producer's own doc comment.
        var city = SmallCity();
        var m = MatchState.Create(11u, ArmyVsHive(), city);
        var factoryId = BuildCompleteFactory(m, city, 0);
        var player = m.Player(0);
        foreach (var (resource, amount) in UnitRosterDef.Get(RosterUnitKind.Rifleman).Cost) player.Grant(resource, amount * 2);

        Assert.False(m.CanTrainUnit(0, factoryId, RosterUnitKind.Rifleman));

        var barracksId = BuildCompleteBarracks(m, city, 0);
        Assert.True(m.CanTrainUnit(0, barracksId, RosterUnitKind.Rifleman));
    }

    [Fact]
    public void TrainUnit_silentNoOp_whenUnaffordable()
    {
        // Barracks, not Factory -- isolates the affordability gate this
        // test is actually named for. A Factory would ALSO reject
        // Rifleman now, but for the producer mismatch (see
        // CanTrainUnit_falseAtFactory_trueAtBarracks_forRifleman), which
        // would make a False result here ambiguous about which check
        // actually failed.
        var city = SmallCity();
        var m = MatchState.Create(3u, ArmyVsHive(), city);
        var barracksId = BuildCompleteBarracks(m, city, 0);
        // deliberately no Grant -- wallet stays at 0 for the roster's cost resources

        Assert.False(m.CanTrainUnit(0, barracksId, RosterUnitKind.Rifleman));
        m.Tick(new List<Command> { new Command(0, CommandKind.TrainUnit, targetEntity: barracksId, argA: (int)RosterUnitKind.Rifleman) });
        Assert.Null(m.FindBuilding(barracksId)!.TrainingKind);
        Assert.Equal(0, m.UnitCount);
    }

    [Fact]
    public void TrainUnit_silentNoOp_whenSlotAlreadyOccupied()
    {
        var city = SmallCity();
        var m = MatchState.Create(4u, ArmyVsHive(), city);
        var barracksId = BuildCompleteBarracks(m, city, 0);
        var player = m.Player(0);
        var def = UnitRosterDef.Get(RosterUnitKind.Rifleman);
        // grant enough for two trains, on top of whatever leftover balance
        // BuildCompleteBarracks's own grant left behind
        var beforeGrant = new Dictionary<ResourceKind, int>();
        foreach (var (resource, amount) in def.Cost)
        {
            beforeGrant[resource] = player.Wallet(resource);
            player.Grant(resource, amount * 2);
        }

        m.Tick(new List<Command> { new Command(0, CommandKind.TrainUnit, targetEntity: barracksId, argA: (int)RosterUnitKind.Rifleman) });
        Assert.Equal(RosterUnitKind.Rifleman, m.FindBuilding(barracksId)!.TrainingKind);
        // exactly one batch's worth was spent (beforeGrant + 2*amount - amount = beforeGrant + amount)
        foreach (var (resource, amount) in def.Cost) Assert.Equal(beforeGrant[resource] + amount, player.Wallet(resource));

        // second TrainUnit command while the slot is occupied -- silent no-op, no double-debit
        m.Tick(new List<Command> { new Command(0, CommandKind.TrainUnit, targetEntity: barracksId, argA: (int)RosterUnitKind.Rifleman) });
        foreach (var (resource, amount) in def.Cost) Assert.Equal(beforeGrant[resource] + amount, player.Wallet(resource));   // still untouched -- not debited twice
    }

    [Fact]
    public void TrainUnit_silentNoOp_whenKindBelongsToAnotherFaction()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, ArmyVsHive(), city);
        // player 0 is HumanArmy; Drone belongs to AlienHive
        var factoryId = BuildCompleteFactory(m, city, 0);
        var player = m.Player(0);
        foreach (var (resource, amount) in UnitRosterDef.Get(RosterUnitKind.Drone).Cost) player.Grant(resource, amount);

        Assert.False(m.CanTrainUnit(0, factoryId, RosterUnitKind.Drone));
        m.Tick(new List<Command> { new Command(0, CommandKind.TrainUnit, targetEntity: factoryId, argA: (int)RosterUnitKind.Drone) });
        Assert.Null(m.FindBuilding(factoryId)!.TrainingKind);
    }

    [Fact]
    public void AlienHive_canTrainItsOwnRosterCostedInIchor()
    {
        var city = SmallCity();
        var m = MatchState.Create(6u, ArmyVsHive(), city);
        var factoryId = BuildCompleteFactory(m, city, 1);   // player 1 is AlienHive
        var player = m.Player(1);
        var def = UnitRosterDef.Get(RosterUnitKind.Drone);
        Assert.Single(def.Cost);
        Assert.Equal(ResourceKind.Ichor, def.Cost[0].Resource);   // "mind control... energy" -- Ichor, the Hive's own energy currency
        player.Grant(ResourceKind.Ichor, def.Cost[0].Amount);

        Assert.True(m.CanTrainUnit(1, factoryId, RosterUnitKind.Drone));
        m.Tick(new List<Command> { new Command(1, CommandKind.TrainUnit, targetEntity: factoryId, argA: (int)RosterUnitKind.Drone) });
        for (var i = 0; i < def.TrainTimeTicks; i++) m.Tick(null);
        Assert.Equal(1, m.UnitCount);
        Assert.Equal(1, m.UnitAt(0).PlayerIndex);
    }

    [Fact]
    public void HumanArmy_recruitCostSpansMultipleResourceKinds()
    {
        // creator direction: "recruiting volunteers, smaller cost but
        // require more resources" -- verify the roster data actually
        // reflects that shape, not just that training works.
        foreach (var kind in new[] { RosterUnitKind.Rifleman, RosterUnitKind.HalfTrack, RosterUnitKind.Tank, RosterUnitKind.ZeppelinGunship })
        {
            var def = UnitRosterDef.Get(kind);
            Assert.True(def.Cost.Count >= 2, $"{kind} should span multiple resource kinds");
        }
        foreach (var kind in new[] { RosterUnitKind.Drone, RosterUnitKind.Spitter, RosterUnitKind.FloaterQueen })
        {
            var def = UnitRosterDef.Get(kind);
            Assert.Single(def.Cost);   // Alien Hive: one heavier Ichor cost, not spread thin
        }
    }

    [Fact]
    public void BigBrain_raisesSupplyCapBy100OnceComplete_costs20Brains()
    {
        var city = SmallCity();
        var m = MatchState.Create(9u, DoctorVsArmy(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);   // MadDoctor
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        var startingCap = player.SupplyCap;

        var def = BuildingDef.Get(BuildingKind.BigBrain);
        Assert.Equal(2, def.Cost.Count);   // 2026-08: BigBrain now also costs Parts (scavenged-metal economy)
        Assert.Equal(ResourceKind.Brains, def.Cost[0].Resource);
        Assert.Equal(20, def.Cost[0].Amount);
        Assert.Equal(ResourceKind.Parts, def.Cost[1].Resource);
        Assert.Equal(80, def.Cost[1].Amount);
        Assert.Equal(100, def.SupplyCapBonus);

        player.Grant(ResourceKind.Brains, 20);
        player.Grant(ResourceKind.Parts, 80);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BigBrain, argA: hex.Q, argB: hex.R) });
        Assert.Equal(0, player.Wallet(ResourceKind.Brains));   // spent in full immediately

        for (var i = 0; i < def.BuildTimeTicks; i++) m.Tick(null);
        Assert.Equal(BuildingState.Complete, m.BuildingAt(0).State);
        Assert.Equal(startingCap + 100, player.SupplyCap);
    }

    [Fact]
    public void BigBrain_doesNotDoubleApplySupplyCapOnRepeatedTicksWhileAlreadyComplete()
    {
        var city = SmallCity();
        var m = MatchState.Create(10u, DoctorVsArmy(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player.Grant(ResourceKind.Brains, 20);
        player.Grant(ResourceKind.Parts, 80);
        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BigBrain, argA: hex.Q, argB: hex.R) });
        for (var i = 0; i < BuildingDef.Get(BuildingKind.BigBrain).BuildTimeTicks; i++) m.Tick(null);
        var capAfterComplete = player.SupplyCap;

        for (var i = 0; i < 50; i++) m.Tick(null);
        Assert.Equal(capAfterComplete, player.SupplyCap);   // stays Complete, bonus never re-applies
    }
}
