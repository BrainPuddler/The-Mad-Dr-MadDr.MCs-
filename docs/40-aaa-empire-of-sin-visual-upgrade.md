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
  AO/roughness set. **Correction to an earlier draft of this doc**: this
  is *not* a confirmed-working precedent — `BrainMaterial`'s own doc
  comment says plainly it's "genuinely unconfirmed in THIS project (no
  prior usage to check against, and no Editor here to compile/render
  it)," and a later 2026-08 weathering-pass session (docs/12) explicitly
  declined to build new per-faction normal maps on top of it for exactly
  that reason — never added to docs/36's checklist either, a real
  tracking gap fixed by this doc's own §3 item 0 work. What *is* true:
  `_BumpMap`/`_NORMALMAP` are stock URP/Lit property/keyword names (not
  hand-authored HLSL like `CreatureVertexColor.shader`), so the risk
  here is the same "shipped, unconfirmed, ship-blind-and-flag" category
  as the rest of this codebase's visual work, not a specially-proven
  low-risk case — treat it accordingly, not as a free pass.
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

0. **[Implemented 2026-09-15, pending Editor verification — see docs/36]
   Facade normal-map rollout** (closes §2.3). Extended `PbrTextureAtlas`
   with a shared `BuildNormalFromHeight` central-difference helper (the
   same technique `BrainTextureKit.BuildNormal` uses) plus independent
   height functions mirroring `BuildBrick`/`BuildLimestone`/
   `BuildDressedStone`'s own per-pixel mortar/joint/jitter rules (not
   shared code — a deliberate re-read, so this pass can't touch the
   already-shipped albedo textures at all). Wired `_BumpMap`/
   `_NORMALMAP` into `BuildingDresser`'s and `BaseDresser`'s `MTextured`
   helpers (both already-existing per-file copies of the same idiom) and
   every wall material that uses Brick/Limestone/DressedStone — 8 call
   sites across both files, plus a fix to `RuntimeCityBuilder.
   ApplyWorldScaledTiling` so the normal map's tiling bucket stays
   locked to the albedo's own world-scaled tiling instead of drifting to
   a fixed (3,3) fallback. **Editor-dependence: low, not zero** — see
   §1's correction above: `_BumpMap`/`_NORMALMAP` are stock URP/Lit
   property/keyword names (lower risk than hand-authored HLSL, which
   already shipped successfully — docs/39 §11 item 2), but this is
   genuinely the first live test of whether they render correctly in
   this project at all, not a specially-proven case. Verified only by
   brace/paren balance and read-through, same standing ceiling as
   everything else here. No new triangles, no new renderers — texture-
   only, inside docs/39 §7's atlas-everything rule.

1. **[Implemented 2026-09-16, pending Editor verification — see docs/36
   entry 26] Wet-response shader pass, gated to a rain/weather state,
   not global** (closes §2.1 the right way this time). docs/28 rows
   14/15 already proved *global* smoothness reads wrong. The fix is
   conditional, not constant: new `WeatherController.IsRaining` (a real
   toggle, exposed via a new `RainToggleHud` button) drives an eased
   `Wetness` value, and new `WetSurfaceRegistry` — same "record each
   material's base value once, blend a live override onto the shared
   Material" shape `NeonRegistry` already uses for night emissive boost
   — raises `_Smoothness` and darkens albedo on `RoadDresser`'s Asphalt/
   Sidewalk/RoundaboutCurb/IslandStone materials only while wet, so dry
   play keeps the already-tuned matte look untouched. No reflection
   probes or SSR needed: raising smoothness alone lets any EXISTING
   light (real streetlamps, the animated sun) already in the scene
   produce a sharper specular response — this doesn't reuse
   `GlowPointRegistry`/`DynamicLightBudget` through any special code
   path, it just benefits from whatever lights are already there once
   the surface itself responds to light differently, which is a
   correction to this entry's own original phrasing above ("reuses the
   existing... registries" overstated what the implementation actually
   needed to do). **Editor-dependence: low-to-medium** — pure material-
   parameter work plus one new static-class pair, same verification
   ceiling as docs/28's own material tuning; the "does it look wet, not
   shiny" judgment call is genuinely visual and needs the creator's
   eyes, same as every docs/28 row.

