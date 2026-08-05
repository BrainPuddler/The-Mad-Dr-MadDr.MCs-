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
/// <summary>2026-08 (creator direction: "Each visible fire instance
/// should receive a persistent random seed. Randomize independently:
/// flame height, width, brightness, flicker speed, animation phase,
/// lean angle, emissive intensity, growth rate, lifetime. Adjacent
/// flames should never look cloned... Do not increase the number of
/// fire objects unnecessarily... Avoid per-frame allocations. Avoid
/// expensive searches. Maintain deterministic behaviour"): one
/// `FirePlume` IS "a visible fire instance" -- its own `GetInstanceID()`
/// (already this file's standing convention for deterministic, non-
/// `UnityEngine.Random` variety) is its persistent seed, sampled ONCE in
/// `Awake` into the jitter fields below, then reused for as long as the
/// plume lives. No new fields beyond these plain floats, no new
/// GameObjects/components, nothing computed per-frame beyond a handful
/// of already-existing multiplies -- every `FirePuff` this plume ever
/// spawns (still the SAME pooled-nothing/reused-`SmokePuff` model as
/// before) just reads these instead of a flat constant, so puffs from
/// the SAME plume share a coherent "personality" while DIFFERENT plumes
/// (each with their own instance ID) never line up.</summary>
public class FirePlume : MonoBehaviour
{
    private float _timer;
    private Light _glow;
    private float _flickerPhase;
    private float _sizeScale = 1f;
    private float _heatScale = 1f;

    // ---- persistent per-instance visual-variation seed ----
    private float _heightJitter = 1f;
    private float _widthJitter = 1f;
    private float _brightnessJitter = 1f;
    private float _emissiveJitter = 1f;
    private float _growthJitter = 1f;
    private float _lifetimeJitter = 1f;
    private float _flickerSpeedJitter = 1f;
    private float _leanYaw;
    private float _leanTilt;

    /// <summary>Deterministic 0..1 hash off this instance's own persistent
    /// seed plus a salt (distinguishes which property is being sampled) --
    /// same GetInstanceID()-bit-masking idiom `RandomFloat01`/`Hash01`
    /// already use elsewhere in this file, just local to this class since
    /// nothing outside `FirePlume` needs it.</summary>
    private static float Jitter(int seed, int salt, float min, float max)
    {
        var t = ((seed + salt * 92821) & 0xFFFF) / 65536f;
        return Mathf.Lerp(min, max, t);
    }

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

    /// <summary>2026-08 (fire-propagation rewrite, creator's own brief:
    /// "Fire growth... should use heat energy rather than simply counting
    /// flames. High-energy regions create larger flames, taller flames,
    /// brighter emissive lighting, stronger flicker"): called every sim
    /// tick by the owning `FireCluster` for as long as this point's own
    /// internal cell keeps heating up (see `FireCluster.TickFireNetwork`)
    /// -- `heatScale` on top of `_sizeScale` is how a single point reads
    /// as growing/intensifying the longer a building stays under attack,
    /// distinct from `_sizeScale` (which is fixed once at ignition, off
    /// the building's own overall size).</summary>
    public void SetHeatScale(float heatScale)
    {
        _heatScale = heatScale;
    }

