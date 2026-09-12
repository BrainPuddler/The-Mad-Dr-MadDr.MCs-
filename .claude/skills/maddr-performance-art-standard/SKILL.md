---
name: maddr-performance-art-standard
description: The creator's standing rule that PERFORMANCE IS A PRIMARY ART REQUIREMENT for MadDr.MCs's semi-isometric, StarCraft-2-style camera — camera facts (fixed 50° pitch, 60° FOV, 8–400 m height, default 70 m), the four zoom bands, on-screen pixel sizes per asset class, triangle/renderer budgets per LOD, LOD transition thresholds, shadow-caster rules, material/draw-call rules (including the URP SRP-Batcher-vs-MaterialPropertyBlock gotcha), VFX caps, and the mandatory two-test validation gate (visual before/after at default zoom + 1/10/25/50 unit cost curve). Consult this BEFORE adding or changing ANY geometry, material, shadow, particle, light, or animation in unity-client or creature-mesh — even when the request is purely "make it look better" — and when reviewing a visual change for whether it is finished. Companion to maddr-aesthetic-preferences (taste) and maddr-lighting-system (pipeline). Full doc with the measured numbers: docs/39.
---

# MadDr.MCs performance-first art standard (condensed)

Source of truth: `docs/39-performance-art-standard.md` (creator brief,
2026-09, numbers measured from the real client). This is the checklist
version. When a specific number matters, read the doc, not this.

The one rule: **maximum perceived visual quality at the actual gameplay
camera distance for minimum performance cost.** Better art is not more
polygons, textures, materials, particles, or bones. It is silhouette,
material separation, readability, and lighting — at 70 m.

## 1. The camera you are building for (measured, `SimpleCameraRig.cs`)

- Perspective, **60° vertical FOV, fixed 50° pitch**, free yaw.
- Height clamped **8–400 m**; the match starts at **70 m** (`SnapTo(.., 70f)`).
- Shadow distance is zoom-tied: `1.9·h + 15`, capped 250 m.
- Ground is y = 0; 1 hex = 20 m; buildings 6/12/30/40 m by tier.

**Zoom bands** (use these names): Close 8–25 m (LOD0) · **Normal 25–110 m,
default 70 (LOD1, the LOD that matters)** · Overview 110–250 m (LOD2) ·
Map 250–400 m (impostor/cull).

**At the default 70 m zoom, 1080p:** ~10 px per ground metre, ~6.6 px per
upright metre. A hydrant is **7 px**, a human **13 px**, a monster
**26 px**, a 12 m apartment ~80 px, a rivet/gauge/bevel **1–2 px
(invisible)**. The view covers ~190 × 180 m (≈ 9 × 9 hexes).

## 2. Before adding geometry, answer in order

1. Which band is it seen in?
2. How many pixels tall at 70 m? Under ~4 px → not geometry.
3. Does it change the silhouette? (horn/hump/tail/turret/cornice/awning:
   yes; rivet/dial/strap/inner surface: no)
4. Does it carry gameplay information? (origin from shape, faction/state
   from colour — aesthetic skill §2/§5)
5. Can texture, roughness, emissive, vertex colour, or the stitch overlay
   do it instead?
6. Will it appear in quantity (>10 on screen)? Then repeated-unit
   budgets apply, not hero budgets.

Survives all six → LOD0 only, unless it also survives step 3 at 26 px →
LOD1.

## 3. Budgets (triangles per instance; renderers = draw-call proxy)

Stock Unity primitives: Cube 12, Cylinder 80, **Sphere 760, Capsule 832**.
**Never spawn a stock Sphere/Capsule in anything that repeats** — use
`PropLibrary`'s low-poly sphere (~80) / 8-side cylinder (~30).

