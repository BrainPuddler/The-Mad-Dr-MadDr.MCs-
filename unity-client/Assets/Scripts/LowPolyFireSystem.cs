using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standalone low-poly EXTERIOR fire system. Wholly independent of
/// DamageFx's smoke/fire (SmokePlume, SmokePuff, FireCluster, FirePlume,
/// DamageFxProfile) -- no shared types, materials, meshes, constants, or
/// method calls in either direction anywhere in this file, LowPolyFireMeshKit.cs,
/// or LowPolyFireProfile.cs. Smoke may later read <see cref="FireInfo"/>
/// to react to a fire; this system never calls into smoke.
///
/// Entry point for callers: the static <see cref="LowPolyFire"/> class
/// below. Everything else is the manager singleton
/// (<see cref="LowPolyFireManager"/>) that owns every live fire in one
/// place and draws them all via GPU instancing (Graphics.DrawMeshInstanced)
/// instead of a GameObject per flame tongue, so dozens-to-hundreds of
/// fires stay cheap. Update cadence follows the brief: animation every
/// frame, growth/fuel/spread on a slower fixed-rate tick
/// (<see cref="LowPolyFireManager.GrowthTickSeconds"/>), so per-frame work
/// stays limited to transform math and draw calls.
/// </summary>
public static class LowPolyFire
{
    /// <summary>Ignite a new fire against a specific building/world
    /// collider, near `nearPoint` (an approximate world position -- the
    /// exact surface point and normal are recovered from the collider
    /// itself, not trusted from the caller). Returns the new fire's id, or
    /// -1 if the collider was null or the pool is full.</summary>
    public static int Ignite(Collider surfaceCollider, Vector3 nearPoint, int seed = 0)
    {
        return LowPolyFireManager.Instance.IgniteOnCollider(surfaceCollider, nearPoint, seed);
    }

    /// <summary>Ignite directly at an already-known surface point/normal
    /// (e.g. a caller that already raycast for its own reasons). The
    /// normal is trusted as-is -- no further surface query is performed.</summary>
    public static int IgniteAt(Vector3 surfacePoint, Vector3 surfaceNormal, int seed = 0)
    {
        return LowPolyFireManager.Instance.SpawnAt(surfacePoint, surfaceNormal, seed, startEnergy: 0f);
    }

    public static bool TryGetInfo(int fireId, out FireInfo info) => LowPolyFireManager.Instance.TryGetInfo(fireId, out info);

    /// <summary>Appends every currently-live fire's read-only snapshot
    /// into `results` (not cleared first, so a caller can batch several
    /// sources). This is the ONLY data surface another system (smoke,
    /// later) is meant to consume -- fire never pushes data out or calls
    /// another system directly.</summary>
    public static void GetActiveFires(List<FireInfo> results) => LowPolyFireManager.Instance.CollectActive(results);

    public static void Extinguish(int fireId) => LowPolyFireManager.Instance.RequestExtinguish(fireId);

    /// <summary>Optional global wind. `directionXZ` need not be normalized;
    /// `strength01` is clamped to 0..1. Wind only ever leans flames within
    /// LowPolyFireProfile.MaxWindLeanDegrees -- upward buoyancy always
    /// stays dominant, per the creator brief ("wind should never
    /// completely flatten flames").</summary>
    public static void SetWind(Vector2 directionXZ, float strength01) => LowPolyFireManager.Instance.SetWind(directionXZ, strength01);
}

/// <summary>Read-only snapshot of one live fire. This struct is the entire
/// public surface another system is allowed to read fire state through.</summary>
public readonly struct FireInfo
{
    public readonly int Id;
    public readonly Vector3 Position;
    public readonly Vector3 SurfaceNormal;
    public readonly float Radius;
    public readonly float Intensity;   // 0..1, current energy
    public readonly float HeatOutput;  // 0..1, energy weighted by remaining fuel richness
    public readonly int GrowthStage;   // 1..5

    public FireInfo(int id, Vector3 position, Vector3 surfaceNormal, float radius, float intensity, float heatOutput, int growthStage)
    {
        Id = id;
        Position = position;
        SurfaceNormal = surfaceNormal;
        Radius = radius;
        Intensity = intensity;
        HeatOutput = heatOutput;
        GrowthStage = growthStage;
    }
}

