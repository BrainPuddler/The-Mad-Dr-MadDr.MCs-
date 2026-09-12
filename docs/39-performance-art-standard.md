# 39 — Performance-First Art Standard for the Semi-Isometric Camera

**Status: normative (creator brief, 2026-09-12), numbers measured from the
real client where a measurement exists, estimated and flagged where it
doesn't.** This doc turns the creator's "PERFORMANCE IS A PRIMARY ART
REQUIREMENT" brief into rules with numbers that come from *this* game:
its actual camera (`SimpleCameraRig`), its actual pipeline (URP,
`PC_RPAsset`/`Mobile_RPAsset`), its actual asset workflow (everything is
code-generated from primitives and `creature-mesh` at Play time —
docs/21 §1), and its actual unit counts (docs/23's 4v4 with hundreds of
units). Where it conflicts with docs/08's v0.1 budget table, **this doc
wins for the Unity client** (docs/08 budgeted an imported-part pipeline
that was never built; §4 below reconciles).

The visual reference is **Empire of Sin** (Romero Games, 2020, Unity) and
the screenshots in `../Inpiration-folder/`. §2 says exactly what to take
from it and what not to.

The one-line rule, from the brief:

> **Maximum perceived visual quality at the actual gameplay camera
> distance for minimum performance cost.**

Everything below is that sentence with the project's own numbers
plugged in.

---

## 0. TL;DR — the rules every visual change is held to

1. **Build for the Normal zoom band (camera height 25–110 m, default
   70 m), not for a close-up.** At the default zoom a 4 m monster is
   about 26 px tall on a 1080p screen. A 0.3 m detail is 2 px. If it
   can't be seen at 70 m, it is texture, colour, or nothing — never
   geometry.
2. **Every repeated asset needs LODs.** Today the client has **zero**
   `LODGroup` components and every monster is 9.6k–16k triangles at all
   distances. Monsters ship with LOD0/1/2 + a Map-band impostor;
   buildings use the engagement-zone tiers; humanoids and vehicles get
   at least a merged far LOD. Transitions are set by screen height
   (§5), not by arbitrary metres.
3. **Silhouette and material separation carry the read.** Spend
   triangles on outline, major masses, animation joints, and shadow
   shape. Spend nothing on rivets, gauges, bevels, or interior parts at
   Normal zoom.
4. **Draw calls are budgeted like triangles.** One shared material per
   family, colour via vertex colour or a colour-keyed material cache,
   never a per-instance `Material`. `MaterialPropertyBlock` is not free
   under URP's SRP Batcher — see §7 before using one on anything that
   appears in quantity.
5. **No stock `Sphere`/`Capsule` primitives in anything spawned in
   numbers.** A Unity sphere is 760 triangles; a capsule 832. They are
   used from 75 sphere call sites today. Use an 80–100-triangle low-poly
   sphere from `PropLibrary` instead.
6. **Shadows are a budget.** Casters: units, vehicles, building
   massing, hero props. Non-casters: window grids, lane paint, props
   under 1 m, VFX, DistantSkyline dressing.
7. **Animation cost scales with distance.** Procedural gaits tick at
   full rate only in the Close/Normal bands; half rate in Overview;
   frozen in Map.
8. **VFX are read at 26 px per monster.** Big clear shapes, short
   lifetimes, hard particle caps. No effect may spawn more than 64
   particles per instance, or exist for longer than 2 s without a
   gameplay reason.
9. **Nothing is "done" on looks alone.** Every visual change passes the
   two-test gate in §10: a visual before/after at the default zoom and a
   1 → 10 → 25 → 50 unit cost curve.

---

## 1. The actual camera (measured from `SimpleCameraRig.cs`)

