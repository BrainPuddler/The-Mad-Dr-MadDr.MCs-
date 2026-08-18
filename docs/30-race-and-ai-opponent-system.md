# 30 — Race Selection & AI Opponent System

Status: **Implemented (2026-08)**. This doc is the design record for the
epic; per-decision rationale that predates or extends it lives in
[12-open-questions.md](12-open-questions.md) as usual.

## 0. What was asked for

Creator direction, verbatim: *"create a system where the user can play
specific races. It should be integrated throughout the game and be
selectable on the game startup. The menu should allow the user to choose
race and ai opponents, then enable a begin match button. This is to be
part of a match system, make it robust and easily expandable."* Follow-up
clarification: 1-4 AI opponents (not just one), and the AI opponents must
**actually play the match** — attack, produce units — with distinct
personalities, documented.

An investigation before any code was written found that a race picker
already existed from an earlier session (`FactionPickerHud`,
`OpponentFactionPickerHud`), but as two separate full-screen pickers that
each committed immediately on click rather than one combined menu with a
summary and a Begin Match button, hardcoded to exactly one AI opponent,
with that opponent's `SkirmishCommander` decision engine fully built and
tested but never actually instantiated in a live match — an AI player got
a race, a base, and a one-time starting army, then sat inert forever. This
doc covers closing all of that in one pass.

## 1. Data model — `PlayerSetup` (match-core, not Unity-side)

`packages/match-core/src/PlayerSetup.cs` is a new readonly struct:
`{ FactionId Faction, bool IsAiControlled, CommanderPersonality?
Personality }`, with `PlayerSetup.Human(faction)` and
`PlayerSetup.Ai(faction, personality)` factories (the latter requires a
personality — an AI slot can never silently go bland by omission).

`PlayerState` (`packages/match-core/src/PlayerState.cs`) grew two new
get-only properties, `IsAiControlled` and `AiPersonality`, threaded through
a backward-compatible constructor (new params default to `false`/`null`,
so `Clone()` and every existing call site needed only a one-line update,
not a rewrite). **Neither field is written into `WriteTo`'s tick hash** —
`CommanderPersonality`'s own header already documents itself as "DATA,
never simulation state... not part of the tick hash," and `IsAiControlled`
is the same category one level up: it decides which EXTERNAL process
submits commands, but has zero influence on `MatchState.Tick`'s own
deterministic math once a command arrives, exactly like `FactionId`
(hashed) differs from personality (not hashed) — `IsAiControlled` sits on
the not-hashed side of that line for the same reason personality does.

`MatchState.Create` was split into two overloads instead of changed
in-place: the new `Create(seed, IReadOnlyList<PlayerSetup>, city)` does the
real work; the original `Create(seed, IReadOnlyList<FactionId>, city)` is
now a 4-line wrapper that builds an all-`PlayerSetup.Human` list and calls
the new overload. Every one of the 302 pre-existing match-core tests
compiles and passes unchanged — this is additive, not a breaking change to
an existing signature.

