# 32 — HUD / UI System: conventions, faction identity, thumbnail icons

**Status: Living.** Read this first, not the rest of this doc suite's
scattered `docs/12` entries in order, when picking up any IMGUI HUD work
in `unity-client/Assets/Scripts/*Hud.cs` (15 scripts as of this
writing, plus `HudStatus.cs` and `Minimap.cs`, which don't follow the
`*Hud.cs` naming pattern but are the same kind of IMGUI overlay). This
doc is the condensed, current-state reference;
`docs/12-open-questions.md` has the full round-by-round reasoning for
everything summarized here (search it for "faction skin"/"thumbnail
icons"/"Minimap legibility" for the source entries). Companion docs:
`docs/28-city-lighting-system.md` (same "living doc, read first" shape,
for the lighting system) and `docs/31-faction-building-architecture.md`
(the PARALLEL system for `BaseDresser.cs`'s 3D-world faction buildings —
**not** this doc's concern; see §4 below for exactly where the line is).

No Unity Editor exists in this environment. Nothing described here as
"shipped" has been seen rendering — every fix is reasoned from code
review, not a screenshot, same standing caveat as `docs/28`'s own.

## 0. Why this doc exists now

Several rounds of UI/UX work landed in one session (2026-08): a Minimap
legibility pass, real thumbnail icons + faction-themed names for
buildings, and clip-proofing for resource text. Before writing this,
`docs/12` was the only record — accurate, but 15,000+ lines long and not
something a future session should have to re-derive architecture from
by grepping. This doc is that consolidation, written specifically so a
session can `/clear` without losing the "how does this actually work
and why" thread.

## 1. Shared IMGUI conventions (apply these to any new HUD work)

- **IMGUI only, no exceptions.** Every HUD in this project is `OnGUI()`-
  based (legacy IMGUI), deliberately — it coexists fine with the New
  Input System (which only replaces the legacy `Input` class, not
  `OnGUI`). Don't introduce UGUI/uGUI for a new panel without a real
  reason; it would be the odd one out.
- **`UiScale.Begin()`/`UiScale.End()`** wrap every `OnGUI()` body. This
  is a single shared reference-canvas scale transform (not letterboxed —
  see `UiScale.cs`'s own header for why letterboxing was tried and
  reverted) so every HUD's hand-authored pixel constants scale correctly
  across resolutions. Every `Rect` a HUD computes must be in that SAME
  reference space (`UiScale.Width`/`Height`, not real
  `Screen.width`/`height`) or the matrix double-scales it off-screen.
- **Rotating an element inside a `UiScale`-wrapped `OnGUI`**: use
  `UiScale.RotateAroundReferencePivot`, not plain
  `GUIUtility.RotateAroundPivot` — the latter has a latent bug once
  `GUI.matrix` already holds `UiScale`'s own scale transform (the pivot
  needs to be in POST-scale space). Bit both `AnalogClockHud` and
  `Minimap` once; fixed in both, but a new rotating element would
  reintroduce it if it reached for the plain Unity API by habit.
- **"Bake once, composite live" for anything procedurally drawn at
  small scale.** Don't rasterize per-pixel inside `OnGUI()` (that's N
  draw calls per frame, N = pixel count — a real perf cliff, not a
  style nit). Bake a small `Texture2D` ONCE (lazily, cached forever —
  the shape never changes at runtime), then `GUI.DrawTexture` it every
  frame — ONE draw call regardless of how much pixel-level detail the
  bake has. Established precedent: `AnalogClockHud`'s face,
  `Minimap`'s terrain texture, `BuildingIconKit`'s nine building
  silhouettes (§3 below).
- **Tint-at-draw-time, don't re-bake per variant.** A silhouette bake
  should be plain WHITE-on-transparent; the caller sets `GUI.color`
  before `DrawTexture` to tint it live. This is how one
  `BuildingIconKit` bake per building KIND works for all three
  factions' own accent colors, instead of needing kinds × factions
  separate bakes.
- **Never guess a fixed width/height for text — measure it.**
  `GUI.skin.label.CalcSize(content)` / `CalcHeight(content, width)`
  before laying out the `Rect` that will hold it. Every "text got
  clipped" bug found this session (Minimap's compass/legend, BuildMenuHud's
  cost line, ResourceHud's wallet panel) had the same root cause: a
  `Rect` sized off a guessed constant instead of the actual string that
  frame. The fix shape is always the same: measure first, size the
  backing box to the measurement (a floor on the old constant, not a
  replacement — short text still gets the old compact size), draw
  second.
