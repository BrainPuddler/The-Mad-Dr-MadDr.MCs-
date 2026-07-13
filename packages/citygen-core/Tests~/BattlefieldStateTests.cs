using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

public class BattlefieldStateTests
{
    public static readonly TheoryData<string> PresetNames = new() { "village", "small_town", "big_city" };

    private static CityPreset PresetByName(string name) => name switch
    {
        "village" => CityPreset.Village(),
        "small_town" => CityPreset.SmallTown(),
        _ => CityPreset.BigCity(),
    };

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void FreshFrom_starts_every_building_and_bridge_fully_intact(string presetName)
    {
        var city = CityGenerator.Generate(42u, PresetByName(presetName));
        var state = BattlefieldState.FreshFrom(city);

        Assert.Equal(city.Buildings.Count, state.Buildings.Count);
        Assert.Equal(city.Bridges.Count, state.Bridges.Count);
        Assert.All(state.Buildings, b => Assert.Equal(DamageStage.Intact, b.Stage));
        Assert.All(state.Bridges, b => Assert.True(b.IsStanding));
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Blocked_to_ground_is_a_superset_of_static_water(string presetName)
    {
        var city = CityGenerator.Generate(42u, PresetByName(presetName));
        var state = BattlefieldState.FreshFrom(city);
        var blocked = state.BlockedToGround();
        foreach (var w in city.Water) Assert.Contains(w, blocked);
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Standing_buildings_block_ground_and_amphibious_but_not_high_ground(string presetName)
    {
        var city = CityGenerator.Generate(42u, PresetByName(presetName));
        var state = BattlefieldState.FreshFrom(city);
        var blockedGround = state.BlockedToGround();
        var blockedAmphibious = state.BlockedToAmphibious();
        var highGround = state.HighGround();

        var building = city.Buildings[0];
        foreach (var h in building.Footprint)
        {
            Assert.Contains(h, blockedGround);
            Assert.Contains(h, blockedAmphibious);
            Assert.Contains(h, highGround); // "still standing" -- docs/04
        }
    }

    [Fact]
    public void Destroying_a_building_opens_it_to_ground_and_amphibious_but_keeps_high_ground()
    {
        var city = CityGenerator.Generate(42u, CityPreset.SmallTown());
        var state = BattlefieldState.FreshFrom(city);

        var target = state.Buildings[0];
        var destroyed = target.ApplyDamage(target.MaxHp);
        state = state.WithBuildingDamage(destroyed);

        var blockedGround = state.BlockedToGround();
        var blockedAmphibious = state.BlockedToAmphibious();
        var highGround = state.HighGround();

        foreach (var h in target.Building.Footprint)
        {
            Assert.DoesNotContain(h, blockedGround);      // rubble is walkable
            Assert.DoesNotContain(h, blockedAmphibious);
            Assert.Contains(h, highGround);                // "a destroyed building's remaining structure" -- docs/04
        }
    }

    [Fact]
    public void Destroying_a_bridge_reverts_its_deck_to_water_blocking_ground_not_amphibious()
    {
        var city = CityGenerator.Generate(42u, CityPreset.SmallTown());
        Assert.NotEmpty(city.Bridges); // small_town always has bridges (docs/18 tuning table)
        var state = BattlefieldState.FreshFrom(city);

        var bridge = state.Bridges[0];

        // Standing: the deck is a road, blocks nobody.
        var blockedGroundBefore = state.BlockedToGround();
        foreach (var h in bridge.Bridge.Footprint) Assert.DoesNotContain(h, blockedGroundBefore);

        var destroyed = bridge.ApplyDamage(bridge.MaxHp);
        state = state.WithBridgeDamage(destroyed);

        var blockedGroundAfter = state.BlockedToGround();
        var blockedAmphibiousAfter = state.BlockedToAmphibious();
        foreach (var h in bridge.Bridge.Footprint)
        {
            Assert.Contains(h, blockedGroundAfter);        // reverted to water -- blocks ground
            Assert.DoesNotContain(h, blockedAmphibiousAfter); // water never blocks amphibious
        }
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void High_ground_includes_generated_ridges(string presetName)
    {
        var city = CityGenerator.Generate(42u, PresetByName(presetName));
        var state = BattlefieldState.FreshFrom(city);
        var highGround = state.HighGround();
        foreach (var r in city.Ridges) Assert.Contains(r, highGround);
    }

    [Fact]
    public void High_ground_excludes_bridge_decks()
    {
        var city = CityGenerator.Generate(42u, CityPreset.SmallTown());
        var state = BattlefieldState.FreshFrom(city);
        var highGround = state.HighGround();
        foreach (var b in city.Bridges)
            foreach (var h in b.Footprint) Assert.DoesNotContain(h, highGround);
    }
}
