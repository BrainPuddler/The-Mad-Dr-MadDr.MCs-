# 28. City lighting system

**Status: Phases 1-3 implemented, then debugged against a real Editor
through several rounds (2026-07).** This doc covers the architecture for
every light in the city — streetlamps, house/apartment windows, neon
signs, marquee chasers — after the first real Editor look at docs/23
Phase 10's street lamps showed them as "big opaque balls of light...
turning the playfield white." That symptom, and the ask for "a bunch of
different lights... keeping them performant," are what this plan
answers.

**If you're picking this up cold, read §0.5 first, not the rest of the
doc in order.** It's the single table of every bug this system has hit
and its current status — everything else here is architecture that
mostly hasn't changed since Phase 1.

## 0.5. Bug history + current status (read this first)

The creator debugged this live against a real Editor across many
rounds — this repo has no Editor, so every fix here was reasoned from
symptoms/screenshots/console output the creator supplied, not directly
observed. That's a real risk of a fix being wrong in a way only the
Editor can catch (see rows 6→7 below, where fixing one bug's root cause
directly caused the next one). Full blow-by-blow reasoning for each row
lives in the docs/12 decision log (search "docs/28"); this table is the
condensed, current-state version.

| # | Symptom reported | Root cause | Fix | Status |
| --- | --- | --- | --- | --- |
| 1 | "some effect but even set to 0 way too large" (bloom) | `nightBloom` was blended TOWARD (weighted by `nightAmount`, which decays through the back half of the night phase) instead of being a real multiplier | Renamed to `bloomScale`, true always-on multiplier on the whole curve, same model as `emissiveScale` | **Confirmed fixed** — creator verified bloom size responds correctly |
| 2 | "it's the real lights under market-stall-canopy, ornate-lamppost-pole" too large | Checked geometry first (hex spacing 20m, furniture offset ±3m — ruled out cross-prop bleed); real cause: `DynamicLightBudget.range` (7m) is a Point light illuminating any nearby geometry, not just the registered prop | Tightened range 7f→3f | **Was WRONG** — see #3 |
| 3 | "light is not pooling on the ground" (regression from #2) | The ornate lamppost's globes mount 5.9m up; 3m of range can't physically reach the ground from there. `range` is a straight-line radius, not a ground-projected size | Reverted range to 8f (clears the mount height with margin) | **Confirmed fixed** |
| 4 | "still not working" / crash on toggling `enableRealLights` off | Two bugs: (a) `Refresh()` threw `ArgumentOutOfRangeException` when `activeBudget == 0` (empty-list fallback path); (b) fresh pooled `Light`s defaulted to Mixed bake mode, causing a GI console warning | (a) Guarded the fallback branch; (b) explicit `lightmapBakeType = Realtime` | **Confirmed fixed** — no more crash/warning |
| 5 | "objects still black, no light I can see" | Both URP Pipeline Assets had Additional Lights set to **Per Vertex**; this city is built entirely from low-poly primitives (a ground quad can have 4-8 vertices), so a point light's contribution can vanish between vertices even when perfectly configured | Set both `PC_RPAsset`/`Mobile_RPAsset` to Per Pixel (flagged mobile perf trade-off, not silently taken) | **Confirmed fixed** (ground pools now visible) |
| 5b | (same report, second cause) | `RenderSettings.ambientMode` was never set anywhere — Unity only reads `ambientLight` in `Flat` mode; scenes default to `Skybox`, so every ambient value this controller ever computed was silently discarded | `LumenCycleController.Start()` now sets `ambientMode = Flat` once | **Confirmed fixed** |
| 6 | ornate-lamppost-pole / market-stall-canopy rendered **pure black** even sitting inside a correctly-working light pool | `ProceduralMeshKit` emitted every face in both windings (deliberate anti-mistake guard) — `RecalculateNormals` averages face normals per vertex, so +N and -N cancelled to exactly zero, and a zero normal makes `dot(N,L)` zero for *any* light | Single winding + `FaceOutward()` (re-winds any triangle facing the mesh centroid). Verified numerically in the flightcheck harness (22/22 zero normals → 0/0) since there's no Editor to look at | **Was INCOMPLETE** — see #7 |
| 7 | Same two props **vanished entirely** (regression from #6) | Fixing #6 reintroduced exactly the risk double-winding existed to prevent: whether Unity's front-face culling agrees with `FaceOutward()`'s notion of "outward" can't be verified without an Editor — it disagreed, so the correctly-wound faces got back-face culled | `PropLibrary.Spawn` clones the material and sets `_Cull = Off`, but ONLY for registered-builder (ProceduralMeshKit) meshes, never the primitive fallback | **Confirmed fixed** — creator reports the props are visible again |
| — | (side finding, not a symptom report) | Ornate lamppost registered a real light per globe (3, half a metre apart) — ~3x stacked intensity on one pavement patch, 3 of 24 budget slots on one fixture | Register one real light per fixture; all 3 globes still glow (emissive, unaffected) | Bundled into the #6/#7 commits, not separately re-verified |
| 8 | "I believe you put the lights in the wall" | `RoadDresser` had NO wall-clearance check at all — `curbLineOffset` assumed a fixed margin to any neighboring building that two independent effects could break: `CardinalAnchor`'s straightening nudge stacking with the curb offset on the same axis (north/south streets), and an arterial street's own curb offset exceeding the raw row gap to a north/south-adjacent building (any street). Full derivation in the docs/12 decision log (search "no wall-clearance check") | `RoadDresser.ClearLateralOffset` now checks the actual building position (via `city.Buildings`) and clamps the sideways offset to stay `BuildingDresser.Half + 1.5m` clear of it, instead of trusting the arithmetic | **Reasoned fix, awaiting creator re-verification** — row 7's confirmation was about the props being visible again, not about their position relative to walls; still separately unverified |
| 9 | After #7/#8, real lights STILL invisible | Not a bug — `DynamicLightBudget.peakIntensity`/`CityLightingProfile.RealLightPeakIntensity` were `[Range(0f, 5f)]`, capping the Inspector slider itself at 5 regardless of what was typed in, and the 0.7 default was apparently just too dim to read on the creator's setup (no config bug found to explain it — checked for a Physical Light Units mismatch specifically, not present) | Widened both to `[Range(0f, 150f)]`; creator confirmed lights visible at a deliberately blown-out diagnostic default of 80. Backed off to 12 as an untuned starting point — **not** a confirmed-good value, the real threshold between "too dim" and "too bright" hasn't been narrowed down yet | **Confirmed lights render at all; final brightness still needs live tuning** |
| 10 | Direction, not a bug report: "make the lights fade a lot faster and hold for duration of the night, then fade off during the daytime" | `nightAmount` (drives real-light intensity + ambient darkness) came from the same continuous per-phase cross-fade as sun/fog/color-grading — it never held steady, drifting toward the next phase's value across the ENTIRE current phase (row 1's `bloomScale` bug was literally invented to route around this same mechanism) | Added `ComputeNightIntensity`, a dedicated trapezoid. **Superseded by row 12** — this round's shape (fade-out over the first half of Day) turned out not to match "turn shortly after dawn" once the creator clarified it; see row 12 for the actual current shape | **Superseded** — see row 12 |
| 11 | Direction: "the down facing spotlight needs to be a lot brighter to read properly" | Not investigated as a bug — a Spot light's cone concentrates the same `intensity` into a narrower solid angle than a Point light, and Unity's spot attenuation reads noticeably dimmer per-pixel at a wide-ish 48° cone than an omnidirectional Point at the same value | New `DynamicLightBudget.spotIntensityMultiplier` (default 5), applied ONLY to Spot-type promoted lights on top of `peakIntensity` — Point lights (windows, ornate lamppost, etc.) are unaffected | **Untuned guess, same status as `peakIntensity` itself** — needs live nudging once visible, not yet seen in a render |
| 12 | Direction, correcting row 10: "ALL the lights should turn off during the day" + "ramp on quickly, hold for the duration of the night, and turn shortly after dawn" | Row 10's shape held through all of Dawn and faded out gradually across the first half of Day (45s) — not what "shortly after dawn" meant. Also: `intensity`'s floor was 0.02 (near-zero, not off) and `neonBoost`'s floor was Day's own authored 0.35 ("barely visible," the ORIGINAL pre-this-whole-effort design intent) — neither was a true 0 | `ComputeNightIntensity` reshaped: fast ease-in over the first 25% of Dusk (unchanged), flat hold through the rest of Dusk + all of Night (unchanged), an eased fade-out over the first 35% of **Dawn** (~10.5s, moved from Day), flat hard 0 for the rest of Dawn + all of Day. Both `DynamicLightBudget`'s `intensity` and `LumenCycleController`'s `neonBoost` now `Lerp` from a hard `0f` floor instead of 0.02/0.35 | **Verified numerically against the compiled method** (reflection, every hold/fade segment boundary+midpoint re-sampled against the new shape) — not yet seen in a real render |
| 13 | Direction: "the building can turn on randomly approaching night time, as if real humans were in the room and realize it's getting too dark... the same goes for late at night, imagine people going to bed... this can vary greatly, but not all lights go off" | Not a bug — `nightAmount` now holds perfectly flat through the whole night (row 12), so it structurally CAN'T tell "just got dark" apart from "3am" the way a per-window bedtime needs to; a new, independent clock was needed | New `DayNightState.CycleProgress` (raw 0..1 cycle position, doesn't hold flat) + new `LightBehaviorKind.Window` in `EmissiveAnimator`: each registration gets its own randomized (deterministic from the existing per-window `seed`, same "same seed always furnishes the same city" approach as everywhere else in this codebase) arrival time in [37.5%, 75%) of the cycle and bedtime in [75%, 98%) of the cycle, dark outside that span, lit (with the existing Flicker-style wobble) inside it, ~2.4s smoothstep transitions at each edge instead of a hard pop. 15% of windows are `AlwaysOn` and skip the bedtime check entirely ("not all lights go off"). `BuildingDresser`'s window registration switched from `Flicker` to `Window`. Purely an emissive/`MaterialPropertyBlock` effect (creator's own direction) — never touches `GlowPointRegistry`/`DynamicLightBudget`, no second real-light system | **Verified numerically** (reflection into `EmissiveAnimator`'s private state): 400 sampled registrations all landed in-range with correct ordering, the AlwaysOn rate came out to 15.25% against a 15% target, and the on/off gate function was confirmed 0 outside a window's span, 1 inside it, and permanently 1 for an AlwaysOn entry — not yet seen in a real render |
| 14 | Confirmed row 12's timing is working, then: "make the roads more reflective, I can barely see the lights on them" | Nothing in `RoadDresser.cs`'s `M()`/`MTextured()` material helpers has EVER set `_Smoothness`/`_Metallic`, on ANY material, since the file's first version — every prop including the road has always rendered at the URP/Lit shader's own default smoothness (~0.5, a diffuse-leaning middling response). A mid-smoothness surface spreads light into a broad, dim specular response instead of a tight bright glint, so even a correctly-positioned, correctly-bright real light (rows 11/12) was never going to visibly reflect off the pavement itself | Added an optional `smoothness` param to `MTextured()`; `Asphalt()` passed 0.92 | **Was TOO shiny** — see row 15 |
| 15 | "the road is too shiny put it back to the original setting. Change the road from black to a textured mid dark gray, that should help us see the light better" | Row 14's 0.92 smoothness overshot | Reverted `Asphalt()`'s smoothness override entirely (back to shader default, matching every other material). Base color changed from near-black (0.17/0.17/0.18) to a mid dark gray (0.35/0.34/0.36) instead — contrast against the road's own color, not surface glossiness, is now the mechanism relied on | **Creator direction, applied as stated** — not yet re-confirmed against a render |
| 16 | "I think the fog isn't transmitting or limiting the transmission of the lights" | Checked the actual scene file first (`SampleScene.unity`) — `m_FogMode: 3` (ExponentialSquared) and `m_AmbientMode: 0` (Skybox, confirming row 5b's earlier fix was right) are both already correctly serialized, ruling out an ambientMode-style silent-default bug. The REAL gap: basic `RenderSettings` fog only fades a rendered SURFACE's color toward the fog color by camera distance — it has no concept of a light source's own reach, so it structurally cannot make a lamp look "swallowed" by fog the way real light-scattering does, no matter how it's tuned | Follow-up direction (asked via a clarifying question rather than guessed): "both, plus a diffusing glow like real lights in fog, and give me max/min settings for the overhang streetlights vs all others." `DynamicLightBudget`'s old single `peakIntensity` + `spotIntensityMultiplier` replaced with `pointIntensityMax`/`pointIntensityMin`/`spotIntensityMax`/`spotIntensityMin` (the Point/Spot split IS the "overhang vs all others streetlights" split — no new categorization needed) blended by a new `fogDimReferenceDensity` (current `RenderSettings.fogDensity` normalized 0..1 against it). `LumenCycleController.fogGlowBoost` adds an extra fog-driven multiplier on top of `bloomScale` for the "diffusing glow" half. `FogDensity` bumped ~1.5-1.7x across all four phase grades for "thicker overall." All new fields mirrored onto `CityLightingProfile` | **Reasoned + hand-verified algebra** (fogT=0 -> ceiling=Max; fogT=1 -> ceiling=Min; composes from already-stub-verified Lerp/Clamp01) — not yet seen in a real render, and the Max/Min/reference-density defaults are as untuned as `peakIntensity` originally was |
| 17 | Confirmed row 16 "it's better", then: "make all the street lights 90% brighter" | Not a bug — a plain scale-up request | Flat x1.9 on all four of row 16's Max/Min fields (both halves of the Point/Spot split = "all the street lights"): pointIntensityMax 12->22.8, pointIntensityMin 4->7.6, spotIntensityMax 60->114, spotIntensityMin 18->34.2. Mirrored onto `CityLightingProfile` | **Applied as stated, simple arithmetic** — not yet re-confirmed against a render |
| 18 | "I want the lights to truly pop, bright and diffuse through the fog. Use something like [an HDRP Local Volumetric Fog / light-source-fog article]" — corrected mid-turn to "stay in URP tho" | The referenced technique (`HDAdditionalLightData` volumetric lights + HDRP's `LocalVolumetricFog`) is HDRP-only and doesn't exist in this project's pipeline (confirmed URP, CLAUDE.md). Separately: `Bloom.scatter` (the actual "diffuse/spread" parameter, distinct from `intensity`) and `Bloom.threshold`'s VALUE (its `overrideState` was `true` since Phase 1, but the value itself was never assigned — same "flag set, value never driven" shape as ambientMode/row 5b) had both been silently riding URP's own Bloom defaults this whole time | New `bloomScatter`/`fogDiffusionBoost` (fog-density-driven scatter boost, clamped to 1) for "diffuse," new `bloomThreshold` (lower than URP's ~0.9 default) for "pop." All three wired into `ApplyBlend()` and mirrored onto `CityLightingProfile`. Created `.claude/skills/maddr-lighting-system` per the creator's "add it to your lighting skill" — captures the URP-vs-HDRP boundary, this fog-approximation mechanism, and the recurring "property never explicitly set" bug pattern that rows 5b/5/6/18 all share, so future sessions don't re-derive or re-hit any of this | **Reasoned, not numerically checkable** (pure Bloom/shader parameters) — not yet seen in a real render |
| 19 | "study this too: github.com/mseonKim/URP-VolumetricFog-ForwardPlus, see if it can be used" | N/A — feasibility research, not a bug | Confirmed genuinely usable: `PC_Renderer.asset` is already `m_RenderingMode: 2` (ForwardPlus, the package's hard requirement); URP 17.3.0 exceeds its 14.0.8 minimum; Unity Companion License is compatible. `Mobile_Renderer.asset` is plain Forward (`0`) so it'd be PC-only. Asked whether to integrate (a new external git dependency + a one-time Editor-only Renderer Feature registration step this environment can't safely do blind); creator asked for an ease/quality/performance comparison against row 18's Bloom approach instead, then chose to stay with row 18 for performance, with the volumetric package kept as a documented future option (`.claude/skills/maddr-lighting-system` §3) | **Evaluated and documented, deliberately not integrated** — this is the final state for this thread, not a pending task |
| 20 | "I need the sun to move across the sky in a realistic manner, so shadows move and shift realistically. I want those long shadows at sunrise and sunsets" | `SunYawDeg` was a single fixed constant ("only elevation/color animate," its own comment said so) — elevation already animated per-phase, yaw never did, so the sun bobbed up/down in place without sweeping the compass: shadow LENGTH changed over the cycle, shadow DIRECTION never did | `SunYawDeg` moved into `PhaseGrade` (per-phase, blended like elevation already was), tracing one continuous 360° sweep proportioned to each phase's own share of the 2400-tick cycle (constant angular speed, not visibly faster during the shorter Dawn/Dusk). Night->Dawn needs a `+360` on the Lerp TARGET specifically (Night's raw yaw is larger than the next Dawn's), or the sweep would reverse across the daytime side instead of continuing through the below-horizon side — `ApplyBlend` now branches on `phase == Night` for this one case. Also: Dawn's `SunElevationDeg` 8->3, Dusk's 4->3 (now symmetric) for "long shadows at sunrise and sunset" specifically, so both transitions sit at the dramatic near-horizon angle right at their own phase boundary | **Verified against the actual compiled `Start()`/`ApplyBlend()` via reflection** (240 samples across a full cycle, sign-convention-agnostic — checked total sweep is 360°, monotonic including the wrap, no anomalous jump; the observed max per-sample step independently matched the predicted SmoothStep peak-derivative value) — not yet seen in a real render |
| 21 | Same message: "lighten up the roads even more they are still too dark" | Row 15's mid dark gray (0.35/0.34/0.36) still read too dark | `Asphalt()`'s base color raised again to a genuinely light gray (0.52/0.51/0.53) | **Applied as stated** — not a numerically checkable claim (pure color choice), not yet re-confirmed against a render |
| 22 | "I see the light shifting but not the shadows" | Not row 20's math (already verified independently) — `RuntimeCityBuilder` frames the camera at `SnapTo(cityCenter, 70f)`, which `SimpleCameraRig` turns into a camera position `~89.6` units from the city center (`sqrt(70^2+56^2)`). Both `PC_RPAsset.asset` and `Mobile_RPAsset.asset` had `m_ShadowDistance: 50` — shorter than the DEFAULT starting camera distance, before any zooming out. Ambient/color changes aren't distance-limited (whole-scene `RenderSettings`), so they stayed visible everywhere; actual shadow casting needs geometry within `m_ShadowDistance` of the camera, which wasn't true for most of the visible city even at default zoom — the shadow system was never getting a chance to render, independent of how correct the sun's rotation was. Also checked and ruled out: `_sun.shadows` IS set (`Soft`); nothing marks generated geometry static or touches `shadowCastingMode`/`receiveShadows` away from Unity's own defaults | `m_ShadowDistance` raised: `PC_RPAsset.asset` 50->150 (several times the default view distance), `Mobile_RPAsset.asset` 50->100 (more conservative for mobile GPU budget, still clears the ~90-unit default view) | **Confirmed by direct distance arithmetic** (not a shader/numeric-math claim — no flightcheck build applies to a pure asset-value edit) — not yet seen in a real render |
| 23 | "Limit the shadows then to objects in the camera view and close to the visible area" | Row 22's fix was a static worst-case value (150/100), sized for the DEFAULT camera distance only — `SimpleCameraRig` lets the player zoom from height 8 to 400 (`MinHeight`/`MaxHeight`), so a fixed distance is either wasted shadow-map resolution when zoomed in close, or (at the top of the zoom range, `400*1.28≈512` units away) too short again, same failure mode as row 22 itself | `SimpleCameraRig.UpdateShadowDistance()` (called from both `SnapTo` and every `Update`) sets `GraphicsSettings.currentRenderPipeline`'s (cast to `UniversalRenderPipelineAsset`) `shadowDistance` every frame from camera height: `min(height * shadowDistancePerHeight(1.9) + shadowDistanceFloor(15), shadowDistanceCap(250))`. 1.9 is the `SnapTo` camera-to-ground ratio (`sqrt(1+0.8^2)≈1.28`) plus margin so the covered radius reaches the actual visible frustum, not just the exact focus point; the 15-unit floor keeps extreme close-in zoom from shrinking distance so tight shadows flicker/pop; the 250-unit cap stops it degrading cascade quality at extreme zoom-out where individual shadows are visually tiny anyway. `PC_RPAsset`/`Mobile_RPAsset`'s static 150/100 values remain as the pre-first-`Update()` fallback only | **Verified against the actual compiled `UpdateShadowDistance()` via reflection** (height=70 default -> 148, comfortably above the ~89.6-unit default camera distance; height=8 min zoom -> 30.2; height>=200 -> capped exactly at 250; monotonic non-decreasing across the full [8,400] range) — not yet seen in a real render |
| 24 | "so are the shadows baked? is that why they don't animate when the sun rises and sets?" then, redirecting once baking was ruled out: "The sun should be moving throughout the day not just sunrise and sunset!" | Not baking — confirmed no GameObject anywhere sets `isStatic`/`staticEditorFlags`, and `SampleScene.unity`'s `m_LightingSettings: {fileID: 0}` means no Lighting Settings asset is assigned; nothing here can produce baked lightmap data. The real cause: `SunYawDeg` got a "proportion the sweep to phase duration" fix in row 20, but `SunElevationDeg` never did — `ApplyBlend` still Lerped it directly between adjacent phase keyframes, so its sweep MAGNITUDE was locked to whatever those two authored values happened to differ by, ignoring phase length. Dawn (30s) and Day (90s) both authored a 27-degree elevation swing, so elevation moved 3x faster during Dawn than during Day (same 3x gap between Dusk and Night) — a visible bob at the short Dawn/Dusk transitions, a near-crawl through the long Day/Night phases | New `ComputeSunElevationDeg(int cycleT)` (same "dedicated function" pattern as `ComputeNightIntensity`): elevation as one continuous arc across the whole 2400-tick cycle, with the peak/trough placed at each long phase's MIDPOINT (solar noon, solar midnight) instead of only at phase boundaries — so the climb from Dawn's low anchor to Day's peak spans Dawn + the first half of Day, and the descent spans the second half of Day + Dusk, keeping every tick of Day inside an actively-moving segment. Reuses the same four authored elevation values already in `BuildGrades` (Dawn/Dusk 3°, Day 30°, Night -8°) — no new tuning numbers, purely a different interpolation between them, so "long shadows at sunrise/sunset" and "never a high overhead noon angle" both still hold | **Verified against the actual compiled `ComputeSunElevationDeg()` via reflection**: all four keyframe values exact, continuous across the full cycle (max single-tick step 0.09°), every 90-tick window in Day/Night moves ≥0.5° except the two windows straddling the genuine peak/trough themselves (expected, confirmed windows just clear of those extrema still move several degrees), and the Dawn-vs-Day rate ratio dropped from the old code's exact 3.0x mismatch to 0.81x — not yet seen in a real render |
| 25 | Row 24's fix still didn't animate shadows visually — creator experimented: "Works now if I change the Directional to a point light or area light, but now the night is too dark." Asked directly what that meant; answer: "The scene had a directional built into it. So there are 2, thus the shadows appear fixed, never animating." | `SampleScene.unity` ships Unity's own stock default "Directional Light" GameObject (intensity 2, soft shadows enabled, fixed rotation `(50, -30, 0)` — the standard new-scene default, never touched by any script here) sitting ALONGSIDE `LumenCycleController`'s own runtime-created "(auto) Sun". Two enabled Directional lights in one scene means URP has to pick ONE as the shadow-casting Main Light — it was picking the scene's static stock one, not the procedurally animated one, so every rotation fix in rows 20/24 (both independently verified correct via reflection) was animating a light that was never the one actually casting shadows. The creator's workaround (retyping the STRAY light to Point/Area in the Inspector) incidentally fixed it by removing it as a competing Directional light, but also removed its always-on intensity-2 contribution, which is what made night read as darker than before | Disabled the stray GameObject in `SampleScene.unity` directly (`m_IsActive: 0`, renamed to flag why) rather than relying on the creator's manual per-session Inspector workaround — confirmed via grep it's the only OTHER `Light:` component in the whole scene file, so this leaves exactly one Directional light: `LumenCycleController`'s own animated sun. Paired with a genuine new feature for the resulting darkness, since removing the stray light's constant intensity-2 contribution has the same net effect as the creator's workaround did: `LumenCycleController.nightFillLift` (new `[Range(0,1)]` field, default 0.35) drives a new `ShadowsMidtonesHighlights` Volume override, an HDR-style tonal-range grader (same family as Lift/Gamma/Gain) that lifts ONLY the shadows band's luminance (`shadows.value.w`), leaving color and mid/highlight tones untouched — raises the floor on crushed-black areas without flattening the lamp-vs-darkness contrast a flat `nightAmbient` bump would (that knob already existed and was deliberately kept near-zero in an earlier round specifically to protect that contrast). Blended by `nightAmount` the same as every other night-only effect: 0 all through Day, ramping to `nightFillLift`'s full value for the Dusk/Night hold | **Scene edit confirmed by direct inspection** (grep found exactly one `Light:` block in `SampleScene.unity` before the edit, now inactive) — this is the most likely real fix for "shadows never animate" but, like everything else in this table, not yet confirmed in a real render. `ShadowsMidtonesHighlights`'s exact field shape could NOT be checked against docs.unity3d.com this session (every fetch attempt got a destination-side 403, confirmed not an org egress block via the agent proxy's own status endpoint) — corroborated instead by matching manual page titles across URP 7.1 through 6000.0 and a real Discussions code sample; **verified against the actual compiled `ApplyBlend()` via reflection** (shadows.w is exactly 0 through Day, exactly `nightFillLift` at full Night, color channels stay untouched at 1/1/1) but the field-shape assumption itself is provisional until a real Editor session confirms it compiles |
| 26 | Immediately after row 25 shipped: "still need an ambient light so we can see in the darkest part of the night" | `nightFillLift` (row 25) only grades the FINAL rendered pixel color in post-processing -- it never changes how anything is actually lit, so it can't help depth/silhouette/shape read in genuinely unlit areas the way real scene ambient does. `nightAmbient` (the actual `RenderSettings.ambientLight` driver) was still sitting at 0.02, the near-black value deliberately chosen several rounds back specifically so lamps would pool against real darkness -- too dark on its own to see anything in the unlit gaps between lamps, row 25's post-process lift notwithstanding | Raised `nightAmbient`'s default 0.02 -> 0.08 (both on `LumenCycleController` and mirrored `CityLightingProfile.NightAmbientBrightness`) -- a real floor of scene lighting, still noticeably darker than Dawn/Dusk's own ambient so the lamp-pool contrast isn't gone, just no longer pitch black in between them. Complements (doesn't replace) row 25's shadow lift: this fixes actual scene lighting, that fixes the final image's tonal floor | **Applied as stated, simple default-value change** — flightcheck compiles clean, not yet re-confirmed against a render |
| 27 | Immediately after row 26: "still way too dark. triple it." | 0.08 (row 26) still not enough | `nightAmbient` 0.08 -> 0.24 (both `LumenCycleController` and `CityLightingProfile.NightAmbientBrightness`), exactly tripled per the creator's own instruction. Also widened `nightAmbient`'s `[Range]` ceiling 0.3 -> 1.0 (matching the profile asset's own range) since 0.24 was already close to the old 0.3 cap and this is the second consecutive "still too dark" round — no reason to assume it's the last | **Applied as stated, exact arithmetic (0.08 x 3 = 0.24)** — flightcheck compiles clean, not yet re-confirmed against a render |
| 28 | "the minimum nightAmbient should be at least 0.12" | A `[Range]` minimum only guards direct Inspector drags — it doesn't stop `ApplyProfile()` copying in a lower value from a `CityLightingProfile` asset, or a future default silently regressing back toward the near-black 0.02 rows 26/27 were about escaping | New `LumenCycleController.MinNightAmbient` const (0.12f), enforced via `Mathf.Max(nightAmbient, MinNightAmbient)` at the actual point of use in `ApplyBlend()` — a genuine runtime floor, not just the `[Range]` slider's lower bound (also raised to 0.12 for consistency, but now redundant with the code-level floor). Mirrored onto `CityLightingProfile.NightAmbientBrightness`'s own `[Range]` | **Verified against the actual compiled `ApplyBlend()` via reflection**: forcing `nightAmbient` to 0.05 (a below-floor value) still reads back exactly 0.12 in `RenderSettings.ambientLight` at full Night; 0.24 (current default) passes through unmodified. Writing this check also surfaced and fixed a real gap in the flightcheck stub itself — `Color.Lerp`/`white`/`black`/`gray`/the `*` operator/the `Color`->`Color32` conversion had all been `default(Color)` no-ops the whole session, which would have silently passed any check reading a `Color.Lerp`-derived value; nothing verified earlier this session happened to rely on it, but it's fixed now regardless — not yet seen in a real render |
| 29 | "The lights should start turning on by 5:00 pm on the clock." | `ComputeNightIntensity`'s fast ease-in started 25% into Dusk — the creator instead wants it tied to a specific position on the new `AnalogClockHud` dial, which (one Lumen cycle = one 12-hour dial revolution) puts "5:00" at cycle tick 1000, solidly inside Day and well before Dusk (1200, which happens to land exactly on the dial's 6:00) | Moved the ramp's start trigger from "25% into Dusk" to the fixed tick 1000 (`LightsOnStartTick`), keeping the same ~75-tick (7.5s) fast-ramp duration as before, now expressed as an absolute duration (`LightsOnRampTicks`) instead of a fraction of Dusk since it no longer starts inside Dusk. Lights fully on by dial ~5:22, comfortably ahead of Dusk. The Dawn fade-out and the flat hold through the rest of the cycle are unchanged | **Verified against the actual compiled `ComputeNightIntensity` via reflection**, cross-checked against the actual compiled `AnalogClockHud.HourHandDeg` to confirm tick 1000 really does read as the dial's 5:00 (150°) rather than just hand-derived: off through tick 999, ramp starts exactly at 1000, strictly between 0 and 1 mid-ramp, exactly 1 by 1075, stays 1 through the rest of Day/Dusk/Night — not yet seen in a real render |

**Row 1's own description of `nightAmount` "decaying through the back
half of the night phase" is now historical** — that was true of the OLD
mechanism at the time it caused the `bloomScale` bug; row 10 (itself now
superseded by row 12) replaced that mechanism, and `nightAmount` now
holds flat through the whole night instead.

**As of the last commit: rows 1, 3, 4, 5, 5b, 6, 7, and 12 (the fade
timing, explicitly) are creator-confirmed working. Row 8
(wall-clearance) is still unverified — visibility being fixed doesn't
confirm position is correct, those are independent questions. Row 9
confirms the lighting pipeline works end-to-end. `peakIntensity` no
longer exists (row 16 replaced it with per-type Max/Min pairs); those
new defaults, row 11's now-folded-in Spot ceiling, and row 15/21's road
color are all still open, unconfirmed tuning tasks. Row 13 (window
occupancy), row 16 (fog dimming + diffuse glow), and row 20 (sun
compass sweep) are reasoned + checked as far as this environment
allows, but NONE of rows 13-18 and 20-29 have been seen in a real
render yet -- that's the whole active open thread. Row 19 is the one
exception: research only, deliberately not integrated, not a pending
render check. Row 22's static `m_ShadowDistance` asset values are now
superseded during actual play by row 23's dynamic, camera-height-driven
distance -- they only matter before the rig's first `Update()` runs.
Row 24 confirms (and documents, since it hadn't been asked before) that
nothing in this project's lighting is baked -- no static geometry, no
Lighting Settings asset -- so "is it baked" can be ruled out by
inspection alone for any future symptom that looks frozen/static.
Row 25 is the likely REAL explanation for why rows 20/24's independently
-verified sun-rotation math never visibly showed up: `SampleScene.unity`
had a second, stray, un-animated Directional Light competing for URP's
Main Light slot the whole time -- now disabled, leaving exactly one
Directional light in the scene, `LumenCycleController`'s own animated
sun.
See `.claude/skills/maddr-lighting-system` for the condensed,
pattern-level version of this table if you're picking up this system
cold.**

## 0. The core problem with "just add more Lights"

A real-time `Light` component is not free: URP shades it per affected
pixel, and even a modest scene tips over into stutter somewhere in the
low hundreds of simultaneous lights depending on target hardware — long
before "a 1950s city full of lit windows" gets anywhere close to
realistic density. Treating every glowing thing as its own `Light`
was *never* going to scale to "hundreds on screen at once," independent
of the brightness bug.

The fix is a **two-tier model**, not a brighter/dimmer numbers tweak:

| Tier | What it is | Cost | Count |
| --- | --- | --- | --- |
| **1. Emissive glow** | An emissive material on the prop itself (`_EmissionColor`) | Effectively free — no extra draw call, no per-pixel lighting pass, renders in the same batch as the mesh | Unlimited |
| **2. Real dynamic light** | An actual `Light` component, illuminating *nearby geometry* (the ground, a wall) | A real per-pixel cost | Budgeted — one shared pool across the WHOLE city |

Every light in the city is Tier 1 by default. Only the
`RealLightBudget` nearest-to-camera glow points (across every kind
combined — streetlamps and windows and neon and marquee all draw from
the SAME pool, not one pool each) get promoted to Tier 2. This is
exactly the technique real 3D games use for "a skyline full of lit
windows at night" — the windows glow, they don't each cast light.

## 1. CityLightingProfile — the tuning surface

`CityLightingProfile.cs` is a `ScriptableObject`
(`Assets > Create > MadDr > City Lighting Profile`) holding every number
that was previously a hardcoded constant: real-light budget/peak
intensity/range, base emissive brightness, the night boost ceiling,
night ambient/bloom, and flicker/buzz/chase timing. Assign one to
`RuntimeCityBuilder.lightingProfile`; leave it unassigned and
`CityLightingProfile.Default` (safe in-code values) is used instead, so
no scene ever breaks from a missing asset.

**Why a ScriptableObject and not just more Inspector fields on
RuntimeCityBuilder:** every light-emitting system (`RoadDresser`,
`BuildingDresser`, `DynamicLightBudget`, `EmissiveAnimator`) needs to
read the same numbers, and several of them are static generator classes
with no scene-object identity of their own. A shared asset referenced
via one static (`CityLightingProfile.Active`, set once at city-build
time by `RuntimeCityBuilder`) is the natural fit — and it's an asset
players/testers can duplicate per-region or per-mood later without
touching code.

### Where to actually tune at runtime (corrected 2026-07)

The first version of this got the ergonomics wrong: half the values were
baked into `_grades`/cached materials at city-build time, and with no
profile asset assigned `CityLightingProfile.Default` is a runtime-created
object that appears nowhere in the Inspector — so the creator's report
was exactly right, **nothing they could reach changed anything.**

The live knobs are now **plain fields on the two MonoBehaviours**
(`LumenCycleController`, `DynamicLightBudget`, both on the
`RuntimeCityBuilder` GameObject), read every frame / every refresh, so
dragging them in Play mode changes the picture immediately. The profile
asset is the *authored defaults* layer: assigned, it seeds those fields
at city-build time; unassigned, the components keep their own values and
nothing is overwritten.

| Symptom | Knob |
| --- | --- |
| Glowing ball too **bright** | `LumenCycleController.emissiveScale` |
| Glowing ball too **large** | `LumenCycleController.bloomScale` |
| Whole scene washed out | `LumenCycleController.nightAmbient` |
| Lit ground patch too strong | `DynamicLightBudget.pointIntensityMax`/`spotIntensityMax` (2026-07: was one shared `peakIntensity`, now a Max/Min pair per Point/Spot type -- see §0.5 row 16) |
| Lit ground patch too wide | `DynamicLightBudget.range` (note: this is a straight-line radius from the light's own position, not a ground-projected size -- it must comfortably exceed the fixture's mount height or there's no ground patch at all, see the 2026-07 correction in the field's own comment) |
| No pool on the ground at all | Check `DynamicLightBudget.range` isn't shorter than the fixture's mount height (e.g. the ornate lamppost globes sit 5.9m up) |
| Isolate lights vs. glow | `DynamicLightBudget.enableRealLights` (off) |
| Lights don't dim in fog / no diffuse glow in fog | `DynamicLightBudget.fogDimReferenceDensity` (intensity floor) + `LumenCycleController.fogGlowBoost` (bloom boost) -- see §0.5 row 16 |

### Why "altering the DynamicLight" specifically did nothing

Two independent reasons up front, both fixed early — see §0.5 for the
FULL list, including several found only after these two:

1. **The glowing balls are not the dynamic lights.** They are the
   emissive bulb *geometry* (small spheres with an emissive material),
   spread into much larger soft blobs by **Bloom**. A `Light` component
   illuminates *other* surfaces — it does not itself render as a ball on
   screen. So no amount of changing light intensity/range could ever fix
   the reported symptom; `emissiveScale` and `bloomScale` are the knobs
   that actually target it. (The bulb spheres were also literally
   oversized — 0.5 m across at RTS camera height — now ~0.25 m.)
2. **The pooled `DynamicLight` GameObjects are overwritten ~3×/second**
   by `DynamicLightBudget.Refresh()`, which repositions/recolors/resizes
   them from its own fields. Hand-editing those objects in the hierarchy
   could never stick. The component's fields are the real source.

## 2. GlowPointRegistry + DynamicLightBudget — the shared real-light pool

Any prop that glows registers its transform + a tint color with
`GlowPointRegistry`, regardless of which dresser spawned it or what kind
of light it represents. `DynamicLightBudget` (one instance per scene,
added by `RuntimeCityBuilder`) refreshes on a timer (a few times a
second, not every frame): finds the nearest `RealLightBudget` registered
points to the camera, and gives exactly those a real `Point` light
(color-matched, intensity/range from the profile, no shadows — these are
fill lights, not key lights). A small pool of `Light` components is
repositioned/recolored/toggled each refresh rather than
created/destroyed.

**One shared budget, not one per kind**, is the important design
decision here: if streetlamps, windows, and neon each got their own
separate budget of (say) 24, that's 72 real lights the moment all three
kinds are in view — exactly the performance cliff this whole design
exists to avoid. One pool means the budget always goes to whatever's
actually nearest the camera right now, whatever kind it is.

## 3. EmissiveAnimator — batched flicker/buzz/chase at scale

The direct answer to "turn on/off and fade with hundreds of them on
screen." `EmissiveAnimator` is a single manager (`EmissiveAnimatorDriver`
runs its one `Update()` per scene) holding a flat list of registered
renderers and pushing a scaled emission color into each one's own
`MaterialPropertyBlock` — **not** a `MonoBehaviour.Update()` per light,
and **not** a separate `Material` instance per light (which would break
SRP batching). This is the standard Unity technique for "many instances
share one Material, each needs its own per-instance tweak," and it's
genuinely cheap: a couple of trigonometry calls plus one `SetColor` per
*animated* instance per frame.

Four behavior kinds (`LightBehaviorKind`):

- **Steady** — no animation at all. Deliberately a no-op: it does NOT
  install a property-block override, so the renderer just shows the
  shared material's own plain color (still correctly riding
  `NeonRegistry`'s day/night boost). Registering a Steady light would
  actually be *worse* than not registering it — a property-block
  override, once installed, takes priority over the material's color for
  that renderer forever, so a "snapshot" registration would freeze that
  one instance at whatever brightness happened to be active the moment
  it registered, ignoring the day/night cycle from then on. Most props
  in a 1950s city (plain streetlamp bulbs, most signage) are Steady and
  need no registration at all.
- **Flicker** — occasional neon dropout: a slow, per-instance,
  out-of-phase brightness wobble.
- **Window** (2026-07, replaced Flicker for house/apartment windows) —
  the same wobble PLUS a per-instance, deterministically-randomized
  arrival/bedtime occupancy schedule read against `DayNightState.
  CycleProgress` (a raw, non-holding 0..1 clock -- `NightAmount` itself
  holds flat through the whole night by design, so it can't tell "just
  got dark" apart from "3am" the way a bedtime needs to). ~2.4s
  smoothstep transitions at each edge, not a hard pop. 15% of windows
  are `AlwaysOn` and skip the bedtime half entirely. Creator direction:
  "as if real humans were in the room... not all lights go off."
- **Buzz** — a failing neon tube: fast, small-amplitude flutter plus
  occasional brief full dropouts. Used for the movie palace's neon today.
- **Chase** — a marquee "clique" sequencer: a shared clock advances a lit
  index along a row of bulbs, each comparing its own slot to the current
  step. Used for the movie palace's marquee bulb row today.

Every registered instance also needs `DayNightState.NeonBoost` folded
in (published by `LumenCycleController` every frame, the same value
`NeonRegistry.SetBoost` receives) — a `MaterialPropertyBlock` override
otherwise ignores the day/night cycle entirely.

## 4. What's wired up today vs. what's still a stub for later

| Light kind | Tier 1 (glow) | Tier 2 (real light) | Behavior |
| --- | --- | --- | --- |
| Streetlamp bulb (overhanging, arm-over-the-road) | `RoadDresser.Bulb()`, profile-driven brightness | Yes, registered as **Spot** (2026-07: was Point — now aimed straight down at the road, `DynamicLightBudget.spotConeAngle` wide, default 48°) | Steady |
| Ornate multi-globe lamppost | same `Bulb()` material, 3 globes | Yes, ONE registered per fixture (2026-07: was all 3, ~0.5m apart — stacked to ~3x intensity on one patch of pavement and burned 3 budget slots on a single fixture) | Steady |
| Apartment windows | new `BuildingDresser.WindowGlow()`, ~2-in-5 floors lit | Yes, registered | **Window** (2026-07: was Flicker — now randomized per-window arrival/bedtime occupancy scheduling, see §0.5 row 13; still wobbles like Flicker while lit) |
| Movie palace neon (underglow/blade/letters) | existing `NeonTeal`/`SignWhite`/`NeonRed` | No (decorative, not budgeted) | **Buzz** |
| Movie palace marquee chaser row | new small bulb row | No (decorative, not budgeted) | **Chase** |

**Deliberately not attempted this pass** (real, separate follow-ups, not
silently skipped):
- Office-tower window bands (`DressOffice`'s single tall strip per face,
  not per-floor) — no per-floor granularity exists there yet to flicker
  individually; would need the same per-floor-strip treatment
  `DressApartment` got.
- Individual window-pane geometry — every "window" here is still one
  whole floor-height strip, not individual panes; true single-window
  lit/dark granularity needs real per-pane geometry, a bigger
  `BuildingDresser` change.
- Per-kind real-light tinting beyond color (e.g. a wider cone/spread for
  a window's spill vs. a streetlamp's pool) — every promoted light is a
  plain `Point` light today regardless of kind.
- A generic "any prop can flicker" author-time hookup — today's Flicker/
  Buzz/Chase wiring is hand-written at each specific spawn site; a
  future pass could expose this as a per-prop-kind config table instead.

## 5. Adding a new light kind (for whoever's next)

1. Spawn the prop with its emissive material as usual.
2. If it should compete for a real light: `GlowPointRegistry.Register(
   transform, tintColor)`. Omitting the third argument gets a Point
   light (omnidirectional pool — right for most fixtures). Pass
   `LightType.Spot` if the fixture is aimed at something specific (2026-
   07: the overhanging streetlight, aimed down at the road) —
   `DynamicLightBudget` aims every promoted Spot light straight down and
   applies its own shared `spotConeAngle`; there's no per-point
   direction/angle yet since only one fixture kind has asked for Spot so
   far. A second one wanting a DIFFERENT aim/angle would need that
   moved onto `GlowPointRegistry`'s per-point data instead of staying a
   single shared field on `DynamicLightBudget`.
3. If it should animate: `EmissiveAnimator.Register(renderer,
   baseEmissionColorBeforeBoost, kind, seed, ...)` — `baseEmission` is
   the material's own `color * emissive` (matching how `M()` computes
   it), NOT a `Color(r,g,b,a)` alpha.
4. Nothing else — `DynamicLightBudget` and `EmissiveAnimatorDriver`
   already run once per scene and pick up every new registration
   automatically.

## Verification

`flightcheck` compiles clean after every change in this doc (stubs have
been extended repeatedly along the way — real `Vector3`/`Mathf`
trig/`RecalculateNormals`/etc, not just enough to compile; see §0.5 row
6, where a stub that was a `{ }` no-op would have made the regression
test pass vacuously). Compiling clean is necessary, not sufficient —
this session has no Unity Editor, so nothing here is visually confirmed
by *this* session; every fix past the first two rows in §0.5 was
reasoned from creator-supplied screenshots/console output/live Inspector
values, iterating in real time against their actual Editor. That process
found real regressions this session's own reasoning introduced (rows 3
and 7) — read §0.5's status column before assuming anything below Phase
1 is settled, and check whether a newer docs/12 entry supersedes it.
