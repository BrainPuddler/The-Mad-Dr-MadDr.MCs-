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

    // 2026-08 (creator report: "I don't see people fleeing from the
    // wreckage of the building"): procedural buildings previously had no
    // occupant concept at all -- RuntimeCityBuilder.ApplyBuildingDamage
    // now disgorges BuildingStats.Occupants(tier) fleeing Citizens on the
    // Destroyed transition, same as the separate RTS-building roster
    // already did.
    [Theory]
    [InlineData(BuildingTier.Small)]
    [InlineData(BuildingTier.Medium)]
    [InlineData(BuildingTier.Large)]
    [InlineData(BuildingTier.Landmark)]
    public void Occupants_is_positive_for_every_tier(BuildingTier tier)
    {
        Assert.True(BuildingStats.Occupants(tier) > 0);
    }

    [Fact]
    public void Occupants_scales_monotonically_with_tier()
    {
        Assert.True(BuildingStats.Occupants(BuildingTier.Small) < BuildingStats.Occupants(BuildingTier.Medium));
        Assert.True(BuildingStats.Occupants(BuildingTier.Medium) < BuildingStats.Occupants(BuildingTier.Large));
        Assert.True(BuildingStats.Occupants(BuildingTier.Large) < BuildingStats.Occupants(BuildingTier.Landmark));
    }

    // 2026-08 (creator direction: "it should start with 1 but then
    // others popup in different places based on the building size up
    // to 8"): the fire-cluster point count per tier.
    //
    // 2026-08 follow-up (creator direction: "YOU may add more fire as
    // the building is attacked. 2-4 depending on the size of the
    // building"): the 1-8 range these tests originally pinned is
    // REPLACED by 2-4 -- updated below rather than left checking
    // numbers that no longer match FireCount's own intended behavior.
    [Theory]
    [InlineData(BuildingTier.Small)]
    [InlineData(BuildingTier.Medium)]
    [InlineData(BuildingTier.Large)]
    [InlineData(BuildingTier.Landmark)]
    public void FireCount_is_positive_for_every_tier(BuildingTier tier)
    {
        Assert.True(BuildingStats.FireCount(tier) > 0);
    }

    [Fact]
    public void FireCount_starts_at_exactly_two_for_Small()
    {
        Assert.Equal(2, BuildingStats.FireCount(BuildingTier.Small));
    }

    [Fact]
    public void FireCount_caps_at_four_for_Landmark()
    {
        Assert.Equal(4, BuildingStats.FireCount(BuildingTier.Landmark));
    }

    /// <summary>Only 3 distinct values (2, 3, 4) cover 4 tiers, so strict
    /// monotonicity across all four is impossible by construction -- Large
    /// and Landmark share the same 4-point ceiling. Small &lt; Medium &lt;=
    /// Large == Landmark is the actual intended shape.</summary>
    [Fact]
    public void FireCount_scales_monotonically_with_tier()
    {
        Assert.True(BuildingStats.FireCount(BuildingTier.Small) < BuildingStats.FireCount(BuildingTier.Medium));
        Assert.True(BuildingStats.FireCount(BuildingTier.Medium) <= BuildingStats.FireCount(BuildingTier.Large));
        Assert.True(BuildingStats.FireCount(BuildingTier.Large) <= BuildingStats.FireCount(BuildingTier.Landmark));
    }

    // 2026-08 (creator report: "I've never seen the smoke either"): the
    // smoke-puff size multiplier per tier -- Small stays 1.0 (unchanged
    // from before this fix), bigger tiers scale up so the plume stays
    // visible against their own bigger silhouette.
    [Theory]
    [InlineData(BuildingTier.Small)]
    [InlineData(BuildingTier.Medium)]
    [InlineData(BuildingTier.Large)]
    [InlineData(BuildingTier.Landmark)]
    public void SmokeScale_is_positive_for_every_tier(BuildingTier tier)
    {
        Assert.True(BuildingStats.SmokeScale(tier) > 0);
    }

    [Fact]
    public void SmokeScale_is_exactly_one_for_Small()
    {
        Assert.Equal(1.0f, BuildingStats.SmokeScale(BuildingTier.Small));
    }

    [Fact]
    public void SmokeScale_scales_monotonically_with_tier()
    {
        Assert.True(BuildingStats.SmokeScale(BuildingTier.Small) < BuildingStats.SmokeScale(BuildingTier.Medium));
        Assert.True(BuildingStats.SmokeScale(BuildingTier.Medium) < BuildingStats.SmokeScale(BuildingTier.Large));
        Assert.True(BuildingStats.SmokeScale(BuildingTier.Large) < BuildingStats.SmokeScale(BuildingTier.Landmark));
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

    // ---- 2026-08 (creator direction: "assign some salvage parts based
    // on the building size... as the lot is cleaned of parts the lot
    // debris is decreased until it is completely cleared") ----

    [Theory]
    [InlineData(BuildingTier.Small)]
    [InlineData(BuildingTier.Medium)]
    [InlineData(BuildingTier.Large)]
    [InlineData(BuildingTier.Landmark)]
    public void ScavengeValue_is_positive_for_every_tier(BuildingTier tier)
    {
        Assert.True(BuildingStats.ScavengeValue(tier) > 0);
    }

    [Fact]
    public void ScavengeValue_scales_monotonically_with_tier()
    {
        Assert.True(BuildingStats.ScavengeValue(BuildingTier.Small) < BuildingStats.ScavengeValue(BuildingTier.Medium));
        Assert.True(BuildingStats.ScavengeValue(BuildingTier.Medium) < BuildingStats.ScavengeValue(BuildingTier.Large));
        Assert.True(BuildingStats.ScavengeValue(BuildingTier.Large) < BuildingStats.ScavengeValue(BuildingTier.Landmark));
    }

    [Fact]
    public void A_standing_building_has_no_scavenge_pile_yet()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Medium);
        var state = BuildingRuntimeState.FullyIntact(building);
        Assert.Equal(0, state.ScavengeRemaining);
        Assert.False(state.IsFullyScavenged);
        Assert.Null(state.DestroyedAtFrame);
    }

    [Fact]
    public void Destruction_rolls_the_scavenge_pile_up_to_the_full_ScavengeValue()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Medium);
        var destroyed = BuildingRuntimeState.FullyIntact(building).ApplyDamage(int.MaxValue / 2);
        Assert.Equal(DamageStage.Destroyed, destroyed.Stage);
        Assert.Equal(BuildingStats.ScavengeValue(BuildingTier.Medium), destroyed.ScavengeRemaining);
        Assert.False(destroyed.IsFullyScavenged);
    }

    [Fact]
    public void ApplyDamage_with_a_frame_stamps_DestroyedAtFrame_only_on_the_newly_destroyed_transition()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var intact = BuildingRuntimeState.FullyIntact(building);

        var stillStanding = intact.ApplyDamage(1, frame: 7);
        Assert.Null(stillStanding.DestroyedAtFrame);

        var destroyed = stillStanding.ApplyDamage(stillStanding.MaxHp, frame: 42);
        Assert.Equal(42, destroyed.DestroyedAtFrame);

        // a second lethal call (amount clamps at 0 HP either way) must NOT
        // re-stamp the frame -- same "roll/stamp exactly once" contract
        // ScavengeRemaining itself already follows.
        var stillDestroyed = destroyed.ApplyDamage(0, frame: 99);
        Assert.Equal(42, stillDestroyed.DestroyedAtFrame);
    }

    [Fact]
    public void WithScavengeConsumed_depletes_the_pile_and_clamps_at_zero()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var destroyed = BuildingRuntimeState.FullyIntact(building).ApplyDamage(int.MaxValue / 2);
        var value = destroyed.ScavengeRemaining;
        Assert.True(value > 0);

        var partial = destroyed.WithScavengeConsumed(value / 2);
        Assert.Equal(value - value / 2, partial.ScavengeRemaining);
        Assert.False(partial.IsFullyScavenged);

        var overConsumed = partial.WithScavengeConsumed(value * 100);
        Assert.Equal(0, overConsumed.ScavengeRemaining);
        Assert.True(overConsumed.IsFullyScavenged);
    }

    [Fact]
    public void WithScavengeConsumed_on_a_standing_building_is_a_no_op()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var intact = BuildingRuntimeState.FullyIntact(building);
        Assert.Same(intact, intact.WithScavengeConsumed(50));
    }

    [Fact]
    public void WithScavengeConsumed_of_a_non_positive_amount_is_a_no_op()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var destroyed = BuildingRuntimeState.FullyIntact(building).ApplyDamage(int.MaxValue / 2);
        Assert.Same(destroyed, destroyed.WithScavengeConsumed(0));
        Assert.Same(destroyed, destroyed.WithScavengeConsumed(-5));
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
