using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §13 amendment D's Phase 6b: "the two enemy faction
/// rosters as genome data." Covers `UnitRosterDef`'s own data (every kind
/// present, faction-correct) and `MatchState.SpawnRosterUnit`'s wiring
/// (spawns with the def's exact stats, rejects a faction mismatch). Does
/// NOT test genome-core integration or real balance -- see FactionRoster.cs's
/// own header for why these numbers are invented placeholders, not real
/// figures.</summary>
public class FactionRosterTests
{
    private static List<FactionId> ArmyVsHive() => new() { FactionId.HumanArmy, FactionId.AlienHive };

    private static CityModel SmallCity() => CityGenerator.Generate(6060u, CityPreset.Village());

    [Theory]
    [InlineData(RosterUnitKind.Rifleman, FactionId.HumanArmy)]
    [InlineData(RosterUnitKind.HalfTrack, FactionId.HumanArmy)]
    [InlineData(RosterUnitKind.Tank, FactionId.HumanArmy)]
    [InlineData(RosterUnitKind.ZeppelinGunship, FactionId.HumanArmy)]
    [InlineData(RosterUnitKind.Drone, FactionId.AlienHive)]
    [InlineData(RosterUnitKind.Spitter, FactionId.AlienHive)]
    [InlineData(RosterUnitKind.FloaterQueen, FactionId.AlienHive)]
    public void Every_roster_kind_belongs_to_its_documented_faction(RosterUnitKind kind, FactionId expectedFaction)
    {
        Assert.Equal(expectedFaction, UnitRosterDef.Get(kind).Faction);
    }

    [Fact]
    public void AllDefs_covers_every_RosterUnitKind_exactly_once()
    {
        var seen = new HashSet<RosterUnitKind>();
        foreach (var def in UnitRosterDef.AllDefs)
        {
            Assert.True(seen.Add(def.Kind), $"{def.Kind} appears more than once");
            // Get(kind) must resolve to the SAME def -- guards against the
            // array-position/enum-int mismatch BuildingDef's own indexing
            // trick would silently break on if a future kind were inserted
            // out of order.
            Assert.Same(def, UnitRosterDef.Get(def.Kind));
        }
        foreach (RosterUnitKind kind in Enum.GetValues(typeof(RosterUnitKind)))
            Assert.Contains(kind, seen);
    }

    [Fact]
    public void Every_roster_unit_has_a_real_positive_stat_block()
    {
        // Not a balance claim (see FactionRoster.cs's header) -- just a
        // sanity floor that no entry is an accidental zero/negative.
        foreach (var def in UnitRosterDef.AllDefs)
        {
            Assert.True(def.Speed > 0, $"{def.Kind} has non-positive Speed");
            Assert.True(def.Radius > 0, $"{def.Kind} has non-positive Radius");
            Assert.True(def.Combat.MaxVitality > 0, $"{def.Kind} has non-positive MaxVitality");
            Assert.True(def.Combat.Reach > 0, $"{def.Kind} has non-positive Reach");
            Assert.True(def.SalvageValue > 0, $"{def.Kind} has non-positive SalvageValue");
        }
    }

    [Fact]
    public void SpawnRosterUnit_uses_the_defs_exact_stat_block()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, ArmyVsHive(), city);
        var def = UnitRosterDef.Get(RosterUnitKind.Tank);

        var id = m.SpawnRosterUnit(0, city.CenterHex, RosterUnitKind.Tank);
        var unit = m.FindUnit(id)!;

        Assert.Equal(def.Speed, unit.Speed);
        Assert.Equal(def.Radius, unit.Radius);
        Assert.Equal(def.SalvageValue, unit.SalvageValue);
        Assert.NotNull(unit.Combat);
        Assert.Equal(def.Combat.MaxVitality, unit.Combat!.Value.MaxVitality);
        Assert.Equal(def.Combat.Power, unit.Combat.Value.Power);
        Assert.Equal(def.Combat.Armor, unit.Combat.Value.Armor);
        Assert.Equal(def.Combat.Reach, unit.Combat.Value.Reach);
    }

    [Fact]
    public void SpawnRosterUnit_rejects_a_kind_that_does_not_belong_to_the_players_faction()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, ArmyVsHive(), city);   // player 0 = HumanArmy

        Assert.Throws<InvalidOperationException>(() => m.SpawnRosterUnit(0, city.CenterHex, RosterUnitKind.Drone));
    }

    [Fact]
    public void SpawnRosterUnit_can_field_a_real_skirmish_of_both_rosters()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, ArmyVsHive(), city);
        var start = city.CenterHex;

        var riflemanId = m.SpawnRosterUnit(0, start, RosterUnitKind.Rifleman);
        var tankId = m.SpawnRosterUnit(0, start, RosterUnitKind.Tank);
        var droneId = m.SpawnRosterUnit(1, start, RosterUnitKind.Drone);
        var queenId = m.SpawnRosterUnit(1, start, RosterUnitKind.FloaterQueen);

        Assert.Equal(4, m.UnitCount);
        Assert.True(m.FindUnit(riflemanId)!.IsAlive);
        Assert.True(m.FindUnit(tankId)!.IsAlive);
        Assert.True(m.FindUnit(droneId)!.IsAlive);
        Assert.True(m.FindUnit(queenId)!.IsAlive);

        // Deterministic across two identical runs, same as every other
        // phase's own determinism proof.
        ulong Run()
        {
            var c = SmallCity();
            var mm = MatchState.Create(3u, ArmyVsHive(), c);
            mm.SpawnRosterUnit(0, c.CenterHex, RosterUnitKind.Rifleman);
            mm.SpawnRosterUnit(0, c.CenterHex, RosterUnitKind.Tank);
            mm.SpawnRosterUnit(1, c.CenterHex, RosterUnitKind.Drone);
            mm.SpawnRosterUnit(1, c.CenterHex, RosterUnitKind.FloaterQueen);
            for (var i = 0; i < 50; i++) mm.Tick(null);
            return mm.Hash();
        }
        Assert.Equal(Run(), Run());
    }
}
