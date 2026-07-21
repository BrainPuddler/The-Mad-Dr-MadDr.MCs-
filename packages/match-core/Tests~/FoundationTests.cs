using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

public class FoundationTests
{
    [Fact]
    public void Three_factions_with_correct_energy_per_origin()
    {
        Assert.Equal(3, FactionDef.AllFactions.Count);
        Assert.Equal(ResourceKind.Blood, FactionDef.Get(FactionId.MadDoctor).Energy);
        Assert.Equal(ResourceKind.Fuel, FactionDef.Get(FactionId.HumanArmy).Energy);
        Assert.Equal(ResourceKind.Ichor, FactionDef.Get(FactionId.AlienHive).Energy);
        // energy follows origin (docs/17 invariant)
        Assert.Equal(ResourceKind.Blood, Resources.EnergyOf(Origin.Organic));
        Assert.Equal(ResourceKind.Fuel, Resources.EnergyOf(Origin.Tech));
        Assert.Equal(ResourceKind.Ichor, Resources.EnergyOf(Origin.Biotech));
    }

    [Fact]
    public void Base_names_are_canon()
    {
        Assert.Equal("The Sanatorium", FactionDef.Get(FactionId.MadDoctor).BaseName);
        Assert.Equal("Fort Vigilance", FactionDef.Get(FactionId.HumanArmy).BaseName);
        Assert.Equal("The Brood Nest", FactionDef.Get(FactionId.AlienHive).BaseName);
    }

    [Fact]
    public void Chimera_track_opens_only_on_all_three_origins()
    {
        var p = new PlayerState(0, FactionId.MadDoctor, MatchState.DefaultSupplyCap);
        Assert.False(p.ChimeraTrackOpen);
        p.RecordSalvage(Origin.Organic);
        p.RecordSalvage(Origin.Tech);
        Assert.False(p.ChimeraTrackOpen);        // two of three -- still closed (the 1v1-reachability fix)
        p.RecordSalvage(Origin.Tech);            // idempotent -- re-salvaging same origin doesn't help
        Assert.False(p.ChimeraTrackOpen);
        p.RecordSalvage(Origin.Biotech);
        Assert.True(p.ChimeraTrackOpen);         // all three present
    }

    [Fact]
    public void Wallet_spend_is_validation_not_clamping()
    {
        var p = new PlayerState(0, FactionId.MadDoctor, MatchState.DefaultSupplyCap);
        p.Grant(ResourceKind.Blood, 50);
        Assert.False(p.TrySpend(ResourceKind.Blood, 51));   // unaffordable: rejected, unchanged
        Assert.Equal(50, p.Wallet(ResourceKind.Blood));
        Assert.True(p.TrySpend(ResourceKind.Blood, 30));
        Assert.Equal(20, p.Wallet(ResourceKind.Blood));
    }

    [Fact]
    public void Player_clone_is_independent_and_equal()
    {
        var p = new PlayerState(2, FactionId.AlienHive, 60);
        p.Grant(ResourceKind.Ichor, 100);
        p.RecordSalvage(Origin.Biotech);
        p.AddSupplyUsed(7);
        var c = p.Clone();

        var h1 = new FnvHash(); p.WriteTo(h1);
        var h2 = new FnvHash(); c.WriteTo(h2);
        Assert.Equal(h1.Value, h2.Value);

        c.Grant(ResourceKind.Ichor, 5);            // mutating the clone...
        Assert.Equal(100, p.Wallet(ResourceKind.Ichor));   // ...doesn't touch the original
    }

    [Fact]
    public void SimRng_is_deterministic_and_matches_citygen_integer_stream()
    {
        // same seed -> same sequence
        var a = new SimRng(42u);
        var b = new SimRng(42u);
        for (var i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());

        // bit-exact with citygen-core's proven-deterministic sfc32: its
        // Rng.Next() returns t/2^32, so our raw t must reproduce it.
        var sim = new SimRng(2026u);
        var city = new MadDr.CityGen.Rng(2026u);
        for (var i = 0; i < 1000; i++)
            Assert.Equal(sim.NextUInt() / 4294967296.0, city.Next(), 12);
    }

    [Fact]
    public void SimRng_state_roundtrips()
    {
        var a = new SimRng(123u);
        for (var i = 0; i < 37; i++) a.NextUInt();
        var restored = new SimRng(a.StateA, a.StateB, a.StateC, a.StateD);
        for (var i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), restored.NextUInt());
    }
}
