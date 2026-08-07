using System;
using System.Collections.Generic;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-08 (creator direction: "Create a faction based army
/// generator. To start making opponents for the game"). Covers
/// `ArmyGenerator.Generate`'s own contract: deterministic, budget-
/// respecting, faction-pure, MadDoctor/Mixed rejected, and that
/// Aggression/Caution actually move the composition (not just decorative
/// parameters). Does NOT cover placement (deliberately not this class's
/// job -- see ArmyGenerator.cs's own header) or Unity wiring (no Editor
/// in this environment to run it).</summary>
public class ArmyGeneratorTests
{
    private static IReadOnlyDictionary<ResourceKind, int> ArmyBudget(int bones, int fuel) =>
        new Dictionary<ResourceKind, int> { { ResourceKind.Bones, bones }, { ResourceKind.Fuel, fuel } };

    private static IReadOnlyDictionary<ResourceKind, int> HiveBudget(int ichor) =>
        new Dictionary<ResourceKind, int> { { ResourceKind.Ichor, ichor } };

    private static int TotalCost(IReadOnlyList<(RosterUnitKind Kind, int Count)> army, ResourceKind resource)
    {
        var total = 0;
        foreach (var (kind, count) in army)
        {
            var def = UnitRosterDef.Get(kind);
            foreach (var (r, amount) in def.Cost)
                if (r == resource) total += amount * count;
        }
        return total;
    }

    [Fact]
    public void Same_inputs_and_seed_produce_identical_composition()
    {
        IReadOnlyList<(RosterUnitKind, int)> Run()
        {
            var rng = new SimRng(42u);
            return ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Balanced(), ArmyBudget(500, 400), rng);
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_seeds_can_produce_different_compositions()
    {
        var budget = ArmyBudget(500, 400);
        var a = ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Balanced(), budget, new SimRng(1u));
        var b = ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Balanced(), budget, new SimRng(2u));
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(ResourceKind.Bones)]
    [InlineData(ResourceKind.Fuel)]
    public void HumanArmy_composition_never_exceeds_its_budget(ResourceKind resource)
    {
        var budget = ArmyBudget(237, 163);
        for (uint seed = 1; seed <= 20; seed++)
        {
            var army = ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Generate(seed), budget, new SimRng(seed));
            Assert.True(TotalCost(army, resource) <= budget[resource]);
        }
    }

    [Fact]
    public void AlienHive_composition_never_exceeds_its_ichor_budget()
    {
        var budget = HiveBudget(311);
        for (uint seed = 1; seed <= 20; seed++)
        {
            var army = ArmyGenerator.Generate(FactionId.AlienHive, CommanderPersonality.Generate(seed), budget, new SimRng(seed));
            Assert.True(TotalCost(army, ResourceKind.Ichor) <= budget[ResourceKind.Ichor]);
        }
    }

    [Fact]
    public void HumanArmy_budget_only_ever_yields_HumanArmy_kinds()
    {
        var army = ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Berserker(), ArmyBudget(1000, 1000), new SimRng(7u));
        Assert.NotEmpty(army);
        foreach (var (kind, _) in army)
            Assert.Equal(FactionId.HumanArmy, UnitRosterDef.Get(kind).Faction);
    }

    [Fact]
    public void AlienHive_budget_only_ever_yields_AlienHive_kinds()
    {
        var army = ArmyGenerator.Generate(FactionId.AlienHive, CommanderPersonality.Turtle(), HiveBudget(500), new SimRng(9u));
        Assert.NotEmpty(army);
        foreach (var (kind, _) in army)
            Assert.Equal(FactionId.AlienHive, UnitRosterDef.Get(kind).Faction);
    }

    [Theory]
    [InlineData(FactionId.MadDoctor)]
    [InlineData(FactionId.Mixed)]
    public void Factions_with_no_fixed_roster_are_rejected(FactionId faction)
    {
        Assert.Throws<ArgumentException>(() =>
            ArmyGenerator.Generate(faction, CommanderPersonality.Balanced(), ArmyBudget(1000, 1000), new SimRng(1u)));
    }

    [Fact]
    public void Zero_budget_yields_an_empty_composition_without_crashing()
    {
        var army = ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Balanced(), ArmyBudget(0, 0), new SimRng(3u));
        Assert.Empty(army);
    }

    [Fact]
    public void Insufficient_budget_for_any_single_unit_yields_an_empty_composition()
    {
        // Cheapest HumanArmy unit (Rifleman) costs 10 Bones + 5 Fuel.
        var army = ArmyGenerator.Generate(FactionId.HumanArmy, CommanderPersonality.Balanced(), ArmyBudget(1, 1), new SimRng(3u));
        Assert.Empty(army);
    }

    // ArmyGenerator's own Weight() scores PER-COST density (Power/cost,
    // Vitality/cost), not raw per-unit averages -- a knapsack fill
    // legitimately spends more of a big budget on a few expensive units
    // than many cheap ones, which can push a raw "average stat per unit"
    // metric in the OPPOSITE direction of what the personality actually
    // optimized for (a Caution army buying fewer, pricier, tankier units
    // can read as higher average Power per unit than an Aggression army's
    // many cheap high-power-density units, despite Aggression correctly
    // winning on POWER SPENT PER RESOURCE). These two tests measure what
    // the algorithm actually targets: total stat gained per resource unit
    // spent across the whole army, matching `ArmyGenerator.CostUnits`'s
    // own "1 Bone == 1 Fuel" flattening.
    private static double Density(IReadOnlyList<(RosterUnitKind Kind, int Count)> army, Func<CombatStats, int> stat)
    {
        long totalStat = 0;
        long totalCost = 0;
        foreach (var (kind, count) in army)
        {
            var def = UnitRosterDef.Get(kind);
            var costUnits = 0;
            foreach (var (_, amount) in def.Cost) costUnits += amount;
            totalStat += (long)stat(def.Combat) * count;
            totalCost += (long)costUnits * count;
        }
        return totalCost == 0 ? 0.0 : (double)totalStat / totalCost;
    }

    [Fact]
    public void Aggression_heavy_commanders_skew_toward_higher_power_density_than_caution_heavy_commanders()
    {
        var budget = ArmyBudget(400, 400);
        var aggressive = CommanderPersonality.Berserker(); // agg 0.95, cau 0.05
        var cautious = CommanderPersonality.Turtle();       // agg 0.15, cau 0.9

        double AveragePowerDensity(CommanderPersonality personality)
        {
            double total = 0;
            for (uint seed = 1; seed <= 30; seed++)
                total += Density(ArmyGenerator.Generate(FactionId.HumanArmy, personality, budget, new SimRng(seed)), c => c.Power);
            return total / 30.0;
        }

        Assert.True(AveragePowerDensity(aggressive) > AveragePowerDensity(cautious));
    }

    [Fact]
    public void Caution_heavy_commanders_skew_toward_higher_vitality_density_than_aggression_heavy_commanders()
    {
        var budget = ArmyBudget(400, 400);
        var aggressive = CommanderPersonality.Berserker();
        var cautious = CommanderPersonality.Turtle();

        double AverageVitalityDensity(CommanderPersonality personality)
        {
            double total = 0;
            for (uint seed = 1; seed <= 30; seed++)
                total += Density(ArmyGenerator.Generate(FactionId.HumanArmy, personality, budget, new SimRng(seed)), c => c.MaxVitality);
            return total / 30.0;
        }

        Assert.True(AverageVitalityDensity(cautious) > AverageVitalityDensity(aggressive));
    }

    [Fact]
    public void Never_exceeds_MaxUnits_even_with_a_vast_budget()
    {
        var army = ArmyGenerator.Generate(FactionId.AlienHive, CommanderPersonality.Balanced(), HiveBudget(1_000_000), new SimRng(5u));
        var total = 0;
        foreach (var (_, count) in army) total += count;
        Assert.True(total <= ArmyGenerator.MaxUnits);
    }
}