2. **[Implemented 2026-09-16, pending Editor verification — see docs/36
   entry 27] Rain VFX** (closes §2.2, pairs with item 1). New
   `RainSystem`: a pool of 260 GPU-instanced falling streaks (a hand-
   authored unit-box mesh, `Graphics.DrawMeshInstanced` — **correction
   to this entry's original text**, which named `Graphics.
   RenderMeshInstanced`; the real precedent, `LowPolyFireSystem`, uses
   `DrawMeshInstanced`, a different/older overload, confirmed by reading
   that file directly rather than trusting this doc's own paraphrase of
   it), active count scaled by `WeatherController.Wetness`, respawning
   around a camera-ground-focus point each time one lands. Landing
   triggers a short-lived growing splash disc from a separate, smaller
   pool (40) with a fixed cool emissive tint — **narrower than this
   entry's original "catch `GlowPointRegistry`'s existing lamp colors"
   idea**: querying nearby lamp color per splash was judged over-
   engineered for a cheap ambient effect and left to item 4's own hand-
   placed, lamp-aware plaza decals instead. Both pools cap well inside
   docs/39 §9's particle/lifetime rules and render nothing in the Map
   band (reuses `AnimationLodBudget.CurrentBand`, the same check
   `MonsterBody`'s own Map-band cull already established). **Editor-
   dependence: low** — the instancing call itself mirrors a real, read
   precedent; the one genuinely new risk is the hand-authored box mesh's
   winding, mitigated with the same `_Cull = Off` safety net
   `PropLibrary`/`RoofPortraitHologram` already carry after docs/28 rows
   6/7's real incident. Whether the streak/splash density and mesh read
   correctly at the Normal-band 26 px test is a visual judgment call for
   the creator, same ceiling as every VFX change here.

   **2026-09-16 polish pass, direct creator direction after seeing it
   run** ("rain streaks should be longer, motion blurred tip and tail.
   a rain impact splash and ripples on surfaces, plus wet surface,
   darker and shiny areas. Clumps of heavier mist floating slowly
   through viewport") — full detail in docs/36 entry 27's own second
   update: streaks lengthened + given a real UV alpha-gradient fade at
   both ends; splashes switched from a filled disc to a proper impact-
   core-plus-expanding-ring via one static radial texture; item 1's
   `WetSurfaceRegistry` values strengthened and `RoadDresser.DressHex`
   now scatters extra `PuddleDecal()`s across ordinary street hexes
   (not just roundabouts) for patchy "wet areas" instead of one flat
   city-wide tint; and a NEW system beyond this doc's original six-item
   plan, `MistSystem.cs` — a small pool of drifting `CloudShard` fog
   clumps, gated by `Wetness` the same way rain itself is. This also
   fixed a real, creator-reported runtime exception (`Graphics
   .DrawMeshInstanced` needs `enableInstancing = true`, missed when
   mirroring `LowPolyFireSystem` the first time) — see docs/36 entry
   27's first update.

   **2026-09-16, second follow-up** ("fall faster and be fast and
   plentiful close to the camera") — base fall speed raised (24-34 m/s,
   was 16-24), plus a real camera-proximity depth cue: streaks within
   32 m of the camera's own ground position (not the point it's looking
   at) fall up to 1.7x faster, and 60% of every respawn lands in that
   same radius instead of spreading uniformly, so the pool visibly
   concentrates in the foreground the way real rain close to a lens
   does. Full detail in docs/36 entry 27's third update.

3. **[Implemented 2026-09-16, pending Editor verification — see docs/36
   entry 28] Monster rim/fill light term** (closes §2.4). A cool rim/
   fresnel term added to `CreatureVertexColor.shader` (the shader
   docs/39 §11 item 2 already hand-authored and confirmed compiles,
   docs/36) — three new per-material Properties (`_RimColor`/
   `_RimPower`/`_RimIntensity`, defaults apply automatically, no
   `LabMeshBuilder` change needed) scaled by a new GLOBAL
   `_MadDrNightAmount`, pushed once per frame from
   `LumenCycleController.ApplyBlend` via `Shader.SetGlobalFloat` — no
   new registration system, no per-monster script hookup, zero per-
   material update cost since every material reads the same global.
   **Editor-dependence: low** — same shader, same proven-precedent
   pattern as item 0, one more property block on an existing pass; this
   IS, however, the first genuinely global (non-per-material) uniform
   any hand-authored shader in this project has used, flagged as its
   own specific unconfirmed detail in docs/36 rather than folded
   silently into "same as before."

4. **[Implemented 2026-09-16, pending Editor verification — see docs/36
   entry 29] Puddle "fake reflection" decals at key plazas/
   intersections** (partial alternative to full reflection probes/SSR,
   which docs/28 row 19 already flagged as Editor-only setup work this
   environment can't do blind). New `RoadDresser.PuddleDecal()`, one
   per roundabout — the generator's own clearest "high-traffic plaza/
   intersection" concept (a named `city.Roundabouts` set with real
   circulating-asphalt geometry already built); the separate
   `liberty_statuette_plaza` landmark archetype (§1 above) is a
   `BuildingDresser` set piece, not a `RoadDresser` ground surface, and
   wasn't pulled into this pass — at a hash-deterministic position, faded
   in/out by `WeatherController.Wetness` rather than always-visible
   (avoiding docs/28 rows 14/15's exact "always-on = too much" mistake
   in a new spot) — **narrower than this entry's original text**: a
   static warm-tinted dark patch, not a literal "vertically-flipped
   skyline silhouette" (that needs a live mirrored-camera render, the
   same Editor-only category as reflection probes/SSR themselves, so it
   would have contradicted this item's own "doesn't need a renderer
   feature" framing). Tinted once at creation by `RoadDresser.LampColor`
   — the same constant every roundabout lamp already registers with
   `GlowPointRegistry` — rather than a live per-frame query, since a
   static decal doesn't need one. **This item also surfaced and fixed a
   real bug in item 1**: a two-layer material-cloning chain
   (`PropLibrary.GetDoubleSidedVariant` then `RuntimeCityBuilder
   .ApplyWorldScaledTiling`) was silently disconnecting every
   `WetSurfaceRegistry`-registered Sphere/Cylinder-shaped material
   (including `Asphalt()` itself) from what `SetWetness` actually
   mutates — see docs/12's own entry and docs/36 entries 26/29 for the
   full story. **Editor-dependence: low, not zero** — a material/prop
   addition, not a renderer-feature registration, but the clone-chain
   fix underneath it is reasoned from reading the call chain, not seen
   rendered.

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

   **2026-09-16, read-only recon of the actual URP config (reading
   asset/scene YAML, no Editor access) — corrected same-day after a web
   check, see below.** First pass here wrongly claimed a plain
   "Add Renderer Feature → Screen Space Reflections" was available and
   gave the creator two Inspector steps to do it. **That was wrong —
   caught by checking Unity's own release notes/forum posts before
   handing the creator real steps, not by anyone reporting back.**
   Native URP SSR does not exist in `6000.3.13f1` (this project's exact
   Editor version) at all — Unity's own team states it's targeting
   Unity 6.7, requiring at least `6000.6.0a7` (an alpha three minor
   versions ahead of what's installed here), still in preview as of
   2026-09-16. There is no "Screen Space Reflections" entry in this
   project's Add Renderer Feature list to click. **Lesson for next
   time, same shape as the `_BumpMap`/Big Brain jar mistake in this
   same doc's §1: reasoning from a feature's generic existence in
   "URP"/"Unity 6" is not the same as confirming it ships in the
   specific Editor version this project is pinned to** — always check
   against the exact version (`6000.3.13f1`) before writing creator
   steps, not against "URP" as a generic moving target.

   What DID hold up from that recon (still true, not retracted):
   `PC_RPAsset.asset` (confirmed active pipeline) already has
   `m_RequireDepthTexture: 1`/`m_RequireOpaqueTexture: 1` on, and the
   scene's one Global Volume already shares `DefaultVolumeProfile.asset`
   (which does carry stray `CopyPasteTestComponent*`/`TestVolume`
   entries, harmless leftover Unity package test-sample data, not
   touched). None of that unlocks SSR on this Unity version, though —
   depth/opaque textures are necessary but not sufficient without the
   feature existing at all.

   **Real options, none of them a two-click Inspector step like the
   puddle decals or docs/28's material tuning:**
   1. **Upgrade the Editor to `6000.6.0a7`+** to get native SSR once it
      ships — an alpha/preview jump this deep into a project carries
      real stability risk (LTS vs. alpha tradeoff), a creator call, not
      a blind recommendation.
   2. **Adopt a third-party open-source URP SSR Renderer Feature**
      (several exist on GitHub, unnamed here since none has been
      evaluated against this project's URP version/pipeline
      customizations) — real third-party rendering code, its own
      integration/compatibility risk, a bigger decision than anything
      else in this backlog.
   3. **Realtime Reflection Probes**, spawned/triggered from code near
      landmarks — the `RuntimeCityBuilder`-builds-at-runtime problem
      from the original recon still applies unchanged: a baked probe in
      the edit-time scene sees nothing, so this needs new runtime code
      (a real, separately-scoped follow-up), and per-frame realtime
      probes are expensive against docs/39's floor.
   4. **Accept item 4's puddle-decal approximation as the ceiling** —
      the original framing of this item, still valid.
   Ask the creator which of these (if any) before doing more here —
   this is the genuine "wait for the creator" case the item's opening
   paragraph already called out, now with the real menu instead of a
   two-click illusion of one.

6. **Volumetric fog integration** — the REAL Renderer Feature stays
   parked, not re-evaluated. docs/28 row 19 already did the feasibility
   work and the creator already chose the cheaper Bloom-based
   approximation for performance. Nothing here changes that calculus;
   don't re-litigate it (same "once the creator calls something
   adequate, that's final" rule the `maddr-editor-verification-workflow`
   memory already states for the SRP-batching item).

   **[Implemented 2026-09-16, pending Editor verification — see docs/36
   entry 30] Cheap approximation shipped instead**, direct creator
   direction ("add the volumetric fog patches") rather than a re-
   evaluation of the real package. New `VolumetricFogPatchSystem.cs`: 8
   stationary ground-fog patches, each a 3-layer stack of soft
   `ProceduralMeshKit.CloudShard` blobs (wide/opaque near the ground,
   smaller/fainter with height — the standard "billboard stack" trick
   for a volume-reading silhouette with no real density field), scaled
   by `DayNightState.NightAmount` plus a smaller `WeatherController
   .Wetness` boost. Not tied to real terrain (rivers, dips) — this
   environment has no simple terrain-height query from a standalone
   MonoBehaviour, so patches scatter generically across the visible
   area rather than anchoring to actual low ground. **Editor-
   dependence: low** — plain GameObjects/MeshRenderers, no
   `Graphics.DrawMeshInstanced`/clone-chain risk like `RainSystem`'s own
   items had; the real Renderer Feature version above remains fully
   blocked regardless of this.

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
