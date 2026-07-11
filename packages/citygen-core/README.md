# MadDr.CityGen

The engine-agnostic logic core for the City Battlefields track
([docs/18](../../docs/18-city-battlefields.md)) — the same architectural
role `packages/genome-core` plays for the genome
([docs/06](../../docs/06-mutator-design.md)): pure data and pure
functions, no `UnityEngine` reference, no rendering, no I/O. A Unity
project imports this as a plain .NET assembly (or an `.asmdef` reference
once one exists) and builds scenes, prefabs, and MonoBehaviours on top —
nothing here ever will.

**This is a first slice, not the full track.** Implemented so far: the
hex grid index and the attack-arc model. Not yet started: procedural
city generation ([docs/18](../../docs/18-city-battlefields.md) §2),
destructible-building state ([docs/18](../../docs/18-city-battlefields.md)
§3), the engagement-zone LOD manager (§5), or the Mutator-service HTTP
client (§6) — see [docs/18](../../docs/18-city-battlefields.md)'s Open
questions for sequencing (Q14: this whole track doesn't block Phases
1–3, and Phase 1's own combat sandbox doesn't exist in this repo yet
either).

```
dotnet test tests/CityGenCore.Tests.csproj   # build + 26 tests
```

| Module | Contents |
| --- | --- |
| `src/HexCoord.cs` | Axial hex coordinates: neighbors, exact distance, `Ring`/`Range` (aura/radius queries), and the world-space (meters) conversion at `HexCoord.HexMeters = 20` ([docs/18](../../docs/18-city-battlefields.md) §1) |
| `src/Facing.cs` | The attack-arc model — `Facing.ArcOf(attacker, defender, defenderFacing)` classifies Front/Flank/Rear ([docs/04](../../docs/04-combat-model.md) posMod) |

## Why a separate package, not inside genome-core

Different concerns, different lifecycles: `genome-core` is the
genotype/phenotype contract (schema-locked, normative,
[docs/06](../../docs/06-mutator-design.md)); this is spatial/battlefield
math that has nothing to do with a creature's genes. Keeping them
separate means a future Unity client references only what it actually
needs, and neither package's tests depend on the other compiling.

## What this package is not

- Not a Unity project: no scenes, prefabs, materials, or the uber-shader
  from [docs/08](../../docs/08-creature-visualization.md) — those are
  GUI/binary-authored assets that belong in an actual `.unity` project,
  not here.
- Not the match sim: this defines the *space* combat runs in (docs/18
  realizes docs/02–04's abstract hex grid in continuous 3D); resolving a
  fight is still [docs/04](../../docs/04-combat-model.md)'s job, unchanged.
- Not city content: no procedural generator, no building catalog yet —
  see the "first slice" note above.
