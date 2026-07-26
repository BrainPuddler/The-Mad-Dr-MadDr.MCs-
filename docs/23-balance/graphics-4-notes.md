# docs/23 Phase 10.4 (Meshes) — shipped vs deferred

## Shipped

**`PropLibrary.cs`** — the code-side infrastructure the plan explicitly
asks for: "keep the deterministic dresser placement logic; swap
`CreatePrimitive` calls for a `PropLibrary` lookup (mesh assets by key,
with primitive fallback so the game never breaks without assets)."
`PropLibrary.Spawn(builder, key, fallbackPrimitiveType, position, scale,
material, parent)` mirrors `RuntimeCityBuilder.SpawnPrim`'s own calling
convention exactly (world-center position, local scale, one material) so
dresser call sites don't need to know or care whether a key resolves to
a real mesh or a primitive. Swapping a key's registered builder for a
real imported mesh later — once an Editor session exists for this
project — is a one-line change here, with no dresser code to touch.

**`ProceduralMeshKit.cs`** — two hand-authored placeholder meshes for
shapes `CreatePrimitive` doesn't offer, built the same manual
vertex/triangle way `LabMeshBuilder` already turns creature-mesh chunks
into live geometry:
- `Frustum(bottomRadius, topRadius, segments)` — a tapered cylinder.
- `Wedge()` — a lean-to awning/ramp shape (a right-triangular prism).

Both are centered at local origin and sized -0.5..0.5 like Unity's own
primitives, so they drop into the exact same position/scale convention.
Every face is emitted in both triangle windings (see the class doc) —
a deliberate, documented safety net against a winding-order mistake this
environment has no Editor to visually catch, at the cost of double the
triangle count on these small, few-per-scene props.

**Two new signature props (registered through `PropLibrary`, wired into
`RoadDresser`'s existing street-furniture switch as new cases 6-7,
extending its modulo range from `%6` to `%8` — the same incremental
pattern every earlier "furniture variety" pass already used):**
- **Ornate multi-globe lamppost** (docs/23 §10's daytime mood-board
  addition) — a `Frustum`-based tapered pole plus three warm globes
  clustered near the top, each an independent `Bulb()`-material sphere
  registered with `StreetLampRegistry` (so all three can independently
  earn a real point light from Phase 10.2's budget system, not just one).
- **Market/vendor stall** ("a market/vendor-stall prop for denser
  sidewalks") — a `Wedge`-based lean-to canopy over a plain counter box.

## Deliberate scope cut: the tram/streetcar is NOT attempted here

The daytime mood-board addition also names "a streetcar running on
visibly embedded rail lines... likely a New York/§8-scoped detail." This
is a materially bigger unit of work than a static prop: a moving vehicle
(wheels/pantograph, a drive loop analogous to `TrafficCar.cs`), a
distinct embedded-rail road-surface treatment (not just a prop spawn),
and region-gating logic that doesn't exist anywhere in `RoadDresser`
today (Phase 8 shipped `CityRegion` as a citygen-core-only field; no
Unity dresser branches on it yet — this phase's `LumenCycleController`
is the only current Unity consumer of `CityModel.Region`, for lighting
grade only). Shipping a shallow version of three different new systems
(vehicle AI, road geometry, region gating) to check a box would violate
this project's own "flag, don't fake" discipline more than leaving it
honestly deferred. A real, separate, larger follow-up.

## Deferred

- Tram/streetcar + embedded rail (see above).
- Region-gating for any of these new props (market stalls/ornate
  lampposts currently appear in every region's street furniture rotation
  identically — no "denser NY sidewalk" distinction exists yet).
- Corpse-part gore caps, vertex-blend seams at creature sockets (docs/23
  §10.5, Creatures) — untouched, a separate sub-phase.

## Verification

`flightcheck` compiles clean. **Not visually verified** — no Unity
Editor exists in this environment, so the frustum/wedge geometry's
actual on-screen silhouette (proportions, whether the winding-safety-net
approach reads cleanly) is unconfirmed.
