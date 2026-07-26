using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §6: "2-4 Loose Experiments wander the neutral streets
/// per match... cycling their aura every 20s through Damage &lt;-&gt; Speed
/// &lt;-&gt; Regen &lt;-&gt; XP-gain. Killing-blow player captures it: the
/// buff attaches to the killing unit for 90s, then the anomaly respawns at
/// a random roundabout." Wander movement is explicitly NOT covered here --
/// see Anomaly.cs's own header for why (no Citizen-walker to reuse in
/// match-core yet, a real prerequisite gap).</summary>
public class AnomalyTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    // Village() uses RoadPattern.MainStreet, which is the one pattern that
    // actually generates CityModel.Roundabouts -- confirmed by reading
    // CityGenerator.cs's own isMainStreet gate.
    private static CityModel SmallCity() => CityGenerator.Generate(5150u, CityPreset.Village());

    private static CombatStats Slayer() => new CombatStats(maxVitality: 1000, power: 500, armor: 0, reach: 1, ferocity: 100.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

    private static HexCoord FindOpenNeighbor(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n)) return n;
        throw new System.InvalidOperationException("no open neighbor found");
    }

    private static HexCoord FindOpenNeighborExcluding(CityModel city, HexCoord from, HexCoord exclude)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n) && !n.Equals(exclude)) return n;
        throw new System.InvalidOperationException("no open neighbor found");
    }

    // ---- CurrentBuff: pure (via a MatchState-spawned anomaly -- the
    // constructor/ApplyDamage/Respawn are sim-internal, same as every
    // other entity's mutators in this codebase; CurrentBuff itself is the
    // one public, directly-callable piece of pure math here). ----

    [Fact]
    public void CurrentBuff_cycles_Damage_Speed_Regen_XpGain_in_that_fixed_order()
    {
        var city = SmallCity();
        var m = MatchState.Create(20u, TwoPlayers(), city);
        var anomaly = m.FindAnomaly(m.SpawnAnomaly(city.Roundabouts[0]))!;   // spawned at Frame 0

        Assert.Equal(AnomalyBuffKind.Damage, anomaly.CurrentBuff(0));
        Assert.Equal(AnomalyBuffKind.Damage, anomaly.CurrentBuff(SimAnomaly.CycleTicks - 1));
        Assert.Equal(AnomalyBuffKind.Speed, anomaly.CurrentBuff(SimAnomaly.CycleTicks));
        Assert.Equal(AnomalyBuffKind.Regen, anomaly.CurrentBuff(SimAnomaly.CycleTicks * 2));
        Assert.Equal(AnomalyBuffKind.XpGain, anomaly.CurrentBuff(SimAnomaly.CycleTicks * 3));
        Assert.Equal(AnomalyBuffKind.Damage, anomaly.CurrentBuff(SimAnomaly.CycleTicks * 4));   // wraps
    }

    [Fact]
    public void CurrentBuff_is_relative_to_SpawnFrame_not_the_absolute_frame()
    {
        var city = SmallCity();
        var m = MatchState.Create(21u, TwoPlayers(), city);
        for (var i = 0; i < 1000; i++) m.Tick(null);   // advance the match clock before spawning
        var anomaly = m.FindAnomaly(m.SpawnAnomaly(city.Roundabouts[0]))!;   // SpawnFrame == 1000, not 0

        Assert.Equal(AnomalyBuffKind.Damage, anomaly.CurrentBuff(1000));
        Assert.Equal(AnomalyBuffKind.Speed, anomaly.CurrentBuff(1000 + SimAnomaly.CycleTicks));
    }

    // ---- MatchState integration: spawn, target, capture ----

    [Fact]
    public void SpawnAnomaly_places_it_alive_at_the_given_hex()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        Assert.True(city.Roundabouts.Count > 0, "Village() should generate at least one roundabout");

        var hex = city.Roundabouts[0];
        var id = m.SpawnAnomaly(hex);
        var anomaly = m.FindAnomaly(id)!;
        Assert.True(anomaly.IsAlive);
        Assert.Equal(1, m.AnomalyCount);

        var (x, z) = hex.ToWorld();
        Assert.Equal(x, anomaly.X);
        Assert.Equal(z, anomaly.Z);
    }

    [Fact]
    public void AttackAnomaly_out_of_range_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var start = city.CenterHex;
        var far = start.Ring(6);
        HexCoord farHex = default;
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var h in far) if (city.Contains(h) && !blocked.Contains(h)) { farHex = h; break; }

        var attackerId = m.SpawnUnit(0, farHex, speed: 3.0, combat: Slayer());
        var anomalyId = m.SpawnAnomaly(city.Roundabouts[0]);

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: attackerId, argA: unchecked((int)anomalyId)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(attackerId)!.Order);
    }

    [Fact]
    public void Killing_an_anomaly_captures_its_current_buff_and_respawns_it_elsewhere()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var roundabout = city.Roundabouts[0];
        var neighbor = FindOpenNeighbor(city, roundabout);

        var attackerId = m.SpawnUnit(0, neighbor, speed: 3.0, combat: Slayer());
        var anomalyId = m.SpawnAnomaly(roundabout);
        var (oldX, oldZ) = roundabout.ToWorld();

        // Overwhelming power (500) vs 50 max vitality -- guaranteed one-tick kill.
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: attackerId, argA: unchecked((int)anomalyId)) });

        var attacker = m.FindUnit(attackerId)!;
        Assert.NotNull(attacker.ActiveBuff);

        var anomaly = m.FindAnomaly(anomalyId)!;
        Assert.True(anomaly.IsAlive);   // respawned, not left dead
        Assert.Equal(SimAnomaly.MaxVitality, anomaly.Vitality);
        // Respawned at A roundabout -- with only one in this seed's city it
        // may coincide with the old position, so just confirm it's a real
        // roundabout hex, not confirm it moved.
        var atARoundabout = false;
        foreach (var r in city.Roundabouts)
        {
            var (rx, rz) = r.ToWorld();
            if (rx == anomaly.X && rz == anomaly.Z) { atARoundabout = true; break; }
        }
        Assert.True(atARoundabout);

        // Its own aura cycle restarted from Damage at the new SpawnFrame
        // (this same tick), not left picking up wherever it left off.
        Assert.Equal(AnomalyBuffKind.Damage, anomaly.CurrentBuff(m.Frame));
    }

    [Fact]
    public void Captured_Damage_buff_raises_EffectivePower_by_the_flagged_multiplier()
    {
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var roundabout = city.Roundabouts[0];
        var neighbor = FindOpenNeighbor(city, roundabout);

        var attackerId = m.SpawnUnit(0, neighbor, speed: 3.0, combat: Slayer());
        var anomalyId = m.SpawnAnomaly(roundabout);
        var attacker = m.FindUnit(attackerId)!;
        var basePower = attacker.EffectivePower;

        // Force the captured buff to Damage regardless of spawn-frame
        // cycle phase by re-spawning the anomaly fresh at Frame 0 (already
        // true here) and killing it before any cycle advance.
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: attackerId, argA: unchecked((int)anomalyId)) });

        Assert.Equal(AnomalyBuffKind.Damage, attacker.ActiveBuff);
        Assert.Equal((int)System.Math.Round(basePower * SimUnit.AnomalyBuffDamageMultiplier), attacker.EffectivePower);
    }

    [Fact]
    public void Anomaly_buff_expires_after_90_simulated_seconds()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var roundabout = city.Roundabouts[0];
        var neighbor = FindOpenNeighbor(city, roundabout);

        var attackerId = m.SpawnUnit(0, neighbor, speed: 3.0, combat: Slayer());
        var anomalyId = m.SpawnAnomaly(roundabout);
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: attackerId, argA: unchecked((int)anomalyId)) });

        var attacker = m.FindUnit(attackerId)!;
        Assert.NotNull(attacker.ActiveBuff);

        var buffTicks = (int)(SimUnit.AnomalyBuffDurationSeconds * MatchState.TicksPerSecond);
        for (var i = 0; i < buffTicks - 2; i++) m.Tick(null);
        Assert.NotNull(attacker.ActiveBuff);   // not yet expired

        for (var i = 0; i < 5; i++) m.Tick(null);
        Assert.Null(attacker.ActiveBuff);   // expired
    }

    [Fact]
    public void Regen_buff_heals_a_flat_percent_of_max_vitality_once_per_simulated_second()
    {
        var city = SmallCity();
        var m = MatchState.Create(6u, TwoPlayers(), city);
        var roundabout = city.Roundabouts[0];
        var healableHex = FindOpenNeighbor(city, roundabout);
        var wounderHex = FindOpenNeighborExcluding(city, healableHex, roundabout);

        var healable = new CombatStats(maxVitality: 1000, power: 500, armor: 0, reach: 1, ferocity: 100.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        // Ferocity 0.001 -> a ~1000s cooldown: the wounder's very first hit
        // (cooldown starts ready) lands, then it can never attack again
        // within this test's ~400-tick window while we wait out the
        // anomaly's own cycle to Regen -- exactly one wound, not a
        // continuous fight that would kill the healable unit outright.
        var wounder = new CombatStats(maxVitality: 100, power: 50, armor: 0, reach: 1, ferocity: 0.001, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var healableId = m.SpawnUnit(0, healableHex, speed: 3.0, combat: healable);
        var wounderId = m.SpawnUnit(1, wounderHex, speed: 3.0, combat: wounder);
        var anomalyId = m.SpawnAnomaly(roundabout);
        var h = m.FindUnit(healableId)!;
        var anomaly = m.FindAnomaly(anomalyId)!;

        // Wound the healable unit first via a REAL attack (only public
        // API, no reflection/internal access), leaving real headroom for
        // the heal to be observable (Heal is capped at EffectiveMaxVitality).
        m.Tick(new List<Command> { new Command(1, CommandKind.AttackUnit, targetEntity: wounderId, argA: unchecked((int)healableId)) });
        Assert.True(h.Vitality < 1000, "the wound should have already landed");

        // Advance to the anomaly's Regen phase, then capture it -- this
        // exercises the buff through the real command pipeline rather
        // than granting it directly.
        while (anomaly.CurrentBuff(m.Frame) != AnomalyBuffKind.Regen) m.Tick(null);
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: healableId, argA: unchecked((int)anomalyId)) });
        Assert.Equal(AnomalyBuffKind.Regen, h.ActiveBuff);

        var vitalityBeforeHeal = h.Vitality;
        for (var i = 0; i < MatchState.TicksPerSecond; i++) m.Tick(null);   // exactly one simulated second contains exactly one grant tick

        // Heal() itself caps at EffectiveMaxVitality (same as any real
        // heal-over-time effect would) -- match that here rather than
        // assume the wound always leaves >= one heal's worth of headroom.
        var expectedHeal = (int)System.Math.Round(h.EffectiveMaxVitality * SimUnit.AnomalyRegenPercentPerSecond / 100.0);
        var expected = System.Math.Min(h.EffectiveMaxVitality, vitalityBeforeHeal + expectedHeal);
        Assert.Equal(expected, h.Vitality);
        Assert.True(vitalityBeforeHeal < expected, "the heal should have raised Vitality by a real, observable amount");
    }

    [Fact]
    public void XpGain_buff_scales_kill_XP_by_the_flagged_multiplier()
    {
        var city = SmallCity();
        var m = MatchState.Create(7u, TwoPlayers(), city);
        var roundabout = city.Roundabouts[0];
        var attackerHex = FindOpenNeighbor(city, roundabout);
        var victimHex = FindOpenNeighborExcluding(city, attackerHex, roundabout);

        var frail = new CombatStats(maxVitality: 1, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
        var attackerId = m.SpawnUnit(0, attackerHex, speed: 3.0, combat: Slayer());
        var victimId = m.SpawnUnit(1, victimHex, speed: 3.0, combat: frail);
        var anomalyId = m.SpawnAnomaly(roundabout);
        var attacker = m.FindUnit(attackerId)!;
        var anomaly = m.FindAnomaly(anomalyId)!;

        // Advance to the anomaly's XpGain phase, then capture it -- again,
        // only public API, exercising the buff through a real kill.
        while (anomaly.CurrentBuff(m.Frame) != AnomalyBuffKind.XpGain) m.Tick(null);
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: attackerId, argA: unchecked((int)anomalyId)) });
        Assert.Equal(AnomalyBuffKind.XpGain, attacker.ActiveBuff);

        var victimLevel = m.FindUnit(victimId)!.Level;
        var expectedXp = (int)System.Math.Round(UnitLeveling.KillXp(victimLevel) * SimUnit.AnomalyXpGainMultiplier);

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: attackerId, argA: unchecked((int)victimId)) });
        Assert.False(m.FindUnit(victimId)!.IsAlive);
        Assert.Equal(expectedXp, attacker.XP);
    }

    // ---- Determinism ----

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_anomalies_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xA0Au, TwoPlayers(), city);
            var roundabout = city.Roundabouts[0];
            var neighbor = FindOpenNeighbor(city, roundabout);
            var attackerId = m.SpawnUnit(0, neighbor, speed: 3.0, combat: Slayer());
            var anomalyId = m.SpawnAnomaly(roundabout);
            m.Tick(new List<Command> { new Command(0, CommandKind.AttackAnomaly, targetEntity: attackerId, argA: unchecked((int)anomalyId)) });
            for (var i = 0; i < 200; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }
}
