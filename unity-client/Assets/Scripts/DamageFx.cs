using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Damage feedback (docs/21 batch 2, item 3): a lazy smoke plume that
/// spawns on a building the moment it crosses into Damaged, and a one-
/// shot dust burst at the instant a building collapses to rubble. No
/// ParticleSystem -- period-appropriate for the primitive-kit dressing
/// pipeline and keeps everything on the project's existing Update-driven
/// animation idiom (no coroutines anywhere else in this codebase).
/// </summary>
public static class DamageFx
{
    /// <summary>Attach a slow smoke plume to a Damaged building. Parent
    /// under the building's own holder transform so it rides along if
    /// that transform ever moves (it doesn't today, but costs nothing).
    ///
    /// 2026-08 (creator report: "I've never seen the smoke either"):
    /// this was already correctly wired to both damage paths -- the bug
    /// wasn't wiring, it was scale. A single fixed-size puff (topping
    /// out ~3 units) reads fine against a 6m Small house but disappears
    /// against a 40m Landmark's own roofline and roof clutter (water
    /// towers, antenna masts) from typical RTS camera height. `scale`
    /// (<see cref="MadDr.CityGen.BuildingStats.SmokeScale"/>) sizes the
    /// puffs up proportionally so bigger buildings get a plume that
    /// actually stays visible against their own bigger silhouette --
    /// Small's scale of 1.0 renders identically to before.
    ///
    /// 2026-08 (creator report: "make sure it is on the outside of the
    /// building"): the plume used to spawn dead-center above the roof,
    /// directly over the <see cref="AttachFireCluster"/> points below it
    /// -- once puffs got bigger/paler this pass they could visually
    /// swallow the fire they're supposed to be rising from. `footprintRadius`
    /// (same value the caller already computes for `AttachFireCluster`)
    /// pushes the origin out PAST the building's own edge instead of over
    /// its center.
    ///
    /// 2026-08 follow-up (creator report: smoke needs to read as blowing
    /// "at the correct angle N, S, E or W"): the outward-offset direction
    /// used to be a per-building hash -- changed to the SAME shared
    /// compass angle every building's wind lean used, so a plume erupts
    /// on the leeward side of its own building and keeps drifting that
    /// same way. SUPERSEDED below.
    ///
    /// 2026-08 follow-up (creator direction: "smoke may spawn from any
    /// place on a building BUT IT MUST start on the building and must
    /// travel out radially from the building so it is always seen"): the
    /// shared-compass-wind idea above is GONE -- it could drift a puff
    /// laterally along or even back across a building it didn't spawn on
    /// the leeward side of, depending on that building's position
    /// relative to the one shared direction, risking exactly the
    /// "hidden behind my own building" failure the creator is now
    /// explicitly guarding against. Back to a per-building angle (any
    /// place around the building, hashed off `holder.GetInstanceID()` --
    /// same "cosmetic jitter, no gameplay meaning" precedent every other
    /// per-building visual variety in this codebase already uses), but
    /// this time <see cref="SmokePlume.Init"/> reuses the EXACT same
    /// angle for its own outward drift (not a separate wind lean) -- a
    /// plume moves in a straight radial line away from wherever it
    /// started, so it is GUARANTEED to be moving away from the building's
    /// own silhouette from the very first frame, regardless of where on
    /// the building it happened to spawn.
    ///
    /// 2026-08 follow-up BUGFIX (creator report: "I still do not see the
    /// fire" -- root cause traced here, affecting smoke too):
    /// `holder.position.y` is NOT reliably ground level. RuntimeCityBuilder's
    /// procedural-building call site passes `cubes[0].transform`, and
    /// `SpawnCube(hex, height/2f, height, ...)` places that cube's own
    /// `position.y` at HALF the building's height (a centered primitive
    /// "sitting on the ground" is positioned at its own vertical middle,
    /// not its base) -- every height-fraction offset computed on top of
    /// that was landing half a building-height too high. `holderGroundOffset`
    /// lets a caller correct for this (RuntimeCityBuilder passes
    /// `-height * 0.5f`; BaseDresser's RTS-roster call site, whose root
    /// transform really is at ground level already, passes the default 0
    /// and is unaffected).
    ///
    /// 2026-08 follow-up (creator direction: "smoke must start from low ON
    /// the building and travel upward"): origin height fraction dropped
    /// from 1.05 (floating above the roof, where it used to spawn already
    /// "arrived") to 0.3 -- low on the wall, well under the roofline
    /// <see cref="AttachFireCluster"/>'s own points sit at -- so a puff's
    /// own rise (<see cref="DamageFxProfile.Active"/>.SmokeRiseSpeed) has
    /// real distance to visibly climb THROUGH and past the structure
    /// instead of starting already above it.</summary>
    public static void AttachSmoke(Transform holder, float height, float footprintRadius, float scale, float holderGroundOffset = 0f)
    {
        var go = new GameObject("SmokePlume");
        go.transform.SetParent(holder, false);
        NormalizeScale(go.transform, holder);
        var angle = ((holder.GetInstanceID() & 0xFFFF) % 360) * Mathf.Deg2Rad;
        var groundY = holder.position.y + holderGroundOffset;
        var pos = new Vector3(
            holder.position.x + Mathf.Sin(angle) * footprintRadius,
            groundY + height * 0.3f,
            holder.position.z + Mathf.Cos(angle) * footprintRadius);
        go.transform.position = pos;
        go.AddComponent<SmokePlume>().Init(scale, angle);
        // 2026-08 (creator direction: "figure out how to verify fire is
        // being seen"): no Editor exists in the environment this was
        // written in, so this is the best available diagnostic -- check
        // the Console for this line to confirm a plume was actually
        // created and see exactly where, rather than having to guess
        // whether a reported "still not visible" is a spawn/wiring bug
        // or a genuine render/camera issue.
        Debug.Log("[DamageFx] Smoke started on " + holder.name + " at world " + pos + " (angle " + (angle * Mathf.Rad2Deg).ToString("F0") + " deg)");
    }

    /// <summary>2026-07 (creator direction: "Building need decent amount
    /// of HPs and should show damage and some low-poly fire when being
    /// attacked"): a flickering, low, EMISSIVE flame plume -- lower on
    /// the building than <see cref="AttachSmoke"/>'s own placement (fire
    /// licks near where it's actually burning; the smoke it produces
    /// rises above it), faster/smaller-lived puffs than smoke's own lazy
    /// drift so it reads as agitated flame rather than another slow gray
    /// cloud. Parented under the building's own holder the same way
    /// AttachSmoke already is, so it's automatically destroyed along
    /// with the rest of the building's geometry once it collapses to
    /// rubble -- no separate cleanup needed.</summary>
    public static void AttachFire(Transform holder, float height)
    {
        var go = new GameObject("FirePlume");
        go.transform.SetParent(holder, false);
        go.transform.position = holder.position + Vector3.up * (height * 0.25f);
        go.AddComponent<FirePlume>();
    }

