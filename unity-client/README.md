# unity-client

The Unity project for the City Battlefields track ([docs/18](../docs/18-city-battlefields.md)).

**Status: created** — Unity **6000.3.13f1** (Unity 6.3), **3D (URP)**
template, with Unity's default Mobile/PC render-pipeline asset split
(matches the mobile-first perf posture of
[docs/08](../docs/08-creature-visualization.md)/[09](../docs/09-multiplayer-architecture.md)).
Contents are still the stock template (SampleScene, TutorialInfo) plus:

- **`Assets/Scripts/HexGridGizmo.cs`** — a Scene-view smoke test for the
  `com.maddr.citygen-core` package reference: drop it on any GameObject
  and it draws the docs/18 hex grid (1 hex = 20 m) plus the 5-hex
  Collection Station radius as gizmos. If it compiles and draws, the
  package wiring works.

## The citygen-core package reference

[`packages/citygen-core`](../packages/citygen-core/) (engine-agnostic hex
grid + attack-arc math, docs/18 §1 / docs/04 posMod) is referenced in
`Packages/manifest.json` as a local package:

```
"com.maddr.citygen-core": "file:../../packages/citygen-core"
```

Notes for whoever opens the Editor:

- **First open after pulling this**: Unity resolves the package, compiles
  `MadDr.CityGen` from source (the `.asmdef` in its `src/`), and
  **generates `.meta` files inside `packages/citygen-core/`** — local
  `file:` packages are mutable, so Unity metadata lands in the package
  folder itself. **Commit those `.meta` files when they appear**; they're
  how asset identities stay stable.
- The package's xunit tests live in `Tests~/` and its dotnet build
  outputs go to `bin~`/`obj~` — tilde-suffixed folders are invisible to
  Unity's importer by convention, which is what keeps the dotnet and
  Unity toolchains from tripping over each other. Run the tests with
  `dotnet test Tests~/CityGenCore.Tests.csproj` from the package folder.

## Two Editor settings to verify before heavy work

`Edit → Project Settings → Editor`:

- **Version Control → Mode**: `Visible Meta Files`
- **Asset Serialization → Mode**: `Force Text`

Both are Unity 6 defaults, so a fresh 6000.x project should already have
them — verify rather than assume, since changing them later re-serializes
half the project.

## Git notes

- `.gitignore` here is Unity's standard template (Library/Temp/obj etc.),
  correct now that a real project sits at this folder's root.
- `.gitattributes` here is Unity's standard template too, but its
  `[attr]` macro definitions moved to the **repo-root** `.gitattributes`
  (git only allows macros in the top-level file — nested definitions
  produced warnings on every git operation). The `lfs` macro currently
  means *plain binary, not Git LFS* — deliberately, so clones don't
  require git-lfs; the upgrade path is documented in the root file.
