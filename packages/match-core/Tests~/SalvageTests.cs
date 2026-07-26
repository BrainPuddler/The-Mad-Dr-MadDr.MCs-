using System.Collections.Generic;
using System.Reflection;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 Phase 6a (docs/04): "every unit death drops 40-60% of
/// its construction components, lootable by either side for 15 seconds."
/// Covers the resource payout and the SalvageCorpse harvest command that
/// docs/12's Phase 4 entry explicitly deferred at the time (see
/// SimUnit.cs's own DeathTick doc comment history). Discovery's
/// family-identification/+5% affinity/Lab-unlock and the roaming Loose
/// Experiment anomalies are NOT covered here -- see Salvage.cs's own
/// header for why match-core stops at rolling a bare
/// YieldsGenomeFragment boolean and goes no further.</summary>
public class SalvageTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(9001u, CityPreset.Village());

    // Overwhelming power + trivial victim vitality + high ferocity -> a
    // guaranteed, same-tick kill, so tests don't need CombatTests'
    // "generous tick ceiling" pattern just to get to a corpse.
    private static CombatStats Slayer() => new CombatStats(maxVitality: 1000, power: 500, armor: 0, reach: 1, ferocity: 100.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
    private static CombatStats Frail() => new CombatStats(maxVitality: 1, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

    private static HexCoord FindOpenNeighbor(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var n in from.Neighbors())
            if (city.Contains(n) && !blocked.Contains(n)) return n;
        throw new System.InvalidOperationException("no open neighbor found");
    }

    /// <summary>Kills idB with a single Tick, returning both IDs. Attacker
    /// ends the tick adjacent to (and alive next to) the fresh corpse --
    /// exactly the position a real harvester would be in after landing
    /// the killing blow.</summary>
    private static (MatchState m, uint attackerId, uint corpseId) SetUpFreshCorpse(uint seed, int corpseSalvageValue)
    {
        var city = SmallCity();
        var m = MatchState.Create(seed, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);

        var attackerId = m.SpawnUnit(0, start, speed: 3.0, combat: Slayer());
        var corpseId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: Frail(), salvageValue: corpseSalvageValue);

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, targetEntity: attackerId, argA: unchecked((int)corpseId)) });

        Assert.False(m.FindUnit(corpseId)!.IsAlive);
        return (m, attackerId, corpseId);
    }

    // ---- SalvageMath: pure ----

    [Fact]
    public void RollPercent_stays_within_the_uniform_40_to_60_band()
    {
        var rng = new SimRng(10u);
        for (var i = 0; i < 500; i++)
            Assert.InRange(SalvageMath.RollPercent(rng), 40, 60);
    }

    [Fact]
    public void RollAmount_of_zero_total_value_is_always_zero()
    {
        var rng = new SimRng(11u);
        for (var i = 0; i < 50; i++)
            Assert.Equal(0, SalvageMath.RollAmount(0, rng));
    }

    [Fact]
    public void RollAmount_of_a_real_total_value_lands_within_the_40_to_60_percent_band()
    {
        var rng = new SimRng(12u);
        for (var i = 0; i < 200; i++)
        {
            var amount = SalvageMath.RollAmount(1000, rng);
            Assert.InRange(amount, 400, 600);
        }
    }

    [Fact]
    public void RollGenomeFragment_fires_roughly_10_percent_of_the_time()
    {
        var rng = new SimRng(13u);
        var hits = 0;
        const int trials = 4000;
        for (var i = 0; i < trials; i++) if (SalvageMath.RollGenomeFragment(rng)) hits++;
        // Bernoulli(0.1) over 4000 trials: mean 400, stddev ~19 -- a
        // +/-150 band is ~8 standard deviations, effectively flake-proof
        // while still catching "always/never fires" regressions.
        Assert.InRange(hits, 250, 550);
    }

    // ---- MatchState integration: death rolls the pile ----

    [Fact]
    public void Death_rolls_a_salvage_pile_within_the_40_to_60_percent_band_of_SalvageValue()
    {
        var (m, _, corpseId) = SetUpFreshCorpse(1u, corpseSalvageValue: 100);
        var corpse = m.FindUnit(corpseId)!;
        Assert.InRange(corpse.SalvageRemaining, 40, 60);
    }

    [Fact]
    public void A_unit_spawned_without_salvage_value_leaves_an_empty_corpse()
    {
        // corpseSalvageValue: 0 -- every pre-Phase-6a SpawnUnit call site.
        var (m, _, corpseId) = SetUpFreshCorpse(2u, corpseSalvageValue: 0);
        Assert.Equal(0, m.FindUnit(corpseId)!.SalvageRemaining);
    }

    // ---- SalvageCorpse command / harvest channel ----

    [Fact]
    public void SalvageCorpse_against_a_living_unit_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var start = city.CenterHex;
        var neighbor = FindOpenNeighbor(city, start);
        var harvesterId = m.SpawnUnit(0, start, speed: 3.0);
        var aliveId = m.SpawnUnit(1, neighbor, speed: 3.0, combat: Frail(), salvageValue: 100);

        m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: harvesterId, argA: unchecked((int)aliveId)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(harvesterId)!.Order);
    }

    [Fact]
    public void SalvageCorpse_out_of_range_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var start = city.CenterHex;
        var far = start.Ring(6);
        HexCoord farHex = default;
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var h in far) if (city.Contains(h) && !blocked.Contains(h)) { farHex = h; break; }

        var (m2, attackerId, corpseId) = SetUpFreshCorpse(5u, corpseSalvageValue: 100);
        var awayId = m2.SpawnUnit(0, farHex, speed: 3.0);

        m2.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: awayId, argA: unchecked((int)corpseId)) });
        Assert.Equal(UnitOrderKind.Idle, m2.FindUnit(awayId)!.Order);
    }

    [Fact]
    public void A_full_harvest_channel_pays_the_whole_pile_to_the_harvester_and_empties_the_corpse()
    {
        var (m, attackerId, corpseId) = SetUpFreshCorpse(6u, corpseSalvageValue: 100);
        var corpse = m.FindUnit(corpseId)!;
        var expectedAmount = corpse.SalvageRemaining;
        Assert.True(expectedAmount > 0);

        m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: attackerId, argA: unchecked((int)corpseId)) });
        Assert.Equal(UnitOrderKind.Salvaging, m.FindUnit(attackerId)!.Order);

        var partsBefore = m.Player(0).Wallet(ResourceKind.Parts);
        var completed = false;
        for (var i = 0; i < 60 && !completed; i++)
        {
            m.Tick(null);
            if (m.FindUnit(attackerId)!.Order == UnitOrderKind.Idle) completed = true;
        }

        Assert.True(completed, "the 3s channel should have completed well within 60 ticks");
        Assert.Equal(partsBefore + expectedAmount, m.Player(0).Wallet(ResourceKind.Parts));
        Assert.Equal(0, m.FindUnit(corpseId)!.SalvageRemaining);
        Assert.Null(m.FindUnit(attackerId)!.SalvageTargetId);
    }

    [Fact]
    public void Harvesting_an_empty_corpse_a_second_time_pays_nothing_more()
    {
        var (m, attackerId, corpseId) = SetUpFreshCorpse(7u, corpseSalvageValue: 100);
        m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: attackerId, argA: unchecked((int)corpseId)) });
        for (var i = 0; i < 60; i++) m.Tick(null);   // first harvest completes

        var partsAfterFirstHarvest = m.Player(0).Wallet(ResourceKind.Parts);
        m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: attackerId, argA: unchecked((int)corpseId)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(attackerId)!.Order);   // rejected -- SalvageRemaining already 0
        for (var i = 0; i < 60; i++) m.Tick(null);
        Assert.Equal(partsAfterFirstHarvest, m.Player(0).Wallet(ResourceKind.Parts));
    }

    [Fact]
    public void Corpse_moving_out_of_range_mid_channel_cancels_it_without_paying_out()
    {
        var (m, attackerId, corpseId) = SetUpFreshCorpse(8u, corpseSalvageValue: 100);
        m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: attackerId, argA: unchecked((int)corpseId)) });
        Assert.Equal(UnitOrderKind.Salvaging, m.FindUnit(attackerId)!.Order);

        // Tick partway through the channel, then yank the CORPSE far away
        // via reflection (the same TeleportUnit idiom EmitterTests uses to
        // reposition a unit without a real MoveTo command) -- proves
        // TickSalvage re-validates range every tick, not just at command
        // issuance, the same "an order can go stale mid-channel" law
        // TickCombat's own Reach check already follows.
        for (var i = 0; i < 10; i++) m.Tick(null);
        Assert.Equal(UnitOrderKind.Salvaging, m.FindUnit(attackerId)!.Order);   // still channeling

        var farHex = m.FindUnit(corpseId)!;
        var city = CityGenerator.Generate(9001u, CityPreset.Village());
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        HexCoord away = default;
        foreach (var h in city.CenterHex.Ring(8)) if (city.Contains(h) && !blocked.Contains(h)) { away = h; break; }
        TeleportUnit(m.FindUnit(corpseId)!, away);

        var partsBefore = m.Player(0).Wallet(ResourceKind.Parts);
        for (var i = 0; i < 30; i++) m.Tick(null);

        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(attackerId)!.Order);   // channel cancelled, not completed
        Assert.Equal(partsBefore, m.Player(0).Wallet(ResourceKind.Parts));
        Assert.True(m.FindUnit(corpseId)!.SalvageRemaining > 0);   // pile untouched
    }

    [Fact]
    public void A_decayed_corpse_can_no_longer_be_harvested()
    {
        var (m, attackerId, corpseId) = SetUpFreshCorpse(9u, corpseSalvageValue: 100);
        for (var i = 0; i < SimUnit.SalvageWindowTicks; i++) m.Tick(null);   // let the window close

        m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: attackerId, argA: unchecked((int)corpseId)) });
        // The attacker's own Order is still AttackUnit (TickCombat never
        // resets an attacker's order just because its target died -- it
        // simply has nothing left to resolve); what matters here is that
        // the harvest command itself was rejected, not folded into a
        // Salvaging channel.
        Assert.NotEqual(UnitOrderKind.Salvaging, m.FindUnit(attackerId)!.Order);
        Assert.Null(m.FindUnit(attackerId)!.SalvageTargetId);
    }

    // ---- Separation no longer moves corpses (docs/23 Phase 6a fix) ----

    [Fact]
    public void A_corpse_stays_put_even_with_a_living_neighbor_overlapping_its_body_radius()
    {
        var (m, attackerId, corpseId) = SetUpFreshCorpse(14u, corpseSalvageValue: 100);
        var corpse = m.FindUnit(corpseId)!;
        var (cx, cz) = (corpse.X, corpse.Z);

        // The attacker is already adjacent (melee range) -- well within
        // combined separation radii -- so the corpse would drift under the
        // pre-Phase-6a behavior if left ticking.
        for (var i = 0; i < 20; i++) m.Tick(null);

        Assert.Equal(cx, m.FindUnit(corpseId)!.X);
        Assert.Equal(cz, m.FindUnit(corpseId)!.Z);
    }

    // ---- Determinism ----

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_salvage_in_play()
    {
        ulong Run()
        {
            var (m, attackerId, corpseId) = SetUpFreshCorpse(0xACE5u, corpseSalvageValue: 100);
            m.Tick(new List<Command> { new Command(0, CommandKind.SalvageCorpse, targetEntity: attackerId, argA: unchecked((int)corpseId)) });
            for (var i = 0; i < 200; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    private static readonly PropertyInfo XProp = typeof(SimUnit).GetProperty("X")!;
    private static readonly PropertyInfo ZProp = typeof(SimUnit).GetProperty("Z")!;

    private static void TeleportUnit(SimUnit unit, HexCoord hex)
    {
        var (x, z) = hex.ToWorld();
        XProp.SetValue(unit, x);
        ZProp.SetValue(unit, z);
    }
}