    private void Awake()
    {
        var seed = GetInstanceID();
        _timer = (seed & 7) * 0.03f;
        // "animation phase" -- already persistent-per-instance via `seed`,
        // unchanged from before this pass, just noted here alongside its
        // siblings rather than re-derived.
        _flickerPhase = (seed & 255) * 0.37f;

        _heightJitter = Jitter(seed, 1, 0.8f, 1.25f);
        _widthJitter = Jitter(seed, 2, 0.8f, 1.25f);
        _brightnessJitter = Jitter(seed, 3, 0.85f, 1.15f);
        _emissiveJitter = Jitter(seed, 4, 0.8f, 1.3f);
        _growthJitter = Jitter(seed, 5, 0.75f, 1.35f);
        _lifetimeJitter = Jitter(seed, 6, 0.8f, 1.3f);
        _flickerSpeedJitter = Jitter(seed, 7, 0.75f, 1.35f);
        _leanYaw = Jitter(seed, 8, 0f, 360f);
        _leanTilt = Jitter(seed, 9, 4f, 16f);

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
        // mechanical, not like fire). `_flickerSpeedJitter` (persistent
        // per-instance, see Awake) means different flames flicker at
        // visibly different rates, not a shared metronome.
        _flickerPhase += Time.deltaTime * 9f * _flickerSpeedJitter;
        var flicker = 0.7f + Mathf.Abs(Mathf.Sin(_flickerPhase) * 0.6f + Mathf.Sin(_flickerPhase * 2.3f) * 0.4f) * 0.5f;
        // this REPLACES a previously-hardcoded `0.35f` literal with the
        // live profile value (default 0.35, so default behavior is
        // byte-identical) -- NOT multiplied together with it, which would
        // have compounded into an unintended ~3x dimming at the default.
        // Read fresh every frame (not cached from Awake) so an Inspector
        // change affects an ALREADY-burning building's glow immediately.
        // `_heatScale` (see SetHeatScale) is the "brighter emissive
        // lighting, stronger flicker" half of heat-driven growth;
        // `_brightnessJitter` is this instance's own persistent variation
        // on top of that.
        _glow.intensity = DamageFxProfile.Active.FireResizePct * flicker * _heatScale * _brightnessJitter;

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
    /// numerous.
    ///
    /// 2026-08 follow-up (fire-propagation rewrite): also multiplied by
    /// `_heatScale` (see `SetHeatScale`) -- "high-energy regions create
    /// larger flames, taller flames" from the creator's own brief, read
    /// fresh on every puff so a point already burning visibly grows as
    /// its own internal cell keeps accumulating heat under sustained
    /// attack, not just once at spawn.
    ///
    /// 2026-08 follow-up (creator direction: persistent per-instance
    /// visual variation, see this class's own doc comment): `size` above
    /// is now split into an independent `_widthJitter`/`_heightJitter`
    /// pair (via `SmokePuff.SetScaleAxisMultiplier` -- `SmokePuff.Update`
    /// otherwise always applies a UNIFORM scale every frame off its own
    /// `_baseScale`, so setting a non-uniform `localScale` here alone
    /// would just get overwritten the next frame; this is a real,
    /// backward-compatible opt-in, defaulting to `Vector3.one` for every
    /// OTHER `SmokePuff` consumer -- smoke/dust/water/muzzle are
    /// byte-for-byte unaffected). Color brightness/emissive intensity/
    /// growth rate/lifetime each get their own persistent multiplier;
    /// `_leanYaw`/`_leanTilt` apply a fixed rotation so every puff from
    /// THIS plume leans the same consistent direction (a coherent lick,
    /// not each puff twitching a new random way) while still differing
    /// from every OTHER plume's own lean.</summary>
    private void SpawnPuff()
    {
        var go = new GameObject("FirePuff");
        go.transform.SetParent(transform, false);
        var id = go.GetInstanceID();
        go.transform.position = transform.position + new Vector3(((id & 3) - 1.5f) * 0.1f, 0f, (((id >> 2) & 3) - 1.5f) * 0.1f);
        go.transform.rotation = Quaternion.Euler(_leanTilt, _leanYaw, 0f);
        var smokeResizePct = DamageFxProfile.Active.SmokeResizePct;
        // this initial scale only holds for one frame -- SmokePuff.Update
        // (below) overwrites it uniformly every frame off _baseScale, the
        // same "explicit spawn scale is cosmetically moot" precedent
        // SmokePlume/DustBurstFx's own spawn-time scale already sets.
        var size = 1.1f * smokeResizePct * DamageFxProfile.Active.FireSizeBoostPct * _sizeScale * _heatScale;
        go.transform.localScale = new Vector3(size * _widthJitter, size * _heightJitter, size * _widthJitter);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = ProceduralMeshKit.FlameShard(5, id * 0.017f);
        var renderer = go.AddComponent<MeshRenderer>();

        var mat = new Material(ShaderUtil.FindRenderableShader());
        var warm = ((id >> 4) & 3) == 0;
        var baseColor = warm ? new Color(0.95f, 0.55f, 0.12f, 0.9f) : new Color(0.98f, 0.78f, 0.2f, 0.9f);
        mat.color = new Color(Mathf.Clamp01(baseColor.r * _brightnessJitter), Mathf.Clamp01(baseColor.g * _brightnessJitter), Mathf.Clamp01(baseColor.b * _brightnessJitter), baseColor.a);
        LabMeshBuilder.MakeTransparent(mat);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", (warm ? new Color(0.95f, 0.35f, 0.05f) : new Color(1f, 0.65f, 0.1f)) * 2.5f * _emissiveJitter);
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
        var life = (1.5f + ((id >> 6) & 3) * 0.24f) * _lifetimeJitter;
        var growth = size * 0.8f * _growthJitter;
        var puff = go.AddComponent<SmokePuff>();
        puff.InitFlame(mat, life, growth, 0.9f, size);
        puff.SetScaleAxisMultiplier(new Vector3(_widthJitter, _heightJitter, _widthJitter));
    }
}

/// <summary>2026-08 REWRITE (creator direction: "use something like this
/// to spawn fire on buildings" -- pasted a full fire-propagation brief:
/// an invisible internal heat/fuel/ventilation network per building,
/// weapon impacts inject heat at an origin point, heat diffuses through
/// neighbouring structure biased strongly upward, and the VISIBLE
/// low-poly flame system only ever spawns/grows where that invisible
/// network crosses an ignition threshold near an exterior surface --
/// "BUT Keep to visible area"): replaces the old flat random-timer
/// spawner with exactly that two-layer model, sized down to stay
/// "simple and performant" (creator direction, same message) --
/// per-building state is a tiny fixed 5x3 grid (15 <see cref="FireCell"/>
/// structs, no heap churn after `Init`), ticked at a throttled interval,
/// not every frame.
///
/// **The grid IS the visible-area constraint, by construction.** Its 5
/// angle columns span ONLY the existing +-35 degree camera-facing arc
/// (`_baseAngle`, unchanged from the prior "keep it locked to that side"
/// contract) -- there is no cell, and therefore no possible ignition or
/// visible flame, outside that slice.
///
/// **Internal network (invisible):** each <see cref="FireCell"/> tracks
/// Heat and Ventilation only (no separate Fuel/StructuralDamage fields --
/// the brief's fuller model; trimmed for "simple and performant" since
/// nothing here needs a building to ever stop burning once lit, matching
/// this file's own "never removes a fire once lit" convention for every
/// other FX class). `TickFireNetwork` diffuses heat from every hot cell
/// to its neighbours once per sim tick, biased `UpwardBias`/
/// `SidewaysBias`/`DownwardBias` (2.5x / 1.6x / 0.2x -- see the follow-up
/// below for why sideways moved off the brief's own literal "100%") and
/// scaled by the RECEIVING cell's own Ventilation.
///
/// **External renderer (visible):** a cell only ever gets a real
/// `FirePlume` when `TickFireNetwork` finds its Heat crossed
/// `IgnitionThreshold` (`IgniteCell`) -- the visible system never spawns
/// or spreads on its own, it only ever reacts to what the invisible
/// network already decided. An already-ignited cell's own continuing
/// heat instead grows its EXISTING flame (`FirePlume.SetHeatScale`).
///
/// 2026-08 follow-up (creator report: "fires is sometimes in the tile
/// but not on the building. BURNING is ONLY allowed ON the building
/// surfaces including roof decoration and other features. Cheat
/// probability of flames on surfaces facing the camera closest to the
/// camera. winder spread of spawn points not just clustered in one spot
/// or a burn line. Allow some crawling. increase the speed of the
/// spread based on number of attack points and amount of time before
/// building is destroyed. shorter time more spawns"): four changes on
/// top of the model above --
///
/// 1. **On-surface placement** (`PickSurfacePoint`, replacing the old
///    fixed-distance `PickClearAngle`): a real `Physics.Raycast` against
///    the building's own massing-cube collider finds where a candidate
///    angle/height actually meets real geometry, instead of assuming a
///    single standoff distance works for every angle of a non-circular
///    footprint -- see that method's own doc comment for the full
///    root-cause writeup.
/// 2. **Camera-facing weight** (`AngleColumnWeights`, used by
///    `PickWeightedColumn`): ongoing hits (`RegisterHit`) are no longer
///    funneled into one fixed origin cell -- they land in a WEIGHTED
///    random column, skewed toward the arc's own centre (the most
///    directly camera-facing angle) but not exclusively there, so
///    heat -- and therefore fire -- starts from several lateral points
///    across the visible facade instead of stacking into one vertical
///    "burn line." `SidewaysBias` was also raised (1.0 -> 1.6) so
///    ordinary diffusion crawls sideways more readily once heat exists
///    in more than one column.
/// 3. **Urgency + attack-rate driven speed** (`_urgency`, `_hitRateEma`):
///    `RegisterHit` now also receives the building's current HP fraction
///    -- `_urgency` rises toward 1 as HP falls toward zero ("amount of
///    time before building is destroyed... shorter time more spawns"),
///    and `_hitRateEma` is a smoothed hits-per-second read off real
///    `Time.time` gaps between calls ("number of attack points": more
///    simultaneous attackers means hits land closer together in time).
///    Both feed `CurrentSimTickInterval` (ticks faster) and raise
///    `_maxIgnitedCells` above its area-based floor (more points allowed)
///    as either climbs.
///
/// Embers (`MaybeSpawnEmber`) are unchanged in spirit -- rare,
/// grid-adjacent-only jumps -- but their chance now also scales with
/// `_urgency`, so "allow some crawling" picks up along with everything
/// else as a building nears destruction.</summary>
public class FireCluster : MonoBehaviour
{
    private float _height;
    private float _footprintRadius;
    private float _baseAngle;
    private float _sizeScale;

