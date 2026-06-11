# 08 — Creature Visualization: Genome → 3D Monster

Status: Draft v0.1 · Pillars served: 1 · Consumes the normative genome schema in [06-mutator-design.md](06-mutator-design.md). Highest technical-risk art system — validated by the Phase-1 spike ([11-roadmap.md](11-roadmap.md), [10-engine-evaluation.md](10-engine-evaluation.md)).

## Goals & constraints

1. Every genome renders as a distinct, *ownable* 3D monster (pillar 1).
2. **Deterministic**: assembly is a pure function of the genome — the same genome must render identically on the owner's phone, their PC, and the opponent's device. No randomness, no device-dependent paths in assembly. (Cross-refs: [06](06-mutator-design.md) immutable genomes, [09](09-multiplayer-architecture.md) match handshake.)
3. **Mobile performance**: up to ~30 simultaneous monsters at 60 fps on a mid-range Android device.
4. Authoring scales: a small art team must be able to grow variety linearly while combinatorial variety grows exponentially.

## The strategy decision: modular socketed parts + parametric deformation

**Chosen approach.** For each of the 6 archetype skeletons, artists author a library of **part meshes** built to a shared socket-and-bone convention. At load time the client assembles a monster:

1. Select the archetype skeleton (rig) from the `bodyPlan` gene.
2. For each slot, attach the part mesh for `partFamilyId` to the skeleton's socket, skinned to the shared bones.
3. Apply `sizeGene`: per-part bone scaling, linearly interpolated within the part's artist-authored min/max scale bounds.
4. Apply `variantGene`: weights across the part's 2–4 authored **blend shapes** (e.g., gnarled / sleek / bloated) — `variantGene` 0–255 maps to a deterministic weight vector.
5. Apply pigment genes via the uber-shader (below).

### Rejected alternatives (recorded so we don't re-litigate)

- **Full procedural mesh generation (Spore-style metaball/skin algorithms).** Rejected: an open-ended R&D project demanding specialist engineers, with unbounded risk on mobile performance and on art-directability (the Universal-horror silhouette language in [01-vision.md](01-vision.md) is hard to guarantee when geometry is synthesized). Spore had a AAA team and years; we don't.
- **Texture/material swaps on fixed meshes.** Rejected: kills pillar 1 — every Shambler would be the same silhouette in different colors. Silhouette variety is the whole point; recolors alone read as a gacha skin system.

The socketed-parts approach is the proven middle path (the standard character-customization architecture), and it makes the genome's `{partFamilyId, sizeGene, variantGene}` allele map 1:1 onto the runtime.

## Part library scope & the combinatorics

Launch budget arithmetic (why this is affordable):

- 6 archetypes × ~8 slots avg × 4–6 part families per slot, with families shared across compatible archetypes ⇒ **~150–250 authored part meshes** (each with 2–4 blend shapes).
- Distinct silhouettes available: even a single biped torso slot with 5 families × 256 sizes × blendshape space, across 8 slots, yields a combinatorial space in the billions; *perceptually* distinct monsters comfortably in the tens of thousands. ~200 authored assets buy effectively unlimited variety — this arithmetic is the business case for the whole pipeline.

Part-family catalog growth is the live-ops content lever ([11-roadmap.md](11-roadmap.md) Phase 6).

## Materials & pigmentation

- **One uber-shader** for all monsters (draw-call discipline). Inputs:
  - 3-channel ID mask per part texture; `pigment.primary/secondary/accent` index into a curated gothic palette LUT (curation keeps the art direction; no free RGB — a thousand-hue rainbow horde would break the Universal-horror tone, [01](01-vision.md)).
  - `pigment.pattern` selects from a small pattern-texture array (mottling, veins, patchwork).
  - A **stitching/wear overlay** at every part seam — the unifying Frankenstein motif: every monster visibly *assembled*. Seam placement comes from socket metadata, so it's automatic per assembly.

## Animation & rigging

- **One shared rig + animation set per archetype** (locomotion, attack, harvest channel, death, idle). Parts conform to the rig; they don't bring animation.
- **Limb-count variance rules**: each archetype's animation set declares optional channels — e.g., serpentine ignores leg channels entirely; hulking's shoulder slots are additive layers. A slot left "vestigial" by a low sizeGene still animates (small flailing arms are comedy gold and on-tone).
- Attack timing syncs to Ferocity ([04-combat-model.md](04-combat-model.md)) by playback-rate scaling within authored bounds.

## Performance budgets (v0.1)

| Budget | Value |
| --- | --- |
| Tris per monster, LOD0 | ≤ 8,000 |
| LODs | 3 (LOD2 ≤ 1,500 tris) + impostor for tactical zoom |
| Simultaneous monsters | ≤ 30 (soft-capped by upkeep economy, [05](05-component-economy.md) / [02](02-gameplay-overview.md)) |
| Skinned bones per monster | ≤ 60 active |
| Draw calls | Runtime mesh combining at assembly (one skinned mesh per monster) **or** GPU instancing per part family — engine-dependent choice, see [10-engine-evaluation.md](10-engine-evaluation.md) |
| Assembly time | All match monsters pre-assembled during the loading handshake ([09](09-multiplayer-architecture.md)); zero assembly hitches mid-match |

## Authoring workflow

1. Artist authors a part against the archetype's socket template (provided as a DCC kit: skeleton, socket gizmos, scale-bounds rig, blendshape naming convention).
2. Automated import validation: socket conformity, tri budget, blendshape count, ID-mask presence.
3. Part registers in the **part catalog** with its family ID, slot/archetype compatibility, scale bounds, and component cost hooks ([05](05-component-economy.md), [06](06-mutator-design.md)).

The catalog is data shared by the Mutator server (validation), the client (rendering), and design (costs) — version it with the genome (`genomeVersion`).

## Fallback plan

If the Phase-1 spike misses 60 fps on the reference device: first drop to LOD-based caps (fewer LOD0 monsters on screen), then cut blend-shape variants on mobile, then reduce simultaneous-monster cap to 20 (with matching upkeep retuning in [05](05-component-economy.md)). The socketed-parts architecture itself has no cheaper sibling — if it fails outright, the de-scope ladder in [11-roadmap.md](11-roadmap.md) does **not** permit cutting to texture swaps; we'd re-scope the match size instead. Pillar 1 is load-bearing.
