using System;
using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using Xunit;

namespace MadDr.MatchCore.Tests;

/// <summary>docs/23 §6 / §13 amendment D Phase 6c: the utility-driven
/// skirmish commander, its dial-able personality, and the procedural
/// generator for it. The headline behavioural claim -- "personality
/// actually changes what the commander does, not just what it logs" -- is
/// pinned by `Personality_decides_the_action`: identical board, two
/// different commanders, two different orders.
///
/// Build-order scripting is NOT covered: no unit-production command
/// exists in match-core at all (see SkirmishCommander.cs's own header),
/// so there is nothing to test yet.</summary>
public class CommanderTests
{
    private static List<FactionId> TwoPlayers() => new() { FactionId.MadDoctor, FactionId.HumanArmy };

    private static CityModel SmallCity() => CityGenerator.Generate(7070u, CityPreset.Village());

    private static CombatStats Slayer() => new CombatStats(maxVitality: 1000, power: 500, armor: 0, reach: 1, ferocity: 100.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
    private static CombatStats Frail() => new CombatStats(maxVitality: 1, power: 0, armor: 0, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);
    private static CombatStats Fighter() => new CombatStats(maxVitality: 200, power: 20, armor: 2, reach: 1, ferocity: 1.0, cunningPercent: 0, affinity: UnitAffinity.Neutral);

    private static List<HexCoord> OpenNeighbors(CityModel city, HexCoord from, int want)
    {
        var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
        var found = new List<HexCoord>();
        foreach (var n in from.Neighbors())
        {
            if (city.Contains(n) && !blocked.Contains(n)) found.Add(n);
            if (found.Count >= want) break;
        }
        if (found.Count < want) throw new InvalidOperationException("city too dense for this test");
        return found;
    }

    // =====================================================================
    // CommanderPersonality: dialing it in
    // =====================================================================

    [Fact]
    public void Traits_outside_zero_to_one_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CommanderPersonality(1.5, 0.5, 0.5, 0.5, 0.5, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommanderPersonality.Balanced().With(CommanderTrait.Greed, -0.1));
    }

    [Fact]
    public void With_changes_exactly_one_axis_and_leaves_the_rest_alone()
    {
        var baseline = CommanderPersonality.Turtle();
        var tweaked = baseline.With(CommanderTrait.Greed, 0.9);

        Assert.Equal(0.9, tweaked.Greed);
        foreach (CommanderTrait t in Enum.GetValues(typeof(CommanderTrait)))
        {
            if (t == CommanderTrait.Greed) continue;
            Assert.Equal(baseline.Trait(t), tweaked.Trait(t));
        }
    }

    [Fact]
    public void A_default_personality_reads_as_balanced_rather_than_throwing()
    {
        // default(struct) leaves the backing array null -- a caller that
        // forgot to assign one should get a bland commander, not a
        // NullReferenceException mid-match.
        var unset = default(CommanderPersonality);
        foreach (CommanderTrait t in Enum.GetValues(typeof(CommanderTrait)))
            Assert.Equal(0.5, unset.Trait(t));
    }

    [Fact]
    public void Every_dial_in_archetype_is_actually_distinct()
    {
        var seen = new HashSet<string>();
        foreach (var a in CommanderPersonality.Archetypes)
            Assert.True(seen.Add(a.ToString()), $"duplicate archetype: {a}");
        Assert.Equal(6, seen.Count);
    }

    [Theory]
    [InlineData(0.0, SkirmishCommander.MinDecisionIntervalTicks)]
    [InlineData(1.0, SkirmishCommander.MaxDecisionIntervalTicks)]
    public void Discipline_sets_the_decision_cadence(double discipline, int expectedInterval)
    {
        var p = CommanderPersonality.Balanced().With(CommanderTrait.Discipline, discipline);
        Assert.Equal(expectedInterval, new SkirmishCommander(0, p).DecisionIntervalTicks);
    }