    // ---- sizing (unchanged from the prior area-based pass -- still the
    // "how many points, how big" ceiling; now the FLOOR `_maxIgnitedCells`
    // rises above as urgency/attack-rate climb, see RegisterHit) ----

    private const float AreaPerFirePoint = 300f;
    // 2026-08 (creator direction: "Increase spawn goal is to make it
    // look logical the building would collapse"): raised from 10 to the
    // grid's own true max (GridAngleSegments * GridHeightBands = 15) --
    // a building right at the edge of destruction should be able to have
    // nearly its ENTIRE visible facade on fire, not still capped a third
    // short of full coverage. Paired with MaxUrgencyBonusCells below (3
    // -> 6) so a small building's own low `_baseMaxIgnitedCells` can
    // actually climb far enough to reach this higher ceiling as urgency
    // rises, not just big-area buildings that already started closer to
    // it.
    private const int MaxFireCountCeiling = 15;
    private const float SizeScaleReferenceArea = 3000f;
    private const float MaxSizeScale = 3f;

    private static float BurnableSurfaceArea(float height, float footprintRadius)
    {
        return 8f * footprintRadius * height;
    }

    // ---- the internal heat network ----

    /// <summary>5 angle columns across the existing +-35 degree visible
    /// arc (matching `AngleSearchOffsets`' own spacing below) x 3 height
    /// bands (low/window, mid/upper-floor, high/roof-edge -- all still
    /// safely under the roofline, per "not on roofs"). 15 cells total:
    /// trivially small to simulate every tick.</summary>
    private const int GridAngleSegments = 5;
    private const int GridHeightBands = 3;
    private static readonly int[] GridAngleOffsets = { -28, -14, 0, 14, 28 };
    private static readonly float[] GridHeightFracs = { 0.28f, 0.5f, 0.72f };

