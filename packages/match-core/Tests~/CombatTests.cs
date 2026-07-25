using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §13 amendment C: the core combat loop (damage
/// formula + arcs + death/salvage event), pulled forward from Phase 6
/// into Phase 4 since XP-on-kill and Phase 6 both consume it. Ranged
/// (Reach>=2) posMod, turn-time-gated facing, chase-to-range movement,
/// actual salvage resource payout, and all of Phase 4's own RPG tasks
/// (XP/Level/Traits/Gear/Fusion) are NOT covered here -- see SimUnit.cs/
/// CombatStats.cs's own header comments and docs/12's Phase 4 entry for
/// why each is a separate, deferred slice.</summary>
public class CombatTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    private static CombatStats Shambler() => new CombatStats(maxVitality: 200, power: 22, armor: 3, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
    private static CombatStats WeakerShambler() => new CombatStats(maxVitality: 200, power: 20, armor: 3, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

    // ---- CombatMath: pure, docs/04's own worked examples ----

    [Fact]
    public void ResolveDamage_matches_docs04_worked_example_A_vs_B_front_on_no_aura()
    {
        // "Expected damage/hit: A -> B: 22x1.0x1.0x1.0 - 3 = 19"
        Assert.Equal(19, CombatMath.ResolveDamage(power: 22, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 100, armor: 3));
        // "B -> A: 17"
        Assert.Equal(17, CombatMath.ResolveDamage(power: 20, posModPercent: 100, emitterModPercent: 100, luckOrCritPercent: 100, armor: 3));
    }

    [Fact]
    public void ResolveDamage_matches_docs04_worked_example_aura_boosted_defender()
    {
        // "B retreats into a matching aura in its strong phase (B's
        // emitterMod 1.25...): B's expected hit = 22"
        Assert.Equal(22, CombatMath.ResolveDamage(power: 20, posModPercent: 100, emitterModPercent: 125, luckOrCritPercent: 100, armor: 3));
    }

    [Fact]
    public void ResolveDamage_never_drops_below_1_even_against_overwhelming_armor()
    {
        Assert.Equal(1, CombatMath.ResolveDamage(power: 5, posModPercent: 85, emitterModPercent: 85, luckOrCritPercent: 85, armor: 999));
    }

    [Fact]
    public void PosModForArc_matches_docs04s_front_flank_rear_table()
    {
        Assert.Equal(100, CombatMath.PosModForArc(Arc.Front));
        Assert.Equal(125, CombatMath.PosModForArc(Arc.Flank));
        Assert.Equal(150, CombatMath.PosModForArc(Arc.Rear));
    }

    [Theory]
    [InlineData(UnitAffinity.Solar, LumenPhase.Day, true, 125)]
    [InlineData(UnitAffinity.Solar, LumenPhase.Night, true, 85)]
    [InlineData(UnitAffinity.Solar, LumenPhase.Dusk, true, 100)]
    [InlineData(UnitAffinity.Solar, LumenPhase.Dawn, true, 100)]
    [InlineData(UnitAffinity.Lunar, LumenPhase.Night, true, 125)]
    [InlineData(UnitAffinity.Lunar, LumenPhase.Day, true, 85)]
    [InlineData(UnitAffinity.Lunar, LumenPhase.Dusk, true, 100)]
    [InlineData(UnitAffinity.Neutral, LumenPhase.Day, true, 110)]
    [InlineData(UnitAffinity.Neutral, LumenPhase.Night, true, 110)]
    [InlineData(UnitAffinity.Solar, LumenPhase.Day, false, 100)]
    [InlineData(UnitAffinity.Lunar, LumenPhase.Night, false, 100)]
    [InlineData(UnitAffinity.Neutral, LumenPhase.Day, false, 100)]
    public void EmitterModPercent_matches_docs03s_affinity_table(UnitAffinity affinity, LumenPhase phase, bool inAura, int expected)
    {
        Assert.Equal(expected, CombatMath.EmitterModPercent(affinity, phase, inAura));
    }

    [Fact]
    public void RollLuckPercent_stays_within_the_uniform_85_to_115_band()
    {
        var rng = new SimRng(1u);
        for (var i = 0; i < 500; i++)
        {
            var roll = CombatMath.RollLuckPercent(rng);
            Assert.InRange(roll, 85, 115);
        }
    }

    [Fact]
    public void RollCrit_never_fires_at_zero_cunning_and_always_fires_at_100()
    {
        var rng = new SimRng(2u);
        for (var i = 0; i < 100; i++) Assert.False(CombatMath.RollCrit(rng, 0));
        for (var i = 0; i < 100; i++) Assert.True(CombatMath.RollCrit(rng, 100));
    }

    // ---- MatchState integration ----

    private static HexCoord FindOpenNeighbor(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n)) return n;
        throw new System.InvalidOperationException("no open neighbor found");
    }

    [Fact]
    public void AttackUnit_out_of_range_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var start = city.CenterHex;
        var far = start.Ring(6);
        HexCoord farHex = default;
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var h in far) if (city.Contains(h) && !blocked.Contains(h)) { farHex = h; break; }

        var idA = m.SpawnUnit(0, start, speed: 3.0, combat: Shambler());
        var idB = m.SpawnUnit(1, farHex, speed: 3.0, combat: Shambler());

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(idA)!.Order);
    }

    [Fact]
    public void AttackUnit_against_a_non_combat_unit_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);

        var idA = m.SpawnUnit(0, start, speed: 3.0, combat: Shambler());
        var idB = m.SpawnUnit(1, neighbor, speed: 3.0);   // no combat stats at all

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(idA)!.Order);   // rejected -- idA never entered AttackUnit
        Assert.Null(m.FindUnit(idB)!.Combat);   // confirms idB really is a non-combat unit, not a test setup mistake
    }

    [Fact]
    public void Adjacent_attack_eventually_kills_and_opens_a_salvage_window()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);

        var idA = m.SpawnUnit(0, start, speed: 3.0, combat: Shambler());
        var idB = m.SpawnUnit(1, neighbor, speed: 3.0, combat: WeakerShambler());

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
        Assert.Equal(UnitOrderKind.AttackUnit, m.FindUnit(idA)!.Order);

        // Ferocity 1.0 -> one attack every 10 ticks; 200 Vitality / a
        // worst-case ~16 dmg-per-hit (22*0.85-3, floor of the luck band)
        // needs well under 200 attacks -- 3000 ticks is a generous ceiling.
        var b = m.FindUnit(idB)!;
        for (var i = 0; i < 3000 && b.IsAlive; i++) m.Tick(null);

        Assert.False(b.IsAlive);
        Assert.NotNull(b.DeathTick);
        Assert.Equal(0, b.Vitality);
        Assert.True(b.IsSalvageable(b.DeathTick!.Value));   // the instant it died
        Assert.True(b.IsSalvageable(b.DeathTick.Value + SimUnit.SalvageWindowTicks - 1));   // last tick of the window
        Assert.False(b.IsSalvageable(b.DeathTick.Value + SimUnit.SalvageWindowTicks));      // window just closed
    }

    [Fact]
    public void Ferocity_gates_attack_rate_to_one_hit_per_reciprocal_second()
    {
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);

        // Ferocity 0.5 attacks/s -> a hit every 20 ticks, never faster
        var slow = new CombatStats(maxVitality: 100000, power: 10, armor: 0, reach: 1, ferocity: 0.5, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var punchingBag = new CombatStats(maxVitality: 100000, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var idA = m.SpawnUnit(0, start, speed: 3.0, combat: slow);
        var idB = m.SpawnUnit(1, neighbor, speed: 3.0, combat: punchingBag);
        var b = m.FindUnit(idB)!;

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
        var afterFirstHit = 100000 - b.Vitality;
        Assert.True(afterFirstHit > 0, "the first tick's attack should have already resolved (cooldown starts ready)");

        for (var i = 0; i < 19; i++) m.Tick(null);   // ticks 2..20 -- not yet another full 1/0.5=2s=20 ticks
        Assert.Equal(afterFirstHit, 100000 - b.Vitality);   // no second hit yet

        m.Tick(null);   // the 20th tick since the first hit
        Assert.True(100000 - b.Vitality > afterFirstHit, "a second hit should land exactly on the 20th tick (1/Ferocity = 2s = 20 ticks)");
    }

    [Fact]
    public void Ranged_reach_attacker_uses_a_flat_posMod_not_an_arc_throw()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var start = city.CenterHex;
        var spots = new List<HexCoord>();
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 0; r <= 30 && spots.Count < 3; r++)
            foreach (var h in start.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) { spots.Add(h); if (spots.Count >= 3) break; }
        // spots[0] is `start` itself (ring 0); use a non-adjacent target
        var ranged = new CombatStats(maxVitality: 100, power: 10, armor: 0, reach: 3, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var target = new CombatStats(maxVitality: 100000, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

        // find a spot within reach 3 but NOT adjacent (distance 2 or 3)
        HexCoord farEnough = default;
        var found = false;
        for (var r = 2; r <= 3 && !found; r++)
            foreach (var h in start.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) { farEnough = h; found = true; break; }
        Assert.True(found, "need a reachable non-adjacent hex for this test");

        var idA = m.SpawnUnit(0, start, speed: 3.0, combat: ranged);
        var idB = m.SpawnUnit(1, farEnough, speed: 3.0, combat: target);

        // must not throw (Facing.ArcOf would throw on a non-adjacent pair)
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
        Assert.Equal(UnitOrderKind.AttackUnit, m.FindUnit(idA)!.Order);
        m.Tick(null);
        Assert.True(m.FindUnit(idB)!.Vitality < 100000);   // the attack actually resolved
    }

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_combat_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xC0Bu, TwoPlayers(), city);
            var start = city.CenterHex;
            var neighbor = FindOpenNeighbor(city, start);
            var idA = m.SpawnUnit(0, start, speed: 3.0, combat: Shambler());
            var idB = m.SpawnUnit(1, neighbor, speed: 3.0, combat: WeakerShambler());
            m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: idA, argA: unchecked((int)idB)) });
            for (var i = 0; i < 500; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }
}
