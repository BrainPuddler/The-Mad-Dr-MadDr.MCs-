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
    /// that transform ever moves (it doesn't today, but costs nothing).</summary>
    public static void AttachSmoke(Transform holder, float height)
    {
        var go = new GameObject("SmokePlume");
        go.transform.SetParent(holder, false);
        go.transform.position = holder.position + Vector3.up * (height * 0.9f);
        go.AddComponent<SmokePlume>();
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
    /// spot `AttachFire` always used.</summary>
    public static void AttachFireCluster(Transform holder, float height, float footprintRadius, int targetCount)
    {
        var go = new GameObject("FireCluster");
        go.transform.SetParent(holder, false);
        go.transform.position = holder.position;
        go.AddComponent<FireCluster>().Init(height, footprintRadius, targetCount);
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

    private void SpawnPuff()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SmokePuff";
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position;
        go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.35f, 0.34f, 0.32f, 0.75f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        go.AddComponent<SmokePuff>().Init(mat);
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
        _glow.range = 6f;
        _glow.intensity = 2.5f;
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
        _glow.intensity = 2.2f * flicker;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 0.12f + (GetInstanceID() & 3) * 0.03f;
        SpawnPuff();
    }

    private void SpawnPuff()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "FirePuff";
        go.transform.SetParent(transform, false);
        var id = go.GetInstanceID();
        go.transform.position = transform.position + new Vector3(((id & 3) - 1.5f) * 0.15f, 0f, (((id >> 2) & 3) - 1.5f) * 0.15f);
        go.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        var warm = ((id >> 4) & 3) == 0;
        mat.color = warm ? new Color(0.95f, 0.55f, 0.12f, 0.9f) : new Color(0.98f, 0.78f, 0.2f, 0.9f);
        LabMeshBuilder.MakeTransparent(mat);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", (warm ? new Color(0.95f, 0.35f, 0.05f) : new Color(1f, 0.65f, 0.1f)) * 2.5f);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        go.AddComponent<SmokePuff>().InitFlame(mat, 0.5f + ((id >> 6) & 3) * 0.08f, 0.6f, 0.9f);
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
        // first point (spawned == 1) stays near center -- a Small
        // building's own single fire shouldn't land at the footprint's
        // own edge; later points spread further out, up to the radius.
        var dist = _spawned == 1 ? 0f : _footprintRadius * (0.25f + ((salt >> 8) & 15) / 15f * 0.65f);
        var offset = new Vector3(Mathf.Cos(angle) * dist, _height * 0.25f, Mathf.Sin(angle) * dist);

        var go = new GameObject("FirePlume");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        go.AddComponent<FirePlume>();
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

    // 2026-08 (creator direction: "fire like movement"): zero for every
    // existing puff kind (smoke/dust/water jet all keep their original
    // dead-straight drift, unchanged), nonzero only via InitFlame below
    // -- a sideways sway ON TOP of the usual upward drift, so a flame
    // puff licks side to side as it rises instead of traveling a
    // straight line the way every other puff in this file always has.
    private float _swayAmp;
    private float _swayFreq;
    private float _swayPhase;

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

    /// <summary>Same shape as <see cref="InitBurst"/> (a fire puff is
    /// still a short-lived rising burst, not a lazy plume) but with a
    /// real side-to-side sway layered on top instead of a straight
    /// drift -- the actual "licking flame" motion.</summary>
    public void InitFlame(Material mat, float life, float growth, float baseAlpha)
    {
        InitBurst(mat, life, growth, baseAlpha);
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
        var scale = 0.8f + t * _growth;
        transform.localScale = new Vector3(scale, scale, scale);
        if (_mat != null)
        {
            var c = _mat.color;
            _mat.color = new Color(c.r, c.g, c.b, _baseAlpha * (1f - t));
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