    /// <summary>2026-08 (creator direction: "it should start with 1 but
    /// then others popup in different places based on the building size
    /// up to 8"): the multi-point successor to <see cref="AttachFire"/>
    /// -- one <see cref="FirePlume"/> lands immediately at a random spot
    /// on the footprint, then more stagger in over the next several
    /// seconds at DIFFERENT scattered spots, up to `targetCount` (see
    /// <see cref="MadDr.CityGen.BuildingStats.FireCount"/> for the
    /// tier->count table both building systems -- procedural and RTS --
    /// share the same numbers for). `footprintRadius` bounds how far a
    /// fire point can land from center -- a bigger building spreads its
    /// fires wider, not just more densely packed at the same single
    /// spot `AttachFire` always used.
    ///
    /// 2026-08 follow-up BUGFIX (creator report: "I still do not see the
    /// fire"): same ground-offset bug <see cref="AttachSmoke"/> was fixed
    /// for -- `holder.position.y` isn't reliably ground level (see that
    /// method's own doc comment for the full root-cause writeup).
    /// `FireCluster.SpawnOne`'s own local-Y-offset math (`_height * 1.0f`
    /// for the roofline) assumes its parent transform's origin IS ground
    /// level, so snapping that parent to the corrected ground Y here
    /// fixes every point this cluster ever spawns without touching
    /// `FireCluster`'s own logic at all.</summary>
    public static void AttachFireCluster(Transform holder, float height, float footprintRadius, int targetCount, float holderGroundOffset = 0f)
    {
        var go = new GameObject("FireCluster");
        go.transform.SetParent(holder, false);
        NormalizeScale(go.transform, holder);
        var groundPos = new Vector3(holder.position.x, holder.position.y + holderGroundOffset, holder.position.z);
        go.transform.position = groundPos;
        go.AddComponent<FireCluster>().Init(height, footprintRadius, targetCount);
        // 2026-08 (creator direction: "figure out how to verify fire is
        // being seen"): see AttachSmoke's own matching log line -- this
        // confirms AttachFireCluster actually ran and shows the corrected
        // ground position the cluster's own points are offset from, so a
        // "still don't see it" report can be checked against the actual
        // numbers instead of guessed at blind.
        Debug.Log("[DamageFx] Fire cluster started on " + holder.name + " at ground " + groundPos + " (roofline will be " + (groundPos.y + height).ToString("F1") + ")");
    }

    /// <summary>2026-08 (creator report: "Finally found the fire debug
    /// markers and FireCluster and firedebug sphere are way up in the
    /// air, high above the building. Could be a scale issue. or a child
    /// parent. but they are NOT on the buildings." -- creator's own
    /// hypothesis was correct): `holder` for the procedural-building call
    /// site is `cubes[0].transform`, and RuntimeCityBuilder.SpawnCube sets
    /// that cube's OWN `localScale` to `(hexSize*0.9, height, hexSize*0.9)`
    /// -- e.g. roughly (18, 14.4, 18) for a real building, wildly
    /// non-uniform. `SetParent(holder, false)` preserves world position
    /// (fine, and is why the absolute `.position` assignments right after
    /// it in both call sites land correctly) but does NOT reset the
    /// child's own `localScale` -- so SmokePlume/FireCluster's wrapper
    /// silently inherits that scale as its effective (lossyScale) world
    /// scale. That's invisible for the wrapper's OWN position (set in
    /// world space), but corrupts every LOCAL offset computed by anything
    /// parented under IT in turn -- concretely, FireCluster.SpawnOne's
    /// `go.transform.localPosition = offset` (meant as real meters) gets
    /// multiplied by the inherited scale when Unity resolves world space,
    /// e.g. a 7m Y offset x 14.4 height-scale lands ~100m in the air.
    /// Setting this wrapper's own `localScale` to the component-wise
    /// inverse of the parent's `lossyScale` right after SetParent cancels
    /// the inheritance, so 1 local unit under the wrapper is back to being
    /// 1 real meter. Cheap and called before any position/child math runs.
    /// No Unity Editor exists in this environment to verify live -- this
    /// is a from-first-principles fix matching the exact numbers in the
    /// creator's report, not a guess.</summary>
    private static void NormalizeScale(Transform child, Transform holder)
    {
        var parentScale = holder.lossyScale;
        child.localScale = new Vector3(
            parentScale.x != 0f ? 1f / parentScale.x : 1f,
            parentScale.y != 0f ? 1f / parentScale.y : 1f,
            parentScale.z != 0f ? 1f / parentScale.z : 1f);
    }

    /// <summary>One-shot muzzle smoke the instant a gun fires (creator
    /// direction, 2026-07: "guns have smoke when they fire") -- small and
    /// quick next to the building SmokePlume's lazy loop or DustBurstFx's
    /// wide radial burst, so it reads as a gunshot, not a fire or a
    /// collapse. Unparented (world-space): the muzzle it fired from keeps
    /// moving/turning, but the puff itself should hang where it was fired
    /// and drift, not get dragged along by the barrel.</summary>
    public static void MuzzleSmoke(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MuzzleSmoke";
        go.transform.position = at;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.6f, 0.58f, 0.55f, 0.65f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        go.AddComponent<SmokePuff>().InitBurst(mat, 0.55f, 1.4f, 0.65f);
    }

    /// <summary>One-shot dust puff burst at a collapsing building's site.</summary>
    public static void DustBurst(Vector3 at, Transform parent)
    {
        var go = new GameObject("DustBurst");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        go.AddComponent<DustBurstFx>();
    }

    /// <summary>A player-built base structure's actual collapse (2026-07,
    /// "buildings need... more rubble when attacked"): the existing
    /// one-shot <see cref="DustBurst"/> plus a lingering pile of scattered
    /// debris chunks, sized off the building's own full-scale footprint
    /// (a Landmark HQ leaves a bigger, longer-lived wreck than a Small
    /// storage shed). Distinct from <see cref="DustBurst"/> -- that one
    /// stays a quick puff-only beat used elsewhere; this is the actual
    /// "there's rubble here now" persistent read.</summary>
    public static void BuildingRubble(Vector3 at, Transform parent, float footprintScale)
    {
        DustBurst(at, parent);
        var go = new GameObject("RubblePile");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        go.AddComponent<RubblePileFx>().Init(footprintScale);
    }

    /// <summary>A vertical water spout where a hydrant just got sheared
    /// off -- sprays for a few seconds, then peters out and cleans
    /// itself up (`WaterSpout`).</summary>
    public static void WaterJet(Vector3 at, Transform parent)
    {
        var go = new GameObject("WaterJet");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        go.AddComponent<WaterSpout>();
    }

    /// <summary>A dark ground stain at a citizen's last position -- the
    /// horror-movie kill mark. Fades out after a while (`GroundStain`)
    /// rather than lingering forever, so a long match's eaten-citizen
    /// count doesn't accumulate into ground clutter.</summary>
    public static void BloodSplatter(Vector3 at, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "BloodSplatter";
        go.transform.SetParent(parent, false);
        go.transform.position = at + Vector3.up * 0.04f;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.42f, 0.05f, 0.06f, 0.85f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        go.AddComponent<GroundStain>().Init(mat, go.transform);
    }
}

/// <summary>A flat ground decal that holds, then fades out and self-
/// destructs. Deterministic-ish size variety off its own instance ID
/// (no gameplay meaning riding on it, so GetInstanceID is fine here
/// unlike the seeded-hash dressers).</summary>
public class GroundStain : MonoBehaviour
{
    private Material _mat;
    private float _age;
    private const float Life = 14f;
    private const float FadeStart = 9f;

    public void Init(Material mat, Transform t)
    {
        _mat = mat;
        var id = GetInstanceID();
        var size = 1.3f + (id & 3) * 0.35f;
        t.localScale = new Vector3(size, 0.05f, size * (0.7f + ((id >> 2) & 3) * 0.15f));
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age > FadeStart && _mat != null)
        {
            var t = Mathf.Clamp01((_age - FadeStart) / (Life - FadeStart));
            var c = _mat.color;
            _mat.color = new Color(c.r, c.g, c.b, 0.85f * (1f - t));
        }
        if (_age >= Life) Object.Destroy(gameObject);
    }
}