| Fact | Value | Source |
| --- | --- | --- |
| Pitch | fixed **50°** down, yaw free (Q/E) | `Quaternion.Euler(50f, _yaw, 0f)` |
| Vertical FOV | **60°** (perspective, not orthographic) | `SampleScene.unity` camera |
| Height clamp | **8 m – 400 m** (`MinHeight`/`MaxHeight`) | scroll zoom and Shift+arrows share the clamp |
| Default height at match start | **70 m** | `RuntimeCityBuilder`: `rig.SnapTo(centre, 70f)` → offset `(0, h, -0.8h)` |
| Shadow distance | `1.9·h + 15`, capped at **250 m** (≈148 m at default zoom) | `UpdateShadowDistance()`, overrides the pipeline asset every frame |
| Ground plane | y = 0, feet stand on it (docs/18) | |
| World scale | 1 hex = 20 m; building heights 6 / 12 / 30 / 40 m by tier | docs/18, `RuntimeCityBuilder` |

### 1.1 Derived screen-space numbers (1080p; 1440p is ×1.33)

Camera-to-ground distance along the view centre is `h / sin 50° = 1.305·h`.
Pixels per metre at screen centre are `935 / distance` (935 px is the
focal length for a 60° vertical FOV at 1080 px). An upright object is
foreshortened by `cos 50° ≈ 0.64`; a ground footprint by `sin 50° ≈ 0.77`.

| Camera height | Distance to centre | px per metre (ground) | px per metre (upright) |
| --- | --- | --- | --- |
| 8 m (floor) | 10 m | 90 | 58 |
| 25 m | 33 m | 29 | 18 |
| **70 m (default)** | **91 m** | **10.2** | **6.6** |
| 110 m | 144 m | 6.5 | 4.2 |
| 250 m | 326 m | 2.9 | 1.8 |
| 400 m (ceiling) | 522 m | 1.8 | 1.1 |

**What that means for asset classes at the default 70 m zoom:**

| Object | World height | On-screen height | Verdict |
| --- | --- | --- | --- |
| Hydrant, crate, mailbox | ~1 m | **~7 px** | a coloured blob: silhouette + one colour, nothing else |
| Human / Worker / Citizen | ~2 m | **~13 px** | colour blocking is the whole read (hat, coat, skin) |
| Typical monster | ~4 m | **~26 px** | outline + 2–3 material masses + one accent |
| Tank / car | ~2 m tall, 5 m long | ~13 × 40 px | body mass + turret/roof + wheels as dark discs |
| House (tier 1) | 6 m | ~40 px | roof form + facade colour + door/window rhythm |
| Apartment block | 12 m | ~79 px | roof, cornice line, window grid, one signage element |
| Office tower | 30 m | ~200 px | massing, window grid, crown |
| Landmark | 40 m | ~260 px | the one place secondary forms are affordable |
| A rivet, gauge, bevel, filler cap | 0.1–0.3 m | **1–2 px** | invisible; texture/roughness only |

The visible ground at the default zoom is roughly **190 m wide × 180 m
deep** (about 9 × 9 hexes). That is the composition the player actually
looks at for most of a match.

### 1.2 The four zoom bands (use these names everywhere)

| Band | Camera height | Who uses it | LOD served |
| --- | --- | --- | --- |
| **Close** | 8–25 m | inspecting one monster, the Lab-style hero framing, screenshots | LOD0 |
| **Normal** | 25–110 m (default 70) | **the game**; where >80 % of play happens | **LOD1 — the most important LOD** |
| **Overview** | 110–250 m | army moves, reading a fight across several blocks | LOD2 |
| **Map** | 250–400 m | strategic overview; a monster is 5–8 px | LOD3 impostor / cull; minimap carries the information |

---

## 2. The Empire of Sin reference — what to take, what to leave

Empire of Sin is the right reference for this game because it solves the
same problem with the same tools: a three-quarter elevated camera over a
dense, period, night-and-rain city, built in Unity, that reads as
expensive without being expensive per object. Reading the five
inspiration images against our camera:

**Take:**

- **Night, rain, and wet-surface specular are the hero look.** The
  strongest EoS frames (the night street corner, the Deluxe Club street)
  get their production value from *lighting and roughness contrast*, not
  from geometry: wet cobble reflecting streetlamp pools, warm window
  glow against deep blue-black, neon and marquee bulbs as emissive
  shapes. We already own every piece of that pipeline (`PbrTextureAtlas`
  wet asphalt, `DynamicLightBudget` spot pools, `EmissiveAnimator`,
  `NeonRegistry`, `LumenCycleController`). Push roughness contrast on
  road/sidewalk atlases and the night grade before adding any polygon.
