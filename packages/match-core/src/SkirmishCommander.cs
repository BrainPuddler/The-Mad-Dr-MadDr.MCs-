using System;
using System.Collections.Generic;
using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>What a <see cref="SkirmishCommander"/> decided one unit
    /// should do this decision -- exposed (rather than buried inside the
    /// command list) so tests and a future debug overlay can ask *why*,
    /// not just *what*.</summary>
    public enum CommanderAction
    {
        None = 0,
        Attack = 1,
        AttackAnomaly = 2,
        Salvage = 3,
        Advance = 4,
        Retreat = 5,
        SeizeEmitter = 6,
    }

    /// <summary>One scored decision. <see cref="Command"/> is exactly what
    /// would be fed to <see cref="MatchState.Tick"/>.</summary>
    public readonly struct CommanderDecision
    {
        public readonly uint UnitId;
        public readonly CommanderAction Action;
        public readonly double Score;
        public readonly Command Command;

        public CommanderDecision(uint unitId, CommanderAction action, double score, Command command)
        {
            UnitId = unitId;
            Action = action;
            Score = score;
            Command = command;
        }
    }

    /// <summary>docs/23 §6 / §13 amendment D Phase 6c: the utility-driven
    /// skirmish commander -- "AI opponents for skirmish use a
    /// utility-driven commander in match-core (build order scripts +
    /// threat maps) so 1-player matches work before netcode."
    ///
    /// **It is a command SOURCE, not part of the simulation.** Nothing in
    /// this class runs inside <see cref="MatchState.Tick"/>; it READS a
    /// state and RETURNS <see cref="Command"/>s, exactly like a human
    /// player's input layer does, and the caller feeds them to the next
    /// tick. That separation is load-bearing, not stylistic:
    /// - lockstep (docs/23 §11) already replicates a command stream, so an
    ///   AI that emits commands needs no new netcode -- one peer runs the
    ///   commander and its orders replicate like anyone else's;
    /// - a replay stays exact without re-running the AI at all, since the
    ///   commands are already in the log;
    /// - and it means this file's `double` utility math can never threaten
    ///   the sim's cross-platform determinism (docs/23 §0's float
    ///   discipline governs the TICK path; a command source sits outside
    ///   it, the same way Unity's mouse-click handler does).
    ///
    /// Decisions themselves are deterministic given (state, personality):
    /// no RNG at all, and every scan runs in entity-ID order with ties
    /// broken by the lower entity ID, so two runs of the same match
    /// produce byte-identical command streams.
    ///
    /// **Personality is the whole design.** Every action below is scored
    /// by the same formula for every commander; only the
    /// <see cref="CommanderPersonality"/> weights differ. See that struct
    /// for the two supported ways to get one -- dial an archetype in by
    /// hand, or generate one procedurally off a seed.
    ///
    /// **Explicitly deferred, not faked:** docs/23's other named
    /// ingredient, *build order scripts*, is only half-possible today.
    /// match-core has `CommandKind.BuildStructure` (so a commander COULD
    /// script structures) but no unit-PRODUCTION command whatsoever --
    /// units are setup-time spawns (<see cref="MatchState.SpawnUnit"/>/
    /// <see cref="MatchState.SpawnRosterUnit"/>), and "a Factory produces
    /// a unit over time" does not exist as a mechanic in any phase
    /// shipped so far. A build order that can't produce units isn't a
    /// build order, so the whole scripted-opening layer waits for that
    /// prerequisite rather than being half-built here; this commander
    /// fights, loots, and takes ground with the army it is handed. Same
    /// "flag the real prerequisite, don't fake around it" discipline every
    /// prior phase used.</summary>
    public sealed class SkirmishCommander
    {
        public int PlayerIndex { get; }
        public CommanderPersonality Personality { get; }

        /// <summary>How often this commander re-evaluates, in ticks --
        /// derived from <see cref="CommanderTrait.Discipline"/> rather
        /// than configured separately, because "how long do you commit to
        /// a plan" IS what that trait means. A scattered opportunist
        /// (discipline 0) re-reads the field every
        /// <see cref="MinDecisionIntervalTicks"/> ticks and will happily
        /// abandon an approach halfway; a methodical commander (discipline
        /// 1) locks in for <see cref="MaxDecisionIntervalTicks"/> and
        /// plays the plan out. Both are real, legible weaknesses -- the
        /// twitchy one never finishes anything, the stubborn one is slow
        /// to react.</summary>
        public int DecisionIntervalTicks { get; }

        public const int MinDecisionIntervalTicks = 2;
        public const int MaxDecisionIntervalTicks = 20;

        public SkirmishCommander(int playerIndex, CommanderPersonality personality)
        {
            if (playerIndex < 0) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            PlayerIndex = playerIndex;
            Personality = personality;
            var span = MaxDecisionIntervalTicks - MinDecisionIntervalTicks;
            DecisionIntervalTicks = MinDecisionIntervalTicks + (int)Math.Round(personality.Discipline * span);
        }

        /// <summary>True on frames this commander actually thinks. Exposed
        /// so a driver loop can skip building the (cheap, but not free)
        /// threat snapshot on the frames in between.</summary>
        public bool IsDecisionFrame(int frame) => frame % DecisionIntervalTicks == 0;

        // ---- Utility base weights (v0.1 placeholders, all of them) ----
        //
        // docs/23 asks for "a utility-driven commander" and never gives a
        // scoring table, so these bases are invented -- the same standing
        // policy as every other unsourced number in this project. What
        // they encode is only the RELATIVE floor of each action before
        // personality touches it; the personality multipliers below are
        // what actually give a commander its character, and they span a
        // wide enough range (roughly 0.3x .. 1.9x) to reorder these bases
        // completely.
        private const double AttackBase = 1.0;
        private const double AnomalyBase = 0.9;
        private const double SalvageBase = 0.8;
        private const double AdvanceBase = 0.5;
        private const double RetreatBase = 1.2;
        private const double EmitterBase = 0.7;

        /// <summary>Score every unit's options and return the winning
        /// order for each -- the introspectable form of
        /// <see cref="DecideCommands"/>. Empty on a non-decision frame.</summary>
        public IReadOnlyList<CommanderDecision> Decide(MatchState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var decisions = new List<CommanderDecision>();
            if (!IsDecisionFrame(state.Frame)) return decisions;

            var threat = ThreatMap.From(state, PlayerIndex);

            for (var i = 0; i < state.UnitCount; i++)
            {
                var unit = state.UnitAt(i);
                if (unit.PlayerIndex != PlayerIndex || !unit.IsAlive || unit.Combat == null) continue;

                // Never interrupt a salvage channel in progress. docs/04's
                // harvest pays out all-or-nothing after 3 seconds, so a
                // commander that re-decides every 2 ticks (discipline 0)
                // would otherwise restart the channel forever and NEVER
                // collect a single corpse -- a real bug the "just re-issue
                // the best action every decision" shape walks straight
                // into. Found while wiring this; guarded here rather than
                // by weakening the personality range.
                if (unit.Order == UnitOrderKind.Salvaging && unit.SalvageTargetId.HasValue) continue;

                var best = ScoreUnit(state, unit, threat);
                if (best.Action != CommanderAction.None) decisions.Add(best);
            }

            return decisions;
        }

        /// <summary>The orders to hand to <see cref="MatchState.Tick"/>
        /// this frame.</summary>
        public IReadOnlyList<Command> DecideCommands(MatchState state)
        {
            var decisions = Decide(state);
            var commands = new List<Command>(decisions.Count);
            for (var i = 0; i < decisions.Count; i++) commands.Add(decisions[i].Command);
            return commands;
        }

        private CommanderDecision ScoreUnit(MatchState state, SimUnit unit, ThreatMap threat)
        {
            var p = Personality;
            var myHex = MatchState.HexOfWorld(unit.X, unit.Z);
            var reach = unit.Combat!.Value.Reach;

            // How exposed this unit is right now, and how hurt -- the two
            // inputs every defensive weighting keys off.
            var localThreat = threat.NormalizedThreatAt(unit.X, unit.Z);
            var maxVitality = unit.EffectiveMaxVitality;
            var woundedness = maxVitality > 0 ? 1.0 - (double)unit.Vitality / maxVitality : 0.0;

            var bestScore = 0.0;
            var bestAction = CommanderAction.None;
            var bestCommand = default(Command);

            void Consider(double score, CommanderAction action, Command command)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = action;
                    bestCommand = command;
                }
            }

            // ---- Fight what's already in range ----
            SimUnit? nearestEnemy = null;
            var nearestEnemyDist = double.MaxValue;

            for (var i = 0; i < state.UnitCount; i++)
            {
                var other = state.UnitAt(i);
                if (other.PlayerIndex == PlayerIndex || other.Combat == null) continue;

                var otherHex = MatchState.HexOfWorld(other.X, other.Z);
                var hexDist = myHex.DistanceTo(otherHex);

                if (other.IsAlive)
                {
                    if (hexDist < nearestEnemyDist) { nearestEnemyDist = hexDist; nearestEnemy = other; }

                    if (hexDist <= reach)
                    {
                        var otherMax = other.EffectiveMaxVitality;
                        var prey = otherMax > 0 ? 1.0 - (double)other.Vitality / otherMax : 0.0;
                        // Aggression sets the floor; opportunism sharpens
                        // the preference for finishing something already
                        // bleeding; caution discounts swinging while
                        // standing in a firestorm.
                        var score = AttackBase
                            * (0.4 + p.Aggression * 1.2)
                            * (1.0 + prey * p.Opportunism)
                            * (1.0 - localThreat * p.Caution * 0.5);
                        Consider(score, CommanderAction.Attack,
                            new Command(PlayerIndex, CommandKind.AttackUnit, unit.EntityId, unchecked((int)other.EntityId)));
                    }
                }
                else if (other.IsSalvageable(state.Frame) && other.SalvageRemaining > 0
                         && hexDist <= MatchState.SalvageRangeHexes)
                {
                    // Greed is the whole story here; opportunism adds a
                    // little "well, it's right there."
                    var score = SalvageBase * (0.2 + p.Greed * 1.5 + p.Opportunism * 0.3);
                    Consider(score, CommanderAction.Salvage,
                        new Command(PlayerIndex, CommandKind.SalvageCorpse, unit.EntityId, unchecked((int)other.EntityId)));
                }
            }

            // ---- Grab a Loose Experiment ----
            SimAnomaly? nearestAnomaly = null;
            var nearestAnomalyDist = double.MaxValue;
            for (var i = 0; i < state.AnomalyCount; i++)
            {
                var anomaly = state.AnomalyAt(i);
                if (!anomaly.IsAlive) continue;
                var hexDist = myHex.DistanceTo(MatchState.HexOfWorld(anomaly.X, anomaly.Z));
                if (hexDist < nearestAnomalyDist) { nearestAnomalyDist = hexDist; nearestAnomaly = anomaly; }
                if (hexDist > reach) continue;

                var score = AnomalyBase * (0.3 + p.Opportunism * 1.4);
                Consider(score, CommanderAction.AttackAnomaly,
                    new Command(PlayerIndex, CommandKind.AttackAnomaly, unit.EntityId, unchecked((int)anomaly.EntityId)));
            }

            // ---- Break off if this is going badly ----
            if (woundedness > 0.0 && localThreat > 0.0)
            {
                var score = RetreatBase * p.Caution * woundedness * localThreat;
                var refuge = RetreatHex(state, unit, myHex, threat);
                if (refuge.HasValue)
                    Consider(score, CommanderAction.Retreat,
                        new Command(PlayerIndex, CommandKind.MoveTo, unit.EntityId, refuge.Value.Q, refuge.Value.R));
            }

            // ---- Take ground ----
            var emitter = NearestSeizableEmitter(state, myHex);
            if (emitter.HasValue)
            {
                // Distance-discounted so a commander doesn't march the
                // whole army across the map past a live fight.
                var falloff = 1.0 / (1.0 + emitter.Value.Distance * 0.1);
                var score = EmitterBase * (0.15 + p.Territoriality * 1.6) * falloff;
                Consider(score, CommanderAction.SeizeEmitter,
                    new Command(PlayerIndex, CommandKind.MoveTo, unit.EntityId, emitter.Value.Hex.Q, emitter.Value.Hex.R));
            }

            // ---- Close the distance ----
            if (nearestEnemy != null && nearestEnemyDist > reach)
            {
                var target = MatchState.HexOfWorld(nearestEnemy.X, nearestEnemy.Z);
                var falloff = 1.0 / (1.0 + nearestEnemyDist * 0.05);
                var score = AdvanceBase * (0.25 + p.Aggression * 1.3) * falloff
                    * (1.0 - localThreat * p.Caution * 0.4);
                Consider(score, CommanderAction.Advance,
                    new Command(PlayerIndex, CommandKind.MoveTo, unit.EntityId, target.Q, target.R));
            }
            else if (nearestEnemy == null && nearestAnomaly != null && nearestAnomalyDist > reach)
            {
                // Nothing to fight -- an opportunist still has somewhere
                // to be.
                var target = MatchState.HexOfWorld(nearestAnomaly.X, nearestAnomaly.Z);
                var falloff = 1.0 / (1.0 + nearestAnomalyDist * 0.05);
                var score = AdvanceBase * (0.2 + p.Opportunism * 1.2) * falloff;
                Consider(score, CommanderAction.Advance,
                    new Command(PlayerIndex, CommandKind.MoveTo, unit.EntityId, target.Q, target.R));
            }

            // Don't re-issue an order the unit is already carrying out --
            // re-pathing an in-flight MoveTo every decision is wasted work,
            // and re-targeting the identical attack would reset nothing
            // useful.
            if (bestAction == CommanderAction.Attack
                && unit.Order == UnitOrderKind.AttackUnit
                && unit.AttackTargetId == unchecked((uint)bestCommand.ArgA))
                return default;

            if (bestAction == CommanderAction.AttackAnomaly
                && unit.Order == UnitOrderKind.AttackAnomaly
                && unit.AttackAnomalyTargetId == unchecked((uint)bestCommand.ArgA))
                return default;

            return new CommanderDecision(unit.EntityId, bestAction, bestScore, bestCommand);
        }

        /// <summary>Where to run to: the commander's own HQ if it has one
        /// (the natural rally point, and the only friendly structure
        /// match-core reliably has), otherwise whichever adjacent hex is
        /// least threatened. Null if neither exists -- a unit with nowhere
        /// safer to be simply doesn't retreat, which is the honest
        /// outcome rather than a flailing random step.</summary>
        private HexCoord? RetreatHex(MatchState state, SimUnit unit, HexCoord myHex, ThreatMap threat)
        {
            for (var i = 0; i < state.BuildingCount; i++)
            {
                var b = state.BuildingAt(i);
                if (b.PlayerIndex == PlayerIndex && b.Kind == BuildingKind.Hq && b.State != BuildingState.Destroyed)
                    return b.Hex;
            }

            HexCoord? best = null;
            var bestThreat = threat.ThreatAt(unit.X, unit.Z);
            foreach (var n in myHex.Neighbors())
            {
                var (nx, nz) = n.ToWorld();
                var t = threat.ThreatAt(nx, nz);
                if (t < bestThreat) { bestThreat = t; best = n; }
            }
            return best;
        }

        /// <summary>Nearest emitter this commander doesn't already own,
        /// with its hex distance. docs/03's capture rule wants a unit
        /// STANDING on the emitter's own hex -- a known, already-logged
        /// gap (that hex is part of its landmark building's solid
        /// footprint, so a ground unit can never actually reach it; see
        /// docs/12's Phase 3.5 entry). This still issues the move, because
        /// the order is correct and the pathfinder simply stops the unit
        /// short: the commander is not the right place to paper over a
        /// city-generation decision, and hard-coding a workaround here
        /// would hide the gap rather than leave it visible for the real
        /// fix.</summary>
        private (HexCoord Hex, int Distance)? NearestSeizableEmitter(MatchState state, HexCoord myHex)
        {
            HexCoord? bestHex = null;
            var bestDist = int.MaxValue;
            for (var i = 0; i < state.EmitterCount; i++)
            {
                var e = state.EmitterAt(i);
                if (e.Owner == PlayerIndex) continue;
                var d = myHex.DistanceTo(e.Hex);
                if (d < bestDist) { bestDist = d; bestHex = e.Hex; }
            }
            return bestHex.HasValue ? (bestHex.Value, bestDist) : ((HexCoord, int)?)null;
        }
    }
}
