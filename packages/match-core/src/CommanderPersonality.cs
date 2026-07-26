using System;
using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §6 / §13 amendment D Phase 6c: the six axes a
    /// <see cref="SkirmishCommander"/>'s decisions are weighted by.
    ///
    /// Deliberately NOT a fresh invented vocabulary: these mirror the
    /// tension docs/16-brains-behavior-command.md already establishes for
    /// individual brains (`command`/`will`/`temperament`/`guile`/`fury`),
    /// lifted one level up from "how does this ONE creature behave under
    /// stress" to "how does the commander of an army spend its turn." The
    /// project's own faction principle -- "a faction is an expression
    /// profile, not a new system" (docs/17) -- applied to AI: a commander
    /// personality is a weighting profile over the SAME action set every
    /// commander can take, not a different decision tree per
    /// personality.</summary>
    public enum CommanderTrait
    {
        /// <summary>Seek fights, push toward the enemy, favour damage over
        /// position. Tension partner: <see cref="Caution"/>.</summary>
        Aggression = 0,

        /// <summary>Pull wounded units out, avoid high-threat ground,
        /// value survival over trades. Tension partner:
        /// <see cref="Aggression"/>.</summary>
        Caution = 1,

        /// <summary>Value loot and economy -- corpse salvage, resource
        /// buildings -- over map presence. Tension partner:
        /// <see cref="Territoriality"/>.</summary>
        Greed = 2,

        /// <summary>Value map control -- emitters, holding ground -- over
        /// immediate income. Tension partner: <see cref="Greed"/>.</summary>
        Territoriality = 3,

        /// <summary>Chase the shiny thing: anomalies, nearly-dead targets,
        /// undefended stragglers. Tension partner:
        /// <see cref="Discipline"/>.</summary>
        Opportunism = 4,

        /// <summary>Commit to the current plan; re-evaluate rarely, keep
        /// the army together. Tension partner:
        /// <see cref="Opportunism"/>.</summary>
        Discipline = 5,
    }

    /// <summary>A commander's decision-weighting profile: six traits, each
    /// in [0,1]. Two ways in, both first-class:
    ///
    /// **Dial it in.** Construct one directly with named arguments, start
    /// from a named archetype (<see cref="Berserker"/>,
    /// <see cref="Turtle"/>, <see cref="Hoarder"/>, ...), and/or nudge a
    /// single axis with <see cref="With"/>:
    /// <code>
    /// var p = CommanderPersonality.Turtle().With(CommanderTrait.Greed, 0.9);
    /// </code>
    ///
    /// **Generate it procedurally.** <see cref="Generate(SimRng)"/> /
    /// <see cref="Generate(uint)"/> roll a personality off the project's
    /// canonical seeded RNG (never `Math.Random` -- CLAUDE.md's determinism
    /// invariant), so "skirmish seed 7's opponent" is the same opponent
    /// every time, on every machine, forever.
    ///
    /// Generation is NOT six independent uniform rolls: that reliably
    /// produces a field of indistinguishable ~0.5 commanders (every trait
    /// regressing to the mean is exactly what makes procedural
    /// personalities feel same-y). Instead each generated commander gets a
    /// **signature**: one <see cref="TensionPairs"/> entry is driven apart
    /// -- one side raised into the top band, its opposite pushed into the
    /// bottom band -- and the remaining four axes are rolled freely for
    /// texture. The result reads as a *character* ("reckless, and it will
    /// not retreat") rather than a statistical blur, while still varying
    /// widely between seeds.
    ///
    /// This struct is DATA, never simulation state: it is not part of the
    /// tick hash, and a commander's own scoring math never enters
    /// <see cref="MatchState.Tick"/> -- see
    /// <see cref="SkirmishCommander"/>'s header for why that separation is
    /// load-bearing for lockstep.</summary>
    public readonly struct CommanderPersonality
    {
        private readonly double[] _traits;

        public double Aggression => Trait(CommanderTrait.Aggression);
        public double Caution => Trait(CommanderTrait.Caution);
        public double Greed => Trait(CommanderTrait.Greed);
        public double Territoriality => Trait(CommanderTrait.Territoriality);
        public double Opportunism => Trait(CommanderTrait.Opportunism);
        public double Discipline => Trait(CommanderTrait.Discipline);

        public const int TraitCount = 6;

        public CommanderPersonality(double aggression, double caution, double greed,
            double territoriality, double opportunism, double discipline)
        {
            _traits = new[]
            {
                Check(aggression, nameof(aggression)),
                Check(caution, nameof(caution)),
                Check(greed, nameof(greed)),
                Check(territoriality, nameof(territoriality)),
                Check(opportunism, nameof(opportunism)),
                Check(discipline, nameof(discipline)),
            };
        }

        private static double Check(double v, string name)
        {
            if (v < 0.0 || v > 1.0) throw new ArgumentOutOfRangeException(name, v, "traits are normalized to [0,1]");
            return v;
        }

        /// <summary>Read one axis. Safe on a `default(CommanderPersonality)`
        /// (returns <see cref="Balanced"/>'s 0.5 everywhere) so a caller
        /// that forgot to assign one gets a bland commander rather than a
        /// NullReferenceException mid-match.</summary>
        public double Trait(CommanderTrait trait) => _traits == null ? 0.5 : _traits[(int)trait];

        /// <summary>Return a copy with one axis changed -- the fine-tuning
        /// half of "dial it in," chainable off any preset.</summary>
        public CommanderPersonality With(CommanderTrait trait, double value)
        {
            var t = new double[TraitCount];
            for (var i = 0; i < TraitCount; i++) t[i] = Trait((CommanderTrait)i);
            t[(int)trait] = Check(value, nameof(value));
            return new CommanderPersonality(t[0], t[1], t[2], t[3], t[4], t[5]);
        }

        /// <summary>The three designed oppositions. A commander high in
        /// BOTH sides of a pair is incoherent (it wants to charge and hold
        /// back at once, and its utility scores cancel into mush) --
        /// <see cref="Generate(SimRng)"/> uses these to guarantee every
        /// rolled commander has at least one axis it clearly is and one it
        /// clearly isn't.</summary>
        public static readonly IReadOnlyList<(CommanderTrait A, CommanderTrait B)> TensionPairs = new[]
        {
            (CommanderTrait.Aggression, CommanderTrait.Caution),
            (CommanderTrait.Greed, CommanderTrait.Territoriality),
            (CommanderTrait.Opportunism, CommanderTrait.Discipline),
        };

        // ---- Dial-in archetypes ----

        /// <summary>Every axis at 0.5 -- the control case. Not
        /// recommended as an opponent (it does everything half-heartedly);
        /// useful as a baseline when A/B-testing a single dialed
        /// axis.</summary>
        public static CommanderPersonality Balanced() => new CommanderPersonality(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);

        /// <summary>All-in attacker: never retreats, never stops to
        /// loot.</summary>
        public static CommanderPersonality Berserker() => new CommanderPersonality(
            aggression: 0.95, caution: 0.05, greed: 0.2, territoriality: 0.35, opportunism: 0.6, discipline: 0.25);

        /// <summary>Defensive: holds ground, pulls wounded units out,
        /// trades only on favourable terms.</summary>
        public static CommanderPersonality Turtle() => new CommanderPersonality(
            aggression: 0.15, caution: 0.9, greed: 0.5, territoriality: 0.7, opportunism: 0.2, discipline: 0.85);

        /// <summary>Economy-first: strips every corpse on the field,
        /// fights only when it pays.</summary>
        public static CommanderPersonality Hoarder() => new CommanderPersonality(
            aggression: 0.3, caution: 0.6, greed: 0.95, territoriality: 0.15, opportunism: 0.7, discipline: 0.5);

        /// <summary>Map-control specialist: takes and holds emitters,
        /// treats fights as a means to territory.</summary>
        public static CommanderPersonality Warlord() => new CommanderPersonality(
            aggression: 0.65, caution: 0.35, greed: 0.2, territoriality: 0.95, opportunism: 0.4, discipline: 0.7);

        /// <summary>Vulture: chases anomalies and finishes wounded
        /// stragglers, abandons plans instantly for a better one.</summary>
        public static CommanderPersonality Opportunist() => new CommanderPersonality(
            aggression: 0.5, caution: 0.45, greed: 0.6, territoriality: 0.3, opportunism: 0.95, discipline: 0.1);

        /// <summary>Every dial-in archetype, for tests/menus that want to
        /// enumerate the presets rather than hardcode a list.</summary>
        public static IReadOnlyList<CommanderPersonality> Archetypes => new[]
        {
            Balanced(), Berserker(), Turtle(), Hoarder(), Warlord(), Opportunist(),
        };

        // ---- Procedural generation ----

        /// <summary>Bands the signature axis and its opposite are forced
        /// into (see this struct's own header for why an unbiased roll
        /// isn't enough). v0.1 placeholders like every other tuning number
        /// in this project -- widen them for blander commanders, narrow
        /// them for cartoonier ones.</summary>
        public const double SignatureFloor = 0.7;
        public const double OppositeCeiling = 0.3;

        /// <summary>The coherence guarantee <see cref="Generate(SimRng)"/>
        /// makes: in EVERY tension pair, the weaker side lands at or below
        /// this. Without it, only the signature pair is decorrelated and
        /// the other two roll free -- which really does produce commanders
        /// scoring maximum Aggression AND maximum Caution at once (seed 7
        /// did exactly that on the first draft). Such a commander isn't
        /// interestingly conflicted, it's just noisy: its charge and
        /// retreat utilities cancel and it dithers. See
        /// <see cref="IsCoherent"/>.</summary>
        public const double CoherenceLimit = 0.5;

        /// <summary>True when no tension pair has BOTH sides strong (see
        /// <see cref="CoherenceLimit"/>). Always true for anything
        /// <see cref="Generate(SimRng)"/> produces; the hand-dialed
        /// archetypes satisfy it too. Deliberately NOT enforced in the
        /// constructor -- a designer who deliberately wants a
        /// self-contradicting commander to see what it does should be able
        /// to build one.</summary>
        public bool IsCoherent
        {
            get
            {
                foreach (var (a, b) in TensionPairs)
                    if (Math.Min(Trait(a), Trait(b)) > CoherenceLimit) return false;
                return true;
            }
        }

        /// <summary>Roll a fresh personality off a seeded stream. The RNG
        /// is advanced by a FIXED number of draws (9 -- six trait values,
        /// one signature choice, and one orientation per non-signature
        /// pair) regardless of outcome, so generating N commanders from
        /// one shared stream is reproducible position-for-position -- the
        /// same "draw COUNT determinism matters as much as draw VALUE"
        /// discipline <see cref="CombatMath.ResolveDamage"/> already
        /// documents for crit-vs-luck.</summary>
        public static CommanderPersonality Generate(SimRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var t = new double[TraitCount];
            for (var i = 0; i < TraitCount; i++) t[i] = Unit(rng);

            // draw 7: which tension pair is this commander's signature,
            // and which side of it. 6 = 3 pairs x 2 orientations.
            var choice = rng.IntRange(TensionPairs.Count * 2);
            var signaturePair = choice / 2;

            for (var i = 0; i < TensionPairs.Count; i++)
            {
                var (a, b) = TensionPairs[i];
                if (i == signaturePair)
                {
                    var (signature, opposite) = (choice % 2) == 0 ? (a, b) : (b, a);
                    // Reuse each axis's already-drawn value, remapped into
                    // its band -- so the signature still VARIES (a 0.72
                    // zealot reads differently from a 0.99 one) instead of
                    // every generated commander pinning to the same
                    // extreme.
                    t[(int)signature] = SignatureFloor + t[(int)signature] * (1.0 - SignatureFloor);
                    t[(int)opposite] = t[(int)opposite] * OppositeCeiling;
                }
                else
                {
                    // The other two pairs still roll freely -- that's where
                    // a commander's texture comes from -- but anti-
                    // correlated, so they can never BOTH come out strong.
                    // Leaving them independent is what produced the
                    // incoherent "maximum aggression AND maximum caution"
                    // commander this constraint exists to rule out (see
                    // CoherenceLimit).
                    //
                    // Which side gets suppressed is drawn, not fixed:
                    // always damping the pair's second member quietly
                    // biased every generated commander away from Caution/
                    // Territoriality/Discipline (they are the `B` of their
                    // pair), which showed up as "Grasping" dominating the
                    // seed gallery.
                    var (strong, damped) = rng.IntRange(2) == 0 ? (a, b) : (b, a);
                    t[(int)damped] = t[(int)damped] * (1.0 - t[(int)strong]);
                }
            }

            return new CommanderPersonality(t[0], t[1], t[2], t[3], t[4], t[5]);
        }

        /// <summary>Convenience: "give me skirmish opponent #seed." Same
        /// seed always yields the same commander.</summary>
        public static CommanderPersonality Generate(uint seed) => Generate(new SimRng(seed));

        /// <summary>Uniform [0,1] in 1001 steps, off the integer-only
        /// <see cref="SimRng"/> (docs/23 §0: the sim draws INTEGERS; a
        /// fraction is a fixed-point ratio of a draw, never an ambient
        /// float source).</summary>
        private static double Unit(SimRng rng) => rng.IntRange(1001) / 1000.0;

        // ---- Readback ----

        /// <summary>The axis this commander leans on hardest -- ties
        /// broken by enum order so it's stable, never
        /// arbitrary.</summary>
        public CommanderTrait DominantTrait
        {
            get
            {
                var best = CommanderTrait.Aggression;
                var bestValue = -1.0;
                for (var i = 0; i < TraitCount; i++)
                {
                    var v = Trait((CommanderTrait)i);
                    if (v > bestValue) { bestValue = v; best = (CommanderTrait)i; }
                }
                return best;
            }
        }

        /// <summary>A short human-readable handle for logs, replay
        /// transcripts, and (eventually) a skirmish-setup menu -- so a
        /// procedurally generated opponent can be NAMED rather than
        /// showing up as six anonymous decimals.</summary>
        /// <summary>How far apart this commander's strongest and weakest
        /// axes are. Near zero means it has no real character at all --
        /// see <see cref="Label"/>.</summary>
        public double Spread
        {
            get
            {
                var min = 1.0;
                var max = 0.0;
                for (var i = 0; i < TraitCount; i++)
                {
                    var v = Trait((CommanderTrait)i);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                return max - min;
            }
        }

        /// <summary>Below this spread, <see cref="DominantTrait"/> is
        /// meaningless -- it's just reporting whichever axis won an
        /// arbitrary tie -- so <see cref="Label"/> says so instead of
        /// naming one. (A flat 0.5-everywhere personality was reading as
        /// "Reckless" purely because Aggression sorts first.)</summary>
        public const double FeaturelessSpread = 0.1;

        public string Label
        {
            get
            {
                if (Spread < FeaturelessSpread) return "Nondescript";
                switch (DominantTrait)
                {
                    case CommanderTrait.Aggression: return "Reckless";
                    case CommanderTrait.Caution: return "Wary";
                    case CommanderTrait.Greed: return "Grasping";
                    case CommanderTrait.Territoriality: return "Entrenching";
                    case CommanderTrait.Opportunism: return "Scavenging";
                    case CommanderTrait.Discipline: return "Methodical";
                    default: return "Inscrutable";
                }
            }
        }

        public override string ToString() =>
            $"{Label} (agg {Aggression:F2}, cau {Caution:F2}, gre {Greed:F2}, ter {Territoriality:F2}, opp {Opportunism:F2}, dis {Discipline:F2})";
    }
}