    // 2026-08 (creator direction: "scale the ai intelligence for
    // Difficulty"): Difficulty is a second, ORTHOGONAL axis on top of
    // Discipline -- these pin that Normal reproduces the pre-2026-08
    // behavior exactly, and that the reaction-speed ordering across every
    // level matches AiDifficulty's own low-to-high intent (Tutorial
    // slowest, Brutal fastest), independent of whatever Discipline says.

    [Fact]
    public void Normal_difficulty_reproduces_the_discipline_only_interval_exactly()
    {
        var p = CommanderPersonality.Balanced().With(CommanderTrait.Discipline, 0.7);
        var withoutDifficulty = new SkirmishCommander(0, p).DecisionIntervalTicks;
        var withNormal = new SkirmishCommander(0, p, AiDifficulty.Normal).DecisionIntervalTicks;
        Assert.Equal(withoutDifficulty, withNormal);
    }

    [Fact]
    public void Higher_difficulty_reacts_no_slower_than_a_lower_one_for_the_same_personality()
    {
        var p = CommanderPersonality.Balanced();
        var levels = new[] { AiDifficulty.Tutorial, AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard, AiDifficulty.Brutal };
        var intervals = new int[levels.Length];
        for (var i = 0; i < levels.Length; i++)
            intervals[i] = new SkirmishCommander(0, p, levels[i]).DecisionIntervalTicks;

        for (var i = 1; i < intervals.Length; i++)
            Assert.True(intervals[i] <= intervals[i - 1],
                $"{levels[i]} ({intervals[i]} ticks) should react at least as fast as {levels[i - 1]} ({intervals[i - 1]} ticks)");
        // Not just non-increasing -- Tutorial and Brutal must be genuinely
        // different, not merely tied at the floor.
        Assert.True(intervals[0] > intervals[intervals.Length - 1]);
    }

    [Fact]
    public void Tutorial_difficulty_never_reacts_faster_than_MinDecisionIntervalTicks()
    {
        // The floor-only clamp (see SkirmishCommander's own doc comment)
        // still has to hold at the SLOW end for every OTHER level, and at
        // the fast end for Brutal -- this just confirms nothing produces
        // a zero/negative interval regardless of how extreme the
        // multiplier gets.
        foreach (AiDifficulty level in Enum.GetValues(typeof(AiDifficulty)))
        {
            var interval = new SkirmishCommander(0, CommanderPersonality.Balanced(), level).DecisionIntervalTicks;
            Assert.True(interval >= SkirmishCommander.MinDecisionIntervalTicks, $"{level}: interval {interval} below the floor");
        }
    }

    // =====================================================================
    // CommanderPersonality: procedural generation
    // =====================================================================

    [Fact]
    public void The_same_seed_always_generates_the_same_commander()
    {
        for (var seed = 1u; seed <= 20u; seed++)
            Assert.Equal(CommanderPersonality.Generate(seed).ToString(),
                         CommanderPersonality.Generate(seed).ToString());
    }

    [Fact]
    public void Every_generated_commander_has_a_signature_axis_and_a_suppressed_opposite()
    {
        // The whole point of biasing generation (see CommanderPersonality's
        // header): an unbiased six-way roll produces indistinguishable
        // ~0.5 commanders. Every roll must land at least one tension pair
        // clearly apart.
        for (var seed = 1u; seed <= 200u; seed++)
        {
            var p = CommanderPersonality.Generate(seed);
            var hasSignature = false;
            foreach (var (a, b) in CommanderPersonality.TensionPairs)
            {
                var highA = p.Trait(a) >= CommanderPersonality.SignatureFloor && p.Trait(b) <= CommanderPersonality.OppositeCeiling;
                var highB = p.Trait(b) >= CommanderPersonality.SignatureFloor && p.Trait(a) <= CommanderPersonality.OppositeCeiling;
                if (highA || highB) { hasSignature = true; break; }
            }
            Assert.True(hasSignature, $"seed {seed} generated a personality with no signature: {p}");
        }
    }

