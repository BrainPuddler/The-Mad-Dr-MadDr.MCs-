using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/30 (selectable races + AI opponents): <see
/// cref="ProductionAdvisor"/>'s own acceptance -- affordability respected,
/// supply never over-claimed, and same-seed determinism (the property a
/// replay depends on).</summary>
public class ProductionAdvisorTests
{
    private static CityModel SmallCity() => CityGenerator.Generate(7001u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var roads = new HashSet<HexCoord>(city.Roads);
        for (var r = 0; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new System.InvalidOperationException("no open hex found");
    }

    private static MatchState AiHumanArmyMatch(uint seed, out ProductionAdvisor advisor)
    {
        var city = SmallCity();
        var players = new List<PlayerSetup>
        {
            PlayerSetup.Human(FactionId.AlienHive),
            PlayerSetup.Ai(FactionId.HumanArmy, CommanderPersonality.Warlord()),
        };
        var m = MatchState.Create(seed, players, city);
        var hqHex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(1, hqHex);
        var factoryHex = FindOpenHex(city, hqHex);
        m.SpawnFactoryForPlayer(1, factoryHex);
        advisor = new ProductionAdvisor(1, CommanderPersonality.Warlord(), seed);
        return m;
    }

    [Fact]
    public void NeverEmitsATrainCommandTheWalletCannotAfford()
    {
        var m = AiHumanArmyMatch(1u, out var advisor);
        // deliberately no Grant -- wallet stays at 0
        for (var frame = 0; frame < 2000; frame++)
        {
            var commands = advisor.DecideCommands(m);
            foreach (var cmd in commands)
                Assert.NotEqual(CommandKind.TrainUnit, cmd.Kind); // nothing affordable, ever
            m.Tick(commands.Count > 0 ? commands : null);
        }
    }

    [Fact]
    public void SpendsWalletDownAndFieldsUnitsWhenAffordable()
    {
        var m = AiHumanArmyMatch(2u, out var advisor);
        var player = m.Player(1);
        player.Grant(ResourceKind.Bones, 5000);
        player.Grant(ResourceKind.Blood, 5000);
        player.Grant(ResourceKind.Fuel, 5000);
        player.Grant(ResourceKind.Parts, 5000);
        var startingWallet = player.Wallet(ResourceKind.Bones);

        for (var frame = 0; frame < 3000; frame++)
        {
            var commands = advisor.DecideCommands(m);
            m.Tick(commands.Count > 0 ? commands : null);
        }

        Assert.True(player.Wallet(ResourceKind.Bones) < startingWallet, "advisor should have spent Bones on training/expansion");
        Assert.True(m.UnitCount > 0, "advisor should have fielded at least one unit given a generous wallet");
    }

    [Fact]
    public void NeverPushesSupplyUsedPastCap()
    {
        var m = AiHumanArmyMatch(3u, out var advisor);
        var player = m.Player(1);
        player.Grant(ResourceKind.Bones, 100000);
        player.Grant(ResourceKind.Blood, 100000);
        player.Grant(ResourceKind.Fuel, 100000);
        player.Grant(ResourceKind.Parts, 100000);

        for (var frame = 0; frame < 6000; frame++)
        {
            var commands = advisor.DecideCommands(m);
            m.Tick(commands.Count > 0 ? commands : null);
            Assert.True(player.SupplyUsed <= player.SupplyCap, $"frame {frame}: SupplyUsed {player.SupplyUsed} > SupplyCap {player.SupplyCap}");
        }
    }

    [Fact]
    public void SameSeedProducesIdenticalCommandStream()
    {
        var m1 = AiHumanArmyMatch(4u, out var advisor1);
        var m2 = AiHumanArmyMatch(4u, out var advisor2);
        m1.Player(1).Grant(ResourceKind.Bones, 3000);
        m1.Player(1).Grant(ResourceKind.Blood, 3000);
        m1.Player(1).Grant(ResourceKind.Fuel, 3000);
        m1.Player(1).Grant(ResourceKind.Parts, 3000);
        m2.Player(1).Grant(ResourceKind.Bones, 3000);
        m2.Player(1).Grant(ResourceKind.Blood, 3000);
        m2.Player(1).Grant(ResourceKind.Fuel, 3000);
        m2.Player(1).Grant(ResourceKind.Parts, 3000);

        for (var frame = 0; frame < 2500; frame++)
        {
            var c1 = advisor1.DecideCommands(m1);
            var c2 = advisor2.DecideCommands(m2);
            Assert.Equal(c1.Count, c2.Count);
            for (var i = 0; i < c1.Count; i++)
            {
                Assert.Equal(c1[i].Kind, c2[i].Kind);
                Assert.Equal(c1[i].TargetEntity, c2[i].TargetEntity);
                Assert.Equal(c1[i].ArgA, c2[i].ArgA);
                Assert.Equal(c1[i].ArgB, c2[i].ArgB);
            }
            m1.Tick(c1.Count > 0 ? c1 : null);
            m2.Tick(c2.Count > 0 ? c2 : null);
        }
    }

    [Fact]
    public void MadDoctorAdvisorNeverThrows_justFieldsNoUnits()
    {
        // ArmyGenerator has no roster for MadDoctor -- ProductionAdvisor
        // must catch that, not propagate it, and simply never train.
        var city = SmallCity();
        var players = new List<PlayerSetup>
        {
            PlayerSetup.Human(FactionId.HumanArmy),
            PlayerSetup.Ai(FactionId.MadDoctor, CommanderPersonality.Balanced()),
        };
        var m = MatchState.Create(5u, players, city);
        var hqHex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(1, hqHex);
        var factoryHex = FindOpenHex(city, hqHex);
        m.SpawnFactoryForPlayer(1, factoryHex);
        m.Player(1).Grant(ResourceKind.Brains, 10000);
        m.Player(1).Grant(ResourceKind.Parts, 10000);

        var advisor = new ProductionAdvisor(1, CommanderPersonality.Balanced(), 5u);
        for (var frame = 0; frame < 500; frame++)
        {
            var commands = advisor.DecideCommands(m);
            m.Tick(commands.Count > 0 ? commands : null);
        }
        Assert.Equal(0, m.UnitCount); // no roster to train from -- correctly never fields anything, never throws
    }
}