    /// <summary>Center-skewed pick weights for `PickWeightedColumn`,
    /// index-aligned with `GridAngleOffsets` -- the middle column (0
    /// degrees off `_baseAngle`, i.e. most directly camera-facing) is
    /// still favored over either edge column, but not as strongly as the
    /// original `{1,2,3,2,1}` pass (a flat 3x centre-vs-edge ratio).
    ///
    /// 2026-08 follow-up (creator direction, alongside a fuller visual-
    /// variation brief: "decrease but do not eliminate[] the spawn facing
    /// camera preference"): ratio pulled down to 2x (`{2,3,4,3,2}`) --
    /// the centre still wins on average ("do not eliminate"), but the
    /// gap to the edges is smaller, letting hits land further round the
    /// arc more often, which reads as organically wider spread once
    /// combined with this cell's own new per-cell `Flammability` variance
    /// (see `FireCell`) instead of the camera weighting alone carrying
    /// all of "keep it wide."</summary>
    private static readonly int[] AngleColumnWeights = { 2, 3, 4, 3, 2 };
    private const int AngleColumnWeightSum = 14;

    /// <summary>2026-08 (creator direction: "Fire should spread as
    /// irregular islands connected by branching fingers, leaving
    /// temporary gaps, hesitating in some areas, suddenly accelerating in
    /// others... The player should never be able to identify the
    /// underlying simulation grid"): `Flammability` is a fixed, PERSISTENT
    /// per-cell multiplier (set once in `Init`, from a deterministic hash
    /// -- see that method's own comment) folded into `SpreadHeat`
    /// alongside `Ventilation`. Some cells are inherently a little
    /// quicker to catch, some a little slower, purely from this one extra
    /// float -- no new objects, no per-frame allocation, no expensive
    /// search, and (since it's hashed off THIS `FireCluster` instance's
    /// own `GetInstanceID()`, freshly assigned every time a new one is
    /// created) genuinely different each time a building catches fire
    /// again, while staying perfectly reproducible within any ONE
    /// ignition for debugging.</summary>
    private struct FireCell
    {
        public float Heat;
        public float Ventilation;
        public float Flammability;
        public bool Ignited;
        public FirePlume Plume;
    }

    private FireCell[] _cells;
    private float[] _heatDelta; // reused scratch buffer -- one alloc in Init, zero per tick
    private int _originCellIndex;
    private int _ignitedCount;
    private int _baseMaxIgnitedCells; // the area-based floor from Init, never lowered
    private int _maxIgnitedCells;     // live cap: baseMaxIgnitedCells + urgency bonus, see RegisterHit
    private int _tickCounter;
    private int _hitCount;
    private float _simTimer;
    private float _urgency;    // 0 at full HP, -> 1 as the building nears destruction
    private float _hitRateEma; // smoothed hits/second, fed by RegisterHit's own Time.time gaps
    private float _lastHitTime = -1f;

    private const float SimTickInterval = 0.5f;    // base tick -- "keep system simple and performant"
    private const float MinSimTickInterval = 0.12f; // fastest this ever ticks, however high urgency/hit-rate climb
    private const float HitRateSmoothing = 0.3f;
    private const float MaxHitRateForSpeedup = 3f;  // cap so a pathological burst can't tick every single frame
    private const float HitRateSpeedupFactor = 0.6f;
    private const float UrgencySpeedupFactor = 1.5f;
    private const int MaxUrgencyBonusCells = 3;     // "shorter time [to destruction], more spawns"

    private const float BaselineVentilation = 1f;
    private const float VentilationOnIgnite = 1.5f;
    private const float VentilationNeighborBleed = 0.15f;
    private const float IgnitionThreshold = 1f;
    private const float InitialImpactHeat = 1.2f; // > IgnitionThreshold: origin cell ignites the instant Init runs, same "starts with 1 immediately" guarantee the old spawner made
    private const float BaseDiffusionRate = 0.12f;
    // "Structural Bias": upward/downward are the brief's own +250%/20%
    // figures, verbatim. Sideways raised 1.0 -> 1.6 (creator follow-up:
    // "winder spread... Allow some crawling") -- literal buoyancy physics
    // says heat shouldn't travel sideways FASTER than it rises, so this
    // stops short of matching Upward, but a flat 1.0 combined with a
    // single seed point was producing one vertical column before any
    // meaningful lateral crawl happened; 1.6 lets sideways keep pace
    // better once `PickWeightedColumn` has already seeded more than one
    // column (see RegisterHit).
    private const float UpwardBias = 2.5f;
    private const float SidewaysBias = 1.6f;
    private const float DownwardBias = 0.2f;
    private const float HeatForMaxScale = 3f;
    private const float HeatScaleCap = 1.6f;
    private const float EmberChancePerTick = 0.03f;
    private const float EmberHeatFloor = 1.6f;
    private const float ImpactHeatPerDamage = 0.01f;
    private const float NeighborImpactBleedFrac = 0.35f;
    private const float SurfaceOffset = 0.12f;  // outward nudge along the hit normal so a flame sits just proud of the surface, not clipped into it
    private const float LosProbeOffset = 0.6f;  // a bigger nudge used ONLY for the camera-visibility test, so testing from a point almost touching the collider doesn't self-occlude against its own building
    private const float MinFlammability = 0.6f; // "hesitating in some areas"
    private const float MaxFlammability = 1.5f; // "suddenly accelerating in others"
    private const int FlammabilitySalt = 5000;  // offset clear of MaybeSpawnEmber's own RandomFloat01(i) salt range, purely to keep the two uses visibly independent in code, not a correctness requirement

