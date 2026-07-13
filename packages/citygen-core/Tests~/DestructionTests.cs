using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

public class DestructionTests
{
    [Theory]
    [InlineData(300, 300, DamageStage.Intact)]
    [InlineData(151, 300, DamageStage.Intact)] // just above 50%
    [InlineData(150, 300, DamageStage.Damaged)] // exactly 50% -- docs/18 SS3 "<=50%"
    [InlineData(1, 300, DamageStage.Damaged)]
    [InlineData(0, 300, DamageStage.Destroyed)]
    public void StageFor_matches_docs18_thresholds_exactly(int currentHp, int maxHp, DamageStage expected)
    {
        Assert.Equal(expected, DamageStaging.StageFor(currentHp, maxHp));
    }

    [Fact]
    public void FullyIntact_starts_at_max_hp_for_its_tier()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Medium);
        var state = BuildingRuntimeState.FullyIntact(building);

        Assert.Equal(BuildingStats.StructureHp(BuildingTier.Medium), state.MaxHp);
        Assert.Equal(state.MaxHp, state.CurrentHp);
        Assert.Equal(DamageStage.Intact, state.Stage);
        Assert.True(state.BlocksMovement);
    }

    [Fact]
    public void ApplyDamage_clamps_at_zero_never_negative()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var state = BuildingRuntimeState.FullyIntact(building);

        var dead = state.ApplyDamage(state.MaxHp * 10);
        Assert.Equal(0, dead.CurrentHp);
        Assert.Equal(DamageStage.Destroyed, dead.Stage);
        Assert.False(dead.BlocksMovement);
    }

    [Fact]
    public void ApplyDamage_never_heals_past_max()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var state = BuildingRuntimeState.FullyIntact(building).ApplyDamage(0);
        Assert.Equal(state.MaxHp, state.CurrentHp);
    }

    [Fact]
    public void ApplyDamage_rejects_negative_amounts()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var state = BuildingRuntimeState.FullyIntact(building);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyDamage(-1));
    }

    [Fact]
    public void ApplyDamage_returns_a_new_instance_original_unchanged()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var intact = BuildingRuntimeState.FullyIntact(building);
        var damaged = intact.ApplyDamage(10);

        Assert.Equal(intact.MaxHp, intact.CurrentHp); // original untouched
        Assert.NotEqual(intact.CurrentHp, damaged.CurrentHp);
    }

    [Theory]
    [InlineData(BuildingTier.Small)]
    [InlineData(BuildingTier.Medium)]
    [InlineData(BuildingTier.Large)]
    [InlineData(BuildingTier.Landmark)]
    public void Every_tier_starts_intact_and_blocking(BuildingTier tier)
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, tier);
        var state = BuildingRuntimeState.FullyIntact(building);
        Assert.Equal(DamageStage.Intact, state.Stage);
        Assert.True(state.BlocksMovement);
    }

    [Fact]
    public void Bridge_reuses_the_large_tier_hp_and_stages_the_same_way()
    {
        var bridge = new Bridge(new[] { new HexCoord(0, 0) });
        var state = BridgeRuntimeState.FullyIntact(bridge);

        Assert.Equal(BuildingStats.StructureHp(BuildingTier.Large), state.MaxHp);
        Assert.True(state.IsStanding);

        var destroyed = state.ApplyDamage(state.MaxHp);
        Assert.False(destroyed.IsStanding);
        Assert.Equal(DamageStage.Destroyed, destroyed.Stage);
    }
}