/// <summary>Spawns a soft gray puff every beat, for as long as the
/// GameObject it's attached to lives (i.e. until the building is
/// destroyed and its holder gets crushed/removed with the rest of the
/// rubble pass).</summary>
public class SmokePlume : MonoBehaviour
{
    private float _timer;
    private float _scale = 1f;

    // 2026-08 (creator direction, confirming a reference image: diagonal
    // drift/lean like wind-blown smoke instead of climbing straight up):
    // set ONCE per plume (i.e. per building) in Init, not per puff -- every
    // puff this plume ever spawns shares the same lean, so the whole
    // column reads as one coherent wind-blown trail rather than
    // independent puffs each wobbling their own random direction.
    private Vector2 _lean;

    /// <summary>`outwardAngle` is the SAME angle <see cref="DamageFx.AttachSmoke"/>
    /// already used to push this plume's origin outside the building's
    /// footprint -- reusing it here keeps the drift moving further in the
    /// direction the plume already started in, instead of potentially
    /// doubling back across the building.
    ///
    /// 2026-08 (creator report: "radiates from one point and does not
    /// travel upward drift away at a diagonal based on wind speed"): the
    /// magnitude is <see cref="DamageFxProfile.Active"/>.SmokeWindSpeed
    /// instead of a hardcoded 0.55.
    ///
    /// 2026-08 follow-up (creator direction: "smoke may spawn from any
    /// place on a building BUT IT MUST start on the building and must
    /// travel out radially from the building so it is always seen"): this
    /// briefly went through a SHARED-compass-direction phase (every
    /// building's plume leaning the exact same N/S/E/W way) -- REMOVED.
    /// `outwardAngle` is back to being per-building (see `AttachSmoke`'s
    /// own doc comment for why the shared version was actually a
    /// visibility risk, not just an aesthetic downgrade), and this drift
    /// is now purely radial -- straight away from wherever the plume
    /// spawned, guaranteed to never lean back toward the building's own
    /// silhouette.</summary>
    public void Init(float scale, float outwardAngle)
    {
        _scale = scale;
        var windSpeed = DamageFxProfile.Active.SmokeWindSpeed;
        _lean = new Vector2(Mathf.Sin(outwardAngle) * windSpeed, Mathf.Cos(outwardAngle) * windSpeed);
    }

    private void Awake()
    {
        _timer = (GetInstanceID() & 7) * 0.1f;
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 0.7f + (GetInstanceID() & 3) * 0.1f;
        SpawnPuff();
    }

    /// <summary>2026-08 (creator direction, confirming a reference image
    /// for shape/color/scale): the round `Sphere` primitive is gone --
    /// each puff is now a <see cref="ProceduralMeshKit.CloudShard"/>, a
    /// jittered angular low-poly chunk (answer "1: angular" to the shape
    /// question). Lightened from the near-black sooty gray the prior
    /// visibility fix used to a cool pale gray -- with the shape, position,
    /// and lean changes in this same pass all adding their own
    /// contrast/readability on top, a return to a lighter, more
    /// traditional smoke color no longer risks blending back into the
    /// building palette the way the original 0.35/0.34/0.32 gray did.
    ///
    /// 2026-08 CORRECTION (creator report: "the smoke is way too big and
    /// I can not see the fire"): the same pass's `ScaleUpPct` size-up on
    /// top of the existing 0.7 resize is GONE -- it made the plume big
    /// enough to visually swallow the fire cluster it rises from. See
    /// `DamageFx.AttachSmoke` for the other half of this fix (moving the
    /// plume's origin outside the building instead of shrinking further).
    ///
    /// 2026-08 follow-up (creator direction: "smoke way way smaller. 0.2
    /// resize."): dropped from 0.7 to 0.2 -- a further cut on top of the
    /// fix above, not a reversal of it.
    ///
    /// 2026-08 follow-up (creator direction: "add inspector for smoke
    /// size"): the flat constant is gone -- now reads
    /// <see cref="DamageFxProfile.Active"/>.SmokeResizePct every spawn
    /// (not cached), so an Inspector slider takes effect on the very next
    /// puff without a city rebuild. 0.2 lives on as that field's own
    /// default.
    ///
    /// 2026-08 follow-up BUGFIX (creator direction: "growth in size
    /// should never exceed 2 times the size of the original"): while
    /// wiring an Inspector cap for this, found `SmokeResizePct` was NEVER
    /// actually reaching the puff's real on-screen size -- `startSize`
    /// here was only ever applied to `localScale` for ONE frame before
    /// `SmokePuff.Update` overwrote it every subsequent frame using its
    /// OWN `_baseScale` field, which `InitPlume` never set (it silently
    /// kept the shared 0.8 default every non-smoke puff kind also uses).
    /// The actual growth formula was ALSO unrelated to that base -- a
    /// flat amount added on top, letting the visible size reach roughly
    /// 9-10x its nominal starting point regardless of what
    /// `SmokeResizePct` was set to. Both are fixed together:
    /// `InitPlume` now takes this method's own `startSize` directly and
    /// stores it as `_baseScale` (so the resize knob finally drives the
    /// size that's actually rendered every frame), and growth is now a
    /// capped multiplier on THAT base (see `InitPlume`'s own doc comment)
    /// instead of an unrelated flat add.</summary>
    private void SpawnPuff()
    {
        var go = new GameObject("SmokePuff");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position;
        var id = go.GetInstanceID();
        var resizePct = DamageFxProfile.Active.SmokeResizePct;
        var startSize = 1.1f * resizePct * _scale;
        go.transform.localScale = new Vector3(startSize, startSize, startSize);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = ProceduralMeshKit.CloudShard(6, id * 0.013f);
        var renderer = go.AddComponent<MeshRenderer>();

        var mat = new Material(ShaderUtil.FindRenderableShader());
        // cool pale gray (was 0.16/0.15/0.14 sooty near-black) -- see the
        // summary above for why the earlier contrast fix's reasoning no
        // longer applies once shape/position/lean are all pulling their
        // own weight too.
        mat.color = new Color(0.68f, 0.7f, 0.74f, 0.8f);
        LabMeshBuilder.MakeTransparent(mat);
        renderer.sharedMaterial = mat;

        go.AddComponent<SmokePuff>().InitPlume(mat, 3.2f, startSize, 0.8f, _lean);
    }
}

/// <summary>Spawns a bright, flickering EMISSIVE puff every beat -- much
/// faster cadence and shorter per-puff life than <see cref="SmokePlume"/>'s
/// own lazy 0.7-1.0s drift, so it reads as agitated flame licking up
/// rather than another slow gray cloud. Lives exactly as long as the
/// GameObject it's attached to (i.e. until the building's holder is torn
/// down at Destroyed).
///
/// 2026-08 (creator direction: "glowing and fire like movement"): two
/// upgrades over the original puff-only version -- (1) a real flickering
/// point `Light`, so this actually casts warm light onto the building
/// and ground around it instead of only self-lighting the fire mesh via
/// emission, which is what "glowing" actually reads as at a distance;
/// (2) each puff now sways side to side as it rises (<see
/// cref="SmokePuff.InitFlame"/>) instead of drifting in a dead-straight
/// line, the actual "licking" motion real flame has that a constant-
/// velocity puff never could.</summary>
public class FirePlume : MonoBehaviour
{
    private float _timer;
    private Light _glow;
    private float _flickerPhase;
    private float _sizeScale = 1f;

    /// <summary>2026-08 (creator direction: "Larger building get more
    /// fires and larger fires"): `sizeScale` comes from <see
    /// cref="FireCluster"/>'s own burnable-surface-area math (see that
    /// class's `Init`) -- 1.0 for anything small enough to sit at the old
    /// tier baseline, up to 3.0 for a genuinely large building. Called
    /// right after `AddComponent`, i.e. AFTER `Awake` has already set
    /// `_glow`'s base range -- multiplies that base rather than
    /// recomputing it, so `FireResizePct`'s own existing range formula is
    /// still the single source of the UN-scaled baseline.</summary>
    public void Init(float sizeScale)
    {
        _sizeScale = sizeScale;
        _glow.range *= sizeScale;
    }

