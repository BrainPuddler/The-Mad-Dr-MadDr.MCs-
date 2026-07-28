using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/23 §2 Phase 2's "Unity: BaseDresser.cs" -- the visual half of
/// every <see cref="SimBuilding"/> match-core already simulates (sim-side
/// placement/cost/lifecycle shipped with Phase 2's own match-core slice;
/// nothing in Unity ever rendered it until now, the same "sim ready,
/// display missing" gap <see cref="LumenHud"/> closed for the emitter/
/// mana HUD). One GameObject per live building, primitive-kit dressing
/// (<see cref="RuntimeCityBuilder.SpawnPrim"/>, shared cached materials --
/// <see cref="BuildingDresser"/>'s own conventions for the CITY
/// GENERATOR's buildings, reused here for PLAYER-built ones), synced
/// every frame against <see cref="SimBridge.BuildingCount"/>/<see
/// cref="SimBridge.BuildingAt"/> since match-core's own building list
/// only ever grows (destroyed buildings stay in it, state flips instead)
/// -- this is the one place that list is walked and turned into/out of
/// existence as GameObjects.
///
/// Renders docs/23 §2's own lifecycle: UnderConstruction scales up from
/// a small translucent "scaffold" toward full size as
/// <see cref="SimBuilding.TicksUntilComplete"/> counts down against its
/// <see cref="BuildingDef.BuildTimeTicks"/>; Complete swaps to a solid,
/// per-<see cref="BuildingKind"/>-hued material; <see cref="SimBuilding.
/// IsDamaged"/> darkens that same material (docs/18 §3's "Damaged" visual
/// state, derived from HP, never its own persisted state); Destroyed
/// despawns the GameObject outright -- no rubble/wreck FX yet (a
/// reasonable follow-up reusing the existing DamageFx rubble system, not
/// attempted here).
///
/// Deliberately does NOT skin the HQ per-faction (docs/23 §2's own
/// "HQ dressing per faction" phrase) -- it renders with the same generic
/// per-kind hue as every other building, just scaled up for its Landmark
/// tier. Per-faction skinning needs the kind of named-archetype variety
/// BuildingDresser already does for the CITY generator's own landmarks;
/// building that same richness for player-constructed HQs is real,
/// separate scope, not attempted here.
/// </summary>
public class BaseDresser : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;

    private static readonly Dictionary<BuildingKind, Material> SolidMats = new Dictionary<BuildingKind, Material>();
    private static readonly Dictionary<BuildingKind, Material> DamagedMats = new Dictionary<BuildingKind, Material>();
    private static Material _scaffoldMat;

    private readonly Dictionary<uint, GameObject> _visuals = new Dictionary<uint, GameObject>();

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder)
    {
        bridge = simBridge;
        builder = cityBuilder;
    }

    private void Update()
    {
        if (bridge == null || !bridge.HasMatch || builder == null) return;

        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);

            if (b.State == BuildingState.Destroyed)
            {
                if (_visuals.TryGetValue(b.EntityId, out var destroyedGo))
                {
                    Object.Destroy(destroyedGo);
                    _visuals.Remove(b.EntityId);
                }
                continue;
            }

            if (!_visuals.TryGetValue(b.EntityId, out var go))
            {
                go = builder.SpawnPrim(PrimitiveType.Cube, Vector3.zero, Vector3.one, ScaffoldMat(), transform);
                go.name = "Building_" + b.Kind + "_" + b.EntityId;
                _visuals[b.EntityId] = go;
            }

            ApplyVisual(go, b);
        }
    }

    private void ApplyVisual(GameObject go, SimBuilding b)
    {
        var def = BuildingDef.Get(b.Kind);
        var hexWorld = builder.WorldOf(b.Hex);
        var groundY = builder.GroundHeightAt(hexWorld);
        var fullScale = FullScaleFor(def);

        var rend = go.GetComponent<Renderer>();

        if (b.State == BuildingState.UnderConstruction)
        {
            var progress = def.BuildTimeTicks > 0 ? 1f - (float)b.TicksUntilComplete / def.BuildTimeTicks : 1f;
            progress = Mathf.Clamp01(progress);
            var scale = Vector3.Lerp(fullScale * 0.15f, fullScale, progress);
            go.transform.localScale = scale;
            go.transform.position = new Vector3(hexWorld.x, groundY + scale.y * 0.5f, hexWorld.z);
            if (rend != null) rend.sharedMaterial = ScaffoldMat();
            return;
        }

        // Complete
        go.transform.localScale = fullScale;
        go.transform.position = new Vector3(hexWorld.x, groundY + fullScale.y * 0.5f, hexWorld.z);
        if (rend != null) rend.sharedMaterial = b.IsDamaged ? DamagedMat(b.Kind) : SolidMat(b.Kind);
    }

    /// <summary>docs/18 §3 tiers reused as a crude scale proxy (real
    /// per-kind silhouettes are BaseDresser's own future art pass, not
    /// this v1's job) -- Landmark-tier (the HQ) reads visibly larger than
    /// the Small/Medium storage-and-utility roster.</summary>
    private static Vector3 FullScaleFor(BuildingDef def)
    {
        if (def.MaxHp >= 3000) return new Vector3(18f, 14f, 18f);   // Landmark (Hq)
        if (def.MaxHp >= 1500) return new Vector3(15f, 10f, 15f);   // Large
        if (def.MaxHp >= 600) return new Vector3(13f, 7f, 13f);     // Medium
        return new Vector3(11f, 5f, 11f);                            // Small
    }

    private static Material ScaffoldMat()
    {
        if (_scaffoldMat == null)
        {
            _scaffoldMat = new Material(ShaderUtil.FindRenderableShader());
            _scaffoldMat.color = new Color(0.75f, 0.68f, 0.5f, 0.55f);
            LabMeshBuilder.MakeTransparent(_scaffoldMat);
        }
        return _scaffoldMat;
    }

    private static Material SolidMat(BuildingKind kind)
    {
        if (SolidMats.TryGetValue(kind, out var mat) && mat != null) return mat;
        var hue = (float)((int)kind % 8) / 8f;
        var rgb = Color.HSVToRGB(hue, 0.45f, 0.75f);
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = rgb;
        SolidMats[kind] = mat;
        return mat;
    }

    private static Material DamagedMat(BuildingKind kind)
    {
        if (DamagedMats.TryGetValue(kind, out var mat) && mat != null) return mat;
        var hue = (float)((int)kind % 8) / 8f;
        var rgb = Color.HSVToRGB(hue, 0.6f, 0.32f);   // darker, more saturated -- scorched read
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = rgb;
        DamagedMats[kind] = mat;
        return mat;
    }
}
