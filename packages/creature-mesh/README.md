# creature-mesh

Engine-agnostic C# port of the Lab's creature renderer
(`site/creature-renderer.js`, docs/08) — the in-game monster
regenerator's geometry half. Feed it a `GenomeDto` (from
`packages/roster-client`) and it returns the same stitched b-movie body
the website shows, as plain vertex/normal/triangle data bucketed by
material, ready for `unity-client`'s `LabMeshBuilder` to turn into
Unity meshes. Zero `UnityEngine` reference, zero I/O.

## Scope (pass 2: all plans + dressed legs)

- **Full geometry engine**: ellipsoid, parallel-transport tube, torus,
  lathe, curved cone, limb-joint hardware — 1:1 with the JS, double
  precision, same constants.
- **All nine body plans at full fidelity**: tetrapod (pear→gorilla
  torso, deltoids, stitch seam), blob (translucent gelatin over
  visible heart chambers / stomach / coiled gut, skirt, boils),
  serpentine (ground coil, S-neck, cobra hood at high girth, downward
  fangs, forked tongue — keeps its own skull at every brain tier, like
  the Lab), winged (double-sided bat-wing membranes + bone/finger
  rig, devil-spade tail), crab (low carapace, fused head, capped
  chelipeds), arachnid (cephalothorax + abdomen + waist pinch, stubby
  pedipalps), avian (forward-leaning raptor profile, long neck),
  treant (bark trunk + five ground roots), floater (gunmetal drone
  hull, stabilizer fins, glowing thruster ring, cockpit head).
  Shared machinery everywhere it's shared in the JS: bolted brass belt
  pelvis, four brain-tier heads (**mastermind's brain under a riveted
  glass dome**), franken face with heart-tier neck bolts, tail gene,
  and every hand (10), sensor (4), and eye (5) part family, honoring
  graft-hue, dormant-sensor, headless, `tiny`, and arm-reach-cap
  rules. Unknown plans fall back to tetrapod, same as the JS.
- **Legs are NOT baked into the mesh** — `CreatureMeshResult.Leg`
  returns the socket frame and the Unity gait rig mounts its no-skate
  legs there (null on blob/serpentine/treant/floater, which ignore the
  leg slot). **`LegKit`** supplies the family's real geometry for the
  rig: hip joint hardware, tapered upper/lower segments authored on
  the rig's y∈[−1,+1] convention (proximal radius at −1), and
  family feet — hoof, talon fan (side-mirrored in data, never by
  negative scale), insect needle point, piston strut, jet nozzle,
  tendril tip, ring-stitched stump.
- **Wings are NOT baked into the mesh either** (winged plan only, same
  reasoning as legs) — `CreatureMeshResult.Wing` returns a
  `WingSocketInfo` with each side's full membrane/bone/finger/joint
  geometry as its OWN chunk set, authored **root-relative** (the
  shoulder joint sits at local origin) so Unity can parent it at the
  root's world position and rotate the whole thing as a rigid hinge —
  the flap — with zero per-frame vertex work. `RootL`/`RootR` mirror
  across x; each side's `side=±1` is baked into its own geometry (not a
  negative Unity scale, which would flip winding).

Dropped relative to the JS (future passes): per-vertex color gradients
(each material chunk is flat-colored), texture tiling, blink/gaze/
breath animation channels, glow halos, faction kits (robot/alien
re-skins), piston_leg's tank-tread variant (its spider-strut mode is
used for the walking rig), the LOD detail dial.

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
