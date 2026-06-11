# 10 — Engine Evaluation & Recommendation

Status: Draft v0.1 · Requirements extracted from [08-creature-visualization.md](08-creature-visualization.md) and [09-multiplayer-architecture.md](09-multiplayer-architecture.md). The decision below is **provisional until the Phase-1 validation spike passes** ([11-roadmap.md](11-roadmap.md)).

## Requirements matrix (weights from our actual risks)

| # | Requirement | Source | Weight |
| --- | --- | --- | --- |
| R1 | Mobile 3D performance & mature iOS/Android build pipeline | [01](01-vision.md), [08](08-creature-visualization.md) | **High** |
| R2 | Runtime modular mesh assembly: skinned-mesh attachment, bone scaling, blend shapes, runtime mesh combining or per-part instancing | [08](08-creature-visualization.md) | **High** |
| R3 | Headless server build for the authoritative sim (or clean sim/engine separation) | [09](09-multiplayer-architecture.md) | **High** |
| R4 | Netcode ecosystem for server-authoritative state sync | [09](09-multiplayer-architecture.md) | Medium |
| R5 | Cross-platform parity mobile + PC (lab bench client) | [01](01-vision.md), [07](07-mutator-server-architecture.md) | Medium |
| R6 | Licensing/runtime fees compatible with f2p-scale mobile | business | Medium |
| R7 | Asset pipeline & DCC tooling for the part-authoring workflow | [08](08-creature-visualization.md) | Medium |
| R8 | Small-team productivity / hiring pool | team | Medium |

## Candidate profiles (honest versions)

### Unity

- **For**: strongest mobile 3D track record in this game's class; R2 is exactly Unity's sweet spot — `SkinnedMeshRenderer` re-binding, runtime bone scaling, blend-shape weights, and mesh combining are mature, documented, battle-tested by every character-customization game on the stores; headless Linux server builds are standard (R3); rich server-authoritative netcode ecosystem (Netcode for GameObjects/Entities, Fish-Net, Mirror) (R4); excellent mobile+PC parity (R5); biggest hiring pool (R8). The repo's existing `.gitignore` is already Unity's.
- **Against / watch items**: the 2023 runtime-fee announcement burned trust; it was walked back in 2024 (fee cancelled, per-seat pricing restored) but **R6 is a standing watch item** — re-evaluate at each Unity licensing announcement. C# GC pressure needs discipline at 60 fps mobile.

### Godot 4

- **For**: MIT-licensed, R6 risk is zero forever; pleasant small-team productivity; C# or GDScript; headless builds easy (R3).
- **Against**: thinnest at *our exact combination* — mobile 3D performance at 30 skinned, blend-shaped, runtime-assembled monsters is unproven at scale; runtime skinned-mesh assembly APIs exist but with far less production mileage (R2); mobile build/store pipeline and netcode ecosystem younger (R1, R4); smallest hiring pool for this profile (R8).
- **Position**: the designated **re-evaluation option if Unity's terms worsen** — record kept here so the pivot analysis is one doc away.

### Unreal

- **For**: best-in-class visuals and animation tooling; C++ server strength (R3); strong netcode heritage (R4).
- **Against**: heaviest runtime for a mid-range-Android RTS (R1 risk inverted — beauty we can't ship at 60 fps is no beauty); modular character workflows are geared to AAA pipelines and bigger teams (R7, R8); 5% royalty fine, but engine complexity is a small-team tax everywhere. Weakest overall fit for *mobile-first, small team, runtime-assembled creatures*.

## Scored comparison

3 = strong, 2 = adequate, 1 = weak; High weight ×3, Medium ×2.

| Req (weight) | Unity | Godot 4 | Unreal |
| --- | --- | --- | --- |
| R1 mobile 3D (×3) | 3 | 2 | 2 |
| R2 runtime assembly (×3) | 3 | 2 | 2 |
| R3 headless server (×3) | 3 | 3 | 3 |
| R4 netcode ecosystem (×2) | 3 | 2 | 3 |
| R5 cross-platform (×2) | 3 | 3 | 2 |
| R6 licensing (×2) | 2 | 3 | 2 |
| R7 asset pipeline (×2) | 3 | 2 | 3 |
| R8 team/hiring (×2) | 3 | 2 | 2 |
| **Weighted total (max 51)** | **49** | **39** | **40** |

## Recommendation

**Unity**, with two explicit caveats:

1. **Provisional until the spike passes.** Phase 1 includes a one-week validation spike: *assemble a 5-part monster from genome data at runtime — socket attachment, bone scaling, blend shapes, uber-shader tint — and render 30 of them at 60 fps on a mid-range Android reference device.* If the spike fails after honest optimization, this doc's Godot/Unreal profiles are the pre-baked pivot analysis. The spike doubles as the [08](08-creature-visualization.md) pipeline proof.
2. **The match server need not be Unity.** A headless Unity sim shares code with the client but drags engine weight into the server fleet; a custom sim (Go/Rust/C#) is leaner but duplicates logic. The combat sim is deliberately simple fixed-point math ([04-combat-model.md](04-combat-model.md)) — small enough to keep this genuinely open. Parked as **Q2 in [12-open-questions.md](12-open-questions.md)**, decide by Phase 3.

The Mutator service is engine-independent regardless ([07-mutator-server-architecture.md](07-mutator-server-architecture.md)).

## Exit criteria (what would change this decision)

- Spike failure on R1/R2 → re-run this matrix with the spike's data; Godot first re-candidate if the failure is licensing/cost-shaped, Unreal if it's rendering-shaped.
- Unity licensing change materially affecting f2p mobile economics → activate the Godot re-evaluation.
- Decision locks at the end of Phase 1; after Phase 2 a pivot costs a rewrite and requires sign-off against the roadmap ([11-roadmap.md](11-roadmap.md)).