    public void Init(float height, float footprintRadius, int targetCount)
    {
        _height = height;
        _footprintRadius = footprintRadius;
        var area = BurnableSurfaceArea(height, footprintRadius);
        var areaBasedCap = Mathf.RoundToInt(area / AreaPerFirePoint);
        _baseMaxIgnitedCells = Mathf.Clamp(Mathf.Max(Mathf.Clamp(targetCount, 1, 4), areaBasedCap), 1, MaxFireCountCeiling);
        _maxIgnitedCells = _baseMaxIgnitedCells;
        _sizeScale = Mathf.Clamp(1f + area / SizeScaleReferenceArea, 1f, MaxSizeScale);
        _baseAngle = CameraFacingAngle();

        _cells = new FireCell[GridAngleSegments * GridHeightBands];
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i].Ventilation = BaselineVentilation;
            // deterministic per-cell hash off THIS instance's own
            // GetInstanceID() (see RandomFloat01) -- reproducible within
            // one ignition (debugging), freshly different across separate
            // ignitions of "the same" building (a new FireCluster
            // GameObject gets a new instance ID every time), same
            // property the rest of this file's own jitter already relies
            // on (see FirePlume's persistent per-instance seed).
            _cells[i].Flammability = Mathf.Lerp(MinFlammability, MaxFlammability, RandomFloat01(FlammabilitySalt + i));
        }
        _heatDelta = new float[_cells.Length];

