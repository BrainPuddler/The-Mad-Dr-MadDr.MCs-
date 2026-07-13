# unity-client

The Unity project for the City Battlefields track ([docs/18](../docs/18-city-battlefields.md)).

**Status: created** — Unity **6000.3.13f1** (Unity 6.3), **3D (URP)**
template, with Unity's default Mobile/PC render-pipeline asset split
(matches the mobile-first perf posture of
[docs/08](../docs/08-creature-visualization.md)/[09](../docs/09-multiplayer-architecture.md)).
Contents are still the stock template (SampleScene, TutorialInfo) plus:

- **`Assets/Scripts/HexGridGizmo.cs`** / **`CityGizmo.cs`** — Scene-view-only
  smoke tests (gizmos, not real GameObjects): the first proves the
  `citygen-core` package reference works at all, the second draws a full
  generated city (roads/buildings/water/ridges/bridges) in the Scene view
  while the Editor isn't playing.
- **`Assets/Scripts/RuntimeCityBuilder.cs`** — the actual payoff: drop it
  on an empty GameObject, hit **Play**, and watch your bred monsters wander
  a real city, not a gizmo preview. Builds the city as real primitive
  GameObjects, then fetches your Menagerie and spawns one wandering
  placeholder body per creature. See "Seeing your monsters" below.
- **`Assets/Scripts/RosterFetcher.cs`** — the roster fetch RuntimeCityBuilder
  drives: live HTTP fetch from `mutator-service`, local-disk cache
  fallback if the service is unreachable. Usable standalone too.
- **`Assets/Scripts/MonsterAvatar.cs`** — one spawned creature: a
  capsule sized by `body.bulk`, colored by body plan, wandering between
  passable hexes (crossing water if and only if it's amphibious).

## Seeing your monsters running around the city

`RuntimeCityBuilder`/`RosterFetcher` default to `https://maddr-mutator.onrender.com`
— the same deployed instance [The Lab](https://brainpuddler.github.io/The-Mad-Dr-MadDr.MCs-/)
(`site/main.js`'s hardcoded `MUTATOR_URL`) uses. **No local server needed**
for the common case:

1. Open [The Lab](https://brainpuddler.github.io/The-Mad-Dr-MadDr.MCs-/)
   and spawn a creature. **Then click "⭐ Save to stable" on it** — the
   Stable *is* your Menagerie for v0.1 (there's no separate "pick your
   active roster" screen yet); spawning alone only saves to your
   browser, never to the account-level roster `RosterFetcher` reads.
2. In the Lab's header, click **"🆔 Account ID"** — copies this browser's
   account ID (docs/07's dev-mode `x-account-id` stand-in for real auth;
   there's no cross-device login yet, so this is literally how a monster
   gets from the Lab to the battlefield today).
3. In Unity: empty GameObject → add `RuntimeCityBuilder` → paste that ID
   into its **Account Id** field → **Play**. First run has nothing to
   fall back to if the service isn't reachable; once one live fetch
   succeeds, a local cache exists for offline runs after that.

**Two gotchas found testing this against the real deployed service:**

- **Cold starts.** Free-tier hosting spins the service down after
  inactivity; the first request after a while can take 30-60 s to wake
  it, longer than `RosterFetcher`'s 8 s default `timeoutSeconds`. If the
  Console shows a fallback-to-cache warning on the first try, just hit
  Play again a minute later.
- **A failed live fetch falls back to whatever's cached, which can be
  stale or empty** (e.g. an earlier test against a different `baseUrl`
  that genuinely had zero creatures). If Console says "0 creatures, from
  local cache," that 0 is old news, not necessarily today's answer --
  delete `Application.persistentDataPath`'s `roster_cache_*.json` (or
  just retry until a `(..., live)` fetch succeeds) before concluding the
  Menagerie is actually empty.

**Running against a local Mutator service instead** (e.g. testing your
own unpushed service changes): `cd packages/genome-core && npm install
&& npm run build`, then `cd ../mutator-service && npm install && npm run
build && npm start` (defaults to `:8787`), and change both
`RuntimeCityBuilder`'s and `RosterFetcher`'s **Base Url** fields to
`http://localhost:8787`. Note the *Lab website itself* is hardcoded to
the deployed URL (`site/main.js`'s `MUTATOR_URL`), so a creature spawned
there won't exist on your local service unless you also edit that
constant and serve `site/` locally.

## The citygen-core and roster-client package references

[`packages/citygen-core`](../packages/citygen-core/) (engine-agnostic hex
grid + attack-arc math, docs/18 §1 / docs/04 posMod) and
[`packages/roster-client`](../packages/roster-client/) (dependency-free
JSON parsing for `mutator-service`'s response shapes, docs/07) are both
referenced in `Packages/manifest.json` as local packages:

```
"com.maddr.citygen-core": "file:../../packages/citygen-core",
"com.maddr.roster-client": "file:../../packages/roster-client"
```

Notes for whoever opens the Editor:

- **First open after pulling this**: Unity resolves both packages,
  compiles `MadDr.CityGen`/`MadDr.RosterClient` from source (each has its
  own `.asmdef`), and **generates `.meta` files inside both package
  folders** — local `file:` packages are mutable, so Unity metadata lands
  in the package folder itself. **Commit those `.meta` files when they
  appear**; they're how asset identities stay stable.
- Both packages' xunit tests live in their own `Tests~/` and their dotnet
  build outputs go to `bin~`/`obj~` — tilde-suffixed folders are invisible
  to Unity's importer by convention, which is what keeps the dotnet and
  Unity toolchains from tripping over each other. Run citygen-core's with
  `dotnet test Tests~/CityGenCore.Tests.csproj` and roster-client's with
  `dotnet test Tests~/RosterClient.Tests.csproj`, each from its own
  package folder.

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
