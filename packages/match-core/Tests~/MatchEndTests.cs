using System.Collections.Generic;
using System.Reflection;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/02 "Victory conditions": <see cref="MatchState.CheckMatchEnd"/>'s
/// own contract -- Elimination (Hq destruction, generalized for N-player
/// FFA), Dominion (60% of emitters held continuously for one full Lumen
/// Cycle), and the 15-minute time-cap territory tiebreak. Full design
/// writeup: docs/12/docs/02.</summary>
public class MatchEndTests
{
    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var roads = new HashSet<HexCoord>(city.Roads);
        for (var r = 0; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new System.InvalidOperationException("no open hex found");
    }

    // Same "reposition directly, bypass pathfinding" technique
    // EmitterTests.cs's own Owned_emitter_can_be_recaptured_by_a_different_player
    // test already established, for the identical reason: an emitter's
    // Site hex is blocked-to-ground (docs/12), so a real MoveTo FROM it
    // fails today -- a separate, already-flagged gap this test isn't
    // about.
    private static readonly PropertyInfo XProp = typeof(SimUnit).GetProperty("X")!;
    private static readonly PropertyInfo ZProp = typeof(SimUnit).GetProperty("Z")!;

    private static void TeleportUnit(SimUnit unit, HexCoord hex)
    {
        var (x, z) = hex.ToWorld();
        XProp.SetValue(unit, x);
        ZProp.SetValue(unit, z);
    }

