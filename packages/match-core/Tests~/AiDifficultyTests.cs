using System;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-08 (creator direction: "scale the ai intelligence for
/// Difficulty. So that in tutorial and early levels players can get a
/// sense of achievement... this needs to be challenging enough without
/// being too easy"). Covers <see cref="AiDifficultyProfile"/>'s own
/// contract in isolation: Normal is the identity, every level is
/// legibly distinct, and the ordering runs the direction the enum's own
/// name implies (Tutorial weakest, Brutal strongest) on every axis.
/// End-to-end behavioral coverage (does a Tutorial advisor actually
/// field fewer units, does a Brutal commander actually react faster)
/// lives alongside the classes that consume this profile
/// (<see cref="CommanderTests"/>, <see cref="ProductionAdvisorTests"/>),
/// not duplicated here.</summary>
public class AiDifficultyTests
{
    [Fact]
    public void Normal_is_the_identity_on_every_axis()
    {
        var normal = AiDifficultyProfile.Get(AiDifficulty.Normal);
        Assert.Equal(1.0, normal.ReactionMultiplier);
        Assert.Equal(1.0, normal.EconomyMultiplier);
        Assert.Equal(1.0, normal.ArmySizeMultiplier);
        Assert.Equal(1.0, normal.StartingArmyMultiplier);
    }

    [Fact]
    public void Every_level_reports_its_own_Level_and_a_distinct_Label()
    {
        var seenLabels = new System.Collections.Generic.HashSet<string>();
        foreach (AiDifficulty level in Enum.GetValues(typeof(AiDifficulty)))
        {
            var profile = AiDifficultyProfile.Get(level);
            Assert.Equal(level, profile.Level);
            Assert.True(seenLabels.Add(profile.Label), $"duplicate label: {profile.Label}");
        }
    }

    [Fact]
    public void ReactionMultiplier_decreases_monotonically_from_Tutorial_to_Brutal()
    {
        // Lower multiplier = faster reactions -- Brutal should be the
        // fastest (lowest multiplier), Tutorial the slowest (highest).
        var levels = new[] { AiDifficulty.Tutorial, AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard, AiDifficulty.Brutal };
        for (var i = 1; i < levels.Length; i++)
        {
            var prev = AiDifficultyProfile.Get(levels[i - 1]).ReactionMultiplier;
            var cur = AiDifficultyProfile.Get(levels[i]).ReactionMultiplier;
            Assert.True(cur < prev, $"{levels[i]} ReactionMultiplier ({cur}) should be lower than {levels[i - 1]}'s ({prev})");
        }
    }

    [Theory]
    [InlineData(true)] // EconomyMultiplier
    [InlineData(false)] // ArmySizeMultiplier
    public void Economy_and_ArmySize_multipliers_increase_monotonically_from_Tutorial_to_Brutal(bool checkEconomy)
    {
        var levels = new[] { AiDifficulty.Tutorial, AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard, AiDifficulty.Brutal };
        for (var i = 1; i < levels.Length; i++)
        {
            var prevProfile = AiDifficultyProfile.Get(levels[i - 1]);
            var curProfile = AiDifficultyProfile.Get(levels[i]);
            var prev = checkEconomy ? prevProfile.EconomyMultiplier : prevProfile.ArmySizeMultiplier;
            var cur = checkEconomy ? curProfile.EconomyMultiplier : curProfile.ArmySizeMultiplier;
            Assert.True(cur > prev, $"{levels[i]} ({cur}) should exceed {levels[i - 1]} ({prev})");
        }
    }

    [Fact]
    public void StartingArmyMultiplier_increases_monotonically_from_Tutorial_to_Brutal()
    {
        var levels = new[] { AiDifficulty.Tutorial, AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard, AiDifficulty.Brutal };
        for (var i = 1; i < levels.Length; i++)
        {
            var prev = AiDifficultyProfile.Get(levels[i - 1]).StartingArmyMultiplier;
            var cur = AiDifficultyProfile.Get(levels[i]).StartingArmyMultiplier;
            Assert.True(cur > prev, $"{levels[i]} StartingArmyMultiplier ({cur}) should exceed {levels[i - 1]}'s ({prev})");
        }
    }

    [Fact]
    public void Tutorial_never_asks_for_more_than_it_would_at_Normal()
    {
        // The brief's own bar ("challenging enough without being too
        // easy") means Tutorial should be WEAKER, never accidentally
        // stronger, than Normal on every axis that means "more capable."
        var tutorial = AiDifficultyProfile.Get(AiDifficulty.Tutorial);
        var normal = AiDifficultyProfile.Get(AiDifficulty.Normal);
        Assert.True(tutorial.EconomyMultiplier < normal.EconomyMultiplier);
        Assert.True(tutorial.ArmySizeMultiplier < normal.ArmySizeMultiplier);
        Assert.True(tutorial.StartingArmyMultiplier < normal.StartingArmyMultiplier);
        Assert.True(tutorial.ReactionMultiplier > normal.ReactionMultiplier); // slower, not faster
    }

    [Fact]
    public void AllLevels_has_exactly_one_entry_per_enum_value_in_enum_order()
    {
        var all = AiDifficultyProfile.AllLevels;
        var values = (AiDifficulty[])Enum.GetValues(typeof(AiDifficulty));
        Assert.Equal(values.Length, all.Count);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(values[i], all[i].Level);
    }
}
