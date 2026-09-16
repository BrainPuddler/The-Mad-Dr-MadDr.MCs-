using UnityEngine;

/// <summary>
/// docs/40 §3 item 2 follow-up (creator direction 2026-09-16: "clumps
/// of heavier mist floating slowly through viewport"). A small pool of
/// large, soft, slow-drifting fog clumps -- <see
/// cref="ProceduralMeshKit.CloudShard"/>, the same lumpy-blob mesh
/// `DamageFx.SmokePuff` already uses for smoke, reused here at a much
/// larger scale and near-zero animation speed (mist drifts, it doesn't
/// billow/rise like smoke). Gated by <see cref="WeatherController
/// .Wetness"/> the same way <see cref="RainSystem"/> is -- heavier
/// wetness means more, thicker mist, near-invisible while dry, same
/// "conditional, not always-on" lesson docs/28 rows 14/15 already
/// taught this whole weather system.
///
/// Individual GameObjects, not GPU-instanced -- the pool here (≤12) is
/// small enough that a per-instance `MaterialPropertyBlock` (docs/39
/// §7's own sanctioned exception for "a bounded set of renderers") is
/// the right tool for each clump's own independent fade, unlike
/// `RainSystem`'s hundreds of streaks, which specifically must NOT use
/// MaterialPropertyBlock per that same section's SRP-batching rule.
/// </summary>
public class MistSystem : MonoBehaviour
{
    private const int MaxClumps = 12;
    private const int MeshVariants = 3;
    // docs/39 §1.1's own visible-ground estimate, same margin logic
    // RainSystem's own SpawnHalfExtent uses -- generous enough to always
    // cover the frame across every band this still renders in.
    private const float SpawnHalfExtent = 130f;
    private const float RecenterMargin = 40f;
    private const float MinHeight = 1.5f, MaxHeight = 5f;
    private const float MinScale = 10f, MaxScale = 22f;
    private const float MinSpeed = 0.4f, MaxSpeed = 1.1f;   // real drifting fog is slow -- this is NOT wind-driven smoke
    private const float FadeSeconds = 3f;

    private struct Clump
    {
        public Transform T;
        public MeshRenderer Renderer;
        public Vector3 Velocity;
        public float BaseAlpha;
        public float Age;
    }

    private Clump[] _clumps;
    private Mesh[] _meshVariants;
    private Material _sharedMat;
    private MaterialPropertyBlock _block;

    private void Awake()
    {
        _meshVariants = new Mesh[MeshVariants];
        for (var i = 0; i < MeshVariants; i++) _meshVariants[i] = ProceduralMeshKit.CloudShard(7, i * 3.71f + 0.4f);
        _sharedMat = BuildMistMaterial();
        _block = new MaterialPropertyBlock();

        _clumps = new Clump[MaxClumps];
        var center = GroundFocusPoint();
        for (var i = 0; i < MaxClumps; i++) _clumps[i] = SpawnClump(center, i);
    }

    private Clump SpawnClump(Vector3 center, int index)
    {
        var go = new GameObject("MistClump");
        go.transform.SetParent(transform, false);
        var pos = center + new Vector3(
            Random.Range(-SpawnHalfExtent, SpawnHalfExtent), Random.Range(MinHeight, MaxHeight),
            Random.Range(-SpawnHalfExtent, SpawnHalfExtent));
        var scale = Random.Range(MinScale, MaxScale);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale * 0.4f, scale);

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = _meshVariants[index % MeshVariants];
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _sharedMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var angle = Random.Range(0f, Mathf.PI * 2f);
        var speed = Random.Range(MinSpeed, MaxSpeed);
        var velocity = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * speed;

        return new Clump
        {
            T = go.transform,
            Renderer = renderer,
            Velocity = velocity,
            BaseAlpha = Random.Range(0.12f, 0.22f),
            Age = Random.Range(0f, FadeSeconds),   // stagger initial fade-in across the pool
        };
    }

    private void Update()
    {
        // docs/39 §9: "No effect renders in the Map band" -- same rule
        // RainSystem's own gate applies, reused verbatim.
        if (AnimationLodBudget.CurrentBand == AnimationLodBudget.Band.Map)
        {
            for (var i = 0; i < _clumps.Length; i++) _clumps[i].Renderer.enabled = false;
            return;
        }

        var wetness = WeatherController.Wetness;
        var center = GroundFocusPoint();
        var dt = Time.deltaTime;

        for (var i = 0; i < _clumps.Length; i++)
        {
            var c = _clumps[i];
            c.Age += dt;
            c.T.position += c.Velocity * dt;

            // Recenter if drifted too far from the camera's own ground
            // focus -- same "always near the player, never obviously
            // teleporting" trick RainSystem's own respawn uses, on a
            // wider margin since a mist clump is far bigger/slower than
            // a rain streak, so an abrupt jump would be far more visible.
            var delta = c.T.position - center;
            var flatDist = new Vector2(delta.x, delta.z).magnitude;
            if (flatDist > SpawnHalfExtent + RecenterMargin)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                c.T.position = center + new Vector3(
                    Mathf.Sin(angle) * SpawnHalfExtent * 0.6f, Random.Range(MinHeight, MaxHeight),
                    Mathf.Cos(angle) * SpawnHalfExtent * 0.6f);
                c.Age = 0f;
            }

            var fadeIn = Mathf.Clamp01(c.Age / FadeSeconds);
            var alpha = c.BaseAlpha * fadeIn * wetness;
            c.Renderer.enabled = alpha > 0.002f;
            if (c.Renderer.enabled)
            {
                _block.SetColor("_BaseColor", new Color(0.82f, 0.84f, 0.86f, alpha));
                c.Renderer.SetPropertyBlock(_block);
            }

            _clumps[i] = c;
        }
    }

    /// <summary>Same ray-plane-against-y=0 technique as `RainSystem`'s
    /// own copy -- kept as an independent copy rather than a shared
    /// helper since both files are meant to stay self-contained VFX
    /// systems (matches this project's existing tolerance for a few
    /// lines of duplication between sibling dresser/VFX files over a
    /// forced shared utility).</summary>
    private static Vector3 GroundFocusPoint()
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;
        var origin = cam.transform.position;
        var dir = cam.transform.forward;
        if (Mathf.Abs(dir.y) < 0.0001f) return origin;
        var t = -origin.y / dir.y;
        return origin + dir * Mathf.Max(0f, t);
    }

    private static Material BuildMistMaterial()
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.82f, 0.84f, 0.86f, 0.18f);
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return mat;
    }
}