- **Building faces are flat planes with texture doing the ornament.**
  EoS brick, mortar, cornices, and window frames are normal-mapped
  texture on near-flat facades. The only geometry that leaves the wall
  plane is what breaks the silhouette at distance: awnings, fire escapes,
  balcony rails, signage boxes. That is exactly docs/33's window-grid
  approach and `FacadeKit`'s awning/oriel modules — keep it that way.
- **Streetlamps and signs are the depth cues.** Regular lamp pools down a
  street tell the eye the street is long. A budgeted spot per lamp
  (already the `GlowPointRegistry` model) buys more depth than any
  amount of prop density.
- **Units read by colour blocking.** In every EoS frame the characters
  are 30–60 px and instantly readable: red dress, white shirt, dark
  overcoat, pale hat. Our humanoids are 13 px and monsters 26 px at
  default zoom, so the rule is stronger for us: one dominant colour
  mass, one secondary, one accent, from the gothic LUT (docs/08).
- **Atmospheric depth hides the far LOD.** EoS lets fog and darkness
  swallow the far end of the street. Our distance fog should do the same
  work at the Overview band so the DistantSkyline tier is never seen
  sharp.
- **Street props are boxes.** Crates, barrels, benches, and cars in the
  Switch screenshot are visibly simple shapes with good textures. At our
  7 px per prop that is already too generous.

**Leave:**

- **The period.** EoS is 1920s–30s Chicago. We are a 1950s North American
  town under a monster-movie sky (aesthetic skill §1, §6). Borrow the
  camera and the light, not the cars or the signage typefaces.
- **The unit scale.** EoS draws a squad of 4–6 hero characters. We draw
  armies. Their per-character cost is irrelevant to us; ours must be an
  order of magnitude lower.
- **Close-zoom hero detail.** EoS's marketing shots are at a lower camera
  than its play camera. Our Close band exists, but it is not what we
  build for.

---

## 3. Camera-aware detail: the decision procedure

Before adding geometry to anything, answer in order:

1. **What band is this seen in?** A monster: all four. A rooftop water
   tower: Normal and Overview only. A Big Brain jar bubble: Close only.
2. **How many pixels tall is it in the Normal band?** Use §1.1. If the
   feature is under ~4 px at 70 m, it is not geometry.
3. **Does it change the silhouette?** Outline against the ground or sky
   is what the eye reads at 26 px. A horn, a hump, a tail, a turret, a
   cornice, an awning: yes. A rivet, a dial, a strap, an inner surface:
   no.
4. **Does it aid gameplay readability?** Origin (organic/tech/biotech)
   must read from shape (aesthetic skill §2); faction and state from
   colour (§5). If a detail carries neither, cut it.
5. **Can texture do it?** Roughness variation, a detail normal map, an
   emissive strip, vertex colour, or the stitch overlay almost always
   can.
6. **Will this exist in quantity?** If more than ~10 can be on screen,
   §4's repeated-asset budgets apply, not the hero budgets.

If a detail survives all six, it goes into LOD0 only unless it also
survives step 3 at 26 px, in which case it belongs in LOD1.

---

## 4. Triangle and renderer budgets (measured baseline → target)

Unity stock primitive costs, for reference: Quad 2, Cube 12, Cylinder 80,
Plane 200, **Sphere 760, Capsule 832** triangles.

### 4.1 Where the client is today (2026-09-12)

