using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §7: "the Lumen Cycle made real" -- the faction x
/// phase modifier table. `LumenClock` and emitter-polarity phase output
/// already shipped in Phase 3.5 (`EmitterTests.cs` covers those); this
/// covers what's NEW in Phase 7: `FactionLumenTable`'s golden numbers, the
/// three axes actually wired into gameplay (Army's Day damage bonus,
/// Hive's Day / Doctor's Night speed multipliers, Doctor's regeneration-
/// quirk swing), and the acceptance duel. Army's vision-radius and Hive's
/// Ichor-income numbers are checked as DATA ONLY -- see
/// FactionLumenModifier.cs's own header for why neither is wired into any
/// gameplay system yet.</summary>
public class LumenModifierTests
{
    private static List<FactionId> ArmyVsHive() => new() { FactionId.HumanArmy, FactionId.AlienHive };
    private static List<FactionId> DoctorVsArmy() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(8080u, CityPreset.Village());

    private static HexCoord FindOpenNeighbor(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n)) return n;
        throw new InvalidOperationException("no open neighbor found");
    }

    private static HexCoord FindFarOpenHex(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 10; r >= 1; r--)
            foreach (var h in from.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) return h;
        throw new InvalidOperationException("no far open hex found");
    }

    private static int PhaseStartFrame(LumenPhase phase)
    {
        switch (phase)
        {
            case LumenPhase.Dawn: return 0;
            case LumenPhase.Day: return LumenClock.DawnTicks;
            case LumenPhase.Dusk: return LumenClock.DawnTicks + LumenClock.DayTicks;
            default: return LumenClock.DawnTicks + LumenClock.DayTicks + LumenClock.DuskTicks;   // Night
        }
    }

    private static void AdvanceToPhase(MatchState m, LumenPhase phase)
    {
        // The NEXT occurrence of this phase's start frame at or after the
        // match's current position -- not just the first cycle's, since a
        // test may advance the same match through multiple phases in
        // sequence (e.g. Night, then Day of the FOLLOWING cycle).
        var target = PhaseStartFrame(phase);
        while (target <= m.Frame) target += LumenClock.CycleTicks;
        while (m.Frame < target) m.Tick(null);
        Assert.Equal(phase, m.CurrentLumenPhase);
    }

    // =====================================================================
    // FactionLumenTable: golden numbers, verbatim from docs/23 §7's table
    // =====================================================================

    [Theory]
    [InlineData(FactionId.MadDoctor, LumenPhase.Dawn, 100, 1.0, 1.0)]
    [InlineData(FactionId.MadDoctor, LumenPhase.Day, 100, 1.0, 0.90)]
    [InlineData(FactionId.MadDoctor, LumenPhase.Dusk, 100, 1.0, 1.0)]
    [InlineData(FactionId.MadDoctor, LumenPhase.Night, 100, 1.10, 1.15)]
    [InlineData(FactionId.HumanArmy, LumenPhase.Dawn, 100, 1.0, 1.0)]
    [InlineData(FactionId.HumanArmy, LumenPhase.Day, 115, 1.0, 1.0)]
    [InlineData(FactionId.HumanArmy, LumenPhase.Dusk, 100, 1.0, 1.0)]
    [InlineData(FactionId.HumanArmy, LumenPhase.Night, 100, 1.0, 1.0)]
    [InlineData(FactionId.AlienHive, LumenPhase.Dawn, 100, 1.0, 1.0)]
    [InlineData(FactionId.AlienHive, LumenPhase.Day, 100, 0.90, 1.0)]
    [InlineData(FactionId.AlienHive, LumenPhase.Dusk, 100, 1.0, 1.0)]
    [InlineData(FactionId.AlienHive, LumenPhase.Night, 100, 1.0, 1.0)]
    public void FactionLumenTable_matches_docs23_7s_table_exactly(
        FactionId faction, LumenPhase phase, int expectedDamagePercent, double expectedSpeedMult, double expectedRegenMult)
    {
        var m = FactionLumenTable.Get(faction, phase);
        Assert.Equal(expectedDamagePercent, m.DamagePercent);
        Assert.Equal(expectedSpeedMult, m.SpeedMultiplier);
        Assert.Equal(expectedRegenMult, m.RegenMultiplier);
    }

    [Fact]
    public void Armys_vision_and_hives_income_numbers_are_recorded_as_data_only()
    {
        // Real docs/23 §7 numbers, carried as data even though nothing
        // consumes them yet (see FactionLumenModifier.cs's own header).
        Assert.Equal(0.85, FactionLumenTable.Get(FactionId.HumanArmy, LumenPhase.Night).VisionMultiplier);
        Assert.Equal(1.15, FactionLumenTable.Get(FactionId.AlienHive, LumenPhase.Dusk).IncomeMultiplier);
        Assert.Equal(1.15, FactionLumenTable.Get(FactionId.AlienHive, LumenPhase.Dawn).IncomeMultiplier);
    }

    // =====================================================================
    // CombatMath.ResolveDamage: the new lumenModPercent term
    // =====================================================================

    [Fact]
    public void ResolveDamage_default_lumen_mod_reproduces_the_pre_Phase7_result_exactly()
    {
        // docs/04's own worked example, unchanged -- the new parameter
        // must be a true no-op when omitted.
        Assert.Equal(19, CombatMath.ResolveDamage(power: 22, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 100, armor: 3));
        Assert.Equal(19, CombatMath.ResolveDamage(power: 22, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 100, armor: 3, lumenModPercent: 100));
    }

    [Fact]
    public void ResolveDamage_applies_armys_real_15_percent_day_bonus()
    {
        // 22 x 1.0 x 1.0 x 1.0 x 1.15 = 25.3 -> round to 25, minus 3 armor = 22.
        Assert.Equal(22, CombatMath.ResolveDamage(power: 22, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 100, armor: 3, lumenModPercent: 115));
    }

    // =====================================================================
    // Wired into real combat/movement
    // =====================================================================

    /// <summary>A hex at EXACTLY distance 2 from `from` -- non-adjacent,
    /// so <c>TickCombat</c>'s posMod is a flat 100 rather than depending
    /// on <c>Facing.ArcOf</c>'s facing-dependent Front/Flank/Rear
    /// classification (which only applies to distance-1 attacks). Needed
    /// wherever a test wants a fully controlled, arithmetic-checkable
    /// damage number.</summary>
    private static HexCoord FindOpenHexAtDistance2(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var h in from.Ring(2))
            if (city.Contains(h) && !blocked.Contains(h)) return h;
        throw new InvalidOperationException("no open distance-2 hex found");
    }

    [Fact]
    public void An_army_units_damage_is_boosted_at_Day_versus_Night()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, ArmyVsHive(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenHexAtDistance2(city, start);

        var punchingBag = new CombatStats(maxVitality: 100000, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        // reach: 3 (so this non-adjacent target is still in range) and
        // cunningPercent: 100 (ALWAYS crits, so luckOrCritPercent is a
        // fixed 150 every hit, no RNG-drawn luck roll at all) together
        // remove every source of randomness from this comparison: the
        // only thing that can still differ between Day and Night is the
        // Lumen damage modifier itself.
        var attackerStats = new CombatStats(maxVitality: 1000, power: 20, armor: 0, reach: 3, ferocity: 100.0, cunningPercent: 100, affinity: UnitAffinity.Neutral);

        AdvanceToPhase(m, LumenPhase.Night);
        var armyId = m.SpawnUnit(0, start, speed: 3.0, combat: attackerStats);   // player 0 = HumanArmy
        var victimId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: punchingBag);

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, armyId, unchecked((int)victimId)) });
        var nightDamage = 100000 - m.FindUnit(victimId)!.Vitality;
        Assert.Equal(CombatMath.ResolveDamage(power: 20, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 150, armor: 0, lumenModPercent: 100), nightDamage);

        AdvanceToPhase(m, LumenPhase.Day);
        var victimId2 = m.SpawnUnit(1, neighbor, speed: 3.0, combat: punchingBag);
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, armyId, unchecked((int)victimId2)) });
        var dayDamage = 100000 - m.FindUnit(victimId2)!.Vitality;
        Assert.Equal(CombatMath.ResolveDamage(power: 20, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 150, armor: 0, lumenModPercent: 115), dayDamage);

        Assert.True(dayDamage > nightDamage, "Army's real Day damage bonus should make this exact same attack hit harder");
    }

    [Fact]
    public void A_hive_unit_covers_less_ground_at_Day_than_at_Night()
    {
        double Distance(LumenPhase phase)
        {
            var city = SmallCity();
            var m = MatchState.Create(2u, ArmyVsHive(), city);
            AdvanceToPhase(m, phase);
            var start = city.CenterHex;
            var far = FindFarOpenHex(city, start);
            var hiveId = m.SpawnUnit(1, start, speed: 5.0);   // player 1 = AlienHive
            m.Tick(new List<Command> { new Command(1, CommandKind.MoveTo, hiveId, far.Q, far.R) });

            var before = m.FindUnit(hiveId)!;
            var (bx, bz) = (before.X, before.Z);
            for (var i = 0; i < 10; i++) m.Tick(null);
            var after = m.FindUnit(hiveId)!;
            var dx = after.X - bx;
            var dz = after.Z - bz;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        var dayDist = Distance(LumenPhase.Day);
        var nightDist = Distance(LumenPhase.Night);

        Assert.True(dayDist < nightDist, "Hive should cover less ground per tick at Day (-10% speed) than at Night (no modifier)");
        Assert.Equal(nightDist * 0.90, dayDist, 3);
    }

    [Fact]
    public void A_doctor_unit_moves_faster_at_Night_than_at_Day()
    {
        double Distance(LumenPhase phase)
        {
            var city = SmallCity();
            var m = MatchState.Create(9u, DoctorVsArmy(), city);
            AdvanceToPhase(m, phase);
            var start = city.CenterHex;
            var far = FindFarOpenHex(city, start);
            var doctorId = m.SpawnUnit(0, start, speed: 5.0);   // player 0 = MadDoctor
            m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, doctorId, far.Q, far.R) });

            var before = m.FindUnit(doctorId)!;
            var (bx, bz) = (before.X, before.Z);
            for (var i = 0; i < 10; i++) m.Tick(null);
            var after = m.FindUnit(doctorId)!;
            var dx = after.X - bx;
            var dz = after.Z - bz;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        var nightDist = Distance(LumenPhase.Night);
        var dayDist = Distance(LumenPhase.Day);   // Day has no Doctor speed modifier -- the plain baseline

        Assert.True(nightDist > dayDist, "Doctor should cover more ground at Night (+10% speed) than at Day (no modifier)");
        Assert.Equal(dayDist * 1.10, nightDist, 3);
    }

    // =====================================================================
    // Regeneration quirk (docs/06 + docs/23 §7's Doctor swing)
    // =====================================================================

    private static (MatchState m, uint woundedId) WoundedDoctorOutOfCombat(uint seed, bool hasQuirk, LumenPhase phase)
    {
        var city = SmallCity();
        var m = MatchState.Create(seed, DoctorVsArmy(), city);
        AdvanceToPhase(m, phase);

        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);
        var wounded = new CombatStats(maxVitality: 1000, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var woundedId = m.SpawnUnit(0, start, speed: 3.0, combat: wounded, hasRegenerationQuirk: hasQuirk);

        // A single near-zero-Ferocity hit to establish a real deficit,
        // then let it fall silent long enough to clear the out-of-combat
        // gate (docs/06: "out of combat only").
        var poker = new CombatStats(maxVitality: 100, power: 500, armor: 0, reach: 1, ferocity: 0.001, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var pokerId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: poker);
        m.Tick(new List<Command> { new Command(1, CommandKind.AttackUnit, pokerId, unchecked((int)woundedId)) });
        Assert.True(m.FindUnit(woundedId)!.Vitality < 1000);

        for (var i = 0; i < 60; i++) m.Tick(null);   // well past the out-of-combat threshold
        return (m, woundedId);
    }

    [Fact]
    public void Regeneration_quirk_heals_the_docs06_baseline_rate_when_the_lumen_modifier_is_neutral()
    {
        var (m, woundedId) = WoundedDoctorOutOfCombat(3u, hasQuirk: true, LumenPhase.Dawn);   // Doctor's regen mod is 1.0 at Dawn
        var wounded = m.FindUnit(woundedId)!;
        var before = wounded.Vitality;

        for (var i = 0; i < MatchState.TicksPerSecond; i++) m.Tick(null);   // exactly one simulated second

        var expectedHeal = (int)Math.Round(wounded.EffectiveMaxVitality * 1.0 / 100.0);   // docs/06: 1% max HP/s
        Assert.Equal(before + expectedHeal, wounded.Vitality);
    }

    [Fact]
    public void Regeneration_quirk_is_scaled_down_at_Day_and_up_at_Night_for_the_Doctor_faction()
    {
        var (dayM, dayId) = WoundedDoctorOutOfCombat(4u, hasQuirk: true, LumenPhase.Day);
        var dayUnit = dayM.FindUnit(dayId)!;
        var dayBefore = dayUnit.Vitality;
        for (var i = 0; i < MatchState.TicksPerSecond; i++) dayM.Tick(null);
        var dayHeal = dayUnit.Vitality - dayBefore;
        Assert.Equal((int)Math.Round(dayUnit.EffectiveMaxVitality * 1.0 / 100.0 * 0.90), dayHeal);

        var (nightM, nightId) = WoundedDoctorOutOfCombat(5u, hasQuirk: true, LumenPhase.Night);
        var nightUnit = nightM.FindUnit(nightId)!;
        var nightBefore = nightUnit.Vitality;
        for (var i = 0; i < MatchState.TicksPerSecond; i++) nightM.Tick(null);
        var nightHeal = nightUnit.Vitality - nightBefore;
        Assert.Equal((int)Math.Round(nightUnit.EffectiveMaxVitality * 1.0 / 100.0 * 1.15), nightHeal);

        Assert.True(nightHeal > dayHeal, "Doctor regen should be stronger at Night than at Day");
    }

    [Fact]
    public void A_unit_without_the_quirk_never_regenerates_no_matter_the_phase()
    {
        var (m, id) = WoundedDoctorOutOfCombat(6u, hasQuirk: false, LumenPhase.Night);   // Night: the best possible regen phase
        var unit = m.FindUnit(id)!;
        var before = unit.Vitality;
        for (var i = 0; i < 5 * MatchState.TicksPerSecond; i++) m.Tick(null);
        Assert.Equal(before, unit.Vitality);
    }

    [Fact]
    public void Regeneration_is_suppressed_while_still_in_combat()
    {
        var city = SmallCity();
        var m = MatchState.Create(7u, DoctorVsArmy(), city);
        AdvanceToPhase(m, LumenPhase.Night);   // Doctor's best regen phase -- if it's ever going to heal, it's now

        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);
        var wounded = new CombatStats(maxVitality: 1000, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var woundedId = m.SpawnUnit(0, start, speed: 3.0, combat: wounded, hasRegenerationQuirk: true);
        var poker = new CombatStats(maxVitality: 100, power: 500, armor: 0, reach: 1, ferocity: 0.001, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var pokerId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: poker);

        m.Tick(new List<Command> { new Command(1, CommandKind.AttackUnit, pokerId, unchecked((int)woundedId)) });
        var afterHit = m.FindUnit(woundedId)!.Vitality;
        Assert.True(afterHit < 1000);

        // Well within the out-of-combat window -- must NOT have healed yet.
        for (var i = 0; i < 20; i++) m.Tick(null);
        Assert.Equal(afterHit, m.FindUnit(woundedId)!.Vitality);
    }

    // =====================================================================
    // Determinism
    // =====================================================================

    [Fact]
    public void Same_seed_same_orders_hashes_identically_across_a_phase_transition_with_lumen_mods_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xC1DEu, ArmyVsHive(), city);
            var start = city.CenterHex;
            var neighbor = FindOpenNeighbor(city, start);
            var armyStats = new CombatStats(maxVitality: 200, power: 20, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
            var hiveStats = new CombatStats(maxVitality: 200, power: 18, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
            var armyId = m.SpawnUnit(0, start, speed: 3.0, combat: armyStats, hasRegenerationQuirk: false);
            var hiveId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: hiveStats);
            m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, armyId, unchecked((int)hiveId)) });
            // Run long enough to cross at least one real phase boundary.
            for (var i = 0; i < LumenClock.DawnTicks + 500; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    // =====================================================================
    // Acceptance: a scripted duel where time of day decides the winner
    // (docs/23 §7's own acceptance bar, "committed as a transcript")
    // =====================================================================

    [Fact]
    public void The_same_matchup_swings_between_a_Day_army_win_and_a_Night_army_loss()
    {
        bool ArmyWinsDuel(LumenPhase phase)
        {
            var city = SmallCity();
            var m = MatchState.Create(0xDEEDu, ArmyVsHive(), city);
            AdvanceToPhase(m, phase);
            var start = city.CenterHex;
            var neighbor = FindOpenNeighbor(city, start);

            // Opponent is baseline 10% stronger -- Army needs its Day
            // bonus to come out ahead; without it (Night), the stronger
            // opponent should win the war of attrition.
            var armyStats = new CombatStats(maxVitality: 200, power: 20, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
            var hiveStats = new CombatStats(maxVitality: 200, power: 22, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
            var armyId = m.SpawnUnit(0, start, speed: 3.0, combat: armyStats);
            var hiveId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: hiveStats);

            m.Tick(new List<Command> {
                new Command(0, CommandKind.AttackUnit, armyId, unchecked((int)hiveId)),
                new Command(1, CommandKind.AttackUnit, hiveId, unchecked((int)armyId)),
            });

            var ticks = 0;
            while (m.FindUnit(armyId)!.IsAlive && m.FindUnit(hiveId)!.IsAlive && ticks < 5000)
            {
                m.Tick(null);
                ticks++;
            }
            return m.FindUnit(armyId)!.IsAlive;
        }

        Assert.True(ArmyWinsDuel(LumenPhase.Day), "Army should win this matchup at Day, when its +15% damage bonus is active");
        Assert.False(ArmyWinsDuel(LumenPhase.Night), "The same Army should lose the same matchup at Night, with no bonus active");
    }
}
