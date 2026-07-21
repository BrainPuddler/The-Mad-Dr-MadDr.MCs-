using System.Collections.Generic;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

public class MatchStateTests
{
    private static List<FactionId> EightPlayers() => new()
    {
        FactionId.MadDoctor, FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor,
        FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor, FactionId.HumanArmy,
    };

    [Fact]
    public void Same_seed_no_commands_hashes_identically_after_10000_ticks()
    {
        // docs/23 Phase 1 acceptance: a headless 8-player match runs 10k
        // ticks deterministically -- identical hash across two runs.
        ulong Run()
        {
            var m = MatchState.Create(1234u, EightPlayers());
            for (var i = 0; i < 10_000; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Same_seed_same_command_stream_hashes_identically()
    {
        List<Command> BundleFor(int frame) => new()
        {
            new Command(frame % 4, CommandKind.Ping, targetEntity: (uint)(frame + 1), argA: frame),
        };

        ulong Run()
        {
            var m = MatchState.Create(99u, EightPlayers());
            for (var i = 0; i < 2000; i++) m.Tick(BundleFor(i));
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Different_seed_diverges()
    {
        var a = MatchState.Create(1u, EightPlayers());
        var b = MatchState.Create(2u, EightPlayers());
        for (var i = 0; i < 100; i++) { a.Tick(null); b.Tick(null); }
        // Phase 1 has no seed-driven per-tick systems yet, so equal seeds
        // would match -- but the RNG state is seeded and hashed, so two
        // seeds must already differ at tick 0.
        Assert.NotEqual(a.Hash(), b.Hash());
    }

    [Fact]
    public void Frame_and_commands_advance_exactly()
    {
        var m = MatchState.Create(7u, EightPlayers());
        Assert.Equal(0, m.Frame);
        m.Tick(new List<Command> { new Command(0, CommandKind.Ping) });
        m.Tick(null);
        Assert.Equal(2, m.Frame);
        Assert.Equal(1, m.CommandsProcessed);
    }

    [Fact]
    public void Entity_ids_are_monotonic_from_one_and_deterministic()
    {
        uint[] Alloc()
        {
            var m = MatchState.Create(5u, EightPlayers());
            var ids = new uint[8];
            for (var i = 0; i < ids.Length; i++) ids[i] = m.AllocateEntityId();
            return ids;
        }
        var first = Alloc();
        Assert.Equal(1u, first[0]);
        for (var i = 1; i < first.Length; i++) Assert.Equal(first[i - 1] + 1, first[i]);
        Assert.Equal(first, Alloc());   // deterministic across runs
    }

    [Fact]
    public void Player_count_bounds_enforced()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            MatchState.Create(1u, new List<FactionId> { FactionId.MadDoctor }));   // 1 player
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            MatchState.Create(1u, new List<FactionId>(new FactionId[9])));         // 9 players
    }
}