| Asset | Measured | How measured |
| --- | --- | --- |
| Monster body (`creature-mesh`), by body plan, default genome | **9,594 (serpentine) – 12,538 (arachnid)** tris; a busy tetrapod with faction hardware **16,001**; **12–23 material chunks = 12–23 renderers each** | `dotnet test Tests~/CreatureMesh.Tests.csproj --filter MeshStats` (real run) |
| Monster LODs | **none** — one mesh at every zoom | `grep LODGroup Assets/Scripts` → 0 hits |
| Monster legs (`LegKit`) and wings | extra chunks on top; legs also fall back to 80-tri cylinders | `MonsterBody.cs` |
| Human rig (`HumanCharacterKit`) | **156 tris, 13 renderers**; hover alien 84 tris | file header (by construction) |
| Building | massing cube (12) + dressing: `BuildingDresser` has 121 primitive spawn sites, 48 of them sphere/cylinder; `BaseDresser` 131 sites, 85 sphere/cylinder | call-site count; per-building totals need the Editor census |
| Street furniture | `RoadDresser` 47 spawn sites, 25 sphere/cylinder | same |
| Stock spheres in the whole client | **75 call sites** (BaseDresser 85 sphere+cylinder, BuildingDresser 48, MonsterBody 29, RoadDresser 25) | grep |
| Window grid | one mesh + one draw per building (docs/33) — the model to copy | |
| Per-frame per-window animation | distance-gated at 250 m (docs/12 Tier 0) | |
| Batching | road surfaces and DistantSkyline dressing static-batched; Engagement/LocalCity dressing not (must stay damage-mutable) | docs/12 Tier 0 / Tier 3 |
| Frame-time numbers | **none exist** — no Profiler capture has ever been taken | docs/12 Tier 0 |

The Lab's JS renderer (`site/creature-renderer.js`) has a tessellation
dial (`_detail`, `segFor()`) that rebuilds a creature until it fits
`TRI_BUDGET = 9000`. The C# port (`creature-mesh`) **dropped that dial**
(its README lists it under "dropped, future passes"). That dial is the
LOD mechanism this pipeline needs; it is the first item in §11.

### 4.2 Targets

Budgets are per *instance on screen* and sized to the worst realistic
case in §10.3. "Renderers" means Unity `Renderer` components, which is
the draw-call proxy that matters under the SRP Batcher.

| Class | Examples | LOD0 (Close) | **LOD1 (Normal)** | LOD2 (Overview) | LOD3 (Map) | Renderers at LOD1 |
| --- | --- | --- | --- | --- | --- | --- |
| **Hero / major unit** | mastermind-tier monster, Big Brain, faction HQ | ≤ 9,000 | **≤ 3,500** | ≤ 900 | impostor quad or cull | ≤ 4 |
| **Standard unit** | every other monster body + legs + wings | ≤ 9,000 (dial) | **≤ 3,000** | ≤ 800 | 2-tri faction-colour billboard, or cull + minimap | ≤ 3 |
| **Mass humanoid** | Worker, Soldier, Citizen, police, militia | 156 (as built) | **156** | ≤ 60 (merged torso+head+legs, 4–5 cubes) | cull | ≤ 13 now → ≤ 2 after merge (§7) |
| **Vehicle** | Tank, TrafficCar, TramCar | ≤ 600 | **≤ 300** | ≤ 100 | cull | ≤ 3 |
| **Important prop** | streetlamp, marquee, water tower, Tesla coil | ≤ 300 | **≤ 150** | ≤ 40 | cull | 1–2 |
| **Background prop** | hydrant, crate, bench, mailbox, tree, rock | ≤ 100 | **≤ 60** | cull at Overview unless > 3 m | cull | 1 |
| **Building (tier 1 house)** | | ≤ 500 | **≤ 350** | massing + roof form + window grid | massing + window grid | ≤ 6 |
| **Building (apartment / office)** | | ≤ 1,200 | **≤ 900** | massing + roof + window grid + one sign | massing + window grid | ≤ 8 |
| **Landmark** | | ≤ 5,000 | **≤ 3,500** | ≤ 1,200 | ≤ 300 | ≤ 12 |
| **Faction base building** | Factory, Control Centre, HQ | ≤ 4,000 | **≤ 2,500** | ≤ 800 | ≤ 200 | ≤ 10 |
| **Distant environment** | table-edge rim, backdrop ring, skyline fill | textures + repetition; ≤ 50 tris per module | | | | static-batched |
| **Gameplay proxy** | selection collider, click plane, hazard volume | 0 rendered | | | | 0 |

