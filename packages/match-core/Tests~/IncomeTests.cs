using System;
using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-08 (creator report: "how do I get fuel?" -> "build me a
/// fuel income mechanic... look at player and race balances to make sure
/// this is equal economy amongst all the races"): real per-second income
/// for the two faction-EXCLUSIVE energy currencies (docs/05) that
/// previously had no source at all -- Fuel (<see
/// cref="MatchState.GrantFuelPumpIncome"/>) and Ichor (<see
/// cref="MatchState.GrantAlienFactoryIchorIncome"/>). Brains already had a
/// real, faction-agnostic source (<see cref="MatchState.
/// GrantHarvestPostIncome"/>, covered by EconomyTests.cs) -- this file is
/// specifically the two that didn't.</summary>
public class IncomeTests
{
    private static List<FactionId> AllThree() => new() { FactionId.MadDoctor, FactionId.HumanArmy, FactionId.AlienHive };
    private static CityModel SmallCity() => CityGenerator.Generate(7070u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var roads = new HashSet<HexCoord>(city.Roads);
        foreach (var h in center.Ring(0)) if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        for (var r = 1; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new InvalidOperationException("no open hex found");
    }

    /// <summary>Same shape as TrainUnitTests.BuildCompleteBuilding --
    /// duplicated rather than shared across test files, same standing
    /// precedent BuildingTests.FindOpenHex's own comment already
    /// documents for this exact helper.</summary>
    private static uint BuildComplete(MatchState m, CityModel city, int playerIndex, BuildingKind kind, HexCoord center)
    {
        var hex = FindOpenHex(city, center);
        var player = m.Player(playerIndex);
        player.AddWorker();
        player.Grant(ResourceKind.Bones, 1000);
        player.Grant(ResourceKind.Blood, 1000);
        player.Grant(ResourceKind.Fuel, 1000);
        player.Grant(ResourceKind.Ichor, 1000);
        player.Grant(ResourceKind.Parts, 1000);
        m.Tick(new List<Command> { new Command(playerIndex, CommandKind.BuildStructure, targetEntity: (uint)kind, argA: hex.Q, argB: hex.R) });
        var id = m.BuildingAt(m.BuildingCount - 1).EntityId;
        var buildTime = BuildingDef.Get(kind).BuildTimeTicks;
        for (var i = 0; i < buildTime; i++) m.Tick(null);
        Assert.Equal(BuildingState.Complete, m.FindBuilding(id)!.State);
        return id;
    }

    [Fact]
    public void FuelPump_grantsFuelForHumanArmy_onceEachSimulatedSecond()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, AllThree(), city);
        var player = m.Player(1);   // player 1 is HumanArmy (see AllThree)

        BuildComplete(m, city, 1, BuildingKind.FuelPump, city.CenterHex);
        // FuelPump's own BuildTimeTicks (100) is an exact multiple of
        // TicksPerSecond (10), so the tick that completes construction
        // could ALSO be the same tick an income grant lands on (building
        // state transitions run before the once-per-second income block,
        // same call). One buffer tick moves off that boundary (101 % 10
        // != 0) so the assertions below aren't coupled to that
        // coincidence -- this test is about income cadence from here
        // forward, not conflating it with construction completion.
        m.Tick(null);
        var baseline = player.Wallet(ResourceKind.Fuel);

        for (var i = 0; i < MatchState.TicksPerSecond - 1; i++) m.Tick(null);
        Assert.Equal(baseline, player.Wallet(ResourceKind.Fuel));   // not yet a full second

        m.Tick(null);   // the exact tick the first second completes
        Assert.Equal(baseline + 2, player.Wallet(ResourceKind.Fuel));

        for (var i = 0; i < MatchState.TicksPerSecond; i++) m.Tick(null);
        Assert.Equal(baseline + 4, player.Wallet(ResourceKind.Fuel));   // a second grant, one per second, not accelerating
    }

    [Fact]
    public void FuelPump_grantsNothing_forMadDoctorOrAlienHive()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, AllThree(), city);
        var doctor = m.Player(0);     // MadDoctor
        var hive = m.Player(2);       // AlienHive

        BuildComplete(m, city, 0, BuildingKind.FuelPump, city.CenterHex);
        BuildComplete(m, city, 2, BuildingKind.FuelPump, new HexCoord(city.CenterHex.Q + 20, city.CenterHex.R + 20));
        var doctorFuel = doctor.Wallet(ResourceKind.Fuel);
        var hiveFuel = hive.Wallet(ResourceKind.Fuel);

        for (var i = 0; i < MatchState.TicksPerSecond * 3; i++) m.Tick(null);

        Assert.Equal(doctorFuel, doctor.Wallet(ResourceKind.Fuel));   // still exactly what it was -- no source for a faction that doesn't spend Fuel
        Assert.Equal(hiveFuel, hive.Wallet(ResourceKind.Fuel));
    }

    [Fact]
    public void AlienFactory_grantsIchorForAlienHive_onceEachSimulatedSecond()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, AllThree(), city);
        var player = m.Player(2);   // player 2 is AlienHive

        BuildComplete(m, city, 2, BuildingKind.Factory, city.CenterHex);
        // Same buffer-tick reasoning as FuelPump_grantsFuelForHumanArmy's
        // own comment -- Factory's BuildTimeTicks (150) is also an exact
        // multiple of TicksPerSecond.
        m.Tick(null);
        var baseline = player.Wallet(ResourceKind.Ichor);

        for (var i = 0; i < MatchState.TicksPerSecond - 1; i++) m.Tick(null);
        Assert.Equal(baseline, player.Wallet(ResourceKind.Ichor));

        m.Tick(null);
        Assert.Equal(baseline + 3, player.Wallet(ResourceKind.Ichor));
    }

    [Fact]
    public void EveryPlayerFactoryGrant_isEmptyExceptAlienHiveAndMixed()
    {
        // 2026-08 (creator direction: "make sure this is equal economy
        // amongst all the races"): every player gets a free starting
        // Factory (SpawnFactoryForPlayer) regardless of faction -- confirm
        // that alone doesn't leak Ichor to a faction that never spends it.
        var city = SmallCity();
        var m = MatchState.Create(4u, AllThree(), city);
        var doctor = m.Player(0);
        var army = m.Player(1);

        BuildComplete(m, city, 0, BuildingKind.Factory, city.CenterHex);
        BuildComplete(m, city, 1, BuildingKind.Factory, new HexCoord(city.CenterHex.Q + 20, city.CenterHex.R + 20));
        var doctorIchor = doctor.Wallet(ResourceKind.Ichor);
        var armyIchor = army.Wallet(ResourceKind.Ichor);

        for (var i = 0; i < MatchState.TicksPerSecond * 3; i++) m.Tick(null);

        Assert.Equal(doctorIchor, doctor.Wallet(ResourceKind.Ichor));
        Assert.Equal(armyIchor, army.Wallet(ResourceKind.Ichor));
    }
}
