using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-07 (creator report: "the list under the clock, never
/// seems to change" -- ResourceHud never updated after a harvester
/// delivered its load, because the Unity-side delivery call only ever
/// incremented a legacy Unity-only counter, never match-core's real
/// wallet). Covers <see cref="CommandKind.BankHarvestLoad"/>, the fix:
/// a harvester monster isn't itself a SimUnit yet (its movement/AI is
/// still Unity-side, docs/27), so unlike every other command kind there
/// is no source entity to validate against -- just PlayerIndex and a
/// positive amount.</summary>
public class BankHarvestLoadTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static MatchState FreshMatch(uint seed) =>
        MatchState.Create(seed, TwoPlayers(), CityGenerator.Generate(seed, CityPreset.Village()));

    [Fact]
    public void Grants_Blood_to_the_issuing_player()
    {
        var m = FreshMatch(1u);
        var before = m.Player(0).Wallet(ResourceKind.Blood);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 25) });
        Assert.Equal(before + 25, m.Player(0).Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Repeated_deliveries_accumulate()
    {
        var m = FreshMatch(2u);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 10) });
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 15) });
        Assert.Equal(25, m.Player(0).Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Only_credits_the_issuing_players_own_wallet()
    {
        var m = FreshMatch(3u);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 40) });
        Assert.Equal(0, m.Player(1).Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Zero_or_negative_amount_is_a_silent_no_op()
    {
        var m = FreshMatch(4u);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 0) });
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: -5) });
        Assert.Equal(0, m.Player(0).Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Out_of_range_player_index_is_a_silent_no_op_not_a_crash()
    {
        var m = FreshMatch(5u);
        m.Tick(new List<Command> { new Command(7, CommandKind.BankHarvestLoad, argA: 25) });
        Assert.Equal(0, m.Player(0).Wallet(ResourceKind.Blood));
        Assert.Equal(0, m.Player(1).Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_harvest_banking_in_play()
    {
        ulong Run()
        {
            var m = FreshMatch(0xBA4Cu);
            m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 12) });
            for (var i = 0; i < 50; i++) m.Tick(null);
            m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 8) });
            for (var i = 0; i < 50; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    // 2026-08 (creator direction: "humans have all the resources, make
    // sure those are properly being harvested... all harvesters can
    // collect all resources"): ArgB now selects WHICH resource a
    // BankHarvestLoad command grants, instead of always meaning Blood.

    [Fact]
    public void ArgB_selects_the_credited_resource()
    {
        var m = FreshMatch(6u);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 30, argB: (int)ResourceKind.Bones) });
        Assert.Equal(0, m.Player(0).Wallet(ResourceKind.Blood));
        Assert.Equal(30, m.Player(0).Wallet(ResourceKind.Bones));
    }

    [Fact]
    public void Omitting_ArgB_still_means_Blood_backward_compatibly()
    {
        // ResourceKind.Blood is enum value 0, the same value ArgB
        // carried implicitly before this parameter existed -- every
        // pre-existing call site (and this test's own sibling tests
        // above) never had to change.
        var m = FreshMatch(7u);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 15) });
        Assert.Equal(15, m.Player(0).Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Different_resources_can_be_banked_in_the_same_tick_independently()
    {
        var m = FreshMatch(8u);
        m.Tick(new List<Command>
        {
            new Command(0, CommandKind.BankHarvestLoad, argA: 9, argB: (int)ResourceKind.Blood),
            new Command(0, CommandKind.BankHarvestLoad, argA: 4, argB: (int)ResourceKind.Bones),
            new Command(0, CommandKind.BankHarvestLoad, argA: 2, argB: (int)ResourceKind.Brains),
        });
        Assert.Equal(9, m.Player(0).Wallet(ResourceKind.Blood));
        Assert.Equal(4, m.Player(0).Wallet(ResourceKind.Bones));
        Assert.Equal(2, m.Player(0).Wallet(ResourceKind.Brains));
    }

    [Fact]
    public void Out_of_range_ArgB_is_a_silent_no_op_not_a_crash()
    {
        var m = FreshMatch(9u);
        m.Tick(new List<Command> { new Command(0, CommandKind.BankHarvestLoad, argA: 25, argB: 999) });
        for (var kind = 0; kind < Resources.Count; kind++)
            Assert.Equal(0, m.Player(0).Wallet((ResourceKind)kind));
    }
}
