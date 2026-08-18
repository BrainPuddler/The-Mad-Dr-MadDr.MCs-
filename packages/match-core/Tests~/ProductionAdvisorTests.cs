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

    /// <summary>counts a player's currently-living units directly off
    /// `MatchState`'s public surface -- the same query <see
    /// cref="ProductionAdvisor"/>'s own private `LiveUnitCount` runs
    /// internally, duplicated here (not exposed for testing) since a test
    /// asserting the real invariant needs the real number, not the
    /// permanently-zero <see cref="PlayerState.SupplyUsed"/> the OLD
    /// version of this test used to check (see this method's own call
    /// site for why that was a tautology, not a real assertion).</summary>
    private static int LiveUnitCountFor(MatchState state, int playerIndex)
    {
        var count = 0;
        for (var i = 0; i < state.UnitCount; i++)
        {
            var u = state.UnitAt(i);
            if (u.PlayerIndex == playerIndex && u.IsAlive) count++;
        }
        return count;
    }

    [Fact]
    public void NeverPushesLiveUnitCountPastSupplyCap()
    {
        // 2026-08: this used to assert player.SupplyUsed <= SupplyCap --
        // a tautology, since nothing in match-core ever calls
        // PlayerState.AddSupplyUsed outside its own test file, so
        // SupplyUsed sits at 0 forever and the assertion could never
        // fail regardless of what the advisor actually did. Replaced
        // with the REAL live count ProductionAdvisor's own gate now
        // reads (see its DecideCommands' own doc comment).
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
            var liveCount = LiveUnitCountFor(m, 1);
            Assert.True(liveCount <= player.SupplyCap, $"frame {frame}: live unit count {liveCount} > SupplyCap {player.SupplyCap}");
        }
    }

    [Fact]
    public void TargetsALargerArmyWhenTheHumanPlayerAlreadyHasMoreUnits()
    {
        // 2026-08 (creator direction: "the roster needs to be able to
        // generate enemies for all races. They should take the number
        // of units from the player, so armies are fairly balanced
        // amongst all ai units and players"): the core new behavior --
        // an AI facing a human with a real standing army should train up
        // toward that army's size, not just a fixed fraction of its own
        // SupplyCap. Same personality/budget/seed/frame-count both runs,
        // the only difference is whether player 0 (human, AlienHive)
        // already has a pile of Drones fielded before the advisor gets
        // to decide anything.
        int RunAndCountAiUnits(bool seedHumanArmy)
        {
            var city = SmallCity();
            var players = new List<PlayerSetup>
            {
                PlayerSetup.Human(FactionId.AlienHive),
                PlayerSetup.Ai(FactionId.HumanArmy, CommanderPersonality.Warlord()),
            };
            var m = MatchState.Create(6u, players, city);
            var hqHex = FindOpenHex(city, city.CenterHex);
            m.SpawnHqForPlayer(1, hqHex);
            var factoryHex = FindOpenHex(city, hqHex);
            m.SpawnFactoryForPlayer(1, factoryHex);

            if (seedHumanArmy)
            {
                // 55 -- comfortably above Warlord's cap-based floor (60
                // SupplyCap * (0.4 + 0.65 aggression * 0.5) = ~43) so the
                // balance target actually becomes the binding constraint,
                // not just noise under the pre-existing floor.
                var humanHex = FindOpenHex(city, city.CenterHex);
                for (var i = 0; i < 55; i++) m.SpawnRosterUnit(0, humanHex, RosterUnitKind.Drone);
            }

            var advisor = new ProductionAdvisor(1, CommanderPersonality.Warlord(), 6u);
            var player = m.Player(1);
            player.Grant(ResourceKind.Bones, 100000);
            player.Grant(ResourceKind.Blood, 100000);
            player.Grant(ResourceKind.Fuel, 100000);
            player.Grant(ResourceKind.Parts, 100000);

            for (var frame = 0; frame < 4000; frame++)
            {
                var commands = advisor.DecideCommands(m);
                m.Tick(commands.Count > 0 ? commands : null);
            }
            return LiveUnitCountFor(m, 1);
        }

        var withoutHumanArmy = RunAndCountAiUnits(false);
        var withHumanArmy = RunAndCountAiUnits(true);
        Assert.True(withHumanArmy > withoutHumanArmy,
            $"expected the AI to field MORE units facing a human with a real standing army (got {withHumanArmy} vs {withoutHumanArmy})");
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
    public void MadDoctorAdvisorNeverThrows_evenUnfunded()
    {
        // Funded with the WRONG currencies (Brains/Parts) -- MadDoctor's
        // real roster (2026-08) costs Blood+Bones, so this AI still can't
        // afford anything, but it must fail soft (never train) rather
        // than throw, same contract as any other underfunded AI player.
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
        Assert.Equal(0, m.UnitCount); // wrong currencies funded -- correctly never fields anything, never throws
    }

    [Fact]
    public void MadDoctorAdvisorFieldsRealUnitsWhenProperlyFunded()
    {
        // 2026-08 follow-up (creator direction: "the roster needs to be
        // able to generate enemies for all races"): the actual new
        // capability -- funded with the CORRECT currencies this time
        // (Blood+Bones, see FactionRoster.cs's own MadDoctor entries),
        // a MadDoctor AI now really trains and fields units, where it
        // used to be structurally incapable of ever doing so.
        var city = SmallCity();
        var players = new List<PlayerSetup>
        {
            PlayerSetup.Human(FactionId.HumanArmy),
            PlayerSetup.Ai(FactionId.MadDoctor, CommanderPersonality.Warlord()),
        };
        var m = MatchState.Create(5u, players, city);
        var hqHex = FindOpenHex(city, city.CenterHex);
        m.SpawnHqForPlayer(1, hqHex);
        var factoryHex = FindOpenHex(city, hqHex);
        m.SpawnFactoryForPlayer(1, factoryHex);
        m.Player(1).Grant(ResourceKind.Blood, 10000);
        m.Player(1).Grant(ResourceKind.Bones, 10000);

        var advisor = new ProductionAdvisor(1, CommanderPersonality.Warlord(), 5u);
        for (var frame = 0; frame < 3000; frame++)
        {
            var commands = advisor.DecideCommands(m);
            m.Tick(commands.Count > 0 ? commands : null);
        }
        Assert.True(m.UnitCount > 0, "a funded MadDoctor advisor should field real units now that a roster exists for it");
        for (var i = 0; i < m.UnitCount; i++)
            if (m.UnitAt(i).PlayerIndex == 1)
                Assert.Equal(FactionId.MadDoctor, UnitRosterDef.Get(m.UnitAt(i).SourceRosterKind!.Value).Faction);
    }
}
