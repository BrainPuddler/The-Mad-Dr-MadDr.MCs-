# 40 — AAA Visual Upgrade: Closing the Empire of Sin Gap

**Status: proposed backlog (2026-09-15), gaps verified against the live
codebase, not against any other doc's prose about itself** (the
graphics-backlog decision log flagged, more than once, that a backlog
item's own description is not proof the thing was built — see docs/12
and docs/36 §12's Citizen-collider incident). Every gap below was
confirmed with a real `grep` against `unity-client/Assets/Scripts/`
this session, not inferred from docs/28/33/39's own summaries of
themselves.

**Relationship to docs/39:** docs/39 is the floor — the performance
budget every visual choice must survive (triangle/renderer counts, LOD
bands, shadow-caster rules, the 1/10/25/50 curve). Its own §11 ranked
backlog (items 0 through 8) is **fully implemented and pushed** as of
2026-09-16 (see the `maddr-graphics-backlog-status` memory / docs/36
entries 14–24) — there is nothing left to do in that specific list.
This doc is the next layer up: **closing the gap between "performant"
and "reads as Empire of Sin," now that the floor is no longer moving.**
Nothing here is allowed to cost more than docs/39 §4/§7/§8 budget for
its asset class. An item that can't be built inside that budget is not
in scope for this doc — it goes back to docs/39 as a budget-revision
proposal instead.

The one-line rule from docs/39 §0 still applies, extended:

> **Maximum perceived visual quality at the actual gameplay camera
> distance for minimum performance cost — and "maximum" now means
> Empire of Sin's own hero look, not just "acceptable."**