    private void Awake()
    {
        _timer = (GetInstanceID() & 7) * 0.03f;
        _flickerPhase = (GetInstanceID() & 255) * 0.37f;

        _glow = gameObject.AddComponent<Light>();
        _glow.type = LightType.Point;
        _glow.color = new Color(1f, 0.55f, 0.15f);
        // 2026-08 (creator report: "the fire is too large"): a 6m-range,
        // 2.5-intensity point light was throwing a glow bigger than the
        // small low-poly flame it was supposed to be lighting up. Shrunk
        // to match a contained shard, not a bonfire -- see
        // DamageFxProfile.FireResizePct's own doc comment for that
        // history AND the later "I still do not see the fire" correction
        // (0.35 turned out to be an over-shrink, reverted to 1.0).
        var fireResizePct = DamageFxProfile.Active.FireResizePct;
        _glow.range = 3f * fireResizePct;
        _glow.intensity = 1.1f * fireResizePct;
        // no shadow-casting -- a handful of these across a burning
        // skyline would be a real per-frame cost for a purely cosmetic
        // beat, same "cheap is the point" reasoning every other FX class
        // in this file already follows (primitives, no ParticleSystem).
        _glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        // fast, irregular flicker (two mismatched sine frequencies beat
        // against each other rather than one clean pulse, which reads as
        // mechanical, not like fire)
        _flickerPhase += Time.deltaTime * 9f;
        var flicker = 0.7f + Mathf.Abs(Mathf.Sin(_flickerPhase) * 0.6f + Mathf.Sin(_flickerPhase * 2.3f) * 0.4f) * 0.5f;
        // this REPLACES a previously-hardcoded `0.35f` literal with the
        // live profile value (default 0.35, so default behavior is
        // byte-identical) -- NOT multiplied together with it, which would
        // have compounded into an unintended ~3x dimming at the default.
        // Read fresh every frame (not cached from Awake) so an Inspector
        // change affects an ALREADY-burning building's glow immediately.
        _glow.intensity = DamageFxProfile.Active.FireResizePct * flicker;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 0.12f + (GetInstanceID() & 3) * 0.03f;
        SpawnPuff();
    }

    /// <summary>2026-08 (creator report: "the fire is too large... it
    /// should look like [reference images: small, angular, faceted low-
    /// poly fire]"): the puff-sphere approach is gone entirely for fire --
    /// a smooth round sphere can't read as "low-poly" no matter how small
    /// it's scaled. Each spawn is now a <see
    /// cref="ProceduralMeshKit.FlameShard"/>, a small jagged shard mesh.
    ///
    /// 2026-08 follow-up (creator direction: "add inspector for fire
    /// size"): puff size/growth read
    /// <see cref="DamageFxProfile.Active"/>.FireResizePct fresh on every
    /// spawn instead of a hardcoded literal -- see that field's own doc
    /// comment for its current default and the "I STILL CAN'T SEE THE
    /// FIRE" history behind it.
    ///
    /// 2026-08 follow-up (creator report: "still no visible fire... for
    /// now make it the size of the smoke"): FireResizePct alone climbed
    /// 1.0 -> 3.0 -> 6.0 across three straight "still can't see it"
    /// reports, so this stops trusting fire's own size knob and borrows
    /// smoke's instead -- `SmokePlume.SpawnPuff`'s own `1.1f *
    /// SmokeResizePct * scale` formula, minus the per-tier `scale` (this
    /// cluster doesn't carry a building-scale value the way SmokePlume
    /// does, and "for now" doesn't call for plumbing one through). Smoke
    /// IS confirmed visible at its own size -- if a flame shard sized by
    /// that exact same knob still can't be spotted, size was never the
    /// actual bug and the next round should look elsewhere (material/
    /// shader/render order) instead of bumping a number a fifth time.
    /// FireResizePct itself is untouched and still drives the point
    /// light's range/intensity below -- only the flame MESH's size
    /// changed here.
    ///
    /// 2026-08 follow-up (creator direction: "Increase the size of fire
    /// by 18%"): the borrowed-from-smoke base above is now multiplied by
    /// <see cref="DamageFxProfile.Active"/>.FireSizeBoostPct (default
    /// 1.18) -- a dedicated fire-only knob rather than bumping
    /// SmokeResizePct itself, since that field is still shared with (and
    /// still confirmed correctly sized for) smoke. Also multiplied by
    /// `_sizeScale` (set in `Init`, see that method's own doc comment) so
    /// a bigger building's fire reads as bigger, not just more
    /// numerous.</summary>
    private void SpawnPuff()
    {
        var go = new GameObject("FirePuff");
        go.transform.SetParent(transform, false);
        var id = go.GetInstanceID();
        go.transform.position = transform.position + new Vector3(((id & 3) - 1.5f) * 0.1f, 0f, (((id >> 2) & 3) - 1.5f) * 0.1f);
        var smokeResizePct = DamageFxProfile.Active.SmokeResizePct;
        // this initial scale only holds for one frame -- SmokePuff.Update
        // (below) overwrites it uniformly every frame off _baseScale, the
        // same "explicit spawn scale is cosmetically moot" precedent
        // SmokePlume/DustBurstFx's own spawn-time scale already sets.
        var size = 1.1f * smokeResizePct * DamageFxProfile.Active.FireSizeBoostPct * _sizeScale;
        go.transform.localScale = new Vector3(size, size, size);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = ProceduralMeshKit.FlameShard(5, id * 0.017f);
        var renderer = go.AddComponent<MeshRenderer>();

        var mat = new Material(ShaderUtil.FindRenderableShader());
        var warm = ((id >> 4) & 3) == 0;
        mat.color = warm ? new Color(0.95f, 0.55f, 0.12f, 0.9f) : new Color(0.98f, 0.78f, 0.2f, 0.9f);
        LabMeshBuilder.MakeTransparent(mat);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", (warm ? new Color(0.95f, 0.35f, 0.05f) : new Color(1f, 0.65f, 0.1f)) * 2.5f);
        // 2026-08 (creator direction: "figure out how to verify fire is
        // being seen and make sure it is NOT hidden by smoke"): both fire
        // and smoke materials go through the SAME MakeTransparent, which
        // sets renderQueue = 3000 for both -- meaning ordinary back-to-
        // front alpha-blend sorting decides which one paints on top,
        // purely by which happens to be nearer the camera at that
        // instant. A smoke puff drifting past a fire point could
        // therefore visually paint over it regardless of size. Bumping
        // fire ONE queue value above smoke's (LabMeshBuilder itself is
        // untouched, so every OTHER transparent object in this file
        // keeps sorting by distance as before) makes fire always
        // composite AFTER (i.e. on top of/visible through) any smoke at
        // the same screen position, independent of which is actually
        // closer to the camera.
        mat.renderQueue = 3001;
        renderer.sharedMaterial = mat;

        // 2026-08 (creator report: "I STILL CAN'T SEE THE FIRE"): life
        // was 0.5-0.74s -- fast enough that an individual shard could
        // blink in and fade back out before the eye has time to register
        // it, unlike smoke's own sustained 3.2s puffs. Tripled to
        // 1.5-2.22s so a flame actually holds on screen long enough to be
        // seen, not just technically rendered for a few frames.
        go.AddComponent<SmokePuff>().InitFlame(mat, 1.5f + ((id >> 6) & 3) * 0.24f, size * 0.8f, 0.9f, size);
    }
}

