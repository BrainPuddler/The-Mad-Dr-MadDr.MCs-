using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §4 "RPG layer" -- XP, levels, and the per-level stat
/// bonus curve. Trait choices, Gear/grafted salvage, and Fusion are NOT
/// covered here -- see UnitLeveling.cs's own header and docs/12's Phase 4
/// RPG entry for exactly why each is a separate, deferred slice (a real
/// content gap for traits, a missing salvage-system prerequisite for
/// gear, Unity/genome-core territory for fusion's rendering).</summary>
public class LevelingTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    // ---- UnitLeveling: pure math ----

    [Theory]
    [InlineData(1, 44)]   // 40 + 4*1
    [InlineData(5, 60)]   // 40 + 4*5
    [InlineData(10, 80)]  // 40 + 4*10
    public void KillXp_matches_docs23_formula(int victimLevel, int expected)
    {
        Assert.Equal(expected, UnitLeveling.KillXp(victimLevel));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(59, 1)]
    [InlineData(60, 2)]
    [InlineData(149, 2)]
    [InlineData(150, 3)]
    [InlineData(2499, 9)]
    [InlineData(2500, 10)]
    [InlineData(3300, 10)]     // the table's own 10th entry -- never a further level-up, capped at MaxLevel
    [InlineData(1_000_000, 10)] // arbitrarily large XP still caps at 10
    public void LevelForXp_matches_docs23s_cumulative_threshold_table(int xp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, UnitLeveling.LevelForXp(xp));
    }

    [Fact]
    public void StatMultiplier_is_1_at_level_1_and_scales_linearly()
    {
        Assert.Equal(1.0, UnitLeveling.StatMultiplier(1, 0.08), 9);
        Assert.Equal(1.08, UnitLeveling.StatMultiplier(2, 0.08), 9);
        Assert.Equal(1.72, UnitLeveling.StatMultiplier(10, 0.08), 9);   // 1 + 0.08*9
    }

    // ---- SimUnit: effective stats ----

    private static CombatStats Fighter(int power = 20, int maxVitality = 200) =>
        new CombatStats(maxVitality, power, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

    [Fact]
    public void Fresh_unit_starts_at_level_1_with_unscaled_effective_stats()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var id = m.SpawnUnit(0, city.CenterHex, speed: 5.0, combat: Fighter(power: 20, maxVitality: 200));
        var u = m.FindUnit(id)!;

        Assert.Equal(1, u.Level);
        Assert.Equal(0, u.XP);
        Assert.Equal(200, u.EffectiveMaxVitality);
        Assert.Equal(20, u.EffectivePower);
        Assert.Equal(5.0, u.EffectiveSpeed, 9);
    }

    [Fact]
    public void GrantXp_on_a_non_combatant_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var id = m.SpawnUnit(0, city.CenterHex, speed: 5.0);   // no Combat
        var u = m.FindUnit(id)!;
        Assert.Equal(1, u.Level);   // Level defined (defaults to 1) even without Combat
        Assert.Equal(0, u.XP);
    }

    [Fact]
    public void Leveling_up_scales_effective_stats_and_preserves_missing_hp_not_a_full_heal()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var id = m.SpawnUnit(0, city.CenterHex, speed: 5.0, combat: Fighter(power: 20, maxVitality: 200));
        var u = m.FindUnit(id)!;

        // reflect straight into Vitality to simulate "already damaged" without needing a real fight
        var vitalityField = typeof(SimUnit).GetProperty("Vitality")!;
        vitalityField.SetValue(u, 150);   // 50 missing out of 200

        var xpField = typeof(SimUnit).GetMethod("GrantXp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        xpField.Invoke(u, new object[] { 60 });   // exactly the level-2 threshold

        Assert.Equal(2, u.Level);
        Assert.Equal(216, u.EffectiveMaxVitality);   // 200 * 1.08 = 216
        Assert.Equal(21, u.EffectivePower);           // round(20 * 1.04) = round(20.8) = 21
        Assert.Equal(5.1, u.EffectiveSpeed, 9);        // 5.0 * 1.02
        Assert.Equal(166, u.Vitality);                 // 150 + (216-200) -- the same 50 missing, not a full heal to 216
    }

    // ---- MatchState integration: kill XP ----

    private static HexCoord FindOpenNeighbor(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n)) return n;
        throw new System.InvalidOperationException("no open neighbor found");
    }

    [Fact]
    public void Killing_blow_grants_the_attacker_exactly_docs23s_kill_xp()
    {
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);

        var attackerStats = Fighter(power: 50, maxVitality: 1000);   // overkill power, dies fast
        var victimStats = Fighter(power: 0, maxVitality: 10);        // 10 HP, no counter-damage
        var idA = m.SpawnUnit(0, start, speed: 3.0, combat: attackerStats);
        var idB = m.SpawnUnit(1, neighbor, speed: 3.0, combat: victimStats);
        var a = m.FindUnit(idA)!;
        var b = m.FindUnit(idB)!;

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
        for (var i = 0; i < 20 && b.IsAlive; i++) m.Tick(null);

        Assert.False(b.IsAlive);
        Assert.Equal(UnitLeveling.KillXp(1), a.XP);   // victim was level 1 at the moment of death
    }

    [Fact]
    public void Golden_scenario_repeated_kills_level_up_a_unit_and_speed_up_its_movement()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var start = city.CenterHex;

        // Ferocity 100/s -> cooldown 0.01s, well under one 0.1s tick, so a
        // single Tick() per spot can resolve a fresh kill every time --
        // Fighter()'s default 1.0 Ferocity would leave the attacker on a
        // 1s cooldown after its first kill, silently skipping every
        // subsequent spot's attack for the rest of this test.
        var attackerStats = new CombatStats(maxVitality: 100000, power: 999, armor: 0, reach: 1, ferocity: 100.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var idA = m.SpawnUnit(0, start, speed: 4.0, combat: attackerStats);
        var a = m.FindUnit(idA)!;

        var spots = new List<HexCoord>();
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 1; r <= 10 && spots.Count < 6; r++)
            foreach (var h in start.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) { spots.Add(h); if (spots.Count >= 6) break; }

        // kill 6 weak victims in a row, one at a time, adjacent each time --
        // reposition the attacker directly (reflection) between kills so
        // this test is about the XP/level math, not pathfinding logistics.
        var xField = typeof(SimUnit).GetProperty("X")!;
        var zField = typeof(SimUnit).GetProperty("Z")!;
        foreach (var spot in spots)
        {
            var (sx, sz) = spot.ToWorld();
            xField.SetValue(a, sx);
            zField.SetValue(a, sz);

            var victim = m.SpawnUnit(1, spot, speed: 3.0, combat: Fighter(power: 0, maxVitality: 1));
            m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)victim)) });
        }

        Assert.True(a.Level > 1, "six kills should have leveled the attacker up at least once");
        Assert.True(a.EffectiveSpeed > 4.0, "a leveled-up unit should move faster than its base Speed");
        Assert.True(a.EffectivePower > 999, "a leveled-up unit should hit harder than its base Power");
    }

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_leveling_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0x1E7u, TwoPlayers(), city);
            var start = city.CenterHex;
            var neighbor = FindOpenNeighbor(city, start);
            var idA = m.SpawnUnit(0, start, speed: 3.0, combat: Fighter(power: 50, maxVitality: 1000));
            var idB = m.SpawnUnit(1, neighbor, speed: 3.0, combat: Fighter(power: 0, maxVitality: 10));
            m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
            for (var i = 0; i < 200; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }
}