**match-core needed zero changes to support more than 2 players.**
`MatchState.Create` already validated `factions.Count` in `[2, 8]` ("2..8
players (1v1..4v4)") before this epic touched it, sized for lockstep 4v4
netcode from day one (docs/23 §11). Combat/threat targeting was already
FFA-shaped: `ThreatMap.From` and `SkirmishCommander`'s own target loop
both already treat "a different `PlayerIndex`" as hostile — grepped for
`1 - playerIndex`/`otherPlayer`/`Player(0)`/`Player(1)` literals across all
of match-core and found zero hits. 1 human + up to 4 AI (5 total) is a
clean subset of an already-designed range, not a special case that needed
new sim-core plumbing.

## 2. AI driver loop — `AiMatchDriver` (match-core)

The tick loop itself needed no new hook: `MatchState.Tick(IReadOnlyList
<Command>? commands)` already takes one flat bundle per tick, keyed by
`Command.PlayerIndex` inside each entry — an AI's commands are
indistinguishable from a human's once queued, exactly as
`SkirmishCommander`'s own header already anticipated ("one peer runs the
commander and its orders replicate like anyone else's").

`AiMatchDriver` (`packages/match-core/src/AiMatchDriver.cs`) is the
missing per-tick caller: built once per match, it constructs one
`SkirmishCommander` + one `ProductionAdvisor` per `IsAiControlled` player
slot (using that slot's own `AiPersonality`), and `DecideCommands(match)`
combines both command sources across every AI player into one list. Pure
match-core — no `UnityEngine` dependency — so the whole loop is
`dotnet test`-able end to end (seed in, thousands of ticks, assert on the
resulting `MatchState`) with zero Unity/flightcheck involvement, and
directly reusable by a future headless dedicated server or bot-fill
feature.

`SimBridge.cs` (Unity) gained a matching `StartMatch(seed,
IReadOnlyList<PlayerSetup>, city)` overload (the old `FactionId`-list
overload now forwards to it, same wrapper pattern as match-core's own
split) which also builds an `AiMatchDriver`, and a `QueueCommands`
bulk-add method. `Pump`'s existing fixed-timestep tick loop now asks the
driver for commands immediately before each `_match.Tick(...)` call — same
pending buffer, same one-tick latency, same lockstep contract every
`QueueXCommand` wrapper already used for human input. An all-human match's
`AiMatchDriver` has zero commanders, so this costs one `bool` read per
tick and nothing else — every scene that doesn't opt in is unaffected.

## 3. Production AI — `ProductionAdvisor` (match-core)

This closes docs/23 §13 amendment D's explicitly deferred "production/
build-order AI" phase. `SkirmishCommander`'s own header used to say *"match
-core has `CommandKind.BuildStructure`... but no unit-PRODUCTION command
whatsoever"* — that was true when written, but `CommandKind.TrainUnit` +
`MatchState.ApplyTrainUnit`/`CanTrainUnit` shipped later in the
worker-economy epic and simply never got scoring logic pointed at them (an
investigation found **zero** call sites for `SimBridge.QueueTrainCommand`
anywhere in Unity — the human player's own production today is a
completely separate, non-lockstep-safe clone-drag mechanic in
`GrabCursor.cs` that never goes through `Command` at all; a
pre-existing gap this epic did not attempt to fix, only flagged).
`ProductionAdvisor` is deliberately built against the REAL
`Command`-pipeline path (`TrainUnit`/`BuildStructure`), not against the
clone-drag mechanic, specifically so it stays lockstep-safe.

`ProductionAdvisor` (`packages/match-core/src/ProductionAdvisor.cs`) mirrors
`SkirmishCommander`'s own shape: a command *source*, not part of the
simulation, `DecisionIntervalTicks` derived from personality, deterministic
given `(state, personality, own RNG stream)`.

**Personality mapping** (each trait either has a concrete translation or is
explicitly left unused, never faked):

| Trait | Drives |
| --- | --- |
| Aggression | Target standing-army size, as a fraction (0.4-0.9) of `SupplyCap` — never claims the full cap, respecting docs/23 §13-E's 20-40-units/60-cap design target |
| Greed | What fraction (0.2-0.8) of the current wallet a training decision commits to a shopping list |
| Territoriality | How often ([0,1] roll) a decision goes to expansion (`BuildStructure`) instead of training |
| Discipline | Decision cadence (20-60 ticks) — same direction `SkirmishCommander` already uses it: low = twitchy, high = commits longer |
| Caution, Opportunism | **Deliberately unused here** — same "don't invent a mapping without a real translation" discipline `ArmyGenerator`'s own header already states for its own narrower Aggression/Caution-only use of personality |

Unit choice reuses `ArmyGenerator.Generate` (a weighted-knapsack shopping
list scored by the SAME Aggression/Caution the combat commander uses) —
not reinvented — filtered against each idle Factory's own
`CanTrainUnit` check, so live wallet state (not a stale budget snapshot)
gates every actual `TrainUnit` command. Expansion does a ring search
(`HexCoord.Ring`, the same public method `MatchState`'s own private
`FindOpenHexNear` already walks) around the player's HQ for a hex
`CanPlaceBuilding` genuinely accepts, from a fixed v0.1 preference order
(storage/economy kinds before `Defense`). `ArmyGenerator.Generate` throws
for MadDoctor/Mixed (no roster data exists for either — see its own
header); `ProductionAdvisor` catches that and simply never trains for
those factions rather than propagating the exception, which is also why
AI opponents are restricted to HumanArmy/AlienHive in the menu (§6).

Verified in `Tests~/ProductionAdvisorTests.cs`: never emits an
unaffordable `TrainUnit`; spends the wallet down and fields units when
funded; never pushes `SupplyUsed` past `SupplyCap` over 6000 ticks; two
advisors built from the same seed produce byte-identical command streams
tick-for-tick (the replay-safety property this whole design depends on);
a MadDoctor-faction advisor never throws, just never trains.

## 4. N-player spawn placement (Unity)

`RuntimeCityBuilder.SpawnStartingBases` generalized from two hardcoded
players (0 and a single fixed offset `(center.Q+18, center.R-9)` for
player 1) to a loop over every configured player. `AiOpponentSeedRing`
computes one seed point per AI opponent, evenly spaced on a ring around
the human's own city-center start (v0.1 placeholder radius, 20 hex units —
same order of magnitude as the original fixed offset, flagged, not claimed
balanced); `FindOpenHexWide`'s existing ring-search then places the actual
HQ/Factory hex from each seed point, unchanged.

**Load-bearing invariant**: the SAME `CommanderPersonality` value must
drive both an opponent's starting army composition
(`SpawnOpponentStartingArmy`, via `ArmyGenerator.Generate`) and its
in-match behavior (`AiMatchDriver`, via `PlayerSetup.Ai`) — a
"Berserker"-labeled opponent fielding a Turtle-weighted starting army would
be a real bug, not a style nitpick. `RuntimeCityBuilder.BeginMatch` is the
one place that resolves each AI opponent's personality (from
`MatchSetupHud`'s config, or "Random" resolved once at menu-confirm time)
and reuses that exact value for both calls — there is no second,
independent roll anywhere in this pipeline.

`SpawnOpponentStartingArmy`'s own RNG seed folds in `playerIndex`
(`seed ^ (playerIndex * 0x9E3779B1)`, same decorrelation formula
`AiMatchDriver` uses for its own per-player streams) so that 2+ opponents
resolved to "Random" don't draw from the identical stream and field
identical armies. **This is a deliberate, flagged scope note, not a silent
regression**: it means the DEFAULT single-fallback-opponent case (every
scene/test that never touches `MatchSetupHud`) now draws a different
starting-army composition for the same `seed` than it did before this
epic, even though the FACTION-SELECTION rule (Army, or Hive if the human
picked Army) is unchanged. The alternative — special-casing the fallback
path to keep the old unfolded seed — was considered and rejected as extra
complexity for a cosmetic-only difference (which specific units an AI
starts with), not a correctness regression.

## 5. Combined menu — `MatchSetupHud` (Unity)

Replaces `FactionPickerHud`/`OpponentFactionPickerHud` (both deleted) with
one screen: a race row (MadDoctor/HumanArmy/AlienHive/Mixed, Mixed gated by
`MixedFactionUnlock.IsUnlocked`, same swatch-button layout as the old
`FactionPickerHud`), 1-4 AI opponent slots (each a race cycle-button
restricted to HumanArmy/AlienHive/Random and a personality cycle-button
over `CommanderPersonality.Archetypes` plus Random), Add/Remove Opponent
buttons gated at the 1-4 range, and a Begin Match button. Same conventions
every picker in this family already established: `UiScale`-scaled IMGUI,
opt-in via `RuntimeCityBuilder.showMatchSetupHud` (default `false`),
chains forward into the UNCHANGED `RegionPickerHud` or straight to
`BeginMatch` on confirm, self-`Destroy`s once confirmed.

AI opponent races are restricted to HumanArmy/AlienHive (never MadDoctor/
Mixed) — not a new restriction, but a pre-existing one (`ArmyGenerator` has
no roster data for either, see §3) surfaced honestly in the menu rather
than offered as an option that would silently field nothing.

`RuntimeCityBuilder.BeginMatch` resolves the empty-config case (no
`MatchSetupHud`, `aiOpponents` list empty) the same way it always did: one
default opponent, Army or Hive depending on the human's own pick — every
existing scene that never opts in keeps working, just with the one flagged
RNG-seed caveat in §4.

## 6. Constraints honored, not reinvented

- **docs/23 §13-E**: 60 supply cap, 20-40 units/player at scale —
  `ProductionAdvisor`'s Aggression-derived target supply fraction respects
  this range rather than inventing new army-size numbers.
- **docs/23 §11**: `MatchState.Create`'s 2-8 player range was sized for
  lockstep 4v4 netcode from the start; 1 human + up to 4 AI is a proper
  subset, not a special case.
- **docs/23 §13 amendment D**: explicitly deferred production/build-order
  AI as its own phase once its prerequisite (a unit-production command)
  existed. `ProductionAdvisor` is that phase, now unblocked.
- **CLAUDE.md's determinism invariant**: every RNG draw in this epic (
  `AiMatchDriver`'s per-player seed folding, `ProductionAdvisor`'s own
  stream, `MatchSetupHud`'s Random-resolution) goes through `SimRng`/
  `CommanderPersonality.Generate`, never `Math.Random`/`UnityEngine.Random`.

## 7. Known gaps / explicitly deferred (not silently skipped)

- **`CommandKind.TrainUnit` still has zero human call sites.** The human
  player's own production remains the non-lockstep-safe `GrabCursor.cs`
  clone-drag mechanic. `ProductionAdvisor` proves the real command path
  works; wiring the human's own UI to it is separate, out-of-scope work.
- **`RosterUnitKind` units still have no Unity-side visual** (a
  pre-existing gap `SimBridge.SpawnRosterUnit`'s own doc comment already
  flags) — AI-trained/spawned units exist in the simulation (queryable,
  fightable, salvageable) but are invisible in the 3D scene.
- **No difficulty axis.** `CommanderPersonality` is a flavor/style dial
  (Berserker vs. Turtle), not a skill dial — a genuinely "easier" or
  "harder" AI opponent is a separate, unbuilt feature.
- **No teams/alliances.** Every player is mutually hostile (FFA), matching
  match-core's existing "any different PlayerIndex is hostile" shape —
  2v2/3v1 style team play is out of scope for this epic.
- **`BuildGhostCursor.RequiresWorker` only gates `BuildingKind.Factory`**,
  and `CanPlaceBuilding` enforces no worker-ownership check for any kind at
  the match-core level — so an AI's `BuildStructure` commands for storage/
  harvest-post/defense buildings aren't blocked by the (Unity-only,
  non-lockstep) worker-gate a human's own placement UI enforces. Low-impact
  today (AI opponents already start with a Factory via
  `SpawnFactoryForPlayer`), flagged rather than silently accepted.