**This is the second Empire-of-Sin-benchmarked plan against this
codebase, not the first — verify against it before re-proposing
anything.** A private Claude Artifact, *"Wave Function Collapse for
City Buildings — Technical & Art-Direction Plan"* (2026-08-10, plan
mode, no code written by that session itself), already ran a three-
director art review against this exact reference and produced a ranked
visual-upgrade list. Checked against the live repo this session (git
log, not the artifact's own claims): **its Tier 0 and Tier 1 are fully
shipped** (`c4872e9`, "docs/30 Tier 1: per-object UV tiling, region-
aware dressing, roof-form variety, region landmarks") **and its Tier 2**
(`1639b5b` onward, "Facade grammar: WFC-driven, mesh-pipeline-ready
building dressing" through the party-wall/window-density follow-ups) —
see the expanded §1 below for exactly what that closed. Only that
plan's own lowest-ranked item, a real wet-asphalt shader, was never
picked up — it survives into this doc's §3 item 1, independently
re-derived from the live code before this cross-reference was found,
which is a second confirmation of the same gap rather than a
duplicate proposal. Its Tier 3 (an authored mesh/module library) stays
correctly blocked on an art pipeline that still doesn't exist here.
That plan is not currently versioned in `docs/` — it lives only as a
Claude-side Artifact — worth asking the creator whether to commit it as
a real numbered doc for git-history durability; not done here without
that confirmation.

---

## 0. TL;DR

1. **docs/39 §2 already wrote the take/leave list against Empire of
   Sin.** This doc doesn't re-derive it — it audits which "take" items
   are actually built (most of the *lighting* half is; see §1) and
   ranks what's left (§3), which turns out to be almost entirely the
   *surface-response* half: wet specular, rain, and real depth on flat
   facades. §2 has the evidence for every claim.
2. **Every item is scoped to survive docs/39's own budget tables** —
   this doc adds no new triangle/renderer/shadow-caster class, it adds
   texture, shader, and material-response work on top of geometry that
   already exists.
3. **Split by Editor-dependence, same line the project has always
   drawn** (`maddr-editor-verification-workflow` memory): shader/texture/
   material work that mirrors an already-proven precedent ships blind,
   flagged in docs/36. Anything that needs a live Renderer Feature
   registration or a Frame Debugger capture as its *first* step is
   named and left for the creator, not attempted blind.
4. **"AAA" in this doc means the cityscape/lighting/material system**
   (the docs/38 rendering boundary). HUD/UI polish is docs/32's job,
   not this doc's — flagged in §5 so it isn't silently rolled in.

---

## 1. Already built, already Empire-of-Sin-grade — don't re-propose these

Verified live in `unity-client/Assets/Scripts/` this session (not
assumed from docs/28's own summary of itself):

- **Two-tier lighting (real light + emissive) with a full day/night
  arc.** `LumenCycleController` (continuous sun elevation *and* yaw
  sweep, docs/28 rows 20/24), `DynamicLightBudget`/`GlowPointRegistry`
  (shared real-light budget, Point/Spot ceilings split, fog-density-
  aware dimming), `EmissiveAnimator` (per-window "motivated like a
  human being" occupancy schedule — arrival/bedtime/always-on, hard
  light-switch transitions per docs/28 row 37, not a dimmer).
- **GPU-batched window grid** (docs/33): one draw call per building,
  individually toggleable window state, solid warm color + a real
  sash/frame inset for depth (docs/28 rows 37–39) — this is the closest
  thing in the codebase to Empire of Sin's own facade-as-flat-plane-
  with-texture-doing-the-ornament approach (docs/39 §2), and it's done.
- **Bloom/fog diffusion tuned for "lights popping through fog"**
  (docs/28 row 18): `bloomScatter`, `fogDiffusionBoost`, `bloomThreshold`
  all live and wired.
- **A real weathering pass on every faction's material palette**
  (`PbrTextureAtlas.cs`, 2026-08): per-faction brick/stone/metal
  mottling, staining, patina — grep confirms this is genuinely
  procedural texture variation, not a flat re-tint.
- **Per-object UV tiling, region-aware palettes, roof-form variety, and
  named region landmarks** (`c4872e9`, 2026-08-11, the WFC-plan's own
  Tier 1): `SpawnPrim` scales texture tiling per-object instead of one
  fixed constant (fixes brick/stone coursing stretching identically
  across a 1 m curb and a 30 m wall — the artifact's own "single
  loudest procedural tell"), `CityModel.Region` now actually reaches
  `BuildingDresser.RegionWall`/`RegionTrim` (NY/Paris/Montreal reweight
  palettes; three cities no longer render as one), apartment-tier roofs
  get hip/stepped-parapet/penthouse variety instead of a uniform flat
  cap, and all five `CityPreset`-named landmark archetypes
  (`liberty_statuette_plaza`, `grand_terminal`, `iron_tower`,
  `marche_tower`, `forum_arena`) have their own set-piece dressing
  instead of falling through to a generic default.
- **Street-face grammar with a real party-wall/street-face distinction**
  (`1639b5b` onward, 2026-08-10–12, the WFC plan's own Tier 2 — the item
  all three of its directors independently converged on as highest-
  value): `FacadeGrammar` (citygen-core) decides which facade module
  goes on which wall face per building, `FacadeKit`/
  `BuildingDresser.DressFacadeGrammar` build it, and party walls
  correctly get blanks while street-facing sides get windows/fire-
  escapes/shopfronts — closing exactly the "every hex dressed
  identically on all four sides" failure that plan's Period director
  flagged as the top period-read problem. Window density and glow-
  budget tuning continued through docs/28 rows 34–39 (§1 above).
- **One real normal-mapped surface**: the Big Brain jar
  (`BaseDresser.cs` lines ~2377–2400, `BrainTextureKit`) sets
  `_BumpMap`/`_NORMALMAP` from a procedurally generated normal/height/
  AO/roughness set. This is the **proven precedent** §3 item 2 mirrors —
  it already compiles and renders (docs/36), so this is not unproven
  territory, it's an unfinished rollout.
- **Volumetric fog was evaluated, not skipped out of ignorance**
  (docs/28 row 19): `URP-VolumetricFog-ForwardPlus` is confirmed
  compatible (ForwardPlus renderer, URP 17.3.0, license), the creator
  chose to stay with the cheaper Bloom-based approximation for
  performance, and the package is kept as a documented option. Nothing
  new to evaluate here — see §4 for why it stays parked.

None of the above needs rework. The gap is elsewhere.

---

## 2. The real, verified gaps

Each gap below is something docs/39 §2 named as part of the Empire of
Sin "hero look" (wet-surface specular, atmospheric depth, believable
facade detail) that a live grep confirms is **not built**, not just
under-tuned.

### 2.1 Wet-surface specular is a paint job, not a material response

`RoadDresser.Asphalt()` names its material `"asphalt-wet"` and
`PbrTextureAtlas` paints "wet streak" highlight bands into the
*diffuse* texture (`PbrTextureAtlas.cs` line 336: *"standing in for a
real reflective wet-asphalt shader"* — the code's own comment admits
this). `RoadDresser.cs` line 56 explicitly documents that real
shininess was tried and reverted (docs/28 rows 14/15: global smoothness
0.92 read as "too shiny," reverted to shader-default response with only
the *base color* lightened). **There is no smoothness/metallic response
anywhere in `RoadDresser`'s material helpers to this day** — grep of
`MTextured`/`M()` confirms `_Smoothness`/`_Metallic` are never set. The
"wet look" is entirely a diffuse-texture illusion; it does not respond
to the streetlamp pools it sits directly under, which is exactly the
effect that makes the Empire of Sin street-corner shot read as
expensive (docs/39 §2: *"wet cobble reflecting streetlamp pools"*).

### 2.2 No rain, no weather state, anywhere

A precise grep (`\brain\b|rainfall|raindrop|weather`, `\bwet\b|puddle|
reflectionprobe|screenspacereflection`) across every script returns only
comment-level hits about *weathering* (the material-aging system, §1)
— zero hits for an actual weather/rain system, zero `ReflectionProbe`
usage, zero puddle geometry or decals. Empire of Sin's single strongest
frame (docs/39 §2's "night street corner") is a *rain* scene; this
codebase has never had rain.

### 2.3 Facade normal-mapping stops at one prop

`_BumpMap`/`_NORMALMAP` appears in exactly one file
(`BaseDresser.cs`, the Big Brain jar). `BuildingDresser.cs`,
`RoadDresser.cs`, and `FacadeKit.cs` — the three files that dress every
building and street in the game — never set a normal map on anything,
despite `PbrTextureAtlas` already computing per-pixel height/mottling
data for brick coursing and stone weathering that a height→normal pass
(the exact technique `BrainTextureKit` already uses and ships) could
turn into real depth almost for free. Empire of Sin's facades read as
expensive specifically because "brick, mortar, cornices are normal-
mapped texture on near-flat facades" (docs/39 §2) — ours are flat-
shaded color only.

### 2.4 Monster rim/fill lighting was named and deferred twice

docs/28 row 31 explicitly scoped "monster-specific rim/fill lighting"
out twice ("needs a registration call from `MonsterAgent.cs`/a material
change in `MonsterBody.cs`, neither a file directly related to
lighting"). Still true today — grep of `MonsterBody.cs`/`MonsterAgent.cs`
for rim/fresnel terms returns nothing. At 26 px (docs/39 §1.1), a rim
light against the night's now-deep, readable darkness (docs/28 rows
25–32) is the cheapest possible silhouette pop available — Empire of
Sin's characters get their pop from being 30–60 px with strong color
blocking; ours are a third that size, so contrast against the
background has to do more of the work.

### 2.5 Not a gap — an intentional exclusion, don't re-propose it

docs/39 §2 explicitly says to **leave** the 1920s–30s period detail —
car models, signage typefaces, the unit-scale-per-character generosity.
Nothing in this doc proposes borrowing Empire of Sin's *content*; every
item below is the *technique* (specular response, atmospheric depth,
facade depth, silhouette contrast) applied to this game's own 1950s
monster-movie town. If a future ask sounds like "make our signage look
like Empire of Sin's," point back at docs/39 §2 rather than building it.

---

## 3. Ranked backlog

Ordered by hero-look gained per unit of (mostly shader/texture, not
geometry) work, same "rank by visible-improvement-per-effort" logic as
docs/39 §11. Each item states its Editor-dependence up front.

0. **Facade normal-map rollout** (closes §2.3). Extend
   `PbrTextureAtlas`'s existing per-material generation functions
   (brick, stone, weathered metal — all already computing jitter/height-
   ish variation for the diffuse channel) with a paired normal-map
   generator using the same height→normal technique `BrainTextureKit`
   already ships and proves compiles (§1). Wire `_BumpMap`/
   `_NORMALMAP` into `BuildingDresser`'s and `RoadDresser`'s shared
   material helpers (`M()`/`MTextured()`) the same way `BaseDresser`
   already does for the jar. **Editor-dependence: low** — this mirrors
   a proven, already-compiling precedent almost exactly (the pattern
   the `maddr-editor-verification-workflow` memory's 2026-09-16 entry
   confirms is reliable blind), ships read-through-verified, flagged in
   docs/36. No new triangles, no new renderers — texture-only, inside
   docs/39 §7's atlas-everything rule.

1. **Wet-response shader pass, gated to a rain/weather state, not
   global** (closes §2.1 the right way this time). docs/28 rows 14/15
   already proved *global* smoothness reads wrong. The AAA fix is
   conditional, not constant: a small `WeatherState` (rain on/off, or
   tied to a phase/match event) that raises smoothness and darkens
   albedo on road/sidewalk/plaza materials *only* while active, so dry
   daytime keeps the already-tuned matte look and rain becomes a
   genuine state change instead of an always-on shine. Reuses the
   existing lamp/window real-light and emissive registries for the
   specular response — no reflection probes, no SSR needed for the
   pavement itself. **Editor-dependence: low-to-medium** — pure
   material/shader-parameter work, same verification ceiling as
   docs/28's own material tuning; the "does it look wet, not shiny"
   judgment call is genuinely visual and needs the creator's eyes once
   built, same as every docs/28 row.

2. **Rain VFX** (closes §2.2, pairs with item 1). Instanced rain
   streaks (`Graphics.RenderMeshInstanced`, the same pattern
   `LowPolyFireSystem` already uses per docs/39 §9) plus a handful of
   ground-level splash puddles as static decals that catch
   `GlowPointRegistry`'s existing lamp colors via emissive tint (not a
   new light, not a reflection probe). Capped hard by docs/39 §9's VFX
   rules (≤ 64 particles per instance-class, no per-unit real light).
   **Editor-dependence: low** — instancing pattern already proven in
   this codebase; the only genuinely new risk is whether the streak
   mesh/material reads correctly at the Normal-band 26 px test, a
   visual judgment call for the creator, same ceiling as every VFX
   change here.

3. **Monster rim/fill light term** (closes §2.4). A cool rim/fresnel
   term added to `CreatureVertexColor.shader` (the shader docs/39 §11
   item 2 already hand-authored and confirmed compiles, docs/36), driven
   by the already-global `LumenCycleController.nightAmount` so it
   strengthens exactly when the background gets darker and readability
   needs it most — no new registration system, no new per-monster
   script hookup beyond a shader property block already present.
   **Editor-dependence: low** — same shader, same proven-precedent
   pattern as item 0, one more property on an existing pass.

4. **Puddle "fake reflection" decals at key plazas/intersections**
   (partial alternative to full reflection probes/SSR, which docs/28
   row 19 already flagged as Editor-only setup work this environment
   can't do blind). A handful of hand-placed, low-poly, semi-
   transparent dark quads at high-traffic plazas, textured with a
   vertically-flipped skyline silhouette and tinted by nearby
   `GlowPointRegistry` colors — the classic pre-SSR "fake puddle"
   trick, cheap and bounded in count (not a per-tile system). **Editor-
   dependence: low** — a material/prop addition, not a renderer-feature
   registration.

5. **Real reflection probes / URP SSR** — deliberately **not** picked
   up here. This is the one item in this backlog that is a genuine
   categorical blocker per the `maddr-editor-verification-workflow`
   memory's own line ("needs a live Editor session" is where the
   creator has stopped work before, docs/39 §11 item 4's Frame-Debugger
   requirement being the precedent). `ReflectionProbe` baking and a URP
   Renderer Feature both require Inspector-side registration this
   environment cannot perform blind. **Do not attempt; wait for the
   creator to do the one-time Editor step, or accept item 4's fake-
   puddle approximation as the ceiling.**

6. **Volumetric fog integration** — stays parked, not re-evaluated.
   docs/28 row 19 already did the feasibility work and the creator
   already chose the cheaper Bloom-based approximation for performance.
   Nothing here changes that calculus; don't re-litigate it (same
   "once the creator calls something adequate, that's final" rule the
   `maddr-editor-verification-workflow` memory already states for the
   SRP-batching item).

---

## 4. Explicitly out of scope for this doc

- **HUD/UI visual polish.** "AAA" read on panels, icons, fonts is
  docs/32-hud-ui-system's territory, not this doc's — this doc is
  scoped to the docs/38 cityscape-rendering boundary (dressing,
  lighting, materials, terrain presentation). If the creator means UI
  too, that's a separate ask against docs/32.
- **New signage/marquee content in an Empire-of-Sin period style.**
  docs/39 §2 already says leave the period; if a future ask wants new
  1950s-appropriate marquee/signage *content* (not technique), that's
  an asset/content addition against docs/17 (factions) or the aesthetic
  skill, not a rendering-system gap this doc tracks.
- **Reflection probes / SSR** (see §3 item 5) — named, not silently
  dropped, but genuinely Editor-gated.
- **Anything that would raise a triangle/renderer/shadow-caster budget
  in docs/39 §4/§7/§8.** If an idea needs a bigger budget to read as
  AAA, the conversation is a docs/39 budget revision, not a docs/40
  backlog item.

---

## 5. Relationship to other docs

- **docs/39** — the performance floor every item here is built inside;
  supersedes nothing here, this doc supersedes nothing there.
- **docs/28** — the lighting bug/decision history; §1 and §2.1/§2.4
  above cite specific rows rather than re-deriving them.
- **docs/33** — the window-grid GPU port; the model item 0 extends to
  facades in general.
- **docs/31** — faction building architecture; its own "honest ceiling"
  note (real normal/bump-mapped surface depth, flagged as a future idea
  in the `maddr-graphics-backlog-status` memory) is exactly item 0,
  now scoped as a real backlog item instead of a loose idea.
- **docs/38** — the rendering-vs-generation index; this doc's scope
  boundary (§4) is the same boundary docs/38 already drew.
- **`maddr-editor-verification-workflow` memory** — the ship-blind/
  flag-in-docs/36/creator-verifies-live rhythm this backlog follows,
  including exactly where the line sits (§3 item 5).
- **The "Wave Function Collapse for City Buildings" Artifact
  (2026-08-10)** — the prior Empire-of-Sin-benchmarked plan against
  this codebase; its Tier 0/1/2 are shipped (§1), its wet-asphalt-shader
  item is this doc's §3 item 1, and its Tier 3 (authored module
  library) is this doc's own §3 item 5 boundary restated. Not yet
  committed into `docs/` as a numbered file — ask before doing that.