/// <summary>Owns every live fire and ember; the only MonoBehaviour in this
/// system. Internal -- other code should go through the static
/// <see cref="LowPolyFire"/> API, not this class directly.</summary>
internal class LowPolyFireManager : MonoBehaviour
{
    private static LowPolyFireManager _instance;

    public static LowPolyFireManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("LowPolyFireManager");
                _instance = go.AddComponent<LowPolyFireManager>();
            }
            return _instance;
        }
    }

    private const float GrowthTickSeconds = 0.2f; // 5 Hz, per the creator brief

    private struct GrowthTier
    {
        public float Height;
        public float Width;
        public int TongueCount;
        public float AnimSpeed;
        public float LightIntensity;
        public float EmberRatePerSecond;
    }

    // Stage 1 tiny flicker -> Stage 5 large roaring flame. Interpolated
    // smoothly between anchors (see Evaluate) rather than snapping.
    private static readonly GrowthTier[] Tiers =
    {
        new GrowthTier { Height = 0.18f, Width = 0.14f, TongueCount = 1, AnimSpeed = 3.5f, LightIntensity = 0f,    EmberRatePerSecond = 0f },
        new GrowthTier { Height = 0.55f, Width = 0.28f, TongueCount = 1, AnimSpeed = 5.0f, LightIntensity = 0.5f,  EmberRatePerSecond = 0f },
        new GrowthTier { Height = 1.05f, Width = 0.50f, TongueCount = 3, AnimSpeed = 6.5f, LightIntensity = 1.1f, EmberRatePerSecond = 0.6f },
        new GrowthTier { Height = 1.85f, Width = 0.75f, TongueCount = 5, AnimSpeed = 8.0f, LightIntensity = 1.9f, EmberRatePerSecond = 1.6f },
        new GrowthTier { Height = 2.80f, Width = 1.05f, TongueCount = 6, AnimSpeed = 9.5f, LightIntensity = 2.8f, EmberRatePerSecond = 3.2f },
    };

    private static GrowthTier Evaluate(float energy01)
    {
        var t = Mathf.Clamp01(energy01) * (Tiers.Length - 1);
        var i0 = Mathf.Clamp(Mathf.FloorToInt(t), 0, Tiers.Length - 2);
        var frac = t - i0;
        var ease = frac * frac * (3f - 2f * frac); // smoothstep -- organic, not a hard stage snap
        var a = Tiers[i0];
        var b = Tiers[i0 + 1];
        return new GrowthTier
        {
            Height = Mathf.Lerp(a.Height, b.Height, ease),
            Width = Mathf.Lerp(a.Width, b.Width, ease),
            TongueCount = Mathf.RoundToInt(Mathf.Lerp(a.TongueCount, b.TongueCount, ease)),
            AnimSpeed = Mathf.Lerp(a.AnimSpeed, b.AnimSpeed, ease),
            LightIntensity = Mathf.Lerp(a.LightIntensity, b.LightIntensity, ease),
            EmberRatePerSecond = Mathf.Lerp(a.EmberRatePerSecond, b.EmberRatePerSecond, ease),
        };
    }

    private struct Emitter
    {
        public bool Active;
        public int Id;
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 GrowthAxis; // world-up projected onto the surface's tangent plane, blended slightly toward Normal
        public float Energy;       // 0..1
        public float GrowthDuration;
        public float Fuel;
        public float MoistureResistance; // 0..1
        public float Age;
        public int Seed;
        public int LightIndex; // -1 if none assigned
        public int ChildCount;
        public float NextSpreadCheckIn;
        public bool Extinguishing;
    }

    private struct Ember
    {
        public bool Active;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Life;
        public int Seed;
    }

    private Emitter[] _emitters;
    private readonly List<int> _activeIndices = new List<int>();
    private readonly List<int> _freeList = new List<int>();
    private readonly Dictionary<int, int> _idToIndex = new Dictionary<int, int>();
    private int _nextId = 1;
    private int _seedCounter = 1;

    private Ember[] _embers;
    private readonly List<int> _activeEmberIndices = new List<int>();
    private readonly List<int> _freeEmberList = new List<int>();

    private Mesh[] _meshVariants;
    private Material[] _materials; // 0 base orange, 1 hot yellow, 2 white-hot, 3 blue core (intense only)
    private const int ColorBucketCount = 4;

    private List<Matrix4x4>[,] _bucketMatrices; // [meshVariant, colorBucket]
    private List<Matrix4x4> _emberMatrices;
    private readonly Matrix4x4[] _drawScratch = new Matrix4x4[1023];

    private Light[] _lightPool;
    private readonly List<int> _freeLights = new List<int>();
    private readonly List<int> _lightCandidates = new List<int>();
    private readonly HashSet<int> _keepAssignedLights = new HashSet<int>();
    private Comparison<int> _lightEnergyComparer;

    private readonly Collider[] _overlapBuf = new Collider[12];

    private Vector2 _windDirection;
    private float _windStrength;

    private float _time;
    private float _growthAccumulator;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;

        var profile = LowPolyFireProfile.Active;
        _emitters = new Emitter[Mathf.Max(1, profile.MaxActiveFires)];
        for (var i = _emitters.Length - 1; i >= 0; i--) _freeList.Add(i);

        _embers = new Ember[Mathf.Max(0, profile.MaxActiveEmbers)];
        for (var i = _embers.Length - 1; i >= 0; i--) _freeEmberList.Add(i);

        BuildMeshVariants();
        BuildMaterials();
        _bucketMatrices = new List<Matrix4x4>[_meshVariants.Length, ColorBucketCount];
        for (var m = 0; m < _meshVariants.Length; m++)
            for (var c = 0; c < ColorBucketCount; c++)
                _bucketMatrices[m, c] = new List<Matrix4x4>();
        _emberMatrices = new List<Matrix4x4>();

        BuildLightPool(profile.MaxLights);
        _lightEnergyComparer = (a, b) => _emitters[b].Energy.CompareTo(_emitters[a].Energy);
    }

    private void BuildMeshVariants()
    {
        const int variantCount = 6;
        _meshVariants = new Mesh[variantCount];
        for (var i = 0; i < variantCount; i++)
            _meshVariants[i] = LowPolyFireMeshKit.BuildTongueMesh(5, (i + 1) * 0.173f);
    }

    private void BuildMaterials()
    {
        _materials = new[]
        {
            MakeFireMaterial(new Color(0.95f, 0.55f, 0.12f), new Color(0.9f, 0.35f, 0.05f) * 1.6f),
            MakeFireMaterial(new Color(0.99f, 0.82f, 0.25f), new Color(1f, 0.55f, 0.08f) * 2.4f),
            MakeFireMaterial(new Color(1f, 0.92f, 0.55f), new Color(1f, 0.7f, 0.15f) * 3.2f),
            MakeFireMaterial(new Color(0.55f, 0.75f, 1f), new Color(0.35f, 0.55f, 1f) * 3.5f),
        };
    }

    private static Material MakeFireMaterial(Color baseColor, Color emission)
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = baseColor;
        mat.enableInstancing = true;
        // flat-shaded stylized look, not shiny plastic
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        // deliberately left opaque (default _Surface = 0) -- no alpha
        // blending, no transparency-sort risk, per the creator brief.
        return mat;
    }

    private void BuildLightPool(int count)
    {
        _lightPool = new Light[Mathf.Max(0, count)];
        for (var i = 0; i < _lightPool.Length; i++)
        {
            var go = new GameObject("LowPolyFireLight");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.15f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            _lightPool[i] = light;
            _freeLights.Add(i);
        }
    }

    // ---- public-facing entry points (called via the LowPolyFire static class) ----

    public int IgniteOnCollider(Collider surfaceCollider, Vector3 nearPoint, int seed)
    {
        if (surfaceCollider == null) return -1;
        if (!TrySnapToSurface(surfaceCollider, nearPoint, out var point, out var normal)) return -1;
        return SpawnAt(point, normal, seed, startEnergy: 0f);
    }

    public bool TryGetInfo(int fireId, out FireInfo info)
    {
        if (_idToIndex.TryGetValue(fireId, out var idx) && _emitters[idx].Active)
        {
            info = ToFireInfo(idx);
            return true;
        }
        info = default;
        return false;
    }

    public void CollectActive(List<FireInfo> results)
    {
        for (var i = 0; i < _activeIndices.Count; i++)
        {
            var idx = _activeIndices[i];
            if (_emitters[idx].Active) results.Add(ToFireInfo(idx));
        }
    }

    public void RequestExtinguish(int fireId)
    {
        if (_idToIndex.TryGetValue(fireId, out var idx) && _emitters[idx].Active)
            _emitters[idx].Extinguishing = true;
    }

    public void SetWind(Vector2 directionXZ, float strength01)
    {
        _windDirection = directionXZ.sqrMagnitude > 0.0001f ? directionXZ.normalized : Vector2.zero;
        _windStrength = Mathf.Clamp01(strength01);
    }

    private FireInfo ToFireInfo(int idx)
    {
        ref var e = ref _emitters[idx];
        var tier = Evaluate(e.Energy);
        var stage = Mathf.Clamp(1 + Mathf.FloorToInt(e.Energy * 4.999f), 1, 5);
        var fuelRichness = Mathf.Clamp01(e.Fuel / Mathf.Max(0.01f, LowPolyFireProfile.Active.BaseFuel));
        return new FireInfo(e.Id, e.Position, e.Normal, tier.Width, e.Energy, e.Energy * (0.5f + 0.5f * fuelRichness), stage);
    }

    // ---- surface detection ----

    /// <summary>Recovers the true exterior surface point and outward
    /// normal nearest `nearPoint` on `collider`, instead of trusting a
    /// caller-guessed point directly -- the creator brief's "detect the
    /// exterior mesh surface" step. Works for any collider type via
    /// ClosestPoint (accurate for the convex primitives -- BoxCollider on
    /// the procedural city's building cubes -- this project's buildings
    /// actually use) plus a Collider.Raycast probe from just outside the
    /// collider's own bounds back toward that point to recover a real
    /// surface normal (ClosestPoint alone gives no normal).</summary>
    private static bool TrySnapToSurface(Collider collider, Vector3 nearPoint, out Vector3 point, out Vector3 normal)
    {
        var closest = collider.ClosestPoint(nearPoint);
        var bounds = collider.bounds;
        var dir = closest - bounds.center;
        if (dir.sqrMagnitude < 0.0001f) dir = nearPoint - bounds.center;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
        dir.Normalize();

        var reach = bounds.extents.magnitude + 1f;
        var rayOrigin = bounds.center - dir * reach;
        if (collider.Raycast(new Ray(rayOrigin, dir), out var hit, reach * 2f + 1f))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = closest;
        normal = dir;
        return true;
    }

    /// <summary>Searches for a spread/satellite-fire landing point along
    /// the CURRENT fire's own surface tangent plane (not a random world
    /// offset) -- climbs a little more than it spreads sideways, biased
    /// downwind if wind is set. `Physics.OverlapSphereNonAlloc` finds
    /// whatever nearby colliders exist (the same building the parent fire
    /// is on, or an adjacent one around a corner); each candidate is
    /// raycast independently so a genuinely nearer/more relevant surface
    /// wins, and a miss (nothing solid out there) correctly yields no
    /// spread rather than a floating fire.</summary>
    private bool TryFindNearbySurfacePoint(in Emitter e, int rngSeed, out Vector3 point, out Vector3 normal)
    {
        var profile = LowPolyFireProfile.Active;
        var tangent = Vector3.Cross(e.Normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(e.Normal, Vector3.right);
        tangent.Normalize();
        var bitangent = Vector3.Cross(e.Normal, tangent).normalized;

        var lateral = (LowPolyFireMeshKit.Hash01(rngSeed, 3) * 2f - 1f) * profile.CrawlRadiusMax;
        var vertical = (0.3f + LowPolyFireMeshKit.Hash01(rngSeed, 5) * 0.5f) * profile.CrawlRadiusMax; // climbing bias
        if (_windStrength > 0.001f)
        {
            var windTangent = Vector3.Dot(new Vector3(_windDirection.x, 0f, _windDirection.y), tangent);
            lateral += windTangent * _windStrength * profile.CrawlRadiusMax * 0.6f;
        }

        var candidate = e.Position + tangent * lateral + bitangent * vertical;
        var searchRadius = profile.CrawlRadiusMax + 1f;
        var probeOrigin = candidate + e.Normal * searchRadius;
        var probeDir = -e.Normal;

        var count = Physics.OverlapSphereNonAlloc(candidate, searchRadius, _overlapBuf);
        var found = false;
        var bestDist = float.MaxValue;
        point = default;
        normal = e.Normal;
        for (var i = 0; i < count; i++)
        {
            var col = _overlapBuf[i];
            if (col == null) continue;
            if (col.Raycast(new Ray(probeOrigin, probeDir), out var hit, searchRadius * 2f + 1f))
            {
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    point = hit.point;
                    normal = hit.normal;
                    found = true;
                }
            }
        }
        return found;
    }

    // ---- lifecycle ----

    public int SpawnAt(Vector3 surfacePoint, Vector3 surfaceNormal, int seed, float startEnergy)
    {
        if (_freeList.Count == 0) return -1; // pool full -- ignitions/spread beyond capacity are silently skipped

        var idx = _freeList[_freeList.Count - 1];
        _freeList.RemoveAt(_freeList.Count - 1);

        var profile = LowPolyFireProfile.Active;
        var s = seed != 0 ? seed : unchecked(_seedCounter++ * 1000003 + Mathf.RoundToInt(_time * 1000f));

        var normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        var up = Vector3.up;
        var projectedUp = up - normal * Vector3.Dot(up, normal);
        var growthAxis = projectedUp.sqrMagnitude > 0.0001f
            ? Vector3.Slerp(projectedUp.normalized, normal, 0.18f).normalized
            : normal;

        var e = new Emitter
        {
            Active = true,
            Id = _nextId++,
            Position = surfacePoint + normal * profile.SurfaceOffset,
            Normal = normal,
            GrowthAxis = growthAxis,
            Energy = Mathf.Clamp01(startEnergy),
            GrowthDuration = profile.BaseGrowthDurationSeconds * (0.75f + LowPolyFireMeshKit.Hash01(s, 1) * 0.5f),
            MoistureResistance = LowPolyFireMeshKit.Hash01(s, 2) * 0.35f,
            Fuel = profile.BaseFuel * (0.7f + LowPolyFireMeshKit.Hash01(s, 4) * 0.6f),
            Age = 0f,
            Seed = s,
            LightIndex = -1,
            ChildCount = 0,
            NextSpreadCheckIn = profile.SpreadCheckInterval * (0.6f + LowPolyFireMeshKit.Hash01(s, 6) * 0.8f),
            Extinguishing = false,
        };
        _emitters[idx] = e;
        _idToIndex[e.Id] = idx;
        _activeIndices.Add(idx);
        return e.Id;
    }

    private void FreeEmitter(int idx)
    {
        _idToIndex.Remove(_emitters[idx].Id);
        if (_emitters[idx].LightIndex >= 0) ReleaseLight(idx);
        _emitters[idx].Active = false;
    }

    private void CompactActiveList()
    {
        for (var i = _activeIndices.Count - 1; i >= 0; i--)
        {
            var idx = _activeIndices[i];
            if (!_emitters[idx].Active)
            {
                var last = _activeIndices.Count - 1;
                _activeIndices[i] = _activeIndices[last];
                _activeIndices.RemoveAt(last);
                _freeList.Add(idx);
            }
        }
    }

    // ---- ticks ----

    private void Update()
    {
        _time = Time.time;
        var dt = Time.deltaTime;

        _growthAccumulator += dt;
        while (_growthAccumulator >= GrowthTickSeconds)
        {
            _growthAccumulator -= GrowthTickSeconds;
            GrowthTick(GrowthTickSeconds);
            SpreadAndReassignLights(GrowthTickSeconds);
            CompactActiveList();
        }

        AnimateAndDraw(dt);
    }

    private void GrowthTick(float dt)
    {
        var profile = LowPolyFireProfile.Active;
        var n0 = _activeIndices.Count; // snapshot -- children spawned this tick join next tick
        for (var i = 0; i < n0; i++)
        {
            var idx = _activeIndices[i];
            var e = _emitters[idx];
            if (!e.Active) continue;

            if (!e.Extinguishing)
            {
                var wobble = 1f + (Mathf.PerlinNoise(e.Seed * 0.017f, _time * 0.25f) - 0.5f) * 0.4f;
                var growthRate = 1f / Mathf.Max(1f, e.GrowthDuration);
                e.Energy = Mathf.Clamp01(e.Energy + growthRate * wobble * dt);

                var burn = profile.BurnRatePerSecond * (0.25f + e.Energy) * (1f - e.MoistureResistance * 0.5f);
                e.Fuel -= burn * dt;
                if (e.Fuel <= 0f) e.Extinguishing = true;
            }
            else
            {
                e.Energy = Mathf.MoveTowards(e.Energy, 0f, dt / Mathf.Max(0.5f, profile.ExtinguishSeconds));
            }

            e.Age += dt;
            _emitters[idx] = e;

            if (e.Extinguishing && e.Energy <= 0.01f && e.Age > 1f)
            {
                FreeEmitter(idx);
            }
        }

        UpdateEmbers(dt);
    }

    private void SpreadAndReassignLights(float dt)
    {
        var profile = LowPolyFireProfile.Active;
        var n0 = _activeIndices.Count;
        for (var i = 0; i < n0; i++)
        {
            var idx = _activeIndices[i];
            var e = _emitters[idx];
            if (!e.Active || e.Extinguishing) continue;

            e.NextSpreadCheckIn -= dt;
            if (e.NextSpreadCheckIn <= 0f)
            {
                e.NextSpreadCheckIn = profile.SpreadCheckInterval * (0.7f + LowPolyFireMeshKit.Hash01(e.Seed, Mathf.FloorToInt(e.Age)) * 0.6f);
                _emitters[idx] = e;

                if (e.Energy >= profile.SpreadEnergyThreshold && e.ChildCount < profile.MaxChildrenPerFire && _freeList.Count > 0)
                {
                    var rollSeed = unchecked(e.Seed * 486187739 + Mathf.RoundToInt(e.Age * 1000f));
                    if (LowPolyFireMeshKit.Hash01(rollSeed, 9) < profile.SpreadChancePerCheck)
                    {
                        if (TryFindNearbySurfacePoint(e, rollSeed, out var point, out var normal))
                        {
                            var childId = SpawnAt(point, normal, seed: 0, startEnergy: e.Energy * profile.SpawnEnergyFraction);
                            if (childId >= 0)
                            {
                                e.ChildCount++;
                                _emitters[idx] = e;
                            }
                        }
                    }
                }
            }
            else
            {
                _emitters[idx] = e;
            }
        }

        ReassignLights(profile);
    }

    private void ReassignLights(LowPolyFireProfile profile)
    {
        var cam = Camera.main;
        var maxDistSqr = profile.MaxLightDistance * profile.MaxLightDistance;

        _lightCandidates.Clear();
        for (var i = 0; i < _activeIndices.Count; i++)
        {
            var idx = _activeIndices[i];
            if (!_emitters[idx].Active) continue;
            var inRange = cam == null || (_emitters[idx].Position - cam.transform.position).sqrMagnitude <= maxDistSqr;
            if (!inRange)
            {
                if (_emitters[idx].LightIndex >= 0) ReleaseLight(idx);
                continue;
            }
            _lightCandidates.Add(idx);
        }
        _lightCandidates.Sort(_lightEnergyComparer);

        _keepAssignedLights.Clear();
        var take = Mathf.Min(profile.MaxLights, _lightCandidates.Count);
        for (var i = 0; i < take; i++) _keepAssignedLights.Add(_lightCandidates[i]);

        for (var i = 0; i < _activeIndices.Count; i++)
        {
            var idx = _activeIndices[i];
            if (_emitters[idx].LightIndex >= 0 && !_keepAssignedLights.Contains(idx)) ReleaseLight(idx);
        }
        foreach (var idx in _keepAssignedLights)
        {
            if (_emitters[idx].LightIndex < 0) AssignLight(idx);
        }
    }

    private void AssignLight(int idx)
    {
        if (_freeLights.Count == 0) return;
        var lightIdx = _freeLights[_freeLights.Count - 1];
        _freeLights.RemoveAt(_freeLights.Count - 1);
        var e = _emitters[idx];
        e.LightIndex = lightIdx;
        _emitters[idx] = e;
        _lightPool[lightIdx].enabled = true;
        _lightPool[lightIdx].intensity = 0f; // fades in via the smoothing in AnimateAndDraw
    }

    private void ReleaseLight(int idx)
    {
        var lightIdx = _emitters[idx].LightIndex;
        if (lightIdx < 0) return;
        _lightPool[lightIdx].enabled = false;
        _freeLights.Add(lightIdx);
        var e = _emitters[idx];
        e.LightIndex = -1;
        _emitters[idx] = e;
    }

    // ---- embers ----

    private void SpawnEmber(Vector3 position, Vector3 normal, int seed)
    {
        if (_freeEmberList.Count == 0) return;
        var idx = _freeEmberList[_freeEmberList.Count - 1];
        _freeEmberList.RemoveAt(_freeEmberList.Count - 1);

        var upSpeed = 1.2f + LowPolyFireMeshKit.Hash01(seed, 21) * 1.6f;
        var side = new Vector3((LowPolyFireMeshKit.Hash01(seed, 22) - 0.5f) * 0.8f, 0f, (LowPolyFireMeshKit.Hash01(seed, 23) - 0.5f) * 0.8f);
        _embers[idx] = new Ember
        {
            Active = true,
            Position = position + normal * 0.05f,
            Velocity = normal * 0.3f + Vector3.up * upSpeed + side,
            Age = 0f,
            Life = 1.2f + LowPolyFireMeshKit.Hash01(seed, 24) * 1.4f,
            Seed = seed,
        };
        _activeEmberIndices.Add(idx);
    }

    private void UpdateEmbers(float dt)
    {
        var n0 = _activeEmberIndices.Count;
        for (var i = n0 - 1; i >= 0; i--)
        {
            var idx = _activeEmberIndices[i];
            var em = _embers[idx];
            if (!em.Active) continue;
            em.Age += dt;
            if (em.Age >= em.Life)
            {
                em.Active = false;
                _embers[idx] = em;
                var last = _activeEmberIndices.Count - 1;
                _activeEmberIndices[i] = _activeEmberIndices[last];
                _activeEmberIndices.RemoveAt(last);
                _freeEmberList.Add(idx);
                continue;
            }
            _embers[idx] = em;
        }

        // spawn new embers proportional to each growth-eligible fire's tier rate
        var na = _activeIndices.Count;
        for (var i = 0; i < na; i++)
        {
            var idx = _activeIndices[i];
            var e = _emitters[idx];
            if (!e.Active || e.Extinguishing) continue;
            var tier = Evaluate(e.Energy);
            if (tier.EmberRatePerSecond <= 0f) continue;
            var chance = tier.EmberRatePerSecond * dt;
            var seed = unchecked(e.Seed * 37 + Mathf.RoundToInt(e.Age * 1000f));
            if (LowPolyFireMeshKit.Hash01(seed, 31) < chance)
                SpawnEmber(e.Position, e.Normal, seed);
        }
    }

    // ---- animation + instanced draw ----

    private void AnimateAndDraw(float dt)
    {
        for (var m = 0; m < _meshVariants.Length; m++)
            for (var c = 0; c < ColorBucketCount; c++)
                _bucketMatrices[m, c].Clear();
        _emberMatrices.Clear();

        var profile = LowPolyFireProfile.Active;
        var na = _activeIndices.Count;
        for (var i = 0; i < na; i++)
        {
            var idx = _activeIndices[i];
            var e = _emitters[idx];
            if (!e.Active) continue;

            var tier = Evaluate(e.Energy);
            var tangent = Vector3.Cross(e.Normal, e.GrowthAxis);
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(e.Normal, Vector3.right);
            tangent.Normalize();
            var bitangent = Vector3.Cross(e.Normal, tangent).normalized;

            var colorBucket = e.Energy > 0.85f ? 2 : (e.Energy > 0.4f ? 1 : 0);

            var tongueCount = Mathf.Clamp(tier.TongueCount, 1, _meshVariants.Length);
            for (var k = 0; k < tongueCount; k++)
            {
                var seedK = unchecked(e.Seed + k * 7919);
                var offsetAngle = LowPolyFireMeshKit.Hash01(seedK, 11) * Mathf.PI * 2f;
                var offsetDist = tier.Width * 0.35f * LowPolyFireMeshKit.Hash01(seedK, 22) * (tongueCount > 1 ? 1f : 0f);
                var tongueBase = e.Position
                    + tangent * (Mathf.Cos(offsetAngle) * offsetDist)
                    + bitangent * (Mathf.Sin(offsetAngle) * offsetDist * 0.4f);

                var phase = seedK * 0.31f;
                var animT = _time * tier.AnimSpeed + phase;
                var stretch = 1f + 0.12f * Mathf.Sin(animT * 1.7f) + 0.06f * Mathf.Sin(animT * 3.1f + 1f);
                var wobbleDeg = (Mathf.Sin(animT) * 0.6f + Mathf.Sin(animT * 2.3f + phase) * 0.4f) * 14f;
                var turbSeed = seedK + Mathf.FloorToInt(_time * 4f); // steps a few times/sec, not every frame
                var turbDeg = (LowPolyFireMeshKit.Hash01(turbSeed, 41) - 0.5f) * 10f;

                var windLeanDeg = 0f;
                if (_windStrength > 0.001f)
                {
                    var windAlong = Vector3.Dot(new Vector3(_windDirection.x, 0f, _windDirection.y), tangent);
                    windLeanDeg = Mathf.Clamp(windAlong * _windStrength * profile.MaxWindLeanDegrees, -profile.MaxWindLeanDegrees, profile.MaxWindLeanDegrees);
                }

                var rotation = Quaternion.AngleAxis(windLeanDeg + wobbleDeg + turbDeg, tangent) * Quaternion.FromToRotation(Vector3.up, e.GrowthAxis);
                var scale = new Vector3(tier.Width, Mathf.Max(0.01f, tier.Height * stretch), tier.Width);
                var matrix = Matrix4x4.TRS(tongueBase, rotation, scale);

                var meshVariant = (int)((uint)seedK % (uint)_meshVariants.Length);
                _bucketMatrices[meshVariant, colorBucket].Add(matrix);

                if (e.Energy > 0.9f)
                {
                    var coreScale = scale * 0.5f;
                    _bucketMatrices[meshVariant, 3].Add(Matrix4x4.TRS(tongueBase, rotation, coreScale));
                }
            }

            if (e.LightIndex >= 0)
            {
                var light = _lightPool[e.LightIndex];
                light.transform.position = e.Position + e.Normal * 0.3f;
                var flicker = 0.75f + Mathf.Abs(Mathf.Sin(_time * 9f + e.Seed) * 0.6f + Mathf.Sin(_time * 21f + e.Seed) * 0.4f) * 0.5f;
                var target = tier.LightIntensity * flicker;
                light.intensity = Mathf.Lerp(light.intensity, target, dt * 5f);
                light.range = Mathf.Lerp(1.5f, 5f, e.Energy);
            }
        }

        for (var i = 0; i < _activeEmberIndices.Count; i++)
        {
            var idx = _activeEmberIndices[i];
            var em = _embers[idx];
            if (!em.Active) continue;
            em.Velocity += Vector3.up * (0.6f * dt);
            em.Position += em.Velocity * dt;
            _embers[idx] = em;

            var t = Mathf.Clamp01(em.Age / em.Life);
            var s = Mathf.Lerp(0.045f, 0.01f, t);
            _emberMatrices.Add(Matrix4x4.TRS(em.Position, Quaternion.identity, Vector3.one * s));
        }

        for (var m = 0; m < _meshVariants.Length; m++)
            for (var c = 0; c < ColorBucketCount; c++)
                FlushBucket(_meshVariants[m], _materials[c], _bucketMatrices[m, c]);

        if (_emberMatrices.Count > 0)
            FlushBucket(_meshVariants[0], _materials[2], _emberMatrices);
    }

    private void FlushBucket(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        var count = matrices.Count;
        if (count == 0) return;
        var i = 0;
        while (i < count)
        {
            var n = Mathf.Min(1023, count - i);
            for (var k = 0; k < n; k++) _drawScratch[k] = matrices[i + k];
            Graphics.DrawMeshInstanced(mesh, 0, material, _drawScratch, n, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            i += n;
        }
    }
}
