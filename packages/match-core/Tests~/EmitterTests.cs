using System.Collections.Generic;
using System.Reflection;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §13 amendment B (Phase 3.5): the Lumen Cycle clock,
/// emitter capture/ownership, and mana income. Unit affinity/emitterMod
/// and the Dominion victory timer are NOT covered here -- both are gated
/// on real prerequisites that don't exist yet (see SimEmitter.cs/
/// PlayerState.cs's own header comments and docs/12's Phase 3.5
/// entry).</summary>
public class EmitterTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    // ---- LumenClock: pure function of Frame ----

    [Theory]
    [InlineData(0, LumenPhase.Dawn)]
    [InlineData(299, LumenPhase.Dawn)]
    [InlineData(300, LumenPhase.Day)]      // Dawn (300 ticks) -> Day
    [InlineData(1199, LumenPhase.Day)]
    [InlineData(1200, LumenPhase.Dusk)]    // Day (900 ticks) -> Dusk
    [InlineData(1499, LumenPhase.Dusk)]
    [InlineData(1500, LumenPhase.Night)]   // Dusk (300 ticks) -> Night
    [InlineData(2399, LumenPhase.Night)]
    [InlineData(2400, LumenPhase.Dawn)]    // Night (900 ticks) -> wraps to Dawn, cycle 2
    [InlineData(2699, LumenPhase.Dawn)]
    [InlineData(2700, LumenPhase.Day)]
    public void LumenClock_phase_boundaries_are_exact(int frame, LumenPhase expected)
    {
        Assert.Equal(expected, LumenClock.PhaseAt(frame));
    }

    [Fact]
    public void LumenClock_cycle_is_240_seconds_at_10_ticks_per_second()
    {
        Assert.Equal(2400, LumenClock.CycleTicks);
        Assert.Equal(300, LumenClock.DawnTicks);
        Assert.Equal(900, LumenClock.DayTicks);
        Assert.Equal(300, LumenClock.DuskTicks);
        Assert.Equal(900, LumenClock.NightTicks);
    }

    [Theory]
    [InlineData(0, 300)]      // frame 0: full Dawn duration remaining
    [InlineData(299, 1)]      // last tick of Dawn
    [InlineData(300, 900)]    // first tick of Day: full Day duration remaining
    [InlineData(1199, 1)]     // last tick of Day
    [InlineData(1200, 300)]   // first tick of Dusk
    [InlineData(1499, 1)]     // last tick of Dusk
    [InlineData(1500, 900)]   // first tick of Night
    [InlineData(2399, 1)]     // last tick of Night, cycle 1
    [InlineData(2400, 300)]   // wraps: first tick of Dawn, cycle 2
    public void LumenClock_TicksUntilNextPhase_matches_PhaseAt_boundaries(int frame, int expectedTicksRemaining)
    {
        Assert.Equal(expectedTicksRemaining, LumenClock.TicksUntilNextPhase(frame));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(299)]
    [InlineData(300)]
    [InlineData(1199)]
    [InlineData(1200)]
    [InlineData(1499)]
    [InlineData(1500)]
    [InlineData(2399)]
    [InlineData(2400)]
    [InlineData(5137)]
    public void LumenClock_TicksUntilNextPhase_lands_exactly_on_the_next_PhaseAt_transition(int frame)
    {
        var currentPhase = LumenClock.PhaseAt(frame);
        var ticksToNext = LumenClock.TicksUntilNextPhase(frame);
        Assert.True(ticksToNext > 0);
        Assert.Equal(currentPhase, LumenClock.PhaseAt(frame + ticksToNext - 1));
        Assert.NotEqual(currentPhase, LumenClock.PhaseAt(frame + ticksToNext));
    }

    // ---- Emitter seeding ----

    [Fact]
    public void MatchState_seeds_one_SimEmitter_per_generated_emitter_landmark()
    {
        var city = SmallCity();
        var expected = 0;
        foreach (var l in city.Landmarks) if (l.Kind == LandmarkKind.Emitter) expected++;

        var m = MatchState.Create(1u, TwoPlayers(), city);
        Assert.Equal(expected, m.EmitterCount);
        Assert.True(m.EmitterCount > 0);
        for (var i = 0; i < m.EmitterCount; i++) Assert.Null(m.EmitterAt(i).Owner);   // uncaptured at match start
    }

    // ---- Capture logic ----

    [Fact]
    public void Capture_progresses_while_uncontested_and_flips_ownership_at_exactly_80_ticks()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var emitterHex = m.EmitterAt(0).Hex;
        m.SpawnUnit(0, emitterHex, speed: 3.0);   // standing on the emitter hex from tick 1

        for (var i = 0; i < SimEmitter.CaptureChannelTicks - 1; i++) m.Tick(null);
        Assert.Null(m.EmitterAt(0).Owner);
        Assert.Equal(SimEmitter.CaptureChannelTicks - 1, m.EmitterAt(0).CaptureProgressTicks);

        m.Tick(null);   // the exact tick that completes the 8s channel
        Assert.Equal(0, m.EmitterAt(0).Owner);
        Assert.Equal(0, m.EmitterAt(0).CaptureProgressTicks);
    }

    [Fact]
    public void Capture_is_a_no_op_when_the_hex_is_empty()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        for (var i = 0; i < 200; i++) m.Tick(null);
        Assert.Null(m.EmitterAt(0).Owner);
        Assert.Equal(0, m.EmitterAt(0).CaptureProgressTicks);
    }

    [Fact]
    public void Capture_resets_if_abandoned_before_completing()
    {
        // NOTE: a Landmark's own Site hex (including an Emitter's) is
        // part of its Landmark-tier building's footprint, which
        // BattlefieldState blocks-to-ground -- a real, separate,
        // discovered gap between docs/03's "stand on the emitter hex"
        // capture rule and docs/18's landmark generation, flagged in
        // docs/12 rather than silently worked around. Pathfinding a
        // MoveTo FROM that hex therefore fails today; this test isn't
        // about pathfinding, so it repositions the unit directly
        // (reflection into SimUnit's private X/Z setters) to isolate
        // the capture STATE MACHINE from that unrelated gap.
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var emitterHex = m.EmitterAt(0).Hex;
        var id = m.SpawnUnit(0, emitterHex, speed: 10.0);

        for (var i = 0; i < 30; i++) m.Tick(null);
        Assert.Equal(30, m.EmitterAt(0).CaptureProgressTicks);

        var farHex = FindFarOpenHex(city, emitterHex);
        TeleportUnit(m.FindUnit(id)!, farHex);
        m.Tick(null);

        Assert.Equal(0, m.EmitterAt(0).CaptureProgressTicks);
        Assert.Null(m.EmitterAt(0).Owner);
    }

    [Fact]
    public void Contested_capture_freezes_progress_without_resetting_it()
    {
        var city = SmallCity();
        var m = MatchState.Create(5u, TwoPlayers(), city);
        var emitter = m.EmitterAt(0);
        m.SpawnUnit(0, emitter.Hex, speed: 3.0);

        for (var i = 0; i < 30; i++) m.Tick(null);
        Assert.Equal(30, emitter.CaptureProgressTicks);

        // an enemy unit enters the aura (within 3 hexes, NOT on the exact
        // hex) -- docs/03: freezes progress, does not reset it
        var auraHex = OpenHexWithinAura(city, emitter.Hex, 2);
        m.SpawnUnit(1, auraHex, speed: 3.0);

        for (var i = 0; i < 50; i++) m.Tick(null);   // far more than enough to have completed if not contested
        Assert.Equal(30, emitter.CaptureProgressTicks);   // frozen, exactly where it was
        Assert.Null(emitter.Owner);
    }

    [Fact]
    public void Owned_emitter_can_be_recaptured_by_a_different_player()
    {
        // see Capture_resets_if_abandoned_before_completing's note on why
        // this repositions directly rather than issuing a MoveTo away
        // from the (blocked-to-ground) landmark hex.
        var city = SmallCity();
        var m = MatchState.Create(6u, TwoPlayers(), city);
        var emitterHex = m.EmitterAt(0).Hex;
        var id0 = m.SpawnUnit(0, emitterHex, speed: 10.0);
        for (var i = 0; i < SimEmitter.CaptureChannelTicks; i++) m.Tick(null);
        Assert.Equal(0, m.EmitterAt(0).Owner);

        // player 0's unit leaves, player 1's unit takes over the hex
        var farHex = FindFarOpenHex(city, emitterHex);
        TeleportUnit(m.FindUnit(id0)!, farHex);
        m.SpawnUnit(1, emitterHex, speed: 3.0);

        for (var i = 0; i < SimEmitter.CaptureChannelTicks; i++) m.Tick(null);
        Assert.Equal(1, m.EmitterAt(0).Owner);   // re-captured by player 1
    }

    // ---- Mana income ----

    [Theory]
    [InlineData(EmitterPolarity.Solar, LumenPhase.Day, 5)]
    [InlineData(EmitterPolarity.Solar, LumenPhase.Dusk, 3)]
    [InlineData(EmitterPolarity.Solar, LumenPhase.Night, 1)]
    [InlineData(EmitterPolarity.Solar, LumenPhase.Dawn, 3)]
    [InlineData(EmitterPolarity.Lunar, LumenPhase.Day, 1)]
    [InlineData(EmitterPolarity.Lunar, LumenPhase.Dusk, 3)]
    [InlineData(EmitterPolarity.Lunar, LumenPhase.Night, 5)]
    [InlineData(EmitterPolarity.Lunar, LumenPhase.Dawn, 3)]
    [InlineData(EmitterPolarity.Twilight, LumenPhase.Day, 3)]
    [InlineData(EmitterPolarity.Twilight, LumenPhase.Dusk, 6)]
    [InlineData(EmitterPolarity.Twilight, LumenPhase.Night, 3)]
    [InlineData(EmitterPolarity.Twilight, LumenPhase.Dawn, 6)]
    public void Owned_emitter_grants_the_exact_docs03_output_once_per_second(EmitterPolarity polarity, LumenPhase phase, int expectedPerSecond)
    {
        // Capture happens JUST BEFORE the target phase starts (not at
        // frame 0), so only a few seconds of income have accrued by the
        // time we measure -- capturing at frame 0 and waiting hundreds/
        // thousands of ticks for a later phase (Dusk/Night/Dawn) would
        // let income accumulate past the 100 cap before the measurement
        // window even starts, making the delta always read as 0.
        var city = SmallCity();
        var m = MatchState.Create(7u, TwoPlayers(), city);

        // Locate the first emitter with this polarity (guaranteed to
        // exist -- Village has 3 emitters, round-robin Solar/Lunar/Twilight).
        SimEmitter target = null;
        for (var i = 0; i < m.EmitterCount; i++)
            if (m.EmitterAt(i).Polarity == polarity) { target = m.EmitterAt(i); break; }
        Assert.NotNull(target);

        var startFrame = PhaseStartFrame(phase);
        var spawnFrame = startFrame - SimEmitter.CaptureChannelTicks;
        if (spawnFrame < 0) spawnFrame += LumenClock.CycleTicks;   // Dawn wraps to the previous cycle's tail
        while (m.Frame < spawnFrame) m.Tick(null);

        m.SpawnUnit(0, target.Hex, speed: 3.0);
        for (var i = 0; i < SimEmitter.CaptureChannelTicks; i++) m.Tick(null);
        Assert.Equal(0, target.Owner);   // captured right around the phase boundary

        // land on the next 10-tick boundary still safely inside the
        // target phase (a handful of ticks at most), THEN measure one
        // full simulated second from there.
        while (m.Frame % MatchState.TicksPerSecond != 0) m.Tick(null);
        var before = m.Player(0).Mana;
        for (var i = 0; i < MatchState.TicksPerSecond; i++) m.Tick(null);
        Assert.Equal(before + expectedPerSecond, m.Player(0).Mana);
    }

    [Fact]
    public void Mana_income_caps_at_100_and_overflow_is_lost()
    {
        var city = SmallCity();
        var m = MatchState.Create(8u, TwoPlayers(), city);
        var emitter = m.EmitterAt(0);
        m.SpawnUnit(0, emitter.Hex, speed: 3.0);
        for (var i = 0; i < SimEmitter.CaptureChannelTicks; i++) m.Tick(null);
        Assert.Equal(0, emitter.Owner);

        // run long enough (several full Lumen cycles) that total income
        // would vastly exceed 100 if uncapped
        for (var i = 0; i < LumenClock.CycleTicks * 2; i++) m.Tick(null);
        Assert.Equal(PlayerState.ManaCap, m.Player(0).Mana);
    }

    [Fact]
    public void Unowned_emitter_grants_no_mana_to_anyone()
    {
        var city = SmallCity();
        var m = MatchState.Create(9u, TwoPlayers(), city);
        for (var i = 0; i < 500; i++) m.Tick(null);
        Assert.Equal(0, m.Player(0).Mana);
        Assert.Equal(0, m.Player(1).Mana);
    }

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_emitters_and_mana_in_play()
    {
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xE317u, TwoPlayers(), city);
            var id = m.SpawnUnit(0, m.EmitterAt(0).Hex, speed: 3.0);
            m.SpawnUnit(1, m.EmitterAt(1).Hex, speed: 3.0);
            for (var i = 0; i < 300; i++) m.Tick(null);
            var far = FindFarOpenHex(city, m.EmitterAt(0).Hex);
            m.Tick(new List<Command> { new Command(0, CommandKind.MoveTo, targetEntity: id, argA: far.Q, argB: far.R) });
            for (var i = 0; i < 400; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }

    // ---- helpers ----

    private static readonly PropertyInfo XProp = typeof(SimUnit).GetProperty("X")!;
    private static readonly PropertyInfo ZProp = typeof(SimUnit).GetProperty("Z")!;

    /// <summary>Test-only direct reposition, bypassing pathfinding
    /// entirely (see the two capture tests that use this for why: a
    /// landmark's own Site hex is blocked-to-ground, so a real MoveTo
    /// FROM it fails today -- a separate, flagged gap, not what these
    /// tests are checking).</summary>
    private static void TeleportUnit(SimUnit unit, HexCoord hex)
    {
        var (x, z) = hex.ToWorld();
        XProp.SetValue(unit, x);
        ZProp.SetValue(unit, z);
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

    private static HexCoord FindFarOpenHex(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 10; r >= 1; r--)
            foreach (var h in from.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) return h;
        throw new System.InvalidOperationException("no far open hex found");
    }

    private static HexCoord OpenHexWithinAura(CityModel city, HexCoord center, int radius)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        foreach (var h in center.Ring(radius))
            if (city.Contains(h) && !blocked.Contains(h)) return h;
        throw new System.InvalidOperationException("no open aura hex found at that radius");
    }
}
