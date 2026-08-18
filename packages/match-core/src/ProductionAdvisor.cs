using System;
using System.Collections.Generic;
using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>docs/30 (selectable races + AI opponents): the economic
    /// twin of <see cref="SkirmishCommander"/> -- where that class decides
    /// what an AI's ARMY does, this class decides what an AI's ECONOMY
    /// does: train units, expand infrastructure. Same "command SOURCE, not
    /// part of the simulation" shape (see <see cref="SkirmishCommander"/>'s
    /// own header for why that separation is load-bearing for lockstep) --
    /// this reads a <see cref="MatchState"/> and returns <see
    /// cref="Command"/>s, never mutates anything itself.
    ///
    /// This is docs/23 §13 amendment D's deferred "production/build-order
    /// AI" phase, explicitly split out of <see cref="SkirmishCommander"/>
    /// (see that class's own "Explicitly deferred, not faked" note) because
    /// its stated prerequisite -- a unit-PRODUCTION command -- did not
    /// exist yet. It has since shipped (<see cref="CommandKind.TrainUnit"/>,
    /// the worker-economy epic): this class is the scoring/wiring half that
    /// was still missing, now that the command itself is real.
    ///
    /// **Personality mapping** (see docs/30 for the full rationale):
    /// <see cref="CommanderTrait.Aggression"/> sets how large a standing
    /// army this advisor wants (a fraction of <see
    /// cref="PlayerState.SupplyCap"/>); <see cref="CommanderTrait.Greed"/>
    /// sets how much of the current wallet it commits per decision;
    /// <see cref="CommanderTrait.Territoriality"/> sets how often it
    /// expands (<see cref="CommandKind.BuildStructure"/>) instead of
    /// training; <see cref="CommanderTrait.Discipline"/> sets its decision
    /// cadence, same direction <see cref="SkirmishCommander"/> already uses
    /// it (low discipline = twitchy re-evaluation, high = commits longer).
    /// <see cref="CommanderTrait.Caution"/> and <see
    /// cref="CommanderTrait.Opportunism"/> are deliberately left unused
    /// here -- same "don't invent a mapping that doesn't have a real
    /// translation" discipline <see cref="ArmyGenerator"/>'s own header
    /// already states for its narrower Aggression/Caution-only use of
    /// personality.</summary>
    public sealed class ProductionAdvisor
    {
        public int PlayerIndex { get; }
        public CommanderPersonality Personality { get; }

        /// <summary>2026-08 (creator direction: "scale the ai intelligence
        /// for Difficulty") -- see <see cref="SkirmishCommander.Difficulty"/>'s
        /// own doc comment; same skill-dial contract, applied to this
        /// advisor's economic decisions instead of combat ones.</summary>
        public AiDifficulty Difficulty { get; }

        /// <summary>Economic decisions don't need <see
        /// cref="SkirmishCommander"/>'s combat-reflex 2-20 tick cadence --
        /// v0.1 placeholder range (2-6s at <see
        /// cref="MatchState.TicksPerSecond"/>), flagged as invented like
        /// every other tuning number in this project. 2026-08: scaled by
        /// <see cref="AiDifficultyProfile.ReactionMultiplier"/> same as
        /// <see cref="SkirmishCommander.DecisionIntervalTicks"/> -- see
        /// that property's own doc comment for the floor-only clamping
        /// rationale.</summary>
        public const int MinDecisionIntervalTicks = 20;
        public const int MaxDecisionIntervalTicks = 60;

        public int DecisionIntervalTicks { get; }

        /// <summary>v0.1 placeholder preference order for <see
        /// cref="CommandKind.BuildStructure"/> -- storage/economy kinds
        /// before <see cref="BuildingKind.Defense"/>, mirroring
        /// Territoriality's own doc comment ("map control... over
        /// immediate income" loosely reinterpreted here as "infrastructure
        /// before army"). <see cref="BuildingKind.Hq"/> and <see
        /// cref="BuildingKind.Factory"/> excluded: Hq is generator-placed
        /// only (never a valid BuildStructure target -- see that enum
        /// value's own doc comment), and every AI opponent already starts
        /// with one Factory via <see
        /// cref="RuntimeCityBuilder.SpawnStartingBases"/> equivalent
        /// setup-time spawn, so a second one isn't this advisor's job to
        /// invent a reason for.</summary>
        private static readonly BuildingKind[] ExpansionPreference =
        {
            BuildingKind.BloodStorage,
            BuildingKind.FuelStorage,
            BuildingKind.PartsStorage,
            BuildingKind.HarvestPost,
            BuildingKind.Defense,
        };

        private readonly SimRng _rng;

        /// <summary>`seed` drives this advisor's own private RNG stream
        /// (shopping-list draws via <see cref="ArmyGenerator.Generate"/>
        /// and the expand-vs-train coin flip) -- separate from <see
        /// cref="MatchState"/>'s own <see cref="SimRng"/>, same "AI
        /// decision-making sits outside the tick, never advances the sim's
        /// own RNG" separation <see cref="SkirmishCommander"/> already
        /// keeps by having NO RNG at all. Two advisors constructed with the
        /// same seed make identical decisions given identical states -- the
        /// same "draw COUNT and VALUE both matter" determinism discipline
        /// <see cref="CommanderPersonality.Generate(SimRng)"/> already
        /// documents, so a replay reproduces this advisor's choices
        /// exactly.</summary>
        public ProductionAdvisor(int playerIndex, CommanderPersonality personality, uint seed, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            if (playerIndex < 0) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            PlayerIndex = playerIndex;
            Personality = personality;
            Difficulty = difficulty;
            var span = MaxDecisionIntervalTicks - MinDecisionIntervalTicks;
            var disciplineTicks = MinDecisionIntervalTicks + (int)Math.Round(personality.Discipline * span);
            var scaled = (int)Math.Round(disciplineTicks * AiDifficultyProfile.Get(difficulty).ReactionMultiplier);
            DecisionIntervalTicks = Math.Max(MinDecisionIntervalTicks, scaled);
            _rng = new SimRng(seed);
        }

        /// <summary>True on frames this advisor actually thinks. Mirrors
        /// <see cref="SkirmishCommander.IsDecisionFrame"/>'s own
        /// shape.</summary>
        public bool IsDecisionFrame(int frame) => frame % DecisionIntervalTicks == 0;

        /// <summary>The commands to hand to <see cref="MatchState.Tick"/>
        /// this frame -- at most one per decision (either one expansion or
        /// one training push across this player's idle production
        /// buildings), never both in the same call. Empty on a
        /// non-decision frame, or (defensively -- see <see
        /// cref="ArmyGenerator"/>'s own header, all four factions have real
        /// roster data as of 2026-08) if this player's faction somehow has
        /// no army roster at all.</summary>
        public IReadOnlyList<Command> DecideCommands(MatchState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var commands = new List<Command>();
            if (!IsDecisionFrame(state.Frame)) return commands;

            var player = state.Player(PlayerIndex);

            // Territoriality -> how often this decision goes to expansion
            // instead of training.
            var expandRoll = _rng.IntRange(1000);
            var goExpand = expandRoll < (int)(Personality.Territoriality * 1000);

            if (goExpand && TryQueueExpansion(state, player, commands))
                return commands;

            // Aggression -> target standing-army size, as a fraction of
            // supply cap (docs/23 §13-E: 60 base / 20-40 units at scale --
            // this deliberately never claims the full cap, leaving room
            // for the "don't spam" ceiling that range already implies).
            var targetSupplyFraction = 0.4 + Personality.Aggression * 0.5;
            var capBasedTarget = (int)(player.SupplyCap * targetSupplyFraction);

            // 2026-08 (creator direction: "the roster needs to be able to
            // generate enemies for all races. They should take the number
            // of units from the player, so armies are fairly balanced
            // amongst all ai units and players"): the cap-based target
            // above is purely self-referential -- it never looked at what
            // anyone ELSE actually has fielded, so a human who outgrew
            // their own early build order could run circles around an AI
            // still capped at a fraction of a fixed constant. `SupplyUsed`
            // doesn't track live unit count on its own (see
            // <see cref="LiveUnitCount"/>'s own doc comment), so this
            // reads the real, current unit tally off `state` directly --
            // the strongest HUMAN player's count (there is normally
            // exactly one; the max guards a hypothetical future multi-
            // human match the same way <see cref="ArmyGenerator"/>'s own
            // faction handling stays generic rather than assuming a
            // single opponent). `balanceFactor` lets Aggression still mean
            // something even once this floor is in play: a Turtle
            // (Aggression 0) merely matches the human's count, a Berserker
            // (Aggression 1) overshoots it by up to 40% -- never a blowout
            // in either direction, and always still clamped to this
            // player's OWN SupplyCap just like the cap-based target always
            // was.
            var humanUnitCount = 0;
            for (var i = 0; i < state.PlayerCount; i++)
            {
                if (i == PlayerIndex || state.Player(i).IsAiControlled) continue;
                var count = LiveUnitCount(state, i);
                if (count > humanUnitCount) humanUnitCount = count;
            }
            var balanceFactor = 0.8 + Personality.Aggression * 0.4;
            var balanceTarget = (int)(humanUnitCount * balanceFactor);
            var uncappedTarget = Math.Max(capBasedTarget, balanceTarget);

            // 2026-08 (creator direction: "scale the ai intelligence for
            // Difficulty... in tutorial and early levels players can get a
            // sense of achievement"): the primary difficulty lever. Both
            // the self-referential floor and the player-relative target
            // above scale together by ArmySizeMultiplier, so a Tutorial
            // opponent deliberately commits to a SMALLER army than even
            // its own cap-based floor would otherwise field, and a Brutal
            // one overcommits past what pure Aggression alone would ask
            // for. Re-clamped to SupplyCap AFTER scaling -- Brutal's >1
            // multiplier must never actually exceed the hard cap.
            var targetSupply = Math.Min(player.SupplyCap,
                (int)(uncappedTarget * AiDifficultyProfile.Get(Difficulty).ArmySizeMultiplier));

            // NOT player.SupplyUsed: nothing in match-core ever calls
            // PlayerState.AddSupplyUsed outside its own test file (a
            // pre-existing gap, confirmed by grep -- units currently join
            // a player's army without ever touching that counter), so it
            // sits permanently at 0 and this gate would otherwise never
            // bind. LiveUnitCount reuses the exact same real, live tally
            // this method just computed humanUnitCount from, applied to
            // this AI's own army instead.
            if (LiveUnitCount(state, PlayerIndex) < targetSupply)
                TryQueueTraining(state, player, commands);

            return commands;
        }

        private bool TryQueueExpansion(MatchState state, PlayerState player, List<Command> commands)
        {
            var hqHex = FindOwnHq(state);
            if (hqHex == null) return false;

            foreach (var kind in ExpansionPreference)
            {
                var def = BuildingDef.Get(kind);
                var affordable = true;
                foreach (var (resource, amount) in def.Cost)
                {
                    if (player.Wallet(resource) < amount) { affordable = false; break; }
                }
                if (!affordable) continue;

                var hex = FindOpenHexForBuilding(state, hqHex.Value, kind);
                if (hex == null) continue;

                commands.Add(new Command(PlayerIndex, CommandKind.BuildStructure,
                    unchecked((uint)kind), hex.Value.Q, hex.Value.R));
                return true;
            }
            return false;
        }

        /// <summary>2026-08 (Barracks/infantry roster pass): which
        /// BuildingKinds this advisor treats as valid TrainUnit producers
        /// -- <see cref="MatchState.CanTrainUnit"/> itself is building-
        /// kind-agnostic (any Complete, player-owned building with an
        /// open slot), but this advisor's own idle-producer scan used to
        /// hardcode <see cref="BuildingKind.Factory"/> as the only kind
        /// worth checking, back when it was the only producer that
        /// existed. Barracks added alongside it now that a second
        /// producer kind is real -- an AI opponent's own starting
        /// Barracks (see <see cref="MatchState.SpawnBarracksForPlayer"/>)
        /// would otherwise sit idle forever under AI control, the exact
        /// "built a building nobody uses" gap this array exists to
        /// avoid.</summary>
        private static readonly BuildingKind[] ProducerKinds = { BuildingKind.Factory, BuildingKind.Barracks };

        private void TryQueueTraining(MatchState state, PlayerState player, List<Command> commands)
        {
            List<uint>? idleProducers = null;
            for (var i = 0; i < state.BuildingCount; i++)
            {
                var b = state.BuildingAt(i);
                if (b.PlayerIndex != PlayerIndex || b.State != BuildingState.Complete) continue;
                if (Array.IndexOf(ProducerKinds, b.Kind) < 0) continue;
                if (b.TrainingKind != null) continue;
                (idleProducers ??= new List<uint>()).Add(b.EntityId);
            }
            if (idleProducers == null) return;

            // Greed -> how much of the current wallet this decision commits
            // to a shopping list. All four factions have real roster data
            // now (2026-08, see ArmyGenerator's own header) so this catch
            // is defensive-only rather than an expected path -- kept
            // rather than removed so a genuinely misconfigured faction
            // still fails soft (never trains) instead of crashing the
            // whole decision loop.
            // 2026-08 difficulty follow-up: EconomyMultiplier scales this
            // same fraction, clamped to [0,1] (spending more than the
            // whole wallet in one decision isn't meaningful) -- a Tutorial
            // opponent commits a smaller slice of its wallet per decision
            // and effectively hoards the rest, a Brutal one commits more.
            var spendFraction = Math.Clamp(
                (0.2 + Personality.Greed * 0.6) * AiDifficultyProfile.Get(Difficulty).EconomyMultiplier, 0.0, 1.0);
            var budget = new Dictionary<ResourceKind, int>(Resources.Count);
            for (var i = 0; i < Resources.Count; i++)
            {
                var kind = (ResourceKind)i;
                budget[kind] = (int)(player.Wallet(kind) * spendFraction);
            }

            IReadOnlyList<(RosterUnitKind Kind, int Count)> shoppingList;
            try
            {
                shoppingList = ArmyGenerator.Generate(player.Faction, Personality, budget, _rng);
            }
            catch (ArgumentException)
            {
                return;
            }
            if (shoppingList.Count == 0) return;

            // One TrainUnit per idle producer, highest-priority (roster
            // order, per ArmyGenerator's own stable-output contract)
            // still-affordable kind from the shopping list -- CanTrainUnit
            // itself re-validates affordability against the LIVE wallet
            // (not the budget snapshot above, which shrinks as earlier
            // producers in this same loop are queued), so this never
            // double-spends across multiple idle buildings in one
            // decision.
            foreach (var buildingId in idleProducers)
            {
                foreach (var (kind, count) in shoppingList)
                {
                    if (count <= 0) continue;
                    if (!state.CanTrainUnit(PlayerIndex, buildingId, kind)) continue;
                    commands.Add(new Command(PlayerIndex, CommandKind.TrainUnit, buildingId, (int)kind));
                    break;
                }
            }
        }

        /// <summary>2026-08 (player-relative army balancing): the real,
        /// current count of `playerIndex`'s living units in `state` --
        /// counts every <see cref="SimUnit"/> with a matching <see
        /// cref="SimUnit.PlayerIndex"/> and <see cref="SimUnit.IsAlive"/>
        /// true. Deliberately NOT <see cref="PlayerState.SupplyUsed"/>:
        /// that field exists and is even hashed, but nothing in match-core
        /// outside its own test file ever calls <see
        /// cref="PlayerState.AddSupplyUsed"/>, so it sits at 0 for the
        /// entire match today -- a real, pre-existing gap this method
        /// works around rather than silently trusting. A plain O(units)
        /// scan, called at most once per decision-frame per AI player
        /// (<see cref="MinDecisionIntervalTicks"/>+ ticks apart, never
        /// per-tick), the same cost class <see cref="TryQueueTraining"/>'s
        /// own idle-producer scan already pays every decision.</summary>
        private static int LiveUnitCount(MatchState state, int playerIndex)
        {
            var count = 0;
            for (var i = 0; i < state.UnitCount; i++)
            {
                var u = state.UnitAt(i);
                if (u.PlayerIndex == playerIndex && u.IsAlive) count++;
            }
            return count;
        }

        private HexCoord? FindOwnHq(MatchState state)
        {
            for (var i = 0; i < state.BuildingCount; i++)
            {
                var b = state.BuildingAt(i);
                if (b.PlayerIndex == PlayerIndex && b.Kind == BuildingKind.Hq && b.State != BuildingState.Destroyed)
                    return b.Hex;
            }
            return null;
        }

        /// <summary>Ring-search out from `center` (mirrors <see
        /// cref="MatchState"/>'s own private `FindOpenHexNear`'s shape,
        /// reusing the SAME public <see cref="HexCoord.Ring"/> that method
        /// walks) for the nearest hex <see
        /// cref="MatchState.CanPlaceBuilding"/> actually accepts for
        /// `kind` -- the one shared validity check <see
        /// cref="MatchState.CanPlaceBuilding"/>'s own doc comment already
        /// establishes, so this can never queue a placement the sim would
        /// then silently reject.</summary>
        private HexCoord? FindOpenHexForBuilding(MatchState state, HexCoord center, BuildingKind kind)
        {
            for (var ring = 1; ring <= 6; ring++)
                foreach (var hex in center.Ring(ring))
                    if (state.CanPlaceBuilding(PlayerIndex, kind, hex))
                        return hex;
            return null;
        }
    }
}
