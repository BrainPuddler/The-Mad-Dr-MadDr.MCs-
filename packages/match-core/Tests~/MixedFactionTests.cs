using System;
using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-07 amendment (docs/12, docs/23 §1 2026-07 update):
/// FactionId.Mixed as a real, pickable 4th starting faction. Covers the
/// creator's own acceptance bar, verbatim: "for each unit in mixed the
/// rules will apply to the race of the unit" -- i.e. no stacked
/// advantage, each fielded unit gets exactly its own race's Lumen
/// bonuses/handicaps, never Mixed's own (neutral) row on top.</summary>
public class MixedFactionTests
{
    private static List<FactionId> MixedVsHive() => new() { FactionId.Mixed, FactionId.AlienHive };
    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        // 2026-07: CanPlaceBuilding now rejects road hexes too -- see
        // BuildingTests.FindOpenHex's own comment for the full story.
        // This file's own seed didn't happen to trigger the failure, but
        // the same latent bug applied here too -- fixed defensively for
        // consistency, not because a test was observed failing.
        var roads = new HashSet<HexCoord>(city.Roads);
        foreach (var h in center.Ring(0)) if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        for (var r = 1; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new InvalidOperationException("no open hex found");
    }

    [Fact]
    public void FactionDef_mixedHasNoSingleOriginOrEnergy()
    {
        var def = FactionDef.Get(FactionId.Mixed);
        Assert.Null(def.OriginBias);
        Assert.Null(def.Energy);
        Assert.Equal("The Patchwork Ward", def.BaseName);
    }

    [Fact]
    public void FactionLumenTable_mixedsOwnRowIsNeutralEveryPhase()
    {
        // The "no undue advantage" guarantee, made explicit: Mixed itself
        // never grants a faction-level bonus/handicap of its own.
        foreach (LumenPhase phase in Enum.GetValues(typeof(LumenPhase)))
        {
            var mod = FactionLumenTable.Get(FactionId.Mixed, phase);
            Assert.Equal(100, mod.DamagePercent);
            Assert.Equal(1.0, mod.SpeedMultiplier);
            Assert.Equal(1.0, mod.RegenMultiplier);
        }
    }

    [Fact]
    public void SpawnRosterUnit_mixedPlayerCanFieldEitherEnemyRoster_taggedWithItsOwnRace()
    {
        var city = SmallCity();
        var m = MatchState.Create(20u, MixedVsHive(), city);
        var hex = FindOpenHex(city, city.CenterHex);

        var riflemanId = m.SpawnRosterUnit(0, hex, RosterUnitKind.Rifleman);
        var droneId = m.SpawnRosterUnit(0, hex, RosterUnitKind.Drone);

        Assert.Equal(FactionId.HumanArmy, m.FindUnit(riflemanId)!.RaceOverride);
        Assert.Equal(FactionId.AlienHive, m.FindUnit(droneId)!.RaceOverride);
    }

    [Fact]
    public void SpawnRosterUnit_nonMixedPlayerStillRejectsWrongFactionRoster()
    {
        // Existing pre-amendment behavior, unchanged: only Mixed gets the
        // "any roster" exception.
        var city = SmallCity();
        var m = MatchState.Create(21u, MixedVsHive(), city);   // player 1 is AlienHive
        var hex = FindOpenHex(city, city.CenterHex);

        Assert.Throws<InvalidOperationException>(() => m.SpawnRosterUnit(1, hex, RosterUnitKind.Rifleman));
    }

    [Fact]
    public void SpawnUnit_nonMixedCallSitesGetNullRaceOverride()
    {
        var city = SmallCity();
        var m = MatchState.Create(22u, MixedVsHive(), city);
        var hex = FindOpenHex(city, city.CenterHex);

        var id = m.SpawnUnit(1, hex, speed: 3.0);   // AlienHive player, plain SpawnUnit, no override supplied
        Assert.Null(m.FindUnit(id)!.RaceOverride);
    }

    [Fact]
    public void MixedUnit_getsItsOwnRacesLumenSpeedBonus_notMixedsNeutralRow()
    {
        // docs/23 §7: Hive's Day speed penalty is -10% (0.90x). A Drone
        // fielded under a Mixed player must feel that exact penalty --
        // proving the per-unit RaceOverride resolution actually reaches
        // Tick's speedMultiplier lookup, not just the data table.
        var city = SmallCity();
        var m = MatchState.Create(23u, MixedVsHive(), city);
        var start = FindOpenHex(city, city.CenterHex);
        var goal = FindOpenHex(city, start.Neighbor(HexEdge.E).Neighbor(HexEdge.E).Neighbor(HexEdge.E));

        var droneId = m.SpawnRosterUnit(0, start, RosterUnitKind.Drone);   // player 0 is Mixed
        while (m.CurrentLumenPhase != LumenPhase.Day) m.Tick(null);

        m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: droneId, argA: goal.Q, argB: goal.R) });
        var (xAfterOneTick, zAfterOneTick) = (m.FindUnit(droneId)!.X, m.FindUnit(droneId)!.Z);
        var (startX, startZ) = start.ToWorld();
        var actualStep = Math.Sqrt(Math.Pow(xAfterOneTick - startX, 2) + Math.Pow(zAfterOneTick - startZ, 2));

        var droneDef = UnitRosterDef.Get(RosterUnitKind.Drone);
        var expectedStep = droneDef.Speed * 0.90 * (1.0 / MatchState.TicksPerSecond);
        Assert.True(Math.Abs(actualStep - expectedStep) < 0.0001, $"expected ~{expectedStep}, got {actualStep}");
    }

    [Fact]
    public void CanTrainUnit_mixedPlayerCanTrainEitherEnemyRoster()
    {
        var city = SmallCity();
        var m = MatchState.Create(24u, MixedVsHive(), city);
        var factoryId = m.SpawnFactoryForPlayer(0, FindOpenHex(city, city.CenterHex));   // player 0 is Mixed
        var player = m.Player(0);
        foreach (var (resource, amount) in UnitRosterDef.Get(RosterUnitKind.Rifleman).Cost) player.Grant(resource, amount);
        foreach (var (resource, amount) in UnitRosterDef.Get(RosterUnitKind.Drone).Cost) player.Grant(resource, amount);

        Assert.True(m.CanTrainUnit(0, factoryId, RosterUnitKind.Rifleman));
        Assert.True(m.CanTrainUnit(0, factoryId, RosterUnitKind.Drone));
    }
}