| Class | LOD0 | **LOD1 (Normal)** | LOD2 | Map | Renderers @LOD1 |
| --- | --- | --- | --- | --- | --- |
| Hero monster / base building / Big Brain | ≤ 9,000 | **≤ 3,500** | ≤ 900 | impostor | ≤ 4 |
| Standard monster (body+legs+wings) | ≤ 9,000 | **≤ 3,000** | ≤ 800 | billboard or cull | ≤ 3 |
| Mass humanoid (rig is 156 tris) | 156 | **156** | ≤ 60 merged | cull | ≤ 2 after merge |
| Vehicle | ≤ 600 | **≤ 300** | ≤ 100 | cull | ≤ 3 |
| Important prop (lamp, marquee, water tower) | ≤ 300 | **≤ 150** | ≤ 40 | cull | 1–2 |
| Background prop (hydrant, crate, tree) | ≤ 100 | **≤ 60** | cull unless > 3 m | cull | 1 |
| House / apartment-office / landmark | 500 / 1,200 / 5,000 | **350 / 900 / 3,500** | massing+roof+window grid | massing+window grid | 6 / 8 / 12 |

Where the client actually is (2026-09): monsters are **9.6k–16k tris in
12–23 renderers each, at every zoom, with zero `LODGroup`s anywhere**;
75 stock-sphere call sites. The Lab's JS renderer has the tessellation
dial (`_detail`/`segFor`, `TRI_BUDGET = 9000`) that the C# `creature-mesh`
port dropped — porting it is the LOD mechanism (docs/39 §11 item 1).

**First real Editor measurement (2026-09-12), Village preset:
85,347 GameObjects / 83,889 renderers / 81,128 colliders.** Root cause of
the collider count: `RuntimeCityBuilder.BuildBuildings` gives **every
footprint hex of every building its own real `BoxCollider`**
(`SpawnCube(hex, …, keepCollider: true)` in a `foreach (var hex in
building.Footprint)` loop) when one collider per building would do —
all of them resolve to the same `Building` in `_buildingByCollider`
anyway. Colliders get **no frustum-culling discount**, unlike renderers,
so this is the single highest-value fix in the backlog: measured, not
estimated, one function, and it is already the smallest preset (docs/39
§11 item 0.5, ranked above the monster LOD work).

Global ceiling at a Normal-band battle: ≤ 2,500 renderers in frustum,
≤ 600 of them units. Worst realistic frame: 50 monsters + 100 humanoids
+ 16 vehicles + ~120 buildings + 20 VFX at 60 fps.

## 4. LOD rules

- `LODGroup` thresholds are **screen-height fractions**, from the pixel
  math: standard monster LOD0 ≥ 6 %, LOD1 ≥ 1.5 %, LOD2 ≥ 0.6 %, below →
  impostor/cull. Humanoid LOD1 ≥ 0.8 %, LOD2 ≥ 0.3 %. Props cull at
  Overview unless > 3 m.
- **`lodBias` must be 1.0** on both quality tiers once LODGroups exist
  (PC tier is currently 2 — it would silently double every threshold).
  Hero generosity lives in the hero's own thresholds.
- Buildings: the docs/18 §5 engagement zones are the LOD ladder —
  Engagement = full mutable dressing; LocalCity = minus props < 1 m and
  signage animation; DistantSkyline = massing + roof + window grid,
  static-batched (already), receive-only shadows.
- Map band: cull the body and let the minimap blip carry it, or one
  instanced 2-tri faction-colour quad. Never a full body at 5–8 px.
- All LODs are built at match load (docs/08 handshake rule). No
  mid-match tessellation or `RecalculateNormals`.

## 5. Materials and draw calls

- One shared material per family; colour via **vertex colour** or the
  **colour-keyed material cache** (`BuildingDresser.M()`); never an
  inline `new Material`.
- **URP's SRP Batcher does not batch a renderer carrying a
  `MaterialPropertyBlock`**, and `_BaseColor`/`_BaseMap_ST` are not
  instanced properties in URP/Lit. So `SpawnPrim`'s per-prim
  `_BaseMap_ST` tiling block and `HumanCharacterKit`'s per-part colour
  blocks likely fall out of batching — **confirm in the Frame Debugger
  before acting**, then fix with a world-space-UV tiling material,
  colour-keyed shared materials, or baked vertex colour. Reserve
  `MaterialPropertyBlock` for genuinely dynamic state on a bounded set
  (damage tint, emissive animation).
