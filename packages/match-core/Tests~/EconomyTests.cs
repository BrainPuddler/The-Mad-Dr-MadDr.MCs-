using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §3 Phase 3 slice: wallet caps raised by completed
/// storage buildings. Income ticks, upkeep drains, and onboard per-unit
/// pools are NOT covered here -- they're gated on prerequisites that
/// haven't landed yet (see BuildingDef.cs/PlayerState.cs's own header
/// comments and docs/12's Phase 3 entry), not silently skipped.</summary>
public class EconomyTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    [Fact]
    public void PlayerState_walletCap_defaultsToUncapped()
    {
        var p = new PlayerState(0, FactionId.MadDoctor, 60);
        Assert.Equal(int.MaxValue, p.WalletCap(ResourceKind.Blood));

        p.Grant(ResourceKind.Blood, 1_000_000);
        Assert.Equal(1_000_000, p.Wallet(ResourceKind.Blood));   // nothing clamps before any storage exists
    }

    [Fact]
    public void RaiseWalletCap_setsExactlyOnFirstRaise_thenAccumulatesNormally()
    {
        var p = new PlayerState(0, FactionId.MadDoctor, 60);
        p.RaiseWalletCap(ResourceKind.Blood, 100);
        Assert.Equal(100, p.WalletCap(ResourceKind.Blood));   // NOT int.MaxValue + 100 (would overflow/wrap)

        p.RaiseWalletCap(ResourceKind.Blood, 50);
        Assert.Equal(150, p.WalletCap(ResourceKind.Blood));   // accumulates normally after the first raise
    }

    [Fact]
    public void Grant_clampsAtTheCap_neverExceedsIt()
    {
        var p = new PlayerState(0, FactionId.MadDoctor, 60);
        p.RaiseWalletCap(ResourceKind.Blood, 50);
        p.Grant(ResourceKind.Blood, 200);
        Assert.Equal(50, p.Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Grant_neverRetroactivelyConfiscatesAnExistingOverCapBalance()
    {
        // docs/22 §6's own Q28 ("does a cap apply retroactively?") is
        // left open -- this pins the non-destructive reading: a cap
        // raised AFTER the wallet already exceeded it doesn't claw
        // anything back, only future Grants are capped.
        var p = new PlayerState(0, FactionId.MadDoctor, 60);
        p.Grant(ResourceKind.Blood, 80);   // uncapped at this point
        p.RaiseWalletCap(ResourceKind.Blood, 50);
        Assert.Equal(80, p.Wallet(ResourceKind.Blood));   // untouched

        p.Grant(ResourceKind.Blood, 10);   // wallet (80) is already over the cap (50) -- zero room, true no-op
        Assert.Equal(80, p.Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void PlayerState_clone_copiesWalletCapsToo()
    {
        var p = new PlayerState(0, FactionId.MadDoctor, 60);
        p.RaiseWalletCap(ResourceKind.Fuel, 75);
        var c = p.Clone();
        Assert.Equal(75, c.WalletCap(ResourceKind.Fuel));
    }

    [Fact]
    public void BuildStructure_completingABloodStorage_raisesTheRealDocs22CapAmount()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);
        player.Grant(ResourceKind.Parts, 100);

        Assert.Equal(int.MaxValue, player.WalletCap(ResourceKind.Blood));   // uncapped before any storage

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });
        var buildTime = BuildingDef.Get(BuildingKind.BloodStorage).BuildTimeTicks;

        // still under construction -- cap must not move early
        for (var i = 0; i < buildTime - 2; i++) m.Tick(null);
        Assert.Equal(int.MaxValue, player.WalletCap(ResourceKind.Blood));

        m.Tick(null);   // the exact tick construction completes
        Assert.Equal(BuildingState.Complete, m.BuildingAt(0).State);
        Assert.Equal(100, player.WalletCap(ResourceKind.Blood));   // docs/22 §6's real Blood Bank cap bonus

        // ticking further while already Complete must not re-apply the bonus
        for (var i = 0; i < 20; i++) m.Tick(null);
        Assert.Equal(100, player.WalletCap(ResourceKind.Blood));
    }

    [Fact]
    public void BuildStructure_completingAFuelStorage_raisesFuelCapOnly_notBlood()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Parts, 100);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.FuelStorage, argA: hex.Q, argB: hex.R) });
        var buildTime = BuildingDef.Get(BuildingKind.FuelStorage).BuildTimeTicks;
        for (var i = 0; i < buildTime; i++) m.Tick(null);

        Assert.Equal(100, player.WalletCap(ResourceKind.Fuel));
        Assert.Equal(int.MaxValue, player.WalletCap(ResourceKind.Blood));   // untouched -- different resource entirely
    }

    [Fact]
    public void BuildStructure_completingAFactory_raisesNoCapAtAll()
    {
        // Factory/Defense/PartsStorage/HarvestPost/FuelPump have no
        // StorageCapBonus (docs/23 §2's Function column) -- completing
        // one must be a true no-op for every wallet cap.
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player = m.Player(0);
        player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player.Grant(ResourceKind.Bones, 100);
        player.Grant(ResourceKind.Blood, 100);
        player.Grant(ResourceKind.Parts, 100);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.Factory, argA: hex.Q, argB: hex.R) });
        var buildTime = BuildingDef.Get(BuildingKind.Factory).BuildTimeTicks;
        for (var i = 0; i < buildTime; i++) m.Tick(null);

        Assert.Equal(BuildingState.Complete, m.BuildingAt(0).State);
        foreach (ResourceKind r in Enum.GetValues(typeof(ResourceKind)))
            Assert.Equal(int.MaxValue, player.WalletCap(r));
    }

    [Fact]
    public void HarvestPost_grantsABrainsTrickle_onlyOnceComplete_andOnlyToItsOwner()
    {
        // 2026-08 (creator report: "where is my big brain base... I
        // don't see them" -- Brains had no passive income at all, only
        // combat-kill/harvester delivery, leaving BigBrain's 20-Brain
        // cost unreachable without active hunting). HarvestPost's own
        // doc comment already called it the buildable Collection
        // Station, but granted nothing once built -- this is the fix.
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var player0 = m.Player(0);
        var player1 = m.Player(1);
        player0.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
        player0.Grant(ResourceKind.Bones, 100);
        player0.Grant(ResourceKind.Parts, 100);

        m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.HarvestPost, argA: hex.Q, argB: hex.R) });
        var buildTime = BuildingDef.Get(BuildingKind.HarvestPost).BuildTimeTicks;

        // still under construction -- no income yet, however long that takes
        for (var i = 0; i < buildTime - 1; i++) m.Tick(null);
        Assert.Equal(0, player0.Wallet(ResourceKind.Brains));

        m.Tick(null);   // the exact tick construction completes
        Assert.Equal(BuildingState.Complete, m.BuildingAt(0).State);

        // the grant gate is on the match's own ABSOLUTE Frame (same idiom
        // as GrantEmitterManaIncome's once-per-second gate), not a timer
        // relative to this building's own completion -- so the next grant
        // boundary depends on exactly which Frame construction finished
        // on, computed here rather than assumed as a round number.
        const int intervalTicks = 200;   // mirrors MatchState's private HarvestPostIncomeIntervalTicks
        var frameNow = 1 + buildTime;   // the placement tick, then buildTime more to Complete
        var ticksToNextGrant = intervalTicks - (frameNow % intervalTicks);

        for (var i = 0; i < ticksToNextGrant - 1; i++) m.Tick(null);
        Assert.Equal(0, player0.Wallet(ResourceKind.Brains));

        m.Tick(null);   // crosses the interval boundary -- exactly one grant
        Assert.Equal(1, player0.Wallet(ResourceKind.Brains));

        for (var i = 0; i < intervalTicks; i++) m.Tick(null);   // a second full interval
        Assert.Equal(2, player0.Wallet(ResourceKind.Brains));

        Assert.Equal(0, player1.Wallet(ResourceKind.Brains));   // never player 0's opponent
    }

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_wallet_caps_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xCA9u, TwoPlayers(), city);
            var hex = FindOpenHex(city, city.CenterHex);
            var player = m.Player(0);
            player.AddWorker();   // 2026-08 tech-wing epic Phase 1: BuildStructure now requires one
            player.Grant(ResourceKind.Bones, 200);
            player.Grant(ResourceKind.Blood, 200);
            player.Grant(ResourceKind.Parts, 200);

            m.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: hex.Q, argB: hex.R) });
            for (var i = 0; i < 150; i++) m.Tick(null);
            player.Grant(ResourceKind.Blood, 500);   // exceeds the newly-raised cap -- exercises the clamp too
            for (var i = 0; i < 20; i++) m.Tick(null);

            return m.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        // 2026-07: CanPlaceBuilding now rejects road hexes too -- see
        // BuildingTests.FindOpenHex's own comment for the full story.
        var roads = new HashSet<HexCoord>(city.Roads);
        for (var r = 0; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new InvalidOperationException("no open hex found");
    }
}