## 8. Verification

- `dotnet test packages/match-core/Tests~/MatchCore.Tests.csproj`: 311/311
  passing (302 pre-existing + 9 new — `ProductionAdvisorTests.cs`,
  `AiMatchDriverTests.cs`), including every pre-existing test file
  unmodified and green.
- Flightcheck (scratch harness compiling real project files against the
  real `MadDr.MatchCore.dll`/`MadDr.CityGen.dll` plus a hand-maintained
  `UnityStub.cs`): zero errors trace to `MatchSetupHud.cs`, `SimBridge.cs`,
  or any touched line of `RuntimeCityBuilder.cs` — the only errors present
  are pre-existing, unrelated harness drift in files this epic never
  touched (`DamageFx.cs`, `MonsterAgent.cs`, and older lines of
  `RuntimeCityBuilder.cs` this epic didn't touch), the same drift flagged
  in multiple prior docs/12 entries this session.
- **Not verified**: actual in-Editor menu rendering/interaction, or a real
  Play-mode match with an AI opponent actually fighting. No Unity Editor
  exists in this environment — this is the same standing limitation every
  prior Unity-side change this session has carried, stated explicitly
  rather than claimed.

## 9. Follow-up (2026-08): all four races + player-relative army sizing

Creator direction, verbatim: *"the roster needs to be able to generate
enemies for all races. They should take the number of units from the
player, so armies are fairly balanced amongst all ai units and players."*
Two closely related but separate gaps, both closed in this pass:

**All four races now have real roster data.** `ArmyGenerator`/
`FactionRoster.cs` previously only supported HumanArmy/AlienHive — a
MadDoctor or Mixed AI opponent could never generate a starting army or
make training decisions, and §5/§6 above documented that as a real,
pre-existing constraint rather than an oversight. It's closed now:

- `FactionRoster.cs` gained three new `RosterUnitKind` entries for
  MadDoctor (`ShamblingGrunt`/`SporeBrute`/`Abomination`, Blood+Bones
  costed, same cheap-swarm/mid-line/heavy shape every other roster
  already has). **These are a flagged v0.1 stand-in, not the Doctor's
  real identity** — the Doctor's actual creatures are custom bred
  through the Mutator (docs/06/07), and match-core has zero reference to
  genome-core (a repo invariant). A real genome → match-core stat bridge
  is a separate, larger job; until it exists, an AI-controlled Mad
  Doctor opponent needed SOMETHING to field, so it gets three generic
  "off-the-rack" horrors instead.
- `ArmyGenerator.RosterFor` now returns the UNION of every faction's
  roster for `FactionId.Mixed`, reusing (not duplicating) the exact
  per-unit `RaceOverride` contract `MatchState.SpawnRosterUnit`/
  `CanTrainUnit` already gave a human Mixed player (`MixedFactionTests.cs`,
  §1 in this doc's own §1 predates this AI-side use). `Generate`'s own
  knapsack loop needed zero changes — it always treated "the roster" as
  an opaque list.
- `MatchSetupHud.AiFactionChoices` (now an instance field,
  `_aiFactionChoices`, built in `Init()`) widened from HumanArmy/AlienHive
  to all four, with Mixed gated behind `MixedFactionUnlock.IsUnlocked`
  for AI opponents too, matching the human's own race row.
- `RuntimeCityBuilder.OpponentStartingArmyBudget` gained a Blood line
  (previously Bones/Fuel/Ichor only) so a MadDoctor/Mixed opponent's
  starting-army budget can actually afford its own roster's currency.

**AI army sizing now reads the human player's live unit count, not just
a fraction of the AI's own SupplyCap.** `ProductionAdvisor.DecideCommands`
used to compute its target standing-army size purely from
`player.SupplyCap * (0.4 + Aggression * 0.5)` — self-referential, blind
to what anyone else had actually fielded. It now also computes the
strongest non-AI player's live unit count and folds that in:
`targetSupply = min(SupplyCap, max(capBasedTarget, humanUnitCount *
(0.8 + Aggression * 0.4)))` — a Turtle personality merely matches the
human's count, a Berserker overshoots it by up to 40%, and the result is
always still clamped to the AI's own SupplyCap.

**A real bug surfaced and fixed along the way, not a new one
introduced**: `PlayerState.SupplyUsed` — the field the old target-supply
gate checked against — turned out to be permanently 0. Nothing in
match-core outside its own test file ever calls
`PlayerState.AddSupplyUsed`; units join a player's army without ever
touching that counter. This meant the OLD gate (`SupplyUsed <
targetSupply`) was a silent no-op that could never actually stop
training, regardless of army size — the "don't claim the full cap"
design intent §3/§6 describe was never really enforced. Rather than wire
up sim-wide supply accounting (a larger, separate job touching every
spawn/death path), `ProductionAdvisor` gained a narrow `LiveUnitCount`
helper (a plain scan of `MatchState.UnitAt`, filtered by `PlayerIndex`
and `IsAlive`) and uses that for BOTH the new human-relative target and
its own gate — the same real number, not the dead field. Flagged, not
silently left broken.

**Known limitation, disclosed rather than solved here**: this balancing
only sees whatever is actually registered as a `SimUnit` in match-core.
Today, a human Mad Doctor player's own creatures — spawned through
`RuntimeCityBuilder.SpawnMonster`/`GrabCursor`'s clone-drag mechanic —
are pure Unity `MonsterAgent` GameObjects and are never registered into
match-core's `SimUnit` list at all (the same gap §7's "CommandKind.
TrainUnit still has zero human call sites" already flagged, one level
deeper). Until that mirroring exists, `humanUnitCount` degrades
gracefully to 0 for a human playing Mad Doctor specifically, and the AI
falls back to the pre-existing cap-based target — never worse than
before this pass, just not yet balanced against that specific case.
HumanArmy/AlienHive/Mixed roster units trained through the real Command
pipeline (today: only AI does this) are counted correctly.

Verified via `packages/match-core/Tests~/ArmyGeneratorTests.cs` (new
MadDoctor/Mixed coverage: budget respected, faction purity, Mixed spans
multiple real factions across seeds) and
`packages/match-core/Tests~/ProductionAdvisorTests.cs` (new: MadDoctor
advisor fields real units when funded in Blood/Bones; an AI trains
toward a LARGER target when the human already has more units fielded;
the SupplyUsed-tautology test replaced with a real live-count
assertion). **No dotnet SDK in this environment** — same standing
limitation as every other match-core change this session; verified by
careful manual review (brace/paren balance, full re-read of every
touched method against real call signatures) rather than a real test
run. The next session with a working `dotnet test` should run
`MatchCore.Tests.csproj` before trusting this pass fully green.

## 10. Follow-up (2026-08): a real Difficulty axis

Creator direction, verbatim: *"Make sure we can scale the ai
intelligence for Difficulty. So that in tutorial and early levels
players can get a sense of achievement, and accomplishments, this needs
to be challenging enough without being too easy."* This is the exact gap
§7 flagged when the AI-opponent epic first shipped — *"No difficulty
axis. `CommanderPersonality` is a flavor/style dial (Berserker vs.
Turtle), not a skill dial... a genuinely 'easier' or 'harder' AI
opponent is a separate, unbuilt feature"* — now built.

**New type, `packages/match-core/src/AiDifficulty.cs`.** `AiDifficulty`
(`Tutorial < Easy < Normal < Hard < Brutal`) plus `AiDifficultyProfile`,
a static per-level lookup table (`AiDifficultyProfile.Get(level)`, same
"static data, `Get` by enum" convention as `FactionDef`/`UnitRosterDef`)
of four multipliers:

| Multiplier | Applies to | Direction |
| --- | --- | --- |
| `ReactionMultiplier` | `SkirmishCommander`/`ProductionAdvisor`'s discipline-derived decision interval | >1 slower, <1 faster |
| `EconomyMultiplier` | `ProductionAdvisor`'s per-decision wallet-spend fraction | scales Greed's 0.2-0.8 range, clamped to [0,1] |
| `ArmySizeMultiplier` | `ProductionAdvisor`'s final target standing-army size (both the SupplyCap-fraction floor and the player-relative balance target added in §9) | re-clamped to SupplyCap after scaling |
| `StartingArmyMultiplier` | `RuntimeCityBuilder.OpponentStartingArmyBudget` | scales the opening-force budget before `ArmyGenerator.Generate` sees it |

Normal is the identity (every multiplier 1.0) — a difficulty-unaware
call site behaves byte-identically to before this feature existed.
Deliberately narrow, matching `ArmyGenerator`'s own "only touch what has
a real translation, leave the rest alone" discipline: **Difficulty never
touches `CombatStats`** (a Tutorial opponent's units hit exactly as hard
and die at exactly the same health as a Brutal opponent's — the
difference is entirely in how FEW of them there are and how SLOWLY they
show up, never a hidden stat nerf) **and never touches personality's own
scoring weights** (a "Reckless" opponent reads as reckless at every
difficulty, just executed better or worse). This is the direct answer to
the brief's "challenging enough without being too easy": Tutorial is
still a live, reacting opponent — it trains, expands, and fights back —
just visibly slower and thinner, never the inert do-nothing bug §0
documents from before this whole epic existed.

**Threading, mirroring exactly how `CommanderPersonality` already
threads through this same pipeline:** `PlayerSetup.Ai(faction,
personality, difficulty = Normal)` -> `PlayerState.AiDifficulty` (never
hashed, same "setup data, not simulation state" category as
`AiPersonality`/`IsAiControlled`) -> `AiMatchDriver` passes it to both
`new SkirmishCommander(i, personality, difficulty)` and `new
ProductionAdvisor(i, personality, seed, difficulty)`. Unlike
`AiPersonality` (nullable, required-when-AI), `AiDifficulty` is a plain
non-nullable field defaulting to Normal everywhere — silently defaulting
is fine here (Normal is a genuinely reasonable default, unlike
`CommanderPersonality.Balanced`, which is explicitly "not recommended as
an opponent").

**Reaction speed** (`SkirmishCommander.DecisionIntervalTicks`/
`ProductionAdvisor.DecisionIntervalTicks`): both already derived their
interval from `Discipline`; the difficulty multiplier now scales that
result, floor-clamped at each class's own `MinDecisionIntervalTicks` but
deliberately NOT ceiling-clamped back down to the old personality-only
`MaxDecisionIntervalTicks` — Tutorial needs real headroom above that
ceiling to read as meaningfully slow rather than merely "as slow as a
methodical Normal commander already was."

**Unity side:** `RuntimeCityBuilder.AiOpponentConfig` gained a
`Difficulty` field (defaults to Normal, additive to every existing call
site); `SpawnOpponentStartingArmy` scales `OpponentStartingArmyBudget`
by it before generating. `MatchSetupHud` gained a fourth per-opponent-row
button (Tutorial/Easy/Normal/Hard/Brutal, no "Random" — unlike
faction/personality, a player picking a difficulty would never want it
left to chance), panel widened 460->560 to fit it without cramping the
existing Faction/Personality buttons.

**A real, separate bug found and fixed in passing, not part of this
feature's own scope:** `SpawnStartingBases`'s call to
`SpawnOpponentStartingArmy` was still gated `if (faction == HumanArmy ||
faction == AlienHive)` — a leftover from before §9's all-races pass,
which made ArmyGenerator support all four factions but never noticed
this SEPARATE Unity-side guard still excluded MadDoctor/Mixed opponents
from ever getting a starting army at all. Removed; every faction now
gets one, unconditionally, matching what §9 already claimed was
possible.

Verified via new `AiDifficultyTests.cs` (profile numbers: Normal is the
identity, every level distinct, monotonic ordering in the direction the
enum name implies) plus new coverage in `CommanderTests.cs`
(Normal-with-explicit-difficulty reproduces the no-difficulty default
exactly; reaction speed orders correctly across all five levels for a
fixed personality) and `ProductionAdvisorTests.cs` (a Brutal advisor
fields at least as many units as a Tutorial one, genuinely more, not
just tied; reaction-speed and default-reproduction checks mirroring
`CommanderTests.cs`). **No dotnet SDK in this environment** — same
standing limitation as every other match-core change this session,
verified by manual review, not a real test run.