/// <summary>2026-08 (creator direction: "it should start with 1 but
/// then others popup in different places based on the building size up
/// to 8"): owns a growing set of <see cref="FirePlume"/> points
/// scattered across a Damaged building's own footprint. The FIRST point
/// lands the instant `Init` runs (so a building never sits Damaged with
/// zero fire showing, matching "should start with 1"); every later
/// point staggers in on its own randomized 2-5s timer, until
/// `targetCount` is reached, then this component goes idle -- it never
/// removes a fire once lit (matching every other FX class in this file:
/// no repair mechanic exists, so nothing here needs to reverse itself
/// either).
///
/// 2026-08 follow-up (creator direction: "Start the fire on the side
/// that the camera happens to be pointing at BUT keep it locked to
/// that side after. DO NOT move the camera to the fire or the fire
/// should NOT move sides after it is started"): the prior pass tried
/// moving the CAMERA to the fire (via SimpleCameraRig.FocusOn) once the
/// creator reported not being able to spot even an unmissable debug
/// marker -- explicitly rejected. This does the inverse: the fire
/// itself reads `Camera.main`'s current position ONCE, at ignition
/// (`_baseAngle`, computed in `Init`, never recomputed after), and
/// every point this cluster ever spawns -- not just the first -- lands
/// within a bounded arc of that SAME angle, instead of the old full
/// 0-360 degree random scatter. A building's fire commits to whichever
/// face happened to be camera-facing when it started burning and stays
/// there for its whole life, so it's never NOT visible because the
/// building itself is between it and wherever the camera happens to be
/// looking right now.</summary>
public class FireCluster : MonoBehaviour
{
    private float _height;
    private float _footprintRadius;
    private int _targetCount;
    private int _spawned;
    private float _nextSpawnIn;
    private float _baseAngle;
    private float _sizeScale;

    // 2026-08 (creator direction: "spawn a number of fires on the
    // building as attacks continue over time. Larger building get more
    // fires and larger fires. Set a sensible limit based of building's
    // burnable surface area"): the flat 2-4 tier table (`BuildingStats.
    // FireCount`/`BaseDresser.FireCountFor`) had no notion of a
    // building's actual real size -- a Landmark and a Large capped at
    // the SAME 4 points regardless of how much taller/wider one actually
    // was. `_height`/`_footprintRadius` are already the real per-building
    // numbers (not a coarse 4-step tier lookup) passed in from the two
    // real call sites (RuntimeCityBuilder.IgniteBuildingIfNeeded for
    // procedural buildings, BaseDresser for the RTS roster), so an area
    // derived from THEM scales with whatever that specific building
    // instance actually measures -- taller AND wider buildings earn more
    // (and bigger) fire without a new tier-boundary table to keep in
    // sync with the other two.
    //
    // `AreaPerFirePoint`/`MaxFireCountCeiling` pick where that scaling
    // lands: 300 sqm per additional point keeps the existing tuned tier
    // numbers exactly as they already were for anything roughly house-to-
    // storefront sized (small area, area-based count comes out at or
    // below the tier floor, so `Mathf.Max` below leaves the tier number
    // untouched -- no regression for anything already dialed in), and
    // only starts adding MORE points once a building's actual wall area
    // clears that tier baseline -- exactly the buildings the creator's
    // report says were being shortchanged. The 10-point ceiling is the
    // "sensible limit": generous headroom above the old flat 4-point cap
    // for a genuinely huge structure, without spawning an unreasonable
    // wall of flame on anything.
    private const float AreaPerFirePoint = 300f;
    private const int MaxFireCountCeiling = 10;

    // Same area value also drives per-point SIZE (`_sizeScale`, read by
    // FirePlume.SpawnPuff) -- "larger fires" for larger buildings, not
    // just more of them. Reference area/ceiling chosen to land in the
    // SAME 1.0-3.0x range `BuildingStats.SmokeScale`'s own tier table
    // already uses for smoke, so a burning Landmark's fire and smoke read
    // as proportionate to each other, not fire alone racing ahead.
    private const float SizeScaleReferenceArea = 3000f;
    private const float MaxSizeScale = 3f;

    /// <summary>Rough burnable wall area: treats `footprintRadius` as a
    /// half-width (the SAME "not a true radius, just a plan-size proxy"
    /// approximation `SpawnOne`'s own `dist = footprintRadius * 1.6f`
    /// line below already leans on) -- four walls, each `2 *
    /// footprintRadius` wide, times the building's real height. Only
    /// needs to be internally consistent (bigger real building -> bigger
    /// number) to drive the two scalings above; not a claim of an exact
    /// square-meter figure.</summary>
    private static float BurnableSurfaceArea(float height, float footprintRadius)
    {
        return 8f * footprintRadius * height;
    }

    public void Init(float height, float footprintRadius, int targetCount)
    {
        _height = height;
        _footprintRadius = footprintRadius;
        var area = BurnableSurfaceArea(height, footprintRadius);
        // 2026-08 (creator direction: "2-4 depending on the size of the
        // building"): the caller's own tier-based `targetCount` (2-4)
        // still sets a FLOOR -- an area-based number below what a tier
        // was already tuned to never shrinks it -- while the area-based
        // figure can push a genuinely large building's cap higher, up to
        // `MaxFireCountCeiling`.
        var areaBasedCap = Mathf.RoundToInt(area / AreaPerFirePoint);
        _targetCount = Mathf.Clamp(Mathf.Max(Mathf.Clamp(targetCount, 1, 4), areaBasedCap), 1, MaxFireCountCeiling);
        _sizeScale = Mathf.Clamp(1f + area / SizeScaleReferenceArea, 1f, MaxSizeScale);
        _baseAngle = CameraFacingAngle();
        SpawnOne();
        _nextSpawnIn = NextInterval();
    }

    /// <summary>The angle (this class's own `Mathf.Cos(angle)*x,
    /// Mathf.Sin(angle)*z` convention -- standard math angle from +X,
    /// matching `SpawnOne`'s own offset formula below) from this
    /// building's own ground position toward wherever `Camera.main`
    /// currently is. Falls back to a per-building hash (the OLD fully-
    /// random behavior) if no main camera exists yet -- still
    /// deterministic and harmless, just not camera-aware, for whatever
    /// edge case (headless test, camera not yet spawned) that implies.</summary>
    private float CameraFacingAngle()
    {
        var cam = Camera.main;
        if (cam == null) return ((GetInstanceID() & 0xFFFF) % 360) * Mathf.Deg2Rad;
        var toCamera = cam.transform.position - transform.position;
        return Mathf.Atan2(toCamera.z, toCamera.x);
    }

    private float NextInterval()
    {
        return 2f + ((GetInstanceID() + _spawned * 977) & 15) * 0.2f; // 2-5s, staggered not metronomic
    }

    private void Update()
    {
        if (_spawned >= _targetCount) return;
        _nextSpawnIn -= Time.deltaTime;
        if (_nextSpawnIn > 0f) return;
        SpawnOne();
        _nextSpawnIn = NextInterval();
    }