- **Backing box, not just a drop shadow, for anything that must read
  against ANY city color behind it.** Two established idioms coexist in
  this codebase, pick based on which the ELEMENT already uses:
  - Drop-shadow only (`HudStatus.Line`, `BuildMenuHud`/`BuildingNavHud`'s
    `DrawShadowedLabel`): a black-offset copy of the label drawn first,
    the real color on top. Cheap, no backing rect, fine for HUD text
    that's part of a panel that already has its own opaque/semi-opaque
    background.
  - Translucent backing rect (`Minimap`'s frame/compass/legend,
    `AnalogClockHud`'s face halo, `BuildMenuHud`/`ResourceHud`'s panel
    backgrounds): a solid-color `Texture2D.whiteTexture` rect drawn
    behind, tinted low-alpha. Use this for anything that ISN'T already
    inside an opaque panel.
  `Minimap`'s own backing color is warm sepia-ink (`BackingColor =
  (0.15, 0.10, 0.06, 0.78)`), not flat black — a small, deliberate nod
  to the journal motif (§5). Other panels (`BuildMenuHud`, `ResourceHud`)
  still use flat near-black; nobody has gone back to re-theme those
  specifically, see §6.

## 2. Faction identity — the ONE canonical palette

**`BaseDresser.OwnerBaseColor(FactionId)`** (public, promoted from
private this session specifically so the HUD layer could reuse it) is
the single source of truth for "what color is this faction," full stop.
Mad Doctor sage-green `(0.42, 0.55, 0.4)`, Human Army steel-blue `(0.34,
0.5, 0.64)`, Alien Hive violet-purple `(0.5, 0.36, 0.62)`, Mixed/
unrecognized neutral gray `(0.55, 0.55, 0.6)`. This is ALSO the palette
`docs/31`'s own §1 "silhouette before color" rule names as the
load-bearing owner-color signal for the 3D world. **Never invent a
second faction palette anywhere** — if a new UI element needs a
faction accent, call this (directly, or via `BuildingFactionSkin.
AccentColorFor`, a thin passthrough) rather than picking new RGB values
that happen to look plausible.

**`BuildingFactionSkin.NameFor(BuildingKind, FactionId)`** — the
display-name half of faction identity. `BuildingDef.cs` (match-core)
has said since Phase 2 that per-faction building name skins ("Blood
Bank / Plasma Reserve / Ichor Cistern" for the same generic
`BuildingKind.BloodStorage`) were a Unity-display concern; this class
is that concern, finally implemented. 27 names (9 kinds × 3 real
factions; `Mixed` falls back to the sim's own generic `BuildingDef.
Name` — Mixed has no single origin/energy of its own to theme toward).
`Hq` reuses `FactionDef.BaseName` verbatim (The Sanatorium / Fort
Vigilance / The Brood Nest) rather than a second copy of the same three
strings. Every other name is this pass's own invented period+faction
flavor — gothic-medical for the Doctor, military-industrial for the
Army, organic-biotech for the Hive, matching `maddr-aesthetic-
preferences`'s per-faction visual language extended to naming. **This
is display-only** — nothing here changes which factions can build which
`BuildingKind`, or what a building actually costs/does; that's
unrelated, untouched sim logic.

**Doctrine**: shape communicates KIND, color communicates FACTION/state.
Don't conflate them (this is the SAME rule `maddr-aesthetic-
preferences` states for creature parts — origin channel vs.
contents/state channel — applied here to buildings). Before this
session, building "icons" were a flat PER-KIND color (no faction
signal at all) plus a text abbreviation — color was doing a job it
wasn't suited for, and shape was doing none. Fixed by swapping which
channel does which job (§3), not by adding a third channel.

## 3. Thumbnail icons — `BuildingIconKit`

Nine building kinds, nine procedurally-baked silhouette pictograms
(28×28, white-on-transparent, bilinear-filtered for smooth edges at
small on-screen sizes — deliberately NOT `Minimap`'s crisp Point
filtering, which wants sharp hex-block edges instead). Built from
analytic shape tests (circle/ring/rect/linear-taper), not hand-authored
art — this project has no icon sprite pipeline, so bold simple
pictographs are the ceiling, same constraint `Minimap`'s own landmark
star/ring icons worked within.

| Kind | Pictogram |
| --- | --- |
| Hq | Ring + cross (command roundel) |
| BloodStorage | Droplet (circular bulb, linear taper to a point) |
| FuelPump | Tank rect with a punched-through gauge cutout + a nozzle appendage |
| FuelStorage | Barrel rect with two transparent "hoop" gaps |
| PartsStorage | Two offset overlapping squares (stacked crates) |
| HarvestPost | Upward chevron over a small base rect |
| Factory | Base block, sawtooth roofline, one chimney |
| Defense | Shield (flat top, tapered point at bottom) |
| BigBrain | Three overlapping circular lobes |

**The one real gotcha if you touch this file**: texture-space Y grows
DOWNWARD (row 0 = top). Three of the nine shapes shipped with the sign
backwards on first draft this session (nozzle/gauge, chevron base,
chimney all landing on the wrong side) before being caught in review —
it is an easy, silent mistake to place a "this goes at the top" feature
at POSITIVE `dy` by reflex. If you add a tenth kind, work through the
sign convention by hand, don't just pattern-match an existing case.

`BuildMenuHud`'s command-card tiles and `BuildingNavHud`'s nav-bar icons
both consume this the same way: faction-accent swatch (§2) as the tile
background, the silhouette drawn on top tinted ivory `(0.92, 0.88,
0.78)` for contrast. The old `BuildingNavHud.IconColorFor`/
`IconAbbrevFor` (flat per-kind color + 2-3 letter abbreviation) are
gone — deleted outright once both consumers stopped calling them, not
left as dead code.

## 4. Scope line: this doc vs. `docs/31`

`docs/31-faction-building-architecture.md` covers `BaseDresser.cs` —
the actual 3D building MESHES/materials a faction's Factory/Control
Centre/Hq are built from in the game world (windows, antennas,
silhouette language). This doc covers the 2D IMGUI HUD layer — how a
building's identity is REPRESENTED in menus/panels (name text, icon
thumbnail, accent color), never its 3D geometry. The two share exactly
one thing on purpose: `BaseDresser.OwnerBaseColor` as the single
palette source (§2) — a faction's building should be the same color
whether you're looking at it in the world or picking it from a menu.
Nothing in this doc touches `BaseDresser.cs`'s mesh-building code, and
nothing in `docs/31`'s scope touches `BuildMenuHud`/`BuildingNavHud`/
`ResourceHud`/`Minimap`.

## 5. Period/journal motif in the HUD, concretely

`maddr-aesthetic-preferences`'s Notebook motif ("every panel feels
hand-sewn into a journal, not a clean modern HUD") is mostly
ASPIRATIONAL for the HUD layer as of this writing — most panels are
still a flat near-black translucent box with plain sans-serif `GUILabel`
text (IMGUI's default skin; no custom font is loaded anywhere in this
project). The one place it's actually been applied: **`Minimap`**,
specifically —
- Warm sepia-ink backing color (`(0.15, 0.10, 0.06, 0.78)`) instead of
  flat black, on the map's own frame and its new compass/legend chips.
- Arterial-road color-coding grounded in real 1950s map conventions
  (USGS topo sheets, Rand McNally road atlases both reserve red/heavier
  ink for the through-route) — researched via three parallel "art
  director" agent passes (1950s military maps / 1950s road-atlas
  conventions / this project's own doctrine) before implementing, all
  three independently converging on the same fix.
- Landmark markers as real pixel silhouettes (starburst / hollow ring)
  instead of same-shape-different-color blobs.

**If asked to extend the journal motif further**, that three-lens
art-direction consultation pattern (spawn parallel research agents with
different period/genre lenses, synthesize before implementing) is the
established, creator-endorsed way to do it for this project — don't
freelance a new visual direction solo when the pattern for getting it
right already exists and worked well.

## 6. Explicitly NOT done — don't assume it's covered

Scoped deliberately, not from running out of things to fix:

- **Not re-themed**: `HudStatus`, `SelectionHud`, `RecallHud`,
  `BattalionHud`, `LabBattalionHud`, `WindowLightsHud`, `LumenHud`,
  `HarvesterMarkerHud`, `RegionPickerHud`, `MatchSetupHud`,
  `DeployingArmyHud`, `BuildGhostCursor`, `BuildingNavHud`'s own
  backing/frame colors (only its icon rendering changed), `GrabCursor`.
  All still flat near-black backing, no faction/period styling applied.
  A full pass across every HUD script was explicitly ruled out as too
  much unverified breadth for one session (no Editor to check any of
  it against) — see the docs/12 entry's own "scope decision" note.
- **Not verified visually, anywhere in this doc.** Every fix described
  above is code-reviewed (brace/paren balance, cross-reference grep,
  manual trace of coordinate math) but has never rendered in a real
  Unity Editor. Treat every "shipped" claim in this doc as "compiles
  and is internally consistent," not "confirmed to look right." The
  concrete open questions once an Editor is available: do the nine
  `BuildingIconKit` pictograms read as distinct/recognizable at true
  ~44-56px tile size, do the Minimap's star/ring icons read at true
  ~220px map scale, does the sepia backing color actually look
  "journal" rather than just "brownish," do the 27 invented faction
  names land the right tone.
- **`ResourceHud`/`BuildMenuHud`'s own frame/backing colors** were NOT
  moved to the sepia tone `Minimap` uses (§5) — only their text-sizing
  was hardened against clipping (§1's "measure, don't guess" fix).
  Visually they're still the older flat near-black panels. Extending
  the sepia backing convention to them is a reasonable, small future
  pass if asked for, not something this round did.