    private static HexCoord FindFarOpenHex(CityModel city, HexCoord from)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        for (var r = 10; r >= 1; r--)
            foreach (var h in from.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h)) return h;
        throw new System.InvalidOperationException("no far open hex found");
    }

    // ---- Elimination ----

    [Fact]
    public void DestroyingTheOnlyOpponentsHqEndsTheMatchWithElimination()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(1u, players, city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(0, hex);
        var hq1 = m.SpawnHqForPlayer(1, hex);   // same hex -- spatial separation is irrelevant here

        Assert.False(m.IsMatchOver);
        m.Tick(null);
        Assert.False(m.IsMatchOver);   // nobody's Hq destroyed yet

        m.ApplyBuildingDamage(hq1, 999999);
        m.Tick(null);

        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.Elimination, m.EndReason);
        Assert.Equal(0, m.WinnerPlayerIndex);
        Assert.True(m.Player(1).IsEliminated);
        Assert.False(m.Player(0).IsEliminated);
    }

    [Fact]
    public void SimultaneousMutualEliminationIsADraw()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(2u, players, city);
        var hex = FindOpenHex(city, city.CenterHex);
        var hq0 = m.SpawnHqForPlayer(0, hex);
        var hq1 = m.SpawnHqForPlayer(1, hex);

        // Both Hqs destroyed before the SAME Tick() call that discovers it.
        m.ApplyBuildingDamage(hq0, 999999);
        m.ApplyBuildingDamage(hq1, 999999);
        m.Tick(null);

        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.Elimination, m.EndReason);
        Assert.Null(m.WinnerPlayerIndex);
        Assert.True(m.Player(0).IsEliminated);
        Assert.True(m.Player(1).IsEliminated);
    }

    [Fact]
    public void ThreePlayerFfa_OneEliminationDoesNotEndTheMatch_LastStandingWins()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor };
        var m = MatchState.Create(3u, players, city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(0, hex);
        var hq1 = m.SpawnHqForPlayer(1, hex);
        var hq2 = m.SpawnHqForPlayer(2, hex);

        m.ApplyBuildingDamage(hq1, 999999);
        m.Tick(null);
        Assert.False(m.IsMatchOver, "two players still standing -- the match must keep running");
        Assert.True(m.Player(1).IsEliminated);
        Assert.False(m.Player(2).IsEliminated);

        m.ApplyBuildingDamage(hq2, 999999);
        m.Tick(null);
        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.Elimination, m.EndReason);
        Assert.Equal(0, m.WinnerPlayerIndex);
    }

    [Fact]
    public void MatchesThatNeverSpawnAnHqNeverSpuriouslyEliminateAnyone()
    {
        // The overwhelming majority of this test SUITE's own other files
        // construct a MatchState directly and never call
        // SpawnHqForPlayer -- CheckMatchEnd must not treat "no Hq" the
        // same as "Hq destroyed."
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(3u, players, city);

        for (var frame = 0; frame < 500; frame++) m.Tick(null);

        Assert.False(m.IsMatchOver);
        Assert.False(m.Player(0).IsEliminated);
        Assert.False(m.Player(1).IsEliminated);
    }

    [Fact]
    public void OnceOverFurtherTicksAreNoOps()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(4u, players, city);
        var hex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(0, hex);
        var hq1 = m.SpawnHqForPlayer(1, hex);
        m.ApplyBuildingDamage(hq1, 999999);
        m.Tick(null);
        Assert.True(m.IsMatchOver);

        var frameAtEnd = m.Frame;
        var commandsAtEnd = m.CommandsProcessed;
        m.Tick(new List<Command> { new Command(0, CommandKind.Ping) });
        m.Tick(null);
        m.Tick(null);

        Assert.Equal(frameAtEnd, m.Frame);
        Assert.Equal(commandsAtEnd, m.CommandsProcessed);
        Assert.Equal(0, m.WinnerPlayerIndex);   // verdict never changes either
    }

    // ---- Dominion ----

    /// <summary>Spawns a unit for `playerIndex` directly on each of the
    /// first `count` emitters' own hexes (same "spawn ON the hex, no
    /// pathfinding needed" technique EmitterTests.cs already
    /// established) and ticks <see cref="SimEmitter.CaptureChannelTicks"/>
    /// times so all of them finish capturing in the same tick. Returns
    /// the spawned units' entity IDs, in emitter order.</summary>
    private static List<uint> CaptureFirstEmitters(MatchState m, int playerIndex, int count)
    {
        var ids = new List<uint>();
        for (var e = 0; e < count; e++) ids.Add(m.SpawnUnit(playerIndex, m.EmitterAt(e).Hex, speed: 3.0));
        for (var i = 0; i < SimEmitter.CaptureChannelTicks; i++) m.Tick(null);
        return ids;
    }

    [Fact]
    public void HoldingSixtyPercentOfEmittersForAFullLumenCycleWinsByDominion()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(5u, players, city);
        var majority = (m.EmitterCount * 3 + 4) / 5;   // comfortably > 60%, integer ceiling-ish
        Assert.True(majority > 0, "test city needs at least one emitter");

        CaptureFirstEmitters(m, 0, majority);
        Assert.False(m.IsMatchOver, "capturing alone isn't Dominion -- the streak still has to run a full cycle");

        for (var i = 0; i < LumenClock.CycleTicks; i++)
        {
            m.Tick(null);
            if (m.IsMatchOver) break;
        }

        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.Dominion, m.EndReason);
        Assert.Equal(0, m.WinnerPlayerIndex);
    }

    [Fact]
    public void DroppingBelowSixtyPercentResetsTheStreakRatherThanFreezingIt()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(6u, players, city);
        var total = m.EmitterCount;
        var majority = (total * 3 + 4) / 5;
        Assert.True(majority > 0, "test city needs at least one emitter");

        var ids = CaptureFirstEmitters(m, 0, majority);
        var streakBeforeDrop = m.Player(0).DominionStreakTicks;
        Assert.True(streakBeforeDrop > 0);

        // Hand emitter 0 over to player 1 via a REAL re-capture (the only
        // way SimEmitter.Owner ever changes -- "captured emitters stay
        // owned until re-captured"): player 0's unit leaves the hex first
        // (teleported, not MoveTo -- see TeleportUnit's own comment for
        // why), THEN player 1 stands there uncontested for the full
        // capture channel. If player 0's unit stayed put, the hex would
        // read as CONTESTED (two different players on the same hex),
        // which only freezes progress -- it would never actually flip
        // Owner, and this test would be checking nothing real.
        var emitterHex = m.EmitterAt(0).Hex;
        var farHex = FindFarOpenHex(city, emitterHex);
        TeleportUnit(m.FindUnit(ids[0])!, farHex);
        m.SpawnUnit(1, emitterHex, speed: 3.0);
        for (var i = 0; i < SimEmitter.CaptureChannelTicks; i++) m.Tick(null);

        Assert.Equal(1, m.EmitterAt(0).Owner);   // genuinely changed hands
        Assert.Equal(0, m.Player(0).DominionStreakTicks);   // reset, not frozen
        Assert.False(m.IsMatchOver);
    }

    // ---- Time cap ----

    [Fact]
    public void TimeCapAwardsTheHigherTerritoryScore()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(7u, players, city);
        var hex = FindOpenHex(city, city.CenterHex);
        // TerritoryScore(0) = 1 (one Complete building), TerritoryScore(1)
        // = 0 -- no emitter involved at all, so no risk of an unrelated
        // Dominion condition firing first and pre-empting the assertion
        // this test actually wants to make.
        m.SpawnFactoryForPlayer(0, hex);

        for (var frame = m.Frame; frame < MatchState.DefaultTimeCapTicks; frame++)
        {
            m.Tick(null);
            if (m.IsMatchOver) break;
        }

        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.TimeCap, m.EndReason);
        Assert.Equal(0, m.WinnerPlayerIndex);
        Assert.True(m.TerritoryScore(0) > m.TerritoryScore(1));
    }

    [Fact]
    public void TimeCapWithATiedScoreIsADraw()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(8u, players, city);
        // Neither player captures anything, builds anything, or fields
        // anyone -- both TerritoryScore(0) and TerritoryScore(1) are
        // exactly 0, a guaranteed tie regardless of the weighting.
        for (var frame = 0; frame < MatchState.DefaultTimeCapTicks; frame++)
        {
            m.Tick(null);
            if (m.IsMatchOver) break;
        }

        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.TimeCap, m.EndReason);
        Assert.Null(m.WinnerPlayerIndex);
    }

    // ---- Match duration (2026-08: "add a game duration selector 15,30,45
    // minutes or unlimited") ----

    [Fact]
    public void ACustomShorterDurationCapsTheMatchAtItsOwnTickCount()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var fiveMinuteTicks = 5 * 60 * MatchState.TicksPerSecond;
        var m = MatchState.Create(10u, players, city, timeCapTicks: fiveMinuteTicks);
        Assert.Equal(fiveMinuteTicks, m.TimeCapTicks);

        // Tick right up to (but not across) the boundary -- CheckMatchEnd
        // fires on Frame >= TimeCapTicks, so fiveMinuteTicks-1 ticks land
        // on Frame == fiveMinuteTicks-1, still comfortably below it.
        for (var frame = 0; frame < fiveMinuteTicks - 1; frame++)
        {
            m.Tick(null);
            Assert.False(m.IsMatchOver, $"frame {m.Frame}: must not end before its own {fiveMinuteTicks}-tick cap");
        }
        Assert.Equal(fiveMinuteTicks - 1, m.Frame);

        m.Tick(null);   // the ONE tick that actually crosses Frame >= fiveMinuteTicks

        Assert.True(m.IsMatchOver);
        Assert.Equal(MatchEndReason.TimeCap, m.EndReason);
        // Nowhere near MatchState.DefaultTimeCapTicks (9000) -- proves the
        // custom duration actually took effect rather than silently
        // falling back to the 15-minute default.
        Assert.True(m.Frame < MatchState.DefaultTimeCapTicks);
    }

    [Fact]
    public void UnlimitedDurationNeverTimeCaps()
    {
        var city = SmallCity();
        var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
        var m = MatchState.Create(11u, players, city, timeCapTicks: null);
        Assert.Null(m.TimeCapTicks);

        // Run comfortably PAST where the old fixed 15-minute cap
        // (DefaultTimeCapTicks) would have fired.
        for (var frame = 0; frame < MatchState.DefaultTimeCapTicks + 500; frame++) m.Tick(null);

        Assert.False(m.IsMatchOver);
        Assert.Equal(MatchEndReason.None, m.EndReason);
    }

    // ---- Determinism ----

    [Fact]
    public void TwoIdenticalMatchesReachTheIdenticalVerdictAndHash()
    {
        MatchState Run()
        {
            var city = SmallCity();
            var players = new List<FactionId> { FactionId.HumanArmy, FactionId.AlienHive };
            var m = MatchState.Create(9u, players, city);
            var hex = FindOpenHex(city, city.CenterHex);
            m.SpawnHqForPlayer(0, hex);
            var hq1 = m.SpawnHqForPlayer(1, hex);
            m.ApplyBuildingDamage(hq1, 999999);
            m.Tick(null);
            return m;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a.IsMatchOver, b.IsMatchOver);
        Assert.Equal(a.WinnerPlayerIndex, b.WinnerPlayerIndex);
        Assert.Equal(a.EndReason, b.EndReason);
        Assert.Equal(a.Hash(), b.Hash());
    }
}
