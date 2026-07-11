# unity-client

The Unity project for the City Battlefields track ([docs/18](../docs/18-city-battlefields.md)).

**Status: not created yet.** This folder exists ahead of time to reserve
the location and carry the Unity-specific `.gitignore` fix (see below) —
there's no Unity project here. It's deliberately *not* pre-populated with
`ProjectSettings/`, `Assets/`, or a `Packages/manifest.json`: those are
version-locked to whatever Editor actually creates them, and Unity Hub's
"New Project" flow works best pointed at an empty (or near-empty) folder.
Hand-authoring them here, blind, risks conflicting with that flow rather
than helping it.

## Setup (once you have Unity Hub + an Editor installed)

1. In Unity Hub, **New Project**, with **Location** set to this repo's
   root and **Project Name** set to `unity-client` — so it lands exactly
   here, not a new sibling folder.
2. Template: **3D (URP)** — Universal Render Pipeline. The design docs
   are mobile-first ([docs/09](../docs/09-multiplayer-architecture.md))
   and assume a shared perf-budgeted uber-shader
   ([docs/08](../docs/08-creature-visualization.md)); URP is the right
   fit, HDRP is desktop/console-only.
3. Before your first commit, set two Editor settings (`Edit → Project
   Settings → Editor`):
   - **Version Control → Mode**: `Visible Meta Files` (not Hidden) — git
     needs to see `.meta` files, or asset references break for anyone
     else who clones the repo.
   - **Asset Serialization → Mode**: `Force Text` — keeps scenes/prefabs
     as readable YAML so diffs and merges are actually possible.

## Referencing `packages/citygen-core`

[`packages/citygen-core`](../packages/citygen-core/) is the engine-agnostic
hex-grid/attack-arc logic (docs/18 §1, docs/04 posMod) — plain C#, zero
`UnityEngine` reference, built and tested independently of this project
(`dotnet test`, see its own README). It isn't laid out as a Unity package
yet (no `package.json`/`.asmdef`) — that's a fast follow once this project
exists and a Unity version is locked in, so the reference can be a single
line in `Packages/manifest.json` rather than a guess made before either
side existed.

## What's actually built so far

Nothing Unity-side. `packages/citygen-core`'s hex grid and arc math are
the only tested code against this track. See
[docs/18](../docs/18-city-battlefields.md)'s implementation note and
Open Questions (Q14 — this track doesn't block Phases 1–3, and Phase 1's
own hex combat sandbox doesn't exist in this repo yet either) for
sequencing context before diving in further.
