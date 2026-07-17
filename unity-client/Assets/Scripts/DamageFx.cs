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

    /// <summary>One-shot dust puff burst at a collapsing building's site.</summary>
    public static void DustBurst(Vector3 at, Transform parent)
    {
        var go = new GameObject("DustBurst");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        go.AddComponent<DustBurstFx>();
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
