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

    /// <summary>2026-08 (creator direction: "figure out how to verify
    /// fire is being seen"): a big (4-unit), fully opaque, bright
    /// emissive sphere -- deliberately NOTHING like the real low-poly
    /// flame shard, so there's no ambiguity about whether it's visible.
    /// If this can't be spotted either, the problem isn't fire's own
    /// size/color/transparency at all. Self-destructs after 20s so it
    /// doesn't linger as permanent clutter once its job (answering "is it
    /// even at the position the log claims") is done. Called from
    /// FireCluster's own first spawn point, gated behind
    /// DamageFxProfile.ShowFireDebugMarkers.</summary>
    public static void SpawnDebugMarker(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "FireDebugMarker";
        go.transform.position = at;
        go.transform.localScale = Vector3.one * 4f;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var magenta = new Color(1f, 0f, 1f);
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = magenta;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", magenta * 3f);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        Object.Destroy(go, 20f);
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
    /// FIRE" history behind it.</summary>
    private void SpawnPuff()
    {
        var go = new GameObject("FirePuff");
        go.transform.SetParent(transform, false);
        var id = go.GetInstanceID();
        go.transform.position = transform.position + new Vector3(((id & 3) - 1.5f) * 0.1f, 0f, (((id >> 2) & 3) - 1.5f) * 0.1f);
        var fireResizePct = DamageFxProfile.Active.FireResizePct;
        // this initial scale only holds for one frame -- SmokePuff.Update
        // (below) overwrites it uniformly every frame off _baseScale, the
        // same "explicit spawn scale is cosmetically moot" precedent
        // SmokePlume/DustBurstFx's own spawn-time scale already sets.
        var size = 0.28f * fireResizePct;
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
        go.AddComponent<SmokePuff>().InitFlame(mat, 1.5f + ((id >> 6) & 3) * 0.24f, 0.25f * fireResizePct, 0.9f, 0.32f * fireResizePct);
    }
}

/// <summary>2026-08 (creator direction: "it should start with 1 but
/// then others popup in different places based on the building size up
/// to 8"): owns a growing set of <see cref="FirePlume"/> points
/// scattered across a Damaged building's own footprint. The FIRST point
/// lands the instant `Init` runs (so a building never sits Damaged with
/// zero fire showing, matching "should start with 1"); every later
/// point staggers in on its own randomized 2-5s timer at a NEW random
/// spot, until `targetCount` is reached, then this component goes
/// idle -- it never removes a fire once lit (matching every other FX
/// class in this file: no repair mechanic exists, so nothing here needs
/// to reverse itself either).</summary>
public class FireCluster : MonoBehaviour
{
    private float _height;
    private float _footprintRadius;
    private int _targetCount;
    private int _spawned;
    private float _nextSpawnIn;

    public void Init(float height, float footprintRadius, int targetCount)
    {
        _height = height;
        _footprintRadius = footprintRadius;
        _targetCount = Mathf.Clamp(targetCount, 1, 8);
        SpawnOne();
        _nextSpawnIn = NextInterval();
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
        var angle = ((salt & 0xFFFF) % 360) * Mathf.Deg2Rad;
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
        var dist = _footprintRadius;
        var offset = new Vector3(Mathf.Cos(angle) * dist, _height * 1.0f, Mathf.Sin(angle) * dist);

        var go = new GameObject("FirePlume");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        go.AddComponent<FirePlume>();

        // 2026-08 (creator report, after confirming the Console log line
        // DOES appear: "is it too small? Or wrong colours? or Too
        // transparent?"): the log proves AttachFireCluster runs and
        // computes a sane position -- it does NOT prove anything at that
        // position is actually visible on screen, which is exactly the
        // open question. Rather than guess at size/color/alpha again (six
        // rounds of that already), drop an UNMISSABLE marker at the
        // FIRST point's exact world position: a big, bright, fully-lit
        // (no transparency, no low-poly subtlety) primitive sphere,
        // nothing like the real flame shard. If the creator can't spot
        // THIS either, the bug is categorically not about fire's own
        // size/shape/color -- it's something else entirely (camera
        // culling mask, a Scene-vs-Game view mixup, etc.) and this rules
        // that whole size-tuning direction out in one look. If they CAN
        // see the marker but not the real flame next to it, that
        // conclusively confirms it IS a size/color/transparency problem
        // with the flame specifically. Gated behind
        // DamageFxProfile.ShowFireDebugMarkers (default true while this
        // is still being diagnosed) so it's a one-flip Inspector toggle
        // to turn off once resolved, not a permanent fixture.
        if (DamageFxProfile.Active.ShowFireDebugMarkers && _spawned == 1)
        {
            DamageFx.SpawnDebugMarker(go.transform.position);
        }
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