Justifying the monster LOD1 number: at 26 px on screen, 3,000 triangles
is already more than 100 triangles per pixel row. Silhouette, three
material masses, and joint deformation are all that survive at that
size. Dropping ellipsoid `seg` from 14 to 8 turns a 616-tri ellipsoid
into 208; tube `sides` from 10 to 6 is a 40 % cut; the dial at
`_detail ≈ 0.55` lands a default body near 3k. LOD2 at `_detail ≈ 0.3`
with the floor of 3 segments lands near 800.

**Global renderer ceiling for a battle view at the Normal band:** ≤ 2,500
renderers in frustum, of which ≤ 600 are unit renderers. Today 50
monsters alone would be 600–1,150 renderers before a single humanoid or
building.

---

## 5. LOD policy

### 5.1 Transitions are screen-height fractions, not metres

Unity's `LODGroup` switches on the object's bounds height as a fraction
of screen height, which is exactly the right metric for a zooming
camera. Set thresholds from §1.1, not by feel:

| Class | LOD0 while ≥ | LOD1 while ≥ | LOD2 while ≥ | below → |
| --- | --- | --- | --- | --- |
| Standard monster (4 m) | 6 % (≈ h < 25 m) | 1.5 % (≈ h < 110 m) | 0.6 % (≈ h < 250 m) | impostor / cull |
| Hero monster / base building | 4 % | 1.0 % | 0.4 % | impostor |
| Humanoid (2 m) | — (LOD0 = LOD1) | 0.8 % | 0.3 % | cull |
| Background prop (1 m) | — | 0.5 % | cull | cull |
| Building | massing never culls; dressing follows the engagement zone (docs/18 §5) | | | |

**`lodBias` must be 1.0 on both quality tiers once `LODGroup`s exist.**
`QualitySettings.asset` currently sets **2** on the PC tier, which would
silently double every threshold above and defeat the budget. Hero
generosity belongs in the hero's own thresholds, not in a global
multiplier. Use `LODGroup` cross-fade (URP supports it) only on hero
units; mass units pop, and at 26 px nobody sees a pop.

### 5.2 Buildings: the engagement zones *are* the LOD system

docs/18 §5's three zones (Engagement ≤ 175 m, LocalCity ≤ 1 km,
DistantSkyline beyond) already classify buildings, and Tier 3
(docs/12) static-batches DistantSkyline dressing. The standard extends
that into a real LOD ladder:

| Zone | Dressing built | Window grid | Shadows | Batching |
| --- | --- | --- | --- | --- |
| Engagement | full LOD1 dressing, damage-mutable | on | cast + receive | none (must stay mutable) |
| LocalCity | LOD1 minus props < 1 m and minus signage animation | on | cast (massing only) | none |
| DistantSkyline | massing cube + roof form + window grid only | on, animation frozen | receive only | static-batched (already) |

Zone centres are static today (landmarks + HQs). When `SimBridge` grows
a "where is combat" query the same table applies with live centres.

### 5.3 Impostors for the Map band

At 250–400 m a monster is 5–8 px. Rendering 3D geometry there is waste.
Two acceptable implementations, cheapest first:

1. **Cull the body, keep the minimap blip and the selection ring.** The
   minimap already draws every unit as a proportional blip
   (docs/12, 2026-08). Zero render cost.
2. **A 2-triangle faction-colour billboard** with the unit's LUT primary
   colour, drawn via `Graphics.RenderMeshInstanced` (the pattern
   `LowPolyFireSystem` already uses). One draw call for all units.

Never keep a full skinned/procedural body alive in the Map band.

---

## 6. Repeated-unit rules (the 50-unit test)

A single monster seen in the Close band should look excellent. Fifty
monsters plus a hundred humanoids in the Normal band must hold 60 fps.
For anything that can appear in numbers:

- **One mesh per LOD, vertex-coloured, one shared material** (§7). The
  per-chunk flat colours `creature-mesh` already emits are exactly what
  vertex colour is for; the stitch/seam overlay and origin-specific
  gloss are texture-atlas lookups on the same material.