    private void SpawnOne()
    {
        _spawned++;
        var salt = GetInstanceID() + _spawned * 733;
        // 2026-08 (creator direction: "keep it locked to that side...
        // the fire should NOT move sides after it is started"): was a
        // full 0-360 degree random spread per point -- now a bounded
        // +-35 degree jitter around `_baseAngle` (the camera-facing
        // direction captured once at ignition, see `Init`/
        // `CameraFacingAngle`), so every point this cluster ever spawns
        // stays on the SAME face of the building instead of wrapping
        // around to whichever side happens to be away from the camera.
        // 2026-08 (creator direction: "the fire should come from the
        // building, be attached to the roof, or the windows"): EVERY
        // point (including the first) now lands 30-90% out toward the
        // footprint's own edge -- near a roof edge/window band, not the
        // dead-center open air the old "first point sits at dist 0"
        // placement floated it in. Height moved from a quarter of the
        // way up the wall to near the roofline itself, so it reads as
        // erupting from the building's own structure instead of hanging
        // in front of it.
        //
        // 2026-08 follow-up (creator report: "I still do not see the
        // fire... check placement on various buildings to make sure it
        // is visible"): 0.92 sat BELOW roofline height, i.e. embedded
        // inside a Landmark-tier building's own roof clutter (water
        // towers, antenna masts -- the exact same class of occlusion the
        // smoke-visibility bug several entries back was traced to for
        // that tier). Raised to right at the roofline (1.0) so a fire
        // point pokes up clear of roof props instead of nesting among
        // them, while still reading as attached to the building rather
        // than floating above it the way AttachSmoke's own 1.05 does.
        //
        // 2026-08 follow-up (creator direction: "same radial rule applies
        // to fire. Always visible"): the old 30-90%-of-radius range put
        // some points as much as 70% of the way back toward the
        // building's own interior/roof-center, exactly the kind of
        // position roof clutter (or the building's own bulk, from a
        // shallow camera angle) could bury a point behind. Fixed to
        // EXACTLY `_footprintRadius` -- the building's own outer edge,
        // same "sits at the true perimeter, not somewhere inward that
        // might be occluded" rule `AttachSmoke` already applies to
        // smoke's own placement (`footprintRadius * 1.0`). Only the
        // ANGLE still varies per point, matching smoke's own "any place
        // around the building" placement freedom.
        //
        // 2026-08 follow-up (creator report: "still no visible fire. Make
        // sure the fire is on the outside of the building"): EXACTLY the
        // footprint radius turned out not to read as "outside" -- the
        // caller's own `footprintRadius` (RuntimeCityBuilder.cs) is a
        // rough `sqrt(hexCount) * hexMeters * 0.4` approximation of a
        // building's plan size, not its true rendered half-width, so a
        // point placed at exactly that distance can still land within
        // the building cube's own visual silhouette instead of clear of
        // it. Pushed 60% further out so fire sits unambiguously past the
        // mesh's own edge, not right at (or inside) an approximated one.
        // Smoke keeps its own unchanged `footprintRadius * 1.0` -- it's
        // the one confirmed-visible effect this round, nothing about its
        // placement is in question.
        var dist = _footprintRadius * 1.6f;
        var jitterDeg = ((salt >> 8) & 0xFFFF) % 71 - 35;
        // 2026-08 (creator question: "could the fire be spawning outside
        // of one building but inside another adjacent one?"): a real,
        // verified risk, not a hypothetical -- BuildingFootprintHalfExtent's
        // own doc comment (RuntimeCityBuilder.cs) already establishes that
        // adjacent buildings' rendered corners can overlap into a
        // neighbouring hex's space (18m-wide boxes on a 20m hex grid),
        // and `dist` above sits right at that same edge for a single-hex
        // building. Clearing THIS building's own silhouette (what `dist`
        // alone guarantees) says nothing about whatever else is standing
        // between a candidate point and the camera in a dense block.
        // `PickClearAngle` searches a small set of angles within the SAME
        // +-35 camera-facing arc (never widening it -- "keep it locked to
        // that side" above still holds) for one with an actually-
        // unobstructed line of sight to the camera, falling back to the
        // original jittered angle if every candidate is blocked (a fire
        // point that's occasionally partly occluded is still far better
        // than this attack ever failing to ignite anything at all).
        // 2026-08 VERIFIED root cause (creator direction: "place fire on
        // walls and windows of building... not on roofs", then confirmed
        // after this shipped: "I SEE SMOKE NO FIRE"): height 1.0 above
        // WAS the roofline, exactly -- worked the actual numbers from a
        // real ignition log rather than guessing again. That report's own
        // console line ("Fire cluster started on Cube at ground (700.00,
        // -4.19, 658.18) (roofline will be 10.2)") plus AttachSmoke's own
        // logged puff height (groundY + height*0.3, i.e. ~0.1 above
        // ground for that same building) gives two REAL, comparable
        // numbers: smoke sits ~0.1m up (confirmed visible); fire sat
        // ~10.2m up -- dead level with the roofline this exact building's
        // own log reported, on a building only ~14.4m tall. A point
        // sitting exactly AT a roofline is exactly where a parapet lip,
        // roof edge, or any roof clutter this file's own history already
        // blamed for hiding things on taller tiers would occlude it --
        // and it's also the ONE height a typical RTS camera's downward
        // look angle is least likely to clear the building's own silhouette
        // to see. This was never a shader/material/marker bug (see this
        // class's own commit history for that ruled-out investigation) --
        // it was always this one number. Height now lands in the WALL
        // band instead -- 30-65% up the building, varied per point off
        // `salt` (the same per-point hash already driving this point's
        // own angle jitter) so a multi-point cluster reads as fire in
        // several different windows, not one uniform band -- comfortably
        // clear of both street level (smoke's own territory) and the
        // roofline (explicitly excluded, per "not on roofs").
        var heightFrac = 0.3f + ((salt >> 16) & 0xFFFF) / 65536f * 0.35f;   // 0.30-0.65 of height
        // computed BEFORE the angle search below so PickClearAngle's own
        // line-of-sight probe tests the EXACT height this point will
        // actually spawn at, not an approximation of it.
        var angle = _baseAngle + PickClearAngle(jitterDeg, dist, heightFrac) * Mathf.Deg2Rad;
        var offset = new Vector3(Mathf.Cos(angle) * dist, _height * heightFrac, Mathf.Sin(angle) * dist);

        var go = new GameObject("FirePlume");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        // 2026-08 (creator report: "yes now on building now"): fire's
        // placement is confirmed working, so the diagnostic-only magenta
        // "FireDebugMarker" sphere this used to drop at the first point
        // (see this file's history for the whole visibility saga it was
        // built to debug) is gone -- `DamageFx.SpawnDebugMarker` and
        // `DamageFxProfile.ShowFireDebugMarkers` were removed with it.
        // `_sizeScale` (see `Init`) is how a bigger building's fire reads
        // as visibly bigger, not just more numerous.
        go.AddComponent<FirePlume>().Init(_sizeScale);
    }

    /// <summary>2026-08 (creator question: "could the fire be spawning
    /// outside of one building but inside another adjacent one? Fire
    /// should look for a clear line of sight to camera to pick initial
    /// spawn points"): searches a small, deterministic set of candidate
    /// angles -- `primaryJitterDeg` first (so an already-clear point keeps
    /// reading exactly as before this change), then progressively wider
    /// offsets, each still clamped inside the SAME +-35 degree arc around
    /// `_baseAngle` that "keep it locked to that side" already commits
    /// this cluster to -- for the first one whose candidate WORLD position
    /// has an unobstructed <see cref="Physics.Linecast(Vector3, Vector3)"/>
    /// to `Camera.main`. Deliberately does NOT filter which collider it
    /// hit (a passing car or citizen counts as "occluded" too, not just a
    /// neighbouring building) -- for a one-off spawn-time pick, treating
    /// any obstruction as reason to try the next candidate is simpler and
    /// safer than maintaining a building-only allowlist, and a transient
    /// occluder at the exact instant of ignition is a rare, harmless
    /// edge case this same search already tries multiple angles against.
    /// Falls back to `primaryJitterDeg` untouched if every candidate is
    /// blocked, or if no `Camera.main` exists yet to test against --
    /// this NEVER prevents a fire point from spawning, only prefers a
    /// clearer one when it can find one.</summary>
    private static readonly int[] AngleSearchOffsets = { 0, 14, -14, 28, -28, 35, -35 };

