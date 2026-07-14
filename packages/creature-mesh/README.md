# creature-mesh

Engine-agnostic C# port of the Lab's creature renderer
(`site/creature-renderer.js`, docs/08) — the in-game monster
regenerator's geometry half. Feed it a `GenomeDto` (from
`packages/roster-client`) and it returns the same stitched b-movie body
the website shows, as plain vertex/normal/triangle data bucketed by
material, ready for `unity-client`'s `LabMeshBuilder` to turn into
Unity meshes. Zero `UnityEngine` reference, zero I/O.

## Pass-1 scope (deliberate)

- **Full geometry engine**: ellipsoid, parallel-transport tube, torus,
  lathe, curved cone, limb-joint hardware — 1:1 with the JS, double
  precision, same constants.
- **Tetrapod plan at full fidelity**: torso lathe (pear→gorilla
  profiles), bolted brass belt pelvis, neck, all four brain-tier heads
  (dim / average / gifted / **mastermind's exposed brain under a
  riveted glass dome**), franken face (brow, jaw, tusks, heart-tier
  neck bolts — titan bolts glow), tail gene, and every hand (10),
  sensor (4), and eye (5) part family, honoring graft-hue,
  dormant-sensor, and headless rules.
- **Legs are NOT baked into the mesh.** `CreatureMeshResult.Leg`
  returns the socket frame (position/normal/length/family); the Unity
  gait rig mounts its no-skate legs there. That contract is why feet
  never slide.
- The other 8 body plans return `null` from `CreatureBuilder.Build`
  and keep their placeholder silhouettes until their own pass.

Dropped relative to the JS (future passes): per-vertex color gradients
(each material chunk is flat-colored), texture tiling, blink/gaze/
breath animation channels, glow halos, faction kits (robot/alien
re-skins), the LOD detail dial.

`Builder.FixWinding()` runs at the end of every build: Unity
single-sides materials where the Lab's shader is two-sided, so any
triangle wound against its analytic normals is flipped rather than
becoming an invisible hole.

## Dual toolchain (same pattern as citygen-core / roster-client)

- Unity consumes `src/` via `package.json` + asmdef
  (`unity-client/Packages/manifest.json`, `file:` reference).
- dotnet builds `src/CreatureMesh.csproj` (net8.0 pinned to C# 9, no
  implicit usings — the asmdef compiler's ceiling) and the xunit suite
  in `Tests~/` (tilde dir: invisible to Unity's importer, like the
  `bin~`/`obj~` build outputs).

```
cd packages/creature-mesh
dotnet test Tests~/CreatureMesh.Tests.csproj
```
