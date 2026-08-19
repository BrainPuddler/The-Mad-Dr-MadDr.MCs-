# 37 — Win/Loss States

Status: **Implemented (2026-08)**. Per-decision rationale that predates or
extends it lives in [12-open-questions.md](12-open-questions.md) as usual.

## 0. What was asked for

Creator question, then direction: **"What is the win/loose [sic] states?"**,
followed by a status audit finding no end-condition system existed anywhere
in the codebase (match-core never checked for a winner; Hq destruction had
zero gameplay consequence; no Unity game-over screen). Creator direction:
**"Yes do that"** — build it.

[02-gameplay-overview.md](02-gameplay-overview.md) already specified three
victory conditions:

1. **Vat destruction** — destroy the enemy Vat (in code: `BuildingKind.Hq`;
   "Vat" is the Mad Doctor's own fictional name for it, used generically in
   the design doc for every faction's HQ).
2. **Dominion** — control ≥60% of emitters continuously for one full Lumen
   Cycle (4 min).
3. **Time** — at the 15-minute cap, higher territory score (hexes
   controlled + emitters held, weighted) wins.

[23-rts-master-build-plan.md](23-rts-master-build-plan.md) and
[12-open-questions.md](12-open-questions.md) (2026-07 entries) had both
already flagged this as a real, explicit gap: emitter capture/ownership/mana
(Dominion's own prerequisite) were built, but "the Dominion victory timer —
needs a match end-condition system that doesn't exist at all."

## 1. `MatchState.CheckMatchEnd` (match-core)

The last step of every `Tick()` call, after that tick's combat/emitter/
frame-advance work is fully settled. Checks the three conditions in a fixed
priority order — Elimination first (most final), then Dominion, then the
time cap — since more than one could theoretically fire the same tick and
only one verdict can stand.

**1. Elimination**, generalizing "Vat destruction" for N-player FFA
(docs/23 §11's own 2-8 player range, not just 1v1): a player is eliminated
the instant their own `Hq` building is found `Destroyed`
(`PlayerState.IsEliminated`, permanent once set). The match ends the moment
only one non-eliminated player remains (that player wins) or zero remain (a
draw, from a simultaneous mutual wipeout on the same tick). For the common
1v1 case this collapses to exactly "destroy the enemy Vat, you win," matching
docs/02's own framing word for word. A `MatchState` that never spawns an Hq
for any player at all (the overwhelming majority of this project's own
existing match-core tests) simply never satisfies this condition — nothing
requires an Hq to exist, only reacts if one that existed got destroyed.

**2. Dominion**: `PlayerState.DominionStreakTicks`, a new per-player counter
incremented every tick a player holds ≥60% of `MatchState.EmitterCount`
(integer math, `owned * 100 >= total * 60`, never a float compare) and RESET
to 0 the instant they drop below it. When a player's streak reaches
`LumenClock.CycleTicks` (2400 ticks, 4 minutes), they win outright. **This is
a genuinely different rule from `SimEmitter`'s own per-emitter capture-
contest freeze** ("an enemy unit in the aura freezes progress, doesn't reset
it") — easy to conflate since both live in the same emitter system, but
Dominion's own "continuously" wording (docs/02) means the AGGREGATE
majority-holding streak resets on any drop, unlike an individual capture
attempt's contest-freeze. Two different players can never both cross 60% of
the same fixed total at once (60+60 > 100), so Dominion can never produce a
tie.

**3. Time cap**: at `MatchState.TimeCapTicks` (15 minutes, 9000 ticks), the
still-active player with the higher `TerritoryScore` wins; an exact tie is a
draw. **`TerritoryScore` is a flagged v0.1 placeholder**, same standing
policy as every other unsourced number in this project
(`FactionRoster.cs`'s own header): this codebase has no hex-ownership grid
at all — only building/unit positions and emitter capture are real,
trackable "territory" — and docs/02 never gives an exact weight for "hexes
controlled + emitters held, weighted." The placeholder: an emitter held
counts 3x a Complete building, since Dominion's own 60%-of-emitters rule
already establishes emitters as this design's real "important" territory
unit. Exposed public (`MatchState.TerritoryScore(playerIndex)`), not just an
internal step of `CheckMatchEnd`, so a live HUD could show a running
readout before the cap actually fires (not built yet — see §4).

**A real, separate bug found and fixed while building this**:
`ProductionAdvisor`'s own pre-existing target-army-size gate checked
`PlayerState.SupplyUsed` — a field nothing in match-core outside its own
test file ever calls `AddSupplyUsed` on, so it sat at 0 forever (fixed in
the previous 2026-08 pass, docs/12's own entry). Not directly related to
win/loss, noted here only because `CheckMatchEnd` is the second place this
session found a genuinely dead simulation field and had to route around it
rather than trust it.

## 2. Determinism

`IsMatchOver`/`WinnerPlayerIndex`/`EndReason` are real simulation-derived
facts — a replay reaching the same tick must recompute the identical
verdict — so all three are hashed in `MatchState.Hash()`, and
`PlayerState.IsEliminated`/`DominionStreakTicks` are hashed in
`PlayerState.WriteTo`, same "DATA that affects the outcome gets hashed"
contract every other piece of real simulation state in this project already
follows (as opposed to `AiPersonality`/`AiDifficulty`, setup data with zero
influence on `Tick`'s own math, which stay deliberately un-hashed).

Once `IsMatchOver` is true, `Tick()` becomes a no-op — checked first thing,
before even processing commands — so a caller (`SimBridge`'s own fixed-
timestep `Pump` loop) can keep calling `Tick` harmlessly forever after
game-over; `Frame`, `CommandsProcessed`, and the verdict itself never change
again.

## 3. Unity side

`SimBridge` gained read-only accessors mirroring `MatchState`'s new surface
(`IsMatchOver`, `WinnerPlayerIndex`, `EndReason`, `TerritoryScore(i)`) and an
early return in `Pump` once the match is over (not required for correctness
— `Tick` itself already no-ops — just avoids running the fixed-timestep
accumulator loop pointlessly every frame forever after game-over).

New `MatchEndHud.cs`: invisible the entire match, then a full-screen dark
modal overlay the instant `SimBridge.IsMatchOver` goes true — VICTORY/DEFEAT/
DRAW from the local player's own perspective (never a raw player-index
number), a one-line reason in plain language per `EndReason`, and a "Play
Again" button. Same self-contained IMGUI idiom every HUD in this project
already uses (own `DrawShadowedLabel`, no shared utility class), styled after
`MatchSetupHud`'s own dark-panel-plus-buttons look. Wired into
`RuntimeCityBuilder.BeginMatch` alongside every other always-on HUD
(`ResourceHud`, `BuildingNavHud`, etc.).

**"Play Again" reloads the active scene**
(`SceneManager.LoadScene(SceneManager.GetActiveScene().name)`) rather than
hand-writing a manual reset path through this project's dozens of live HUD/
economy/monster/city components — none of them expose a `Reset()` method,
and a scene reload is the standard, safe way to guarantee every piece of
state (match-core's `MatchState` included, since the reload re-runs
`BeginMatch` from scratch) actually returns to a clean start, rather than
risking a half-reset scene with stale GameObjects left over from the
finished match. This is the FIRST use of `UnityEngine.SceneManagement`
anywhere in this project (confirmed by grep) — a deliberate, minimal-new-
surface choice, not an oversight.

## 4. Known gaps / explicitly deferred, not silently skipped

- **No live territory-score or Dominion-streak HUD.** Both are exposed as
  real, queryable `MatchState`/`SimBridge` methods (§1, §3), but nothing
  draws them yet — a player has no visibility into "how close is the enemy
  to a Dominion win" or "who's ahead on territory" before the moment either
  condition actually fires. `MatchEndHud` only appears at the very end.
- **`TerritoryScore`'s weighting is unsourced** (§1) — flagged, not a claim
  of balance.
- **AI opponents don't reason about winning.** `SkirmishCommander`/
  `ProductionAdvisor` (docs/30) still only ever fight/train/expand — neither
  reads `TerritoryScore`, chases Dominion deliberately, or defends its own
  Hq with any special urgency once threatened. A "the AI knows it's about to
  lose and panics" layer is a separate, unbuilt feature.
- **No spectator/observer perspective.** `MatchEndHud` is hardcoded to one
  `localPlayerIndex` (0, matching every other single-player-perspective HUD
  in this project) — a hypothetical local-multiplayer or replay-viewer mode
  would need a different reporting surface.

## 5. Verification

`packages/match-core/Tests~/MatchEndTests.cs` (new): 2-player elimination
end-to-end, simultaneous mutual elimination (draw), 3-player FFA (one
elimination doesn't end the match, last-standing wins), matches that never
spawn an Hq never spuriously eliminate anyone, `Tick` becomes a true no-op
once over, a real Dominion win via actual emitter capture (the same
"`SpawnUnit` directly onto the emitter hex, tick `CaptureChannelTicks`
times" technique `EmitterTests.cs` already established), a genuine
ownership hand-off proving the Dominion streak RESETS rather than freezes
(reusing `EmitterTests.cs`'s own proven `TeleportUnit`/`FindFarOpenHex`
reflection helpers to reposition a unit off a blocked-to-ground landmark
hex, the same documented gap that test suite already works around), a
time-cap win via a real territory-score gap, a tied time-cap draw, and a
Hash()-level determinism check across two identically-constructed matches.

**No .NET SDK in this environment** — same standing limitation as every
match-core change this session; verified by careful manual review (brace/
paren balance, full re-read of every touched method against real call
signatures and the mechanics `EmitterTests.cs` already proved work) rather
than a real `dotnet test` run. **No Unity Editor in this environment** —
`MatchEndHud`'s actual on-screen appearance, layout, and the "Play Again"
scene-reload button are unverified beyond compile-level review; see
docs/36's own pending-verification checklist, which gained a matching entry.

## 6. Follow-up (2026-08): a live win-progress HUD

Creator direction, verbatim: *"Provide a hud above the mini map showing
win game % as red and green text. For all three win game states."*
`MatchEndHud` (§3) only appears once a match is already decided --
nothing showed the player how they were doing on any of the three
conditions WHILE the match was still live. New `WinProgressHud.cs`
closes that: three rows (Army / Dominion / Territory), docked directly
above `Minimap`'s own live `ScreenRect` (same "read a neighbour's rect,
dock against it" convention `SelectionHud`/`RecallHud` already use for
that corner), each a percentage in bold red-or-green text.

**None of these are a real modeled win probability** -- no AI/statistical
model exists anywhere in this project for that, and this pass didn't
invent one. Each is a simple, honest SHARE metric built entirely from
state `MatchState`/`SimBridge` already expose, same "flagged v0.1
heuristic, not claimed balanced" policy as `TerritoryScore`'s own
weighting (§1):

- **Army %** (an Elimination proxy): the local player's own live unit
  count as a share of (itself + the single strongest opponent's live
  unit count) -- the same intuition as any RTS "army value" comparison.
  Computed client-side in the HUD itself, tallying `SimBridge.UnitAt`
  by `PlayerIndex`/`IsAlive` -- no new match-core surface needed, since
  unit iteration was already public.
- **Dominion %**: `PlayerState.DominionStreakTicks` as a share of the
  full `LumenClock.CycleTicks` hold a Dominion win requires. Reads 0%
  the instant the local player drops under 60% emitter control -- the
  streak itself resets then (§1), not a display bug. New `SimBridge.
  PlayerDominionStreakTicks(playerIndex)` accessor, mirroring the
  existing `PlayerMana` pattern exactly.
- **Territory %**: the local player's own `MatchState.TerritoryScore`
  (already exposed via `SimBridge.TerritoryScore`, §3) as a share of
  (itself + the strongest opponent's) -- same shape as Army %, but the
  actual number that decides the time cap.

A new `SimBridge.PlayerCount` accessor (mirroring the bounds-check
`PlayerFaction` already did internally) lets the HUD loop every opponent
to find the strongest one for both share metrics. 50% is the neutral
default whenever a share's denominator is exactly 0 (e.g. before either
side has fielded a unit or captured any territory) -- reads as neither
meaningfully green nor red, which is the honest state of "no data yet"
rather than an arbitrary pick.

Invisible once `SimBridge.IsMatchOver` goes true -- `MatchEndHud` takes
over at that point, and a live "still climbing toward Dominion" readout
stops meaning anything once the match is already decided.

**No Unity Editor in this environment** -- `WinProgressHud`'s actual
on-screen docking against a real `Minimap.ScreenRect`, its readability
at real HUD scale, and whether the three percentages feel meaningful in
an actual match are all unverified beyond compile-level review. Added to
docs/36's pending-verification checklist.
