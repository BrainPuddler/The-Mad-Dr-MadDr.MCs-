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