- ≤ 1 material per mass unit (heroes ≤ 2; blob's translucent shell is
  the sanctioned second pass); ≤ 4 slots per building set; atlas
  anything sharing a shader; no prop texture > 512².
- Transparency only for water, glass, blob gelatin, VFX, hologram.

## 6. Shadows

Cast: units/vehicles at LOD0–1, building massing, Engagement-zone
dressing, trees/lamps/water towers in Engagement/LocalCity.
**Off:** window-grid meshes (currently `On` in `BuildingWindowGrid.cs` —
wrong), props < 1 m, lane paint/sidewalks (receive only), VFX,
DistantSkyline dressing, units at LOD2+.
Pipeline targets: PC cascades 4 → **2**; additional-light shadows
**spot-only, 1024, ≤ 4**; Mobile unchanged. Don't raise the 250 m cap.
`QualitySettings.shadowDistance` is ignored under URP.

## 7. Animation and VFX

- Gaits stay pivot-based (≤ 20 pivots per monster, 13 per humanoid);
  no `SkinnedMeshRenderer` on mass units.
- **LOD-aware ticking:** full rate in Close/Normal, half in Overview,
  frozen in Map. Wing flap/blink/gaze/breath are Close/Normal only.
- VFX: ≤ 64 particles and ≤ 2 s per combat effect, ≤ 2 materials, real
  lights only via `GlowPointRegistry`; must read as a shape (ring, bolt,
  disc, column) at 26 px; anything that appears ×20 is instanced
  (`LowPolyFireSystem` pattern) or pooled. No VFX in the Map band except
  AoE rings.
- Ambient plumes follow aesthetic skill §7a; ≤ 12 live puffs per source.

## 8. Empire of Sin — what the reference means here

Take: night + rain + wet-surface roughness contrast as the hero look;
flat facades with texture ornament and only silhouette-breakers (awnings,
fire escapes, sign boxes) as geometry; streetlamp pools and signage as
depth cues; units read by 1 dominant + 1 secondary + 1 accent LUT
colour; fog swallowing the far LOD; boxy street props.
Leave: the 1920s period (we are 1950s), the 4–6-hero squad scale (we
draw armies — per-unit cost must be ~10× lower), close-zoom hero detail.

## 9. Definition of done (nothing is finished on looks alone)

- [ ] Band named; pixel size at 70 m computed for any new detail.
- [ ] LODs exist per §3 with §4 thresholds.
- [ ] No stock Sphere/Capsule; no inline `new Material`; no
      `MaterialPropertyBlock` on a repeated static renderer.
- [ ] Shadow casting set per §6.
- [ ] **Visual test:** before/after screenshot at `SnapTo(.., 70f)` —
      substantially better, or indistinguishable (then it's LOD0-only).
- [ ] **Performance test:** census triangles/renderers by category +
      Frame Debugger SRP-batch vs plain-draw count, recorded in docs/12.
- [ ] **1 → 10 → 25 → 50 curve** recorded for anything that repeats.

If forced to choose between two approaches, pick the one with the
greatest visible improvement at 70 m for the lowest cost — and say so in
the docs/12 entry.

## 10. Self-check — the mistakes this standard exists to catch

1. Detail that only shows in a zoomed-in screenshot, shipped as
   geometry.
2. A stock Sphere/Capsule reached for because it was convenient.
3. A new part/prop/effect with no LOD because "it's just one" — then it
   appears ×50.
4. A per-instance `Material` or a `MaterialPropertyBlock` on something
   static and numerous.
5. A real `Light` or `ParticleSystem` per unit.
6. Shadows left on for something whose shadow is 2 px.
7. "Done" declared with no census/Frame Debugger number and no
   before/after at the default framing.