        // "Fire always begins at ... weapon impact locations": the origin
        // cell is the visible arc's own dead-centre, low/window band --
        // exactly where the very first flame has always landed under the
        // prior "starts with 1" contract, so a building never sits
        // Damaged with zero fire showing.
        _originCellIndex = CellIndex(GridAngleSegments / 2, 0);
        _cells[_originCellIndex].Heat = InitialImpactHeat;
        IgniteCell(_originCellIndex);
        _simTimer = SimTickInterval;
    }

    /// <summary>The angle (this class's own `Mathf.Cos(angle)*x,
    /// Mathf.Sin(angle)*z` convention -- standard math angle from +X,
    /// matching `IgniteCell`'s own offset formula below) from this
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

    private static int CellIndex(int angleIndex, int bandIndex)
    {
        return angleIndex * GridHeightBands + bandIndex;
    }

    private void Update()
    {
        if (_cells == null) return;
        _simTimer -= Time.deltaTime;
        if (_simTimer > 0f) return;
        _simTimer = CurrentSimTickInterval();
        TickFireNetwork();
    }

    /// <summary>2026-08 (creator direction: "increase the speed of the
    /// spread based on number of attack points and amount of time before
    /// building is destroyed. shorter time more spawns"): the base 0.5s
    /// tick shrinks as either `_hitRateEma` (more simultaneous attackers
    /// land hits closer together in time -- see `RegisterHit`) or
    /// `_urgency` (this building's own HP fraction falling toward zero)
    /// climbs, clamped at `MinSimTickInterval` so neither can push this
    /// into a per-frame cost. Faster ticking alone already means more
    /// spawns over the same real time, since `_maxIgnitedCells` (also
    /// raised by urgency, see `RegisterHit`) simply gets reached sooner.
    ///
    /// 2026-08 follow-up (creator direction: "give me an inspector
    /// setting for spawn rate of fires"): `DamageFxProfile.
    /// FireSpawnRateMultiplier` folds into the SAME `speedup` factor the
    /// automatic urgency/hit-rate math already computes -- a flat
    /// creator-facing override layered on top of (not replacing) that
    /// per-building automatic behavior, read fresh every tick so an
    /// Inspector drag affects an already-burning building immediately,
    /// same "live in Play mode" contract every other `DamageFxProfile`
    /// field already has.</summary>
    private float CurrentSimTickInterval()
    {
        var speedup = 1f + Mathf.Min(_hitRateEma, MaxHitRateForSpeedup) * HitRateSpeedupFactor + _urgency * UrgencySpeedupFactor;
        speedup *= Mathf.Max(0.01f, DamageFxProfile.Active.FireSpawnRateMultiplier);
        return Mathf.Max(MinSimTickInterval, SimTickInterval / speedup);
    }

    /// <summary>One simulation step: diffuse heat from every hot cell to
    /// its neighbours (buffered into `_heatDelta` first so a cell can't
    /// spread heat it only just received THIS tick -- an explicit-Euler
    /// step, not a cascading one), apply the buffered deltas, then ignite
    /// any cell that just crossed threshold (bounded by `_maxIgnitedCells`)
    /// or grow an already-ignited cell's flame off its own rising heat.
    /// Deliberately non-conservative (heat radiates outward without
    /// cooling its source) -- a real thermodynamic sim isn't the goal,
    /// a believable, monotonically-growing fire read is, matching this
    /// file's own established "never removes a fire once lit" policy for
    /// every other FX class here.</summary>
    private void TickFireNetwork()
    {
        System.Array.Clear(_heatDelta, 0, _heatDelta.Length);
        for (var a = 0; a < GridAngleSegments; a++)
        {
            for (var b = 0; b < GridHeightBands; b++)
            {
                var from = CellIndex(a, b);
                if (_cells[from].Heat <= 0f) continue;
                SpreadHeat(from, a - 1, b, SidewaysBias);
                SpreadHeat(from, a + 1, b, SidewaysBias);
                SpreadHeat(from, a, b + 1, UpwardBias);
                SpreadHeat(from, a, b - 1, DownwardBias);
            }
        }

        for (var i = 0; i < _cells.Length; i++)
        {
            if (_heatDelta[i] <= 0f) continue;
            _cells[i].Heat += _heatDelta[i];
            if (!_cells[i].Ignited)
            {
                if (_cells[i].Heat >= IgnitionThreshold && _ignitedCount < _maxIgnitedCells)
                    IgniteCell(i);
            }
            else if (_cells[i].Plume != null)
            {
                var heatScale = Mathf.Clamp(1f + _cells[i].Heat / HeatForMaxScale, 1f, HeatScaleCap);
                _cells[i].Plume.SetHeatScale(heatScale);
            }
        }

        _tickCounter++;
        MaybeSpawnEmber();
    }

    private void SpreadHeat(int fromIndex, int toAngle, int toBand, float bias)
    {
        if (toAngle < 0 || toAngle >= GridAngleSegments) return; // no wrap -- the grid IS the visible arc, nothing propagates past its edge
        if (toBand < 0 || toBand >= GridHeightBands) return;
        var toIndex = CellIndex(toAngle, toBand);
        // Flammability (see FireCell's own doc comment): a fixed per-cell
        // multiplier alongside Ventilation -- some cells inherently catch
        // a little faster or slower, which is what turns an otherwise
        // perfectly even radiating diffusion into "irregular islands...
        // hesitating in some areas, suddenly accelerating in others."
        var amount = _cells[fromIndex].Heat * BaseDiffusionRate * bias * _cells[toIndex].Ventilation * _cells[toIndex].Flammability;
        _heatDelta[toIndex] += amount;
    }

    /// <summary>Spawns this cell's real, visible `FirePlume` -- the ONLY
    /// place in this class that ever creates one. Placement now comes
    /// from `PickSurfacePoint` (a real raycast against the building's own
    /// collider) instead of a fixed standoff distance -- see that
    /// method's own doc comment for why.</summary>
    private void IgniteCell(int index)
    {
        _cells[index].Ignited = true;
        _ignitedCount++;
        _cells[index].Ventilation = VentilationOnIgnite; // "ventilation dramatically increases spread" -- a broken-out cell is now an opening
        BleedVentilationToNeighbors(index);

        var a = index / GridHeightBands;
        var b = index % GridHeightBands;
        var heightFrac = GridHeightFracs[b];
        var worldPoint = PickSurfacePoint(GridAngleOffsets[a], heightFrac);

        var go = new GameObject("FirePlume");
        go.transform.SetParent(transform, false);
        go.transform.position = worldPoint;
        var plume = go.AddComponent<FirePlume>();
        plume.Init(_sizeScale);
        _cells[index].Plume = plume;
    }

    private void BleedVentilationToNeighbors(int index)
    {
        var a = index / GridHeightBands;
        var b = index % GridHeightBands;
        BleedVentilationOne(a - 1, b);
        BleedVentilationOne(a + 1, b);
        BleedVentilationOne(a, b - 1);
        BleedVentilationOne(a, b + 1);
    }

    private void BleedVentilationOne(int a, int b)
    {
        if (a < 0 || a >= GridAngleSegments || b < 0 || b >= GridHeightBands) return;
        var idx = CellIndex(a, b);
        _cells[idx].Ventilation = Mathf.Min(_cells[idx].Ventilation + VentilationNeighborBleed, VentilationOnIgnite - 0.1f);
    }

    /// <summary>2026-08 (creator direction: "winder spread of spawn
    /// points not just clustered in one spot or a burn line... Cheat
    /// [skew] probability of flames on surfaces facing the camera closest
    /// to the camera... increase the speed of the spread based on number
    /// of attack points and amount of time before building is destroyed.
    /// shorter time more spawns"): called once per landed hit from
    /// `RuntimeCityBuilder.ApplyBuildingDamage`, with that hit's own
    /// damage amount standing in for "weapon energy" and `hpFraction01`
    /// (`CurrentHp / MaxHp`, already available at that call site) telling
    /// this cluster how close to destruction the building now is.
    ///
    /// Earlier this fed every hit into ONE fixed origin cell -- combined
    /// with the strong upward bias, that produced a single vertical "burn
    /// line" instead of a spread facade. `PickWeightedColumn` now spreads
    /// hits across several angle columns, weighted toward (never
    /// exclusively) the camera-facing centre -- see `AngleColumnWeights`.
    /// A fraction of each hit's heat still bleeds to its own column's
    /// immediate neighbours (mostly upward), so "higher-energy impacts
    /// ignite multiple adjacent cells immediately" still holds per-hit,
    /// just from a varying starting column instead of always the same one.
    ///
    /// `_hitRateEma` (a smoothed hits-per-second read off real `Time.time`
    /// gaps between calls) is this method's own proxy for "number of
    /// attack points" -- there is no real attacker-identity tracking in
    /// this pipeline, but more simultaneous attackers necessarily means
    /// hits land closer together in time, which this can observe directly
    /// without needing to know who's hitting. `_urgency` rises as
    /// `hpFraction01` falls. Both feed `CurrentSimTickInterval` (ticks
    /// faster) and raise `_maxIgnitedCells` above its area-based floor
    /// (`_baseMaxIgnitedCells`) here, capped at `MaxFireCountCeiling`
    /// either way.
    ///
    /// 2026-08 follow-up (creator direction: "randomize if projectile
    /// will cause a fire. Goal make the fire pattern look organic, not
    /// procedural"): hit-rate/urgency tracking above runs unconditionally
    /// for every real hit (that's genuine attack pressure), but whether
    /// THIS hit's own heat actually reaches the network is gated behind
    /// `DamageFxProfile.Active.FireIgnitionChancePerHit` -- a hit that
    /// fails the roll deals its damage and is fully counted for pacing
    /// purposes, it just doesn't visibly stoke the fire this time.</summary>
    public void RegisterHit(float energy, float hpFraction01)
    {
        if (_cells == null || energy <= 0f) return;
        _hitCount++;

        var now = Time.time;
        if (_lastHitTime > 0f)
        {
            var dt = Mathf.Max(0.05f, now - _lastHitTime);
            _hitRateEma = Mathf.Lerp(_hitRateEma, 1f / dt, HitRateSmoothing);
        }
        _lastHitTime = now;

        _urgency = Mathf.Clamp01(1f - Mathf.Clamp01(hpFraction01));
        _maxIgnitedCells = Mathf.Min(MaxFireCountCeiling, _baseMaxIgnitedCells + Mathf.RoundToInt(_urgency * MaxUrgencyBonusCells));

        // 2026-08 (creator direction: "randomize if projectile will cause
        // a fire. Goal make the fire pattern look organic, not
        // procedural"): hit-rate/urgency above still see EVERY real hit
        // -- that's actual attack pressure, not this hit's own visible
        // fire consequence -- but whether THIS hit's own energy actually
        // reaches the heat network is now a per-hit roll. Skipping some
        // hits (rather than feeding heat every single time, in lockstep
        // with the damage numbers) is what keeps the resulting pattern
        // from reading as a mechanical, obviously-simulated response to
        // combat.
        if (RandomFloat01(_hitCount * 733) > DamageFxProfile.Active.FireIgnitionChancePerHit) return;

        var heat = energy * ImpactHeatPerDamage;
        var column = PickWeightedColumn(_hitCount);
        var cellIndex = CellIndex(column, 0); // low/window band -- same band the very first flame always starts in
        _cells[cellIndex].Heat += heat;
        var bleed = heat * NeighborImpactBleedFrac;
        AddHeatIfValid(column - 1, 0, bleed);
        AddHeatIfValid(column + 1, 0, bleed);
        AddHeatIfValid(column, 1, bleed * UpwardBias);
    }

    private int PickWeightedColumn(int salt)
    {
        var roll = ((GetInstanceID() + salt * 733) & 0xFFFF) % AngleColumnWeightSum;
        var cum = 0;
        for (var a = 0; a < GridAngleSegments; a++)
        {
            cum += AngleColumnWeights[a];
            if (roll < cum) return a;
        }
        return GridAngleSegments / 2;
    }

    private void AddHeatIfValid(int a, int b, float amount)
    {
        if (a < 0 || a >= GridAngleSegments || b < 0 || b >= GridHeightBands) return;
        _cells[CellIndex(a, b)].Heat += amount;
    }

    /// <summary>"Large exterior flames occasionally eject burning embers
    /// ... these should be rare but dramatic": a small per-tick chance
    /// for an already-hot, already-ignited cell to instantly push ONE
    /// grid-adjacent unignited neighbour to ignition, bypassing the
    /// normal gradual diffusion climb -- "allow some crawling." Capped at
    /// one ember per tick (a cascade would stop reading as "rare") and,
    /// since a neighbour is by definition only a cell-width away, it can
    /// never manifest as fire "racing across an intact wall." Chance
    /// scales with `_urgency` (see `RegisterHit`) so crawling picks up
    /// along with everything else as a building nears destruction.</summary>
    private void MaybeSpawnEmber()
    {
        if (_ignitedCount >= _maxIgnitedCells) return;
        var chance = EmberChancePerTick * (1f + _urgency);
        for (var i = 0; i < _cells.Length; i++)
        {
            if (!_cells[i].Ignited || _cells[i].Heat < EmberHeatFloor) continue;
            if (RandomFloat01(i) > chance) continue;
            var a = i / GridHeightBands;
            var b = i % GridHeightBands;
            var target = PickUnignitedNeighbor(a, b);
            if (target < 0) continue;
            _cells[target].Heat = Mathf.Max(_cells[target].Heat, IgnitionThreshold);
            IgniteCell(target);
            return;
        }
    }

    private int PickUnignitedNeighbor(int a, int b)
    {
        int found;
        if (TryUnignited(a - 1, b, out found)) return found;
        if (TryUnignited(a + 1, b, out found)) return found;
        if (TryUnignited(a, b + 1, out found)) return found;
        if (TryUnignited(a, b - 1, out found)) return found;
        return -1;
    }

    private bool TryUnignited(int a, int b, out int index)
    {
        index = -1;
        if (a < 0 || a >= GridAngleSegments || b < 0 || b >= GridHeightBands) return false;
        var idx = CellIndex(a, b);
        if (_cells[idx].Ignited) return false;
        index = idx;
        return true;
    }

    private float RandomFloat01(int salt)
    {
        return ((GetInstanceID() + (_tickCounter * 977) + salt * 733) & 0xFFFF) / 65536f;
    }

    /// <summary>2026-08 follow-up (creator report: "fires is sometimes in
    /// the tile but not on the building. BURNING is ONLY allowed ON the
    /// building surfaces including roof decoration and other features"):
    /// replaces the old `PickClearAngle`, which placed every point at a
    /// single FIXED radial distance (`footprintRadius * 1.6`) regardless
    /// of angle -- a crude approximation that could fall short of a real
    /// wall (still "in the tile," floating in open air short of any
    /// geometry) or overshoot well past it, depending on where a
    /// non-circular footprint's true face/corner actually sits relative
    /// to that one fixed number.
    ///
    /// This casts a real `Physics.Raycast` INWARD from well outside the
    /// footprint (`_footprintRadius * 3`) at the candidate angle/height,
    /// and uses the ACTUAL hit point (nudged out along the hit normal by
    /// `SurfaceOffset` so the flame sits just proud of the surface rather
    /// than clipped into it) -- pinned to real geometry for every angle
    /// and every footprint shape, not a generic standoff distance. Only
    /// the building's own massing cube carries a collider (dressing --
    /// windows, cornices, water towers, "roof decoration and other
    /// features" -- is visual-only, no collider of its own), so a hit
    /// always lands on that shared silhouette; since dressing is built
    /// directly onto/around that same box, a point pinned to its surface
    /// still reads as on the building, near its decoration, not floating
    /// apart from either.
    ///
    /// Same candidate-search shape as the old method (`primaryJitterDeg`
    /// first, then progressively wider offsets, all within the SAME +-35
    /// degree visible arc) -- but a candidate is now rejected for EITHER
    /// a missed raycast (no real surface there) OR an occluded camera
    /// line of sight (tested from a point pushed further out,
    /// `LosProbeOffset`, than the flame's own placement, so the test
    /// doesn't self-occlude against the very surface it's standing on).
    /// Falls back to the OLD fixed-distance approximation only if every
    /// candidate fails both tests -- this still never prevents a fire
    /// point from spawning, it only risks (rarely) not sitting exactly on
    /// real geometry that one time.</summary>
    private static readonly int[] AngleSearchOffsets = { 0, 14, -14, 28, -28, 35, -35 };

    private Vector3 PickSurfacePoint(int primaryJitterDeg, float heightFrac)
    {
        var probeDist = _footprintRadius * 3f;
        var targetY = _height * heightFrac;
        var cam = Camera.main;
        for (var i = 0; i < AngleSearchOffsets.Length; i++)
        {
            var jitter = Mathf.Clamp(primaryJitterDeg + AngleSearchOffsets[i], -35, 35);
            var candidateAngle = _baseAngle + jitter * Mathf.Deg2Rad;
            var dirIn = new Vector3(-Mathf.Cos(candidateAngle), 0f, -Mathf.Sin(candidateAngle));
            var rayOrigin = transform.position + new Vector3(Mathf.Cos(candidateAngle) * probeDist, targetY, Mathf.Sin(candidateAngle) * probeDist);
            RaycastHit hit;
            if (!Physics.Raycast(rayOrigin, dirIn, out hit, probeDist)) continue;
            if (cam != null)
            {
                var losProbe = hit.point + hit.normal * LosProbeOffset;
                if (Physics.Linecast(losProbe, cam.transform.position)) continue;
            }
            return hit.point + hit.normal * SurfaceOffset;
        }

        var fallbackAngle = _baseAngle + primaryJitterDeg * Mathf.Deg2Rad;
        var fallbackDist = _footprintRadius * 1.6f;
        return transform.position + new Vector3(Mathf.Cos(fallbackAngle) * fallbackDist, targetY, Mathf.Sin(fallbackAngle) * fallbackDist);
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

    // 2026-08 (creator direction: "randomize independently: flame
    // height, width"): defaults to Vector3.one -- every EXISTING puff
    // kind (smoke/dust/water/muzzle) never calls SetScaleAxisMultiplier,
    // so `Update`'s scale line below is byte-for-byte unchanged for all
    // of them. Only `FirePlume.SpawnPuff` sets this, to a plume's own
    // persistent (X=width, Y=height, Z=width) jitter, so height and
    // width can differ from each other -- `Update`'s own uniform `scale`
    // float still drives the OVERALL size/growth-over-life exactly as
    // before, this just un-does the "always a perfect cube" constraint
    // on top of it.
    private Vector3 _scaleAxisMultiplier = Vector3.one;

    public void SetScaleAxisMultiplier(Vector3 multiplier)
    {
        _scaleAxisMultiplier = multiplier;
    }

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
        transform.localScale = new Vector3(scale * _scaleAxisMultiplier.x, scale * _scaleAxisMultiplier.y, scale * _scaleAxisMultiplier.z);
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