    private int PickClearAngle(int primaryJitterDeg, float dist, float heightFrac)
    {
        var cam = Camera.main;
        if (cam == null) return primaryJitterDeg;
        for (var i = 0; i < AngleSearchOffsets.Length; i++)
        {
            var jitter = Mathf.Clamp(primaryJitterDeg + AngleSearchOffsets[i], -35, 35);
            var candidateAngle = _baseAngle + jitter * Mathf.Deg2Rad;
            var localOffset = new Vector3(Mathf.Cos(candidateAngle) * dist, _height * heightFrac, Mathf.Sin(candidateAngle) * dist);
            var worldPoint = transform.TransformPoint(localOffset);
            if (!Physics.Linecast(worldPoint, cam.transform.position))
                return jitter;
        }
        return primaryJitterDeg;
    }
}

/// <summary>A single rising, fading, growing puff -- self-destructs when
/// its life runs out. Used by both the ongoing SmokePlume and the one-
/// shot DustBurstFx.</summary>
public class SmokePuff : MonoBehaviour
{
    private Material _mat;
    private float _age;
    private float _life = 2.2f;
    private Vector3 _drift = Vector3.up;
    private float _growth = 2.2f;
    private float _baseAlpha = 0.75f;

    // 2026-08 (creator report: "the fire is too large"): every existing
    // puff kind (smoke/dust/water/muzzle) keeps this at 0.8, its original
    // hardcoded floor, completely unchanged -- only InitFlame overrides
    // it, so shrinking fire specifically can never touch the smoke fix
    // that shipped right before this one.
    private float _baseScale = 0.8f;

    // 2026-08 (creator direction: "the smoke should start small and
    // float upward getting bigger and dissipating"): used by the ORIGINAL
    // (non-smoke) growth formula below -- `_baseScale * Lerp(_startScaleFraction,
    // 1, t) + t * _growth` -- still exactly 1.0 (no-op) for every puff
    // kind that uses that formula (fire/dust/water/muzzle).
    //
    // 2026-08 follow-up: smoke no longer uses this field at all -- see
    // `_useGrowthMultiplier`/`_growthMultiplier` below and `InitPlume`'s
    // own doc comment for why (the old start-fraction-plus-flat-growth
    // model let a puff balloon to ~9-10x its own starting size, well past
    // what "growth in size should never exceed 2 times the size of the
    // original" asks for).
    private float _startScaleFraction = 1f;

    // 2026-08 (creator direction: "fire like movement"): zero for every
    // existing puff kind (smoke/dust/water jet all keep their original
    // dead-straight drift, unchanged), nonzero only via InitFlame below
    // -- a sideways sway ON TOP of the usual upward drift, so a flame
    // puff licks side to side as it rises instead of traveling a
    // straight line the way every other puff in this file always has.
    private float _swayAmp;
    private float _swayFreq;
    private float _swayPhase;

    // 2026-08 (creator direction: "always smooth fading out of upper
    // large chunks of smoke"): false (linear alpha fade, `1f - t`,
    // completely unchanged) for every existing puff kind -- only
    // InitPlume (smoke) sets this, so fire/dust/water/muzzle keep their
    // original fade curve exactly.
    private bool _easeFade;

    // 2026-08 (creator direction: "growth in size should never exceed 2
    // times the size of the original"): false (the old `_baseScale *
    // Lerp(_startScaleFraction, 1, t) + t * _growth` formula, completely
    // unchanged) for every existing puff kind -- only InitPlume (smoke)
    // sets this true, switching Update's scale math to a clean
    // `_baseScale * Lerp(1, _growthMultiplier, t)` (start size times a
    // capped multiplier) instead.
    private bool _useGrowthMultiplier;
    private float _growthMultiplier = 2f;

    public void Init(Material mat)
    {
        _mat = mat;
        var id = GetInstanceID();
        _drift = new Vector3(((id & 3) - 1.5f) * 0.3f, 1.4f, (((id >> 2) & 3) - 1.5f) * 0.3f);
    }

    public void InitBurst(Material mat, float life, float growth, float baseAlpha)
    {
        Init(mat);
        _life = life;
        _growth = growth;
        _baseAlpha = baseAlpha;
        _drift = new Vector3(_drift.x, 0.6f, _drift.z);
    }

    /// <summary>Same lazy rise <see cref="Init"/> already gives a smoke
    /// puff (unlike <see cref="InitBurst"/>'s faster 0.6-up drift, tuned
    /// for a quick one-shot burst, not an ongoing plume) with life/alpha
    /// overridden. `lean` (2026-08, confirming a reference image: diagonal
    /// drift instead of straight-up) is added on top of `Init`'s own
    /// small per-puff horizontal wobble -- every puff from the same
    /// `SmokePlume` shares the same `lean` value, so the whole column
    /// drifts one coherent wind-blown direction instead of each puff
    /// wandering its own random way.
    ///
    /// 2026-08 follow-up (creator direction: "always smooth fading out
    /// of upper large chunks of smoke"): sets `_easeFade`, which swaps
    /// `Update`'s fade curve from a constant-rate linear ramp to a
    /// smoothstep ease -- alpha barely moves for the first stretch of
    /// life, drops through the middle, then eases toward zero rather
    /// than hitting it at a fixed rate the whole way. The biggest, oldest
    /// puffs (the "upper large chunks," near the end of their own life
    /// and the top of the column) are specifically the ones this changes
    /// the feel of, since a linear ramp is already close to zero alpha by
    /// the time they're that old/big -- easing keeps them visibly present
    /// longer and then dissolves them gradually instead of at the same
    /// flat rate as a puff half their age.
    ///
    /// 2026-08 follow-up BUGFIX/redesign (creator direction: "growth in
    /// size should never exceed 2 times the size of the original"; "give
    /// me inspector setting to alter drift"): `startSize` (renamed from
    /// the old `growth` parameter, and now genuinely THE puff's rendered
    /// starting size -- see `SmokePlume.SpawnPuff`'s own doc comment for
    /// why the old wiring never actually achieved that) becomes
    /// `_baseScale` directly, replacing the old shared-default-0.8
    /// nobody had actually pointed `SmokeResizePct` at. `_growthMultiplier`
    /// is read from <see cref="DamageFxProfile.Active"/>.SmokeGrowthMultiplier
    /// and clamped to [1, 2] here in code -- NOT just via the Inspector's
    /// own [Range] attribute, which a script or an out-of-date serialized
    /// asset could bypass -- so a puff genuinely cannot exceed double its
    /// own starting size regardless of what's configured. Vertical rise
    /// speed is now <see cref="DamageFxProfile.Active"/>.SmokeRiseSpeed
    /// instead of the flat 1.4 baked into `Init` above (still shared by
    /// every OTHER puff kind, unchanged).</summary>
    public void InitPlume(Material mat, float life, float startSize, float baseAlpha, Vector2 lean)
    {
        Init(mat);
        _life = life;
        _baseAlpha = baseAlpha;
        _baseScale = startSize;
        _useGrowthMultiplier = true;
        _growthMultiplier = Mathf.Clamp(DamageFxProfile.Active.SmokeGrowthMultiplier, 1f, 2f);
        var riseSpeed = DamageFxProfile.Active.SmokeRiseSpeed;
        _drift = new Vector3(_drift.x + lean.x, riseSpeed, _drift.z + lean.y);
        _easeFade = true;
    }