    [Fact]
    public void No_generated_commander_is_self_contradicting()
    {
        // Regression: the first draft only decorrelated the SIGNATURE
        // pair and let the other two roll independently, which really did
        // produce commanders scoring maximum Aggression AND maximum
        // Caution at once (seed 7 was the culprit) -- a commander whose
        // charge and retreat utilities cancel into dithering.
        for (var seed = 1u; seed <= 500u; seed++)
        {
            var p = CommanderPersonality.Generate(seed);
            Assert.True(p.IsCoherent, $"seed {seed} generated a self-contradicting personality: {p}");
        }
    }

    [Fact]
    public void A_featureless_personality_is_not_mislabelled_as_a_character()
    {
        // Regression: Balanced() reported as "Reckless" purely because
        // Aggression sorts first among six identical 0.5s.
        Assert.Equal("Nondescript", CommanderPersonality.Balanced().Label);
        Assert.NotEqual("Nondescript", CommanderPersonality.Berserker().Label);
    }

    [Fact]
    public void Generation_does_not_systematically_favour_one_identity()
    {
        // Regression: always damping the second member of each
        // non-signature pair quietly biased every commander away from
        // Caution/Territoriality/Discipline, so the seed gallery came out
        // overwhelmingly "Grasping". No identity should run away with it.
        var counts = new Dictionary<CommanderTrait, int>();
        foreach (CommanderTrait t in Enum.GetValues(typeof(CommanderTrait))) counts[t] = 0;

        const int samples = 600;
        for (var seed = 1u; seed <= samples; seed++)
            counts[CommanderPersonality.Generate(seed).DominantTrait]++;

        // Perfectly even would be 1/6 (100). Allow a wide band -- this is
        // a "no runaway" check, not a uniformity claim.
        foreach (var kv in counts)
            Assert.InRange(kv.Value, samples / 20, samples / 2);
    }

    [Fact]
    public void Generation_produces_real_variety_across_seeds()
    {
        var distinct = new HashSet<string>();
        var dominants = new HashSet<CommanderTrait>();
        for (var seed = 1u; seed <= 100u; seed++)
        {
            var p = CommanderPersonality.Generate(seed);
            distinct.Add(p.ToString());
            dominants.Add(p.DominantTrait);
        }
        // No two seeds collapsing to the same commander, and the signature
        // machinery reaching every axis rather than favouring one corner.
        Assert.Equal(100, distinct.Count);
        Assert.Equal(CommanderPersonality.TraitCount, dominants.Count);
    }

    [Fact]
    public void Generating_a_squad_of_commanders_off_one_stream_is_reproducible()
    {
        // Fixed draw count per Generate() -- so pulling N commanders from a
        // shared stream lands each one in the same position every run.
        List<string> Roll()
        {
            var rng = new SimRng(4242u);
            var all = new List<string>();
            for (var i = 0; i < 8; i++) all.Add(CommanderPersonality.Generate(rng).ToString());
            return all;
        }
        Assert.Equal(Roll(), Roll());
    }

    [Fact]
    public void A_generated_commander_carries_a_readable_label()
    {
        for (var seed = 1u; seed <= 30u; seed++)
            Assert.False(string.IsNullOrWhiteSpace(CommanderPersonality.Generate(seed).Label));
    }

    // =====================================================================
    // ThreatMap
    // =====================================================================

    [Fact]
    public void An_empty_field_has_no_threat_anywhere()
    {
        var city = SmallCity();
        var m = MatchState.Create(1u, TwoPlayers(), city);
        var threat = ThreatMap.From(m, 0);

        Assert.Equal(0, threat.SourceCount);
        Assert.Equal(0.0, threat.ThreatAt(0.0, 0.0));
        Assert.Equal(0.0, threat.NormalizedThreatAt(0.0, 0.0));
    }