- **Assemble once, at match load** (docs/08's handshake rule stands):
  LOD0/1/2 meshes are all built during the loading screen. No mid-match
  tessellation, no per-frame `RecalculateNormals`.
- **Gait rigs stay pivot-based, not skinned.** The no-skate procedural
  gait (docs/25, docs/34) moves whole limb transforms; a 4-leg monster
  is ~12 pivots and a humanoid 13. That is already cheap; keep it under
  20 pivots per monster and never add a `SkinnedMeshRenderer` for a
  mass unit.
- **LOD-aware animation:** the gait/idle/breath ticks run every frame
  in Close/Normal, every second frame in Overview, and not at all in
  Map. Wing flap, blink, gaze, and breath channels are Close/Normal only.
- **No per-unit real `Light`, no per-unit particle system that lives
  longer than its effect.** Glow is emissive (`GlowPointRegistry` if it
  must compete for a real light). Muzzle flash / hit sparks are pooled.
- **Health bars, selection rings, and labels are IMGUI or one shared
  instanced quad** — never one `Canvas` per unit.

---

## 7. Material and draw-call rules

The project already has the right instincts (shared cached materials in
`BuildingDresser.M()`, `MaterialPropertyBlock` instead of `new Material`
since Tier 0, SRP Batcher on in both pipeline assets, `enableInstancing`
on the shared rig/window materials). Two facts sharpen them:

- **URP's SRP Batcher does not batch a renderer that carries a
  `MaterialPropertyBlock`** (documented Unity limitation). Such a renderer
  falls back to a regular draw, and to GPU instancing only if the
  property it overrides is an instanced property in the shader —
  `_BaseColor` and `_BaseMap_ST` in URP/Lit are **not**. Consequences in
  this codebase, to be **confirmed in the Frame Debugger** before acting
  (no Editor exists in the environment that wrote this):
  - `SpawnPrim` → `ApplyWorldScaledTiling` puts an `_BaseMap_ST` block on
    **every textured dressing primitive** (Tier 1), which likely takes
    all of them out of SRP batching.
  - `HumanCharacterKit` and the damage override use per-renderer colour
    blocks — 13 unbatched draws per humanoid.
- **Fix pattern, cheapest first:** (1) a **world-space-UV / triplanar
  variant of the atlas material** so tiling needs no per-object state at
  all; (2) **colour-keyed shared materials** (the `M()` cache already
  does this) instead of colour blocks for anything static; (3) **vertex
  colour** for anything procedural (creatures, rig cubes via a tiny
  per-part mesh with baked colour); (4) reserve `MaterialPropertyBlock`
  for genuinely dynamic per-instance state (damage tint, emissive
  animation) on a bounded set of renderers.

Hard rules:

- ≤ 1 material per mass unit at LOD1/2; ≤ 2 for heroes (opaque +
  translucent/emissive). Blob's translucent gelatin over organs is the
  one sanctioned second pass.
- ≤ 4 material slots per building dressing set; all from the existing
  ~21-material cache + `PbrTextureAtlas`. New colours come from the LUT
  and join the cache; they never mint a material inline.
- Textures: atlas everything that shares a shader. Max 2048² on PC, 1024²
  on Mobile for an atlas; no single prop texture above 512².
- Transparent/alpha-blended geometry is limited to water, glass, blob
  gelatin, VFX, and the hologram. Everything else is opaque or
  alpha-tested.

---

## 8. Shadow rules

Shadows are the cheapest thing that makes a 50°-down camera read as a
diorama, and the easiest to overspend. The zoom-tied shadow distance
(§1) is already right. On top of it:

| Caster class | Main-light shadows | Notes |
| --- | --- | --- |
| Monsters, humanoids, vehicles | **cast** at LOD0/LOD1; **off** at LOD2+ | unit shadows are the diorama read |
| Building massing cubes | cast | the long dawn/dusk shadows (docs/28 row 20) come from these |
| Building dressing (cornices, awnings, roof forms) | cast in Engagement zone only | |
| **Window grid meshes** | **off** — currently `ShadowCastingMode.On` in `BuildingWindowGrid.cs` | a flat layer on a facade; its shadow is the wall's shadow |
| Street furniture < 1 m, lane paint, crosswalks, sidewalks | off (receive only) | 7 px objects have 2 px shadows |
| Trees, rocks, water towers, lamp posts | cast in Engagement/LocalCity; off in DistantSkyline | |
| VFX, holograms, cursors | off (already) | |

Pipeline-asset settings this standard sets:

| Setting | PC now | PC target | Mobile now | Mobile target | Why |
| --- | --- | --- | --- | --- | --- |
| Main-light cascades | 4 | **2** | 1 | 1 | shadow distance never exceeds 250 m and the camera pitch is fixed; 2 cascades at 2048 give more texels where units stand |
| Additional-light shadows | on, 2048 | **spot only, 1024, ≤ 4 lights** | off | off | a shadowed point light is six shadow passes; only the aimed streetlamp spots earn one |
| Soft shadows | on | on | off | off | |
| `QualitySettings.shadowDistance` (40) | ignored under URP; leave, but never tune it expecting an effect | | | | |

Distant shadows are hidden by fog at the Overview band; do not raise the
250 m cap to "fix" them.

---

## 9. VFX rules

VFX are judged at the same 26 px. `SpecialAttackVfx`'s area/psionic
shapes and `LowPolyFireSystem`'s instanced flames are the right pattern:
few, large, high-contrast primitives.

- **Per-instance caps:** ≤ 64 particles, ≤ 2 s lifetime for combat
  effects, ≤ 1 real `Light` per effect and only via the budget, ≤ 2
  materials.
- **Screen-space test:** a one-shot effect must read as a shape at
  26 px: a ring, a bolt, a burst disc, a column. Sparks and embers are
  Close-band garnish inside the cap, never the effect.
- **Ambient plumes** follow the aesthetic skill §7a rules (column, size
  variety, growth cap); count per chimney ≤ 12 live puffs.
- **Instanced first:** any effect that can appear ×20 (fire, muzzle
  flash, hit sparks, blood) is `RenderMeshInstanced` or a single pooled
  `ParticleSystem`, not one system per unit.
- **No effect renders in the Map band** except area-of-effect rings,
  which are gameplay information.

---

## 10. Validation — the two-test gate

An asset or visual system is finished when it passes both tests on the
creator's machine. Nothing in this repo's history has a frame-time
number attached to it (docs/12 Tier 0); this gate is how that changes.

### 10.1 Visual test

Two screenshots from `SnapTo(centre, 70f)` (the default framing), same
seed, same Lumen phase, before and after. The after must be
*substantially* better or *indistinguishable* at 100 % zoom. A change
that is only visible when the screenshot is enlarged has failed the
band test and belongs in LOD0 only.

### 10.2 Performance test

`Profiler.BeginSample` phases already wrap the city build;
`LogCityBuildCensus` already logs GameObject/renderer/collider counts.
Extend the census with **total triangles and renderers by category**
(units / humanoids / buildings / props / VFX) and read the Frame
Debugger's **SRP Batch count vs. plain draw count** at the default
framing. Record in the docs/12 entry for the change:

- ms per frame (CPU main, render thread, GPU) at Normal band, Village
  preset, night phase (the heaviest lighting).
- renderer count and triangle count in frustum.
- number of SRP batches vs. unbatched draws.

### 10.3 The unit-count curve

For any repeated asset, spawn it in the Normal band and record frame
time at **1 → 10 → 25 → 50** instances. The curve must be roughly
linear with a slope that keeps the worst realistic case under budget:

| Worst realistic Normal-band frame (v0.1) | Count |
| --- | --- |
| Monsters | 50 |
| Humanoids (workers, soldiers, police, militia, citizens) | 100 |
| Vehicles (traffic, tanks, trams) | 16 |
| Buildings in frustum (Village) | ~120 |
| Live VFX | 20 |
| Real lights | `DynamicLightBudget.budget` |

Targets: **PC 60 fps (16.6 ms) at 1440p**; **Mobile tier 60 fps at 30
monsters + 60 humanoids** (docs/08's mobile line, unchanged). If the
50-monster frame is over budget, the fix order is: LOD thresholds →
renderer merge → shadow casters → triangle budget → unit cap (the last
one is a design change and goes through docs/05/docs/23, not this doc).

### 10.4 Definition of done for a visual change

- [ ] Band named; pixel size at 70 m computed for the new detail.
- [ ] LOD levels exist where §4.2 requires them; thresholds from §5.1.
- [ ] No stock Sphere/Capsule; no inline `new Material`; no
      `MaterialPropertyBlock` on a repeated static renderer.
- [ ] Shadow casting set per §8.
- [ ] Census + Frame Debugger numbers recorded in docs/12.
- [ ] 1/10/25/50 curve recorded if the asset repeats.
- [ ] Visual before/after at default framing attached or described.

---

## 11. Backlog, ranked by visible-improvement-per-millisecond

Ordered by expected milliseconds saved against the §10.3 frame, per unit
of work. Step 0 gates the rest: the brief's own rule is that budgets are
set from measurement, and no measurement exists yet.

0. **Measure.** Extend `LogCityBuildCensus` with triangle/renderer
   totals by category; add a dev key that spawns 10/25/50 default
   monsters in a ring around the camera focus; take the first Profiler
   and Frame Debugger capture at the default framing. Record it in
   docs/12 as the baseline every later entry compares against.
1. **Port the tessellation dial into `creature-mesh`** (`MeshCore`:
   multiply `seg`/`sides`/lathe `seg` by a `Detail` factor with the JS
   floors) and have `LabMeshBuilder.Attach` build LOD0/1/2 at
   `Detail = 1 / 0.55 / 0.3`, wired into one `LODGroup` per monster with
   §5.1 thresholds plus Map-band cull. Biggest single per-unit saving
   (≈ 12k → 3k tris in the band that matters).
2. **Merge creature chunks into one vertex-coloured mesh per LOD** with
   one shared vertex-colour URP material (translucent blob shell stays a
   second renderer). 12–23 renderers → 2–3 per monster; at 50 monsters
   that is ~800 fewer draws.
3. **Low-poly sphere/cylinder in `PropLibrary`** (icosphere subdiv-1 ≈
   80 tris, 8-side cylinder ≈ 30 tris) and a lint rule: no
   `PrimitiveType.Sphere/Capsule` outside VFX and the Big Brain jar.
   Touches `BaseDresser`, `BuildingDresser`, `RoadDresser`,
   `MonsterBody` fallbacks.
4. **Confirm and fix the `MaterialPropertyBlock` batching break** (§7):
   Frame Debugger first; then world-space-UV tiling material instead of
   per-prim `_BaseMap_ST`, and colour-keyed shared materials or baked
   vertex colour for humanoid parts.
5. **Shadow hygiene** (§8): window grids off, sub-1 m props off,
   cascades 4 → 2 on PC, additional-light shadows spot-only.
6. **LOD-aware animation** in `HumanCharacterAnimator` and
   `MonsterBody`: tick rate by band; freeze in Map.
7. **Map-band impostor** via instanced faction quads or cull + minimap.
8. **Set `lodBias` to 1.0 on the PC tier** when step 1 lands.

Each step is its own docs/12 entry with the §10 numbers.

---

## 12. Relationship to other docs

- **docs/08** — its "Performance budgets (v0.1)" table assumed imported
  part meshes with blend shapes. The actual pipeline is procedural
  (`creature-mesh` ← the Lab's JS). docs/08's *goals* (determinism,
  ≤ 30 monsters on mobile, one uber-material, assembly at load) stand;
  its triangle/LOD numbers are superseded by §4.2 here.
- **docs/18 §5** — engagement zones are the building LOD ladder (§5.2).
- **docs/21** — the primitive-based world pipeline this standard budgets.
- **docs/28 / docs/33 / lighting skill** — the two-tier light model and
  the GPU window grid are the reference implementations of "cheap at
  scale"; this doc adds the shadow-caster and cascade rules on top.
- **docs/34 / docs/35** — the humanoid rig's own ~120–300-tri budget is
  adopted unchanged; §7 adds the renderer-count concern.
- **`maddr-aesthetic-preferences` skill** — taste; this doc is cost. When
  they conflict, §0 rule 9 applies: the cheaper way to get the same read
  at 70 m wins, and the skill's "silhouette variety is the selling
  point" is a *support* for this doc, not an exception to it —
  silhouettes are cheap.
- **`maddr-performance-art-standard` skill** — the condensed version of
  this doc for every future visual session.