    /// <summary>Same shape as <see cref="InitBurst"/> (a fire puff is
    /// still a short-lived rising burst, not a lazy plume) but with a
    /// real side-to-side sway layered on top instead of a straight
    /// drift -- the actual "licking flame" motion. `baseScale` overrides
    /// the 0.8 every other puff kind uses (2026-08, "the fire is too
    /// large") -- fire's own low-poly shard mesh is meant to read as a
    /// small, contained flame, not a puff-sized blob.</summary>
    public void InitFlame(Material mat, float life, float growth, float baseAlpha, float baseScale)
    {
        InitBurst(mat, life, growth, baseAlpha);
        _baseScale = baseScale;
        _drift = new Vector3(0f, 1.1f, 0f); // faster, dead-vertical rise -- the sway supplies the sideways motion instead
        var id = GetInstanceID();
        _swayAmp = 0.5f + (id & 3) * 0.15f;
        _swayFreq = 5f + ((id >> 2) & 3) * 1.5f;
        _swayPhase = (id & 255) * 0.13f;
    }

    /// <summary>Fully-specified drift -- the hydrant water jet uses this
    /// to fire droplets UP hard with a slight scatter, unlike smoke's
    /// lazy rise or dust's outward roll.</summary>
    public void InitJet(Material mat, Vector3 drift, float life, float growth, float baseAlpha)
    {
        _mat = mat;
        _drift = drift;
        _life = life;
        _growth = growth;
        _baseAlpha = baseAlpha;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        var t = Mathf.Clamp01(_age / _life);
        transform.position += _drift * Time.deltaTime;
        if (_swayAmp > 0f)
        {
            _swayPhase += Time.deltaTime * _swayFreq;
            // sway grows WITH the puff's own age -- a flame licks wider
            // the higher it climbs, not a fixed-amplitude wobble from
            // the moment it's born
            transform.position += Vector3.right * (Mathf.Sin(_swayPhase) * _swayAmp * t * Time.deltaTime * 3f);
        }
        // smoke (_useGrowthMultiplier) grows from its OWN starting size up
        // to at most `_growthMultiplier` times that (hard-capped to 2x --
        // see InitPlume's own doc comment); every other puff kind keeps
        // the original formula exactly.
        var scale = _useGrowthMultiplier
            ? _baseScale * Mathf.Lerp(1f, _growthMultiplier, t)
            : _baseScale * Mathf.Lerp(_startScaleFraction, 1f, t) + t * _growth;
        transform.localScale = new Vector3(scale, scale, scale);
        if (_mat != null)
        {
            // smoke (_easeFade) uses a smoothstep ease instead of a
            // constant-rate linear ramp -- see InitPlume's summary for why
            var fadeT = _easeFade ? t * t * (3f - 2f * t) : t;
            var c = _mat.color;
            _mat.color = new Color(c.r, c.g, c.b, _baseAlpha * (1f - fadeT));
        }
        if (t >= 1f) Object.Destroy(gameObject);
    }
}

/// <summary>Sprays water droplets upward for a few seconds after a
/// hydrant is sheared off, then stops emitting and destroys itself once
/// the last droplet has faded.</summary>
public class WaterSpout : MonoBehaviour
{
    private float _age;
    private float _emitTimer;
    private const float SprayDuration = 6f;

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= SprayDuration)
        {
            // droplets live ~1.1s; linger past the last one, then clean up
            Object.Destroy(gameObject, 1.5f);
            enabled = false;
            return;
        }

        _emitTimer -= Time.deltaTime;
        if (_emitTimer > 0f) return;
        _emitTimer = 0.12f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "WaterDroplet";
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position + Vector3.up * 0.6f;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.5f, 0.72f, 0.85f, 0.8f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        // hard vertical jet with a slight per-droplet scatter
        var id = go.GetInstanceID();
        var drift = new Vector3(((id & 7) - 3.5f) * 0.22f, 5.5f, (((id >> 3) & 7) - 3.5f) * 0.22f);
        go.AddComponent<SmokePuff>().InitJet(mat, drift, 1.1f, 0.9f, 0.8f);
    }
}

/// <summary>A quick radial burst of dust puffs -- the "something just
/// fell down" beat for a building's collapse.</summary>
public class DustBurstFx : MonoBehaviour
{
    private void Awake()
    {
        for (var i = 0; i < 5; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DustPuff";
            go.transform.SetParent(transform, false);
            var angle = i * 72f * Mathf.PI / 180f;
            var dir = new Vector3(Mathf.Cos(angle), 0.25f, Mathf.Sin(angle));
            go.transform.position = transform.position + dir * 2f;
            go.transform.localScale = Vector3.one * 1.6f;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var mat = new Material(ShaderUtil.FindRenderableShader());
            mat.color = new Color(0.45f, 0.42f, 0.36f, 0.8f);
            LabMeshBuilder.MakeTransparent(mat);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;

            go.AddComponent<SmokePuff>().InitBurst(mat, 0.9f, 3.2f, 0.8f);
        }
        Object.Destroy(gameObject, 1.2f);
    }
}

/// <summary>A pile of scattered debris chunks left where a building stood
/// -- unlike the puff-based FX above (which self-destruct in ~1-2s), this
/// lingers for a real while, then fades and cleans itself up, same fade
/// convention as <see cref="GroundStain"/> (so a long match's destroyed
/// bases don't accumulate into permanent clutter).</summary>
public class RubblePileFx : MonoBehaviour
{
    private const float Life = 40f;
    private const float FadeStart = 30f;
    private readonly List<Renderer> _chunks = new List<Renderer>();
    private readonly List<Material> _mats = new List<Material>();
    private float _age;

    /// <summary>footprintScale: the building's own full-scale footprint
    /// (BaseDresser's FullScaleFor) -- chunk count and scatter radius both
    /// grow with it, so a Landmark HQ leaves visibly more wreckage than a
    /// Small storage shed.</summary>
    public void Init(float footprintScale)
    {
        var chunkCount = Mathf.Clamp(Mathf.RoundToInt(4f + footprintScale * 1.5f), 4, 14);
        var radius = Mathf.Max(1.5f, footprintScale * 0.6f);
        var id = GetInstanceID();

        for (var i = 0; i < chunkCount; i++)
        {
            var salt = id + i * 977;
            var angle = ((salt & 0xFFFF) % 360) * Mathf.PI / 180f;
            var dist = radius * (0.3f + ((salt >> 8) & 15) / 15f * 0.7f);
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "RubbleChunk";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = dir * dist + Vector3.up * (0.15f + ((salt >> 4) & 7) * 0.03f);
            var chunkScale = 0.4f + ((salt >> 12) & 7) / 7f * 0.5f;
            go.transform.localScale = new Vector3(chunkScale, chunkScale * 0.6f, chunkScale);
            go.transform.localRotation = Quaternion.Euler(0f, (salt & 4095) / 4096f * 360f, 0f);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var mat = new Material(ShaderUtil.FindRenderableShader());
            var gray = 0.28f + ((salt >> 6) & 7) / 7f * 0.18f;
            mat.color = new Color(gray, gray * 0.94f, gray * 0.88f, 1f);
            LabMeshBuilder.MakeTransparent(mat);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
            _chunks.Add(renderer);
            _mats.Add(mat);
        }
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age > FadeStart)
        {
            var t = Mathf.Clamp01((_age - FadeStart) / (Life - FadeStart));
            for (var i = 0; i < _mats.Count; i++)
            {
                if (_mats[i] == null) continue;
                var c = _mats[i].color;
                _mats[i].color = new Color(c.r, c.g, c.b, 1f - t);
            }
        }
        if (_age >= Life) Object.Destroy(gameObject);
    }
}
