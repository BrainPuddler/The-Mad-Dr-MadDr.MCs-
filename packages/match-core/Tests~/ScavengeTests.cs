using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-08 (creator direction: "when a building is destroyed,
/// the debris field is scavenged for any usable metal by the zombie
/// workers, and monsters. Then the area is cleared and the area
/// reclaimed, so players can build in that area"): the building-side
/// twin of SalvageTests.cs -- same 40-60% roll, same channel-command
/// shape, same "one channel empties the whole pile" payout, but the
/// looted entity is a Destroyed SimBuilding's wreck instead of a unit's
/// corpse, and completing the loot (or letting it decay unscavenged) is
/// now part of what reopens the hex to new construction.</summary>
public class ScavengeTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(7001u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var roads = new HashSet<HexCoord>(city.Roads);
        foreach (var h in center.Ring(0)) if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        for (var r = 1; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new System.InvalidOperationException("no open hex found");
    }

    /// <summary>Spawns an HQ (Complete immediately, no BuildStructure wait),
    /// destroys it in one hit, and drops a scavenger unit adjacent to its
    /// hex -- the fastest path to a fresh, lootable wreck a real scavenger
    /// is already in range of, mirroring SalvageTests.SetUpFreshCorpse's
    /// own "attacker ends up adjacent to the fresh loot" setup.</summary>
    private static (MatchState m, uint scavengerId, uint buildingId, HexCoord hex) SetUpFreshWreck(uint seed)
    {
        var city = SmallCity();
        var m = MatchState.Create(seed, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var buildingId = m.SpawnHqForPlayer(0, hex);

        var maxHp = BuildingDef.Get(BuildingKind.Hq).MaxHp;
        m.ApplyBuildingDamage(buildingId, maxHp);   // exactly lethal
        Assert.Equal(BuildingState.Destroyed, m.FindBuilding(buildingId)!.State);

        var scavengerId = m.SpawnUnit(1, hex, speed: 3.0);
        return (m, scavengerId, buildingId, hex);
    }

    // ---- Destruction rolls the pile ----

    [Fact]
    public void Destruction_rolls_a_scavenge_pile_within_the_40_to_60_percent_band_of_ScavengeValue()
    {
        var (m, _, buildingId, _) = SetUpFreshWreck(1u);
        var expectedValue = BuildingDef.Get(BuildingKind.Hq).ScavengeValue;
        Assert.True(expectedValue > 0);
        Assert.InRange(m.FindBuilding(buildingId)!.ScavengeRemaining, expectedValue * 40 / 100, expectedValue * 60 / 100);
    }

    // ---- ScavengeDebris command / harvest channel ----

    [Fact]
    public void ScavengeDebris_against_a_standing_building_is_a_silent_no_op()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var hex = FindOpenHex(city, city.CenterHex);
        var standingId = m.SpawnHqForPlayer(0, hex);
        var scavengerId = m.SpawnUnit(1, hex, speed: 3.0);

        m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: scavengerId, argA: unchecked((int)standingId)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(scavengerId)!.Order);
    }

    [Fact]
    public void ScavengeDebris_out_of_range_is_a_silent_no_op()
    {
        var (m, _, buildingId, hex) = SetUpFreshWreck(3u);
        var city = SmallCity();
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        HexCoord farHex = default;
        foreach (var h in hex.Ring(6))
            if (city.Contains(h) && !blocked.Contains(h)) { farHex = h; break; }
        var awayId = m.SpawnUnit(1, farHex, speed: 3.0);

        m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: awayId, argA: unchecked((int)buildingId)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(awayId)!.Order);
    }

    [Fact]
    public void A_full_scavenge_channel_pays_the_whole_pile_to_the_scavenger_and_empties_the_wreck()
    {
        var (m, scavengerId, buildingId, _) = SetUpFreshWreck(4u);
        var expectedAmount = m.FindBuilding(buildingId)!.ScavengeRemaining;
        Assert.True(expectedAmount > 0);

        m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: scavengerId, argA: unchecked((int)buildingId)) });
        Assert.Equal(UnitOrderKind.Scavenging, m.FindUnit(scavengerId)!.Order);

        var partsBefore = m.Player(1).Wallet(ResourceKind.Parts);
        var completed = false;
        for (var i = 0; i < 60 && !completed; i++)
        {
            m.Tick(null);
            if (m.FindUnit(scavengerId)!.Order == UnitOrderKind.Idle) completed = true;
        }

        Assert.True(completed, "the 3s channel should have completed well within 60 ticks");
        Assert.Equal(partsBefore + expectedAmount, m.Player(1).Wallet(ResourceKind.Parts));
        Assert.Equal(0, m.FindBuilding(buildingId)!.ScavengeRemaining);
        Assert.Null(m.FindUnit(scavengerId)!.ScavengeTargetId);
    }

    [Fact]
    public void Scavenging_an_already_empty_wreck_a_second_time_pays_nothing_more()
    {
        var (m, scavengerId, buildingId, _) = SetUpFreshWreck(5u);
        m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: scavengerId, argA: unchecked((int)buildingId)) });
        for (var i = 0; i < 60; i++) m.Tick(null);   // first scavenge completes

        var partsAfterFirst = m.Player(1).Wallet(ResourceKind.Parts);
        m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: scavengerId, argA: unchecked((int)buildingId)) });
        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(scavengerId)!.Order);   // rejected -- ScavengeRemaining already 0
        for (var i = 0; i < 60; i++) m.Tick(null);
        Assert.Equal(partsAfterFirst, m.Player(1).Wallet(ResourceKind.Parts));
    }

    // ---- Reclaim gating: scavenging vs. the decay fallback ----

    [Fact]
    public void A_fully_scavenged_hex_reopens_right_at_RubbleClearTicks()
    {
        var (m, scavengerId, buildingId, hex) = SetUpFreshWreck(6u);
        m.Player(0).Grant(ResourceKind.Bones, 1000);
        m.Player(0).Grant(ResourceKind.Fuel, 1000);
        var destroyedAtFrame = m.FindBuilding(buildingId)!.DestroyedAtFrame!.Value;
        m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: scavengerId, argA: unchecked((int)buildingId)) });
        for (var i = 0; i < 60; i++) m.Tick(null);   // channel completes well before RubbleClearTicks (20s)
        Assert.Equal(0, m.FindBuilding(buildingId)!.ScavengeRemaining);

        // continue ticking to exactly RubbleClearTicks total elapsed since
        // destruction -- computed from Frame/DestroyedAtFrame directly
        // (not a hand-counted tick total) so this stays correct regardless
        // of exactly how many ticks the scavenge channel above took.
        var elapsed = m.Frame - destroyedAtFrame;
        for (var i = elapsed; i < MatchState.RubbleClearTicks; i++) m.Tick(null);
        Assert.True(m.CanPlaceBuilding(0, BuildingKind.FuelStorage, hex));
    }

    [Fact]
    public void An_unscavenged_hex_stays_blocked_past_RubbleClearTicks_until_DebrisDecayTicks_pass()
    {
        var (m, _, _, hex) = SetUpFreshWreck(7u);
        m.Player(0).Grant(ResourceKind.Bones, 1000);
        m.Player(0).Grant(ResourceKind.Fuel, 1000);
        // nobody scavenges -- tick well past the minimum 20s clear window
        for (var i = 0; i < MatchState.RubbleClearTicks; i++) m.Tick(null);
        Assert.False(m.CanPlaceBuilding(0, BuildingKind.FuelStorage, hex));   // still holding metal, still blocked

        // one tick short of the decay ceiling -- still blocked
        for (var i = MatchState.RubbleClearTicks; i < MatchState.DebrisDecayTicks - 1; i++) m.Tick(null);
        Assert.False(m.CanPlaceBuilding(0, BuildingKind.FuelStorage, hex));

        m.Tick(null);   // the exact tick the debris finally decays
        Assert.True(m.CanPlaceBuilding(0, BuildingKind.FuelStorage, hex));
    }

    // ---- Determinism ----

    [Fact]
    public void Same_seed_same_orders_hashes_identically_with_scavenge_in_play()
    {
        ulong Run()
        {
            var (m, scavengerId, buildingId, _) = SetUpFreshWreck(0xB0A7u);
            m.Tick(new List<Command> { new Command(1, CommandKind.ScavengeDebris, targetEntity: scavengerId, argA: unchecked((int)buildingId)) });
            for (var i = 0; i < 200; i++) m.Tick(null);
            return m.Hash();
        }
        Assert.Equal(Run(), Run());
    }
}