    [Fact]
    public void Only_living_enemies_count_as_threat()
    {
        var city = SmallCity();
        var m = MatchState.Create(2u, TwoPlayers(), city);
        var c = city.CenterHex;
        var n = OpenNeighbors(city, c, 1)[0];

        m.SpawnUnit(0, c, speed: 3.0, combat: Fighter());          // mine -- not a threat to me
        var killerId = m.SpawnUnit(0, n, speed: 3.0, combat: Slayer());
        var victimId = m.SpawnUnit(1, n, speed: 3.0, combat: Frail());

        Assert.Equal(1, ThreatMap.From(m, 0).SourceCount);   // the live enemy

        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, killerId, unchecked((int)victimId)) });
        Assert.False(m.FindUnit(victimId)!.IsAlive);

        Assert.Equal(0, ThreatMap.From(m, 0).SourceCount);   // a corpse threatens nobody
    }

    [Fact]
    public void Threat_falls_off_with_distance_and_reaches_exactly_zero_at_the_radius()
    {
        var city = SmallCity();
        var m = MatchState.Create(3u, TwoPlayers(), city);
        var c = city.CenterHex;
        m.SpawnUnit(1, c, speed: 3.0, combat: Fighter());

        var threat = ThreatMap.From(m, 0);
        var (ex, ez) = c.ToWorld();

        var atSource = threat.ThreatAt(ex, ez);
        var nearby = threat.ThreatAt(ex + ThreatMap.ThreatRadiusMeters / 2.0, ez);
        var atEdge = threat.ThreatAt(ex + ThreatMap.ThreatRadiusMeters, ez);

        Assert.True(atSource > nearby, "threat must decrease with distance");
        Assert.True(nearby > 0.0);
        Assert.Equal(0.0, atEdge);
        Assert.InRange(threat.NormalizedThreatAt(ex, ez), 0.0, 1.0);
    }

    // =====================================================================
    // SkirmishCommander
    // =====================================================================

    /// <summary>Board: one of MY units standing on a lootable corpse, with
    /// a live enemy directly adjacent. Both a fight and a payday are in
    /// range, so which one gets chosen is decided purely by
    /// personality.</summary>
    private static (MatchState m, uint deciderId, uint enemyId, uint corpseId) FightOrLootBoard(uint seed)
    {
        var city = SmallCity();
        var m = MatchState.Create(seed, TwoPlayers(), city);
        var c = city.CenterHex;
        var ring = OpenNeighbors(city, c, 2);

        var deciderId = m.SpawnUnit(0, c, speed: 3.0, combat: Fighter());
        var corpseId = m.SpawnUnit(1, c, speed: 3.0, combat: Frail(), salvageValue: 100);
        var killerId = m.SpawnUnit(0, ring[0], speed: 3.0, combat: Slayer());
        var enemyId = m.SpawnUnit(1, ring[1], speed: 3.0, combat: Fighter());

        // One tick: the killer drops the frail unit, leaving a fresh
        // corpse under the decider's feet. Combat resolves before the
        // separation pass, and corpses are inert (Phase 6a), so nothing
        // drifts off its hex afterwards.
        m.Tick(new List<Command> { new Command(0, CommandKind.AttackUnit, killerId, unchecked((int)corpseId)) });

        var corpse = m.FindUnit(corpseId)!;
        Assert.False(corpse.IsAlive);
        Assert.True(corpse.SalvageRemaining > 0);
        Assert.True(m.FindUnit(enemyId)!.IsAlive);
        return (m, deciderId, enemyId, corpseId);
    }

    private static CommanderDecision DecisionFor(MatchState m, SkirmishCommander cmd, uint unitId)
    {
        // Advance to this commander's own next thinking frame -- its
        // cadence is personality-derived, so different commanders wake on
        // different frames.
        for (var guard = 0; guard < 64 && !cmd.IsDecisionFrame(m.Frame); guard++) m.Tick(null);
        Assert.True(cmd.IsDecisionFrame(m.Frame), "never reached a decision frame");

        foreach (var d in cmd.Decide(m))
            if (d.UnitId == unitId) return d;
        throw new InvalidOperationException($"commander issued no order for unit {unitId}");
    }

    [Fact]
    public void Personality_decides_the_action_on_an_identical_board()
    {
        // THE headline claim of this phase: same board, same rules, same
        // scoring code -- only the weights differ.
        var (aggressiveBoard, aggDecider, _, _) = FightOrLootBoard(10u);
        var berserker = new SkirmishCommander(0, CommanderPersonality.Berserker());
        Assert.Equal(CommanderAction.Attack, DecisionFor(aggressiveBoard, berserker, aggDecider).Action);

        var (greedyBoard, greedyDecider, _, _) = FightOrLootBoard(10u);
        var hoarder = new SkirmishCommander(0, CommanderPersonality.Hoarder());
        Assert.Equal(CommanderAction.Salvage, DecisionFor(greedyBoard, hoarder, greedyDecider).Action);
    }

    [Fact]
    public void An_issued_order_is_the_real_command_the_sim_accepts()
    {
        var (m, deciderId, enemyId, _) = FightOrLootBoard(11u);
        var berserker = new SkirmishCommander(0, CommanderPersonality.Berserker());
        var decision = DecisionFor(m, berserker, deciderId);

        Assert.Equal(CommandKind.AttackUnit, decision.Command.Kind);
        Assert.Equal(deciderId, decision.Command.TargetEntity);
        Assert.Equal(unchecked((int)enemyId), decision.Command.ArgA);

        m.Tick(new List<Command> { decision.Command });
        Assert.Equal(UnitOrderKind.AttackUnit, m.FindUnit(deciderId)!.Order);
        Assert.Equal(enemyId, m.FindUnit(deciderId)!.AttackTargetId);
    }

    [Fact]
    public void A_salvage_channel_in_progress_is_never_restarted()
    {
        // The trap this shape walks into: a low-discipline commander
        // re-decides every couple of ticks, re-issues SalvageCorpse, and
        // resets the 3-second channel forever -- collecting nothing, all
        // match. Guarded explicitly in Decide().
        var (m, _, _, corpseId) = FightOrLootBoard(12u);
        var twitchy = new SkirmishCommander(0,
            CommanderPersonality.Hoarder().With(CommanderTrait.Discipline, 0.0));
        Assert.Equal(SkirmishCommander.MinDecisionIntervalTicks, twitchy.DecisionIntervalTicks);

        var partsBefore = m.Player(0).Wallet(ResourceKind.Parts);
        var expected = m.FindUnit(corpseId)!.SalvageRemaining;

        // Drive the commander for real: every tick, hand it the state and
        // feed back whatever it wants to do.
        var completed = false;
        for (var i = 0; i < 120 && !completed; i++)
        {
            m.Tick(twitchy.DecideCommands(m));
            if (m.Player(0).Wallet(ResourceKind.Parts) > partsBefore) completed = true;
        }

        Assert.True(completed, "a greedy commander must actually finish a harvest, not restart it forever");
        Assert.Equal(partsBefore + expected, m.Player(0).Wallet(ResourceKind.Parts));
        Assert.Equal(0, m.FindUnit(corpseId)!.SalvageRemaining);
    }

    [Fact]
    public void A_commander_never_issues_orders_for_units_it_does_not_own()
    {
        var (m, _, _, _) = FightOrLootBoard(13u);
        var cmd = new SkirmishCommander(1, CommanderPersonality.Berserker());
        for (var guard = 0; guard < 64 && !cmd.IsDecisionFrame(m.Frame); guard++) m.Tick(null);

        foreach (var d in cmd.Decide(m))
        {
            Assert.Equal(1, m.FindUnit(d.UnitId)!.PlayerIndex);
            Assert.Equal(1, d.Command.PlayerIndex);
        }
    }

    [Fact]
    public void A_commander_thinks_only_on_its_own_cadence()
    {
        var city = SmallCity();
        var m = MatchState.Create(14u, TwoPlayers(), city);
        var c = city.CenterHex;
        var n = OpenNeighbors(city, c, 1)[0];
        m.SpawnUnit(0, c, speed: 3.0, combat: Fighter());
        m.SpawnUnit(1, n, speed: 3.0, combat: Fighter());

        var methodical = new SkirmishCommander(0,
            CommanderPersonality.Berserker().With(CommanderTrait.Discipline, 1.0));
        Assert.Equal(SkirmishCommander.MaxDecisionIntervalTicks, methodical.DecisionIntervalTicks);

        var thinkingFrames = 0;
        for (var i = 0; i < SkirmishCommander.MaxDecisionIntervalTicks * 3; i++)
        {
            if (methodical.Decide(m).Count > 0) thinkingFrames++;
            m.Tick(null);
        }
        // Frames 0, 20, 40 within the 60-tick window -- and the unit has
        // already been ordered to attack by then, so re-issues are
        // suppressed; the ceiling is what matters.
        Assert.True(thinkingFrames <= 3, $"a methodical commander should think rarely, thought {thinkingFrames} times");
    }

    [Fact]
    public void A_headless_AI_vs_AI_skirmish_is_deterministic()
    {
        // docs/23 §6's acceptance bar in miniature: AI-vs-AI, driven end
        // to end by the commanders themselves, identical hashes across two
        // runs. (The full "3-faction, 50k ticks" version needs faction
        // rosters fielded at scale plus a win condition, neither of which
        // this phase owns.)
        ulong Run()
        {
            var city = SmallCity();
            var m = MatchState.Create(0xC0FFEEu, TwoPlayers(), city);
            var c = city.CenterHex;
            var ring = OpenNeighbors(city, c, 4);

            m.SpawnUnit(0, c, speed: 3.0, combat: Fighter(), salvageValue: 50);
            m.SpawnUnit(0, ring[0], speed: 3.0, combat: Fighter(), salvageValue: 50);
            m.SpawnUnit(1, ring[1], speed: 3.0, combat: Fighter(), salvageValue: 50);
            m.SpawnUnit(1, ring[2], speed: 3.0, combat: Fighter(), salvageValue: 50);
            m.SpawnAnomaly(city.Roundabouts.Count > 0 ? city.Roundabouts[0] : ring[3]);

            // Two procedurally generated opponents -- no hand-dialing.
            var a = new SkirmishCommander(0, CommanderPersonality.Generate(101u));
            var b = new SkirmishCommander(1, CommanderPersonality.Generate(202u));

            for (var i = 0; i < 600; i++)
            {
                var orders = new List<Command>();
                orders.AddRange(a.DecideCommands(m));
                orders.AddRange(b.DecideCommands(m));
                m.Tick(orders);
            }
            return m.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Procedurally_generated_commanders_actually_fight_a_match_out()
    {
        // Not a balance claim -- just proof the generator produces
        // commanders that DO something, rather than sitting idle all
        // match because some trait combination scores nothing.
        var actedAtLeastOnce = 0;
        for (var seed = 1u; seed <= 12u; seed++)
        {
            var city = SmallCity();
            var m = MatchState.Create(seed, TwoPlayers(), city);
            var c = city.CenterHex;
            var ring = OpenNeighbors(city, c, 2);
            m.SpawnUnit(0, c, speed: 3.0, combat: Fighter(), salvageValue: 50);
            m.SpawnUnit(1, ring[0], speed: 3.0, combat: Fighter(), salvageValue: 50);

            var cmd = new SkirmishCommander(0, CommanderPersonality.Generate(seed));
            var issued = 0;
            for (var i = 0; i < 200; i++)
            {
                var orders = cmd.DecideCommands(m);
                issued += orders.Count;
                m.Tick(orders);
            }
            if (issued > 0) actedAtLeastOnce++;
        }
        Assert.Equal(12, actedAtLeastOnce);
    }
}
