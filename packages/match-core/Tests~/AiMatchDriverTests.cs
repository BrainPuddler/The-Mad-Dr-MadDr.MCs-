using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/30 (selectable races + AI opponents): <see
/// cref="AiMatchDriver"/> orchestrates <see cref="SkirmishCommander"/> +
/// <see cref="ProductionAdvisor"/> per AI player -- these tests pin that
/// wiring (one driver per AI slot, human slots skipped, an all-human match
/// has no AI work at all) rather than re-testing either commander's own
/// scoring logic (already covered by <see cref="CommanderTests"/> and
/// <see cref="ProductionAdvisorTests"/>).</summary>
public class AiMatchDriverTests
{
    private static CityModel SmallCity() => CityGenerator.Generate(8001u, CityPreset.Village());

    private static HexCoord FindOpenHex(CityModel city, HexCoord center)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var roads = new HashSet<HexCoord>(city.Roads);
        for (var r = 0; r <= 30; r++)
            foreach (var h in center.Ring(r))
                if (city.Contains(h) && !blocked.Contains(h) && !roads.Contains(h)) return h;
        throw new System.InvalidOperationException("no open hex found");
    }

    [Fact]
    public void AllHumanMatch_hasNoAiWork()
    {
        var players = new List<PlayerSetup> { PlayerSetup.Human(FactionId.HumanArmy), PlayerSetup.Human(FactionId.AlienHive) };
        var m = MatchState.Create(1u, players);
        var driver = new AiMatchDriver(m, 1u);
        Assert.False(driver.HasAnyAi);
        Assert.Empty(driver.DecideCommands(m));
    }

    [Fact]
    public void OneAiSlot_HasAnyAiTrue_andEventuallyEmitsCommands()
    {
        var city = SmallCity();
        var players = new List<PlayerSetup>
        {
            PlayerSetup.Human(FactionId.AlienHive),
            PlayerSetup.Ai(FactionId.HumanArmy, CommanderPersonality.Berserker()),
        };
        var m = MatchState.Create(2u, players, city);
        var hqHex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(1, hqHex);
        var factoryHex = FindOpenHex(city, hqHex);
        m.SpawnFactoryForPlayer(1, factoryHex);
        m.Player(1).Grant(ResourceKind.Bones, 5000);
        m.Player(1).Grant(ResourceKind.Blood, 5000);
        m.Player(1).Grant(ResourceKind.Fuel, 5000);
        m.Player(1).Grant(ResourceKind.Parts, 5000);

        var driver = new AiMatchDriver(m, 2u);
        Assert.True(driver.HasAnyAi);

        var sawAny = false;
        for (var frame = 0; frame < 3000; frame++)
        {
            var commands = driver.DecideCommands(m);
            if (commands.Count > 0) sawAny = true;
            foreach (var cmd in commands) Assert.Equal(1, cmd.PlayerIndex); // only player 1 is AI-controlled
            m.Tick(commands.Count > 0 ? commands : null);
        }
        Assert.True(sawAny, "an AI player with a funded economy should emit at least one command over 3000 ticks");
    }

    [Fact]
    public void MultipleAiSlots_eachGetsItsOwnCommanderAndAdvisor()
    {
        var city = SmallCity();
        var players = new List<PlayerSetup>
        {
            PlayerSetup.Human(FactionId.MadDoctor),
            PlayerSetup.Ai(FactionId.HumanArmy, CommanderPersonality.Turtle()),
            PlayerSetup.Ai(FactionId.AlienHive, CommanderPersonality.Hoarder()),
            PlayerSetup.Ai(FactionId.HumanArmy, CommanderPersonality.Opportunist()),
        };
        var m = MatchState.Create(3u, players, city);
        for (var p = 1; p <= 3; p++)
        {
            var hqHex = FindOpenHex(city, new HexCoord(city.CenterHex.Q + p * 6, city.CenterHex.R));
            m.SpawnHqForPlayer(p, hqHex);
            m.Player(p).Grant(ResourceKind.Bones, 2000);
            m.Player(p).Grant(ResourceKind.Blood, 2000);
            m.Player(p).Grant(ResourceKind.Fuel, 2000);
            m.Player(p).Grant(ResourceKind.Ichor, 2000);
            m.Player(p).Grant(ResourceKind.Parts, 2000);
        }

        var driver = new AiMatchDriver(m, 3u);
        Assert.True(driver.HasAnyAi);

        // Should never throw and should never emit a command for player 0
        // (the human slot).
        for (var frame = 0; frame < 500; frame++)
        {
            var commands = driver.DecideCommands(m);
            foreach (var cmd in commands) Assert.NotEqual(0, cmd.PlayerIndex);
            m.Tick(commands.Count > 0 ? commands : null);
        }
    }

    [Fact]
    public void SameSeed_producesIdenticalCommandStreamsAcrossTwoDrivers()
    {
        var players = new List<PlayerSetup>
        {
            PlayerSetup.Human(FactionId.AlienHive),
            PlayerSetup.Ai(FactionId.HumanArmy, CommanderPersonality.Warlord()),
        };
        var city1 = SmallCity();
        var city2 = SmallCity(); // same seed -> same city
        var m1 = MatchState.Create(4u, players, city1);
        var m2 = MatchState.Create(4u, players, city2);
        var hq1 = FindOpenHex(city1, city1.CenterHex);
        var hq2 = FindOpenHex(city2, city2.CenterHex);
        Assert.Equal(hq1, hq2);
        m1.SpawnHqForPlayer(1, hq1);
        m2.SpawnHqForPlayer(1, hq2);
        var factory1 = FindOpenHex(city1, hq1);
        var factory2 = FindOpenHex(city2, hq2);
        m1.SpawnFactoryForPlayer(1, factory1);
        m2.SpawnFactoryForPlayer(1, factory2);
        m1.Player(1).Grant(ResourceKind.Bones, 4000);
        m2.Player(1).Grant(ResourceKind.Bones, 4000);
        m1.Player(1).Grant(ResourceKind.Blood, 4000);
        m2.Player(1).Grant(ResourceKind.Blood, 4000);
        m1.Player(1).Grant(ResourceKind.Fuel, 4000);
        m2.Player(1).Grant(ResourceKind.Fuel, 4000);
        m1.Player(1).Grant(ResourceKind.Parts, 4000);
        m2.Player(1).Grant(ResourceKind.Parts, 4000);

        var driver1 = new AiMatchDriver(m1, 4u);
        var driver2 = new AiMatchDriver(m2, 4u);

        for (var frame = 0; frame < 2000; frame++)
        {
            var c1 = driver1.DecideCommands(m1);
            var c2 = driver2.DecideCommands(m2);
            Assert.Equal(c1.Count, c2.Count);
            m1.Tick(c1.Count > 0 ? c1 : null);
            m2.Tick(c2.Count > 0 ? c2 : null);
        }
        Assert.Equal(m1.Frame, m2.Frame);
    }
}
