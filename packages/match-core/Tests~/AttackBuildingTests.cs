using System;
using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>2026-07 (creator direction: "Building need decent amount of
/// HPs and should show damage and some low-poly fire when being
/// attacked"): CommandKind.AttackBuilding's full cycle -- a genuine gap
/// closed this phase, not a pre-existing feature. Before this, <see
/// cref="MatchState.ApplyBuildingDamage"/> existed but nothing in the sim
/// ever called it; a player-built SimBuilding (HQ/Factory/etc) could not
/// be attacked at all. See docs/12's 2026-07 entry for the full
/// account.</summary>
public class AttackBuildingTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };
    private static CityModel SmallCity() => CityGenerator.Generate(4242u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        // 2026-07: CanPlaceBuilding now rejects road hexes too (creator
        // report: "the factory and central base is in the middle of a
        // road") -- excluded here so this picker never hands a test a
        // hex that would silently fail to build on.
        var roads = new HashSet<HexCoord>(city.Roads);
        foreach (var h in center.Ring(0)) if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        for (var r = 1; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new InvalidOperationException("no open hex found");
    }

    private static CombatStats Attacker(int power) =>
        new CombatStats(maxVitality: 100, power: power, armor: 0, reach: 3, ferocity: 5.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

    [Fact]
    public void AttackBuilding_damagesAndEventuallyDestroysTheBuilding()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var factoryHex = FindOpenHex(city, city.CenterHex);
        var factoryId = m.SpawnFactoryForPlayer(1, factoryHex);   // player 1's building

        // a strong attacker, reach 3, so exact adjacency isn't required
        var attackerId = m.SpawnUnit(0, factoryHex.Neighbor(HexEdge.E).Neighbor(HexEdge.E), speed: 3.0, combat: Attacker(power: 500));

        var startHp = m.FindBuilding(factoryId)!.Hp;
        Assert.True(startHp > 0);

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackBuilding, targetEntity: attackerId, argA: (int)factoryId) });
        Assert.Equal(UnitOrderKind.AttackBuilding, m.FindUnit(attackerId)!.Order);

        // Ferocity 5/s -> cooldown 0.2s -> resolves within 2 ticks (10tps);
        // keep ticking well past the building's own max HP worth of hits
        // so this test doesn't depend on tuning the exact hit count.
        var destroyed = false;
        for (var i = 0; i < 2000 && !destroyed; i++)
        {
            m.Tick(null);
            if (m.FindBuilding(factoryId)!.State == BuildingState.Destroyed) destroyed = true;
        }

        Assert.True(destroyed, "the building should have been destroyed well within 2000 ticks at 500 power vs. a Medium-tier Factory");
        Assert.Equal(0, m.FindBuilding(factoryId)!.Hp);
        // docs/23 §2 "Destroyed -- rubble hexes reopen" is pre-existing
        // ApplyBuildingDamage behavior, not new here -- the new surface
        // under test is that combat can actually REACH that method at
        // all now, which `destroyed` above already confirms.
    }

    [Fact]
    public void AttackBuilding_silentNoOp_whenOutOfReach()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var factoryHex = FindOpenHex(city, city.CenterHex);
        var factoryId = m.SpawnFactoryForPlayer(1, factoryHex);

        // SpawnUnit never validates its hex is on-map or open (it's a pure
        // coordinate transform, see MatchState.SpawnUnit's own body) --
        // this test only cares about DISTANCE from the building, so a
        // hex far off in Q/R space is a simpler, more reliable way to
        // guarantee "out of a reach-3 attacker's range" than searching
        // for an actually-open far hex that might not exist on a small
        // preset like Village.
        var farHex = new HexCoord(factoryHex.Q + 30, factoryHex.R + 30);
        Assert.True(farHex.DistanceTo(factoryHex) > 3, "test setup sanity check");
        var attackerId = m.SpawnUnit(0, farHex, speed: 3.0, combat: Attacker(power: 50));

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackBuilding, targetEntity: attackerId, argA: (int)factoryId) });

        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(attackerId)!.Order);
        Assert.Equal(BuildingDef.Get(BuildingKind.Factory).MaxHp, m.FindBuilding(factoryId)!.Hp);
    }

    [Fact]
    public void AttackBuilding_silentNoOp_onAlreadyDestroyedBuilding()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var factoryHex = FindOpenHex(city, city.CenterHex);
        var factoryId = m.SpawnFactoryForPlayer(1, factoryHex);

        // one massive hit to destroy it outright
        m.ApplyBuildingDamage(factoryId, BuildingDef.Get(BuildingKind.Factory).MaxHp + 1000);
        Assert.Equal(BuildingState.Destroyed, m.FindBuilding(factoryId)!.State);

        var attackerId = m.SpawnUnit(0, factoryHex.Neighbor(HexEdge.E), speed: 3.0, combat: Attacker(power: 50));
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackBuilding, targetEntity: attackerId, argA: (int)factoryId) });

        Assert.Equal(UnitOrderKind.Idle, m.FindUnit(attackerId)!.Order);
    }

    [Fact]
    public void AttackBuilding_unitJustWaits_ifBuildingDestroyedMidChannel()
    {
        var city = SmallCity();
        var m = MatchState.Create(4u, TwoPlayers(), city);
        var factoryHex = FindOpenHex(city, city.CenterHex);
        var factoryId = m.SpawnFactoryForPlayer(1, factoryHex);
        var attackerId = m.SpawnUnit(0, factoryHex.Neighbor(HexEdge.E), speed: 3.0, combat: Attacker(power: 50));

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackBuilding, targetEntity: attackerId, argA: (int)factoryId) });
        Assert.Equal(UnitOrderKind.AttackBuilding, m.FindUnit(attackerId)!.Order);

        // destroy the building out from under the mid-channel attacker
        m.ApplyBuildingDamage(factoryId, BuildingDef.Get(BuildingKind.Factory).MaxHp + 1000);
        Assert.Equal(BuildingState.Destroyed, m.FindBuilding(factoryId)!.State);

        // same "target gone -- unit just waits, doesn't auto-idle"
        // contract TickCombat already documents for a dead unit defender --
        // ticking further must not throw, and the order stays put.
        for (var i = 0; i < 20; i++) m.Tick(null);
        Assert.Equal(UnitOrderKind.AttackBuilding, m.FindUnit(attackerId)!.Order);
    }

    [Fact]
    public void AttackBuilding_respectsBuildingsOwnArmor()
    {
        // docs/23 §2's tier table: Landmark (the HQ) has the highest armor
        // (8) of any kind -- verify TickBuildingCombat actually reads
        // BuildingDef.Get(kind).Armor per-kind rather than a flat 0/constant
        // by comparing net damage against a low-armor Small building vs.
        // the high-armor HQ from an IDENTICAL hit.
        var city = SmallCity();
        var mSmall = MatchState.Create(5u, TwoPlayers(), city);
        var smallHex = FindOpenHex(city, city.CenterHex);
        // BloodStorage is Small tier (armor 2)
        mSmall.Player(0).Grant(ResourceKind.Bones, 1000);
        mSmall.Player(0).Grant(ResourceKind.Blood, 1000);
        mSmall.Player(0).Grant(ResourceKind.Parts, 1000);
        mSmall.Tick(new List<Command> { new Command(0, CommandKind.BuildStructure, targetEntity: (uint)BuildingKind.BloodStorage, argA: smallHex.Q, argB: smallHex.R) });
        var smallId = mSmall.BuildingAt(0).EntityId;
        for (var i = 0; i < BuildingDef.Get(BuildingKind.BloodStorage).BuildTimeTicks; i++) mSmall.Tick(null);

        var mHq = MatchState.Create(5u, TwoPlayers(), city);
        var hqHex = FindOpenHex(city, city.CenterHex);
        var hqId = mHq.SpawnHqForPlayer(0, hqHex);

        var attackerSmall = mSmall.SpawnUnit(1, smallHex.Neighbor(HexEdge.E), speed: 3.0, combat: Attacker(power: 20));
        var attackerHq = mHq.SpawnUnit(1, hqHex.Neighbor(HexEdge.E), speed: 3.0, combat: Attacker(power: 20));

        mSmall.Tick(new List<Command> { new Command(1, CommandKind.AttackBuilding, targetEntity: attackerSmall, argA: (int)smallId) });
        mHq.Tick(new List<Command> { new Command(1, CommandKind.AttackBuilding, targetEntity: attackerHq, argA: (int)hqId) });

        var smallDamageTaken = BuildingDef.Get(BuildingKind.BloodStorage).MaxHp - mSmall.FindBuilding(smallId)!.Hp;
        var hqDamageTaken = BuildingDef.Get(BuildingKind.Hq).MaxHp - mHq.FindBuilding(hqId)!.Hp;

        Assert.True(smallDamageTaken > 0);
        Assert.True(hqDamageTaken > 0);
        Assert.True(hqDamageTaken < smallDamageTaken, $"HQ (armor {BuildingDef.Get(BuildingKind.Hq).Armor}) should take LESS net damage from an identical hit than BloodStorage (armor {BuildingDef.Get(BuildingKind.BloodStorage).Armor}) -- got HQ={hqDamageTaken}, Small={smallDamageTaken}");
    }
}
