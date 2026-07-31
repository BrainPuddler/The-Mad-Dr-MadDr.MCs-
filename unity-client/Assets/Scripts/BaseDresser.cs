using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/23 §2 Phase 2's "Unity: BaseDresser.cs" -- the visual half of
/// every <see cref="SimBuilding"/> match-core already simulates (sim-side
/// placement/cost/lifecycle shipped with Phase 2's own match-core slice;
/// nothing in Unity ever rendered it until now, the same "sim ready,
/// display missing" gap <see cref="LumenHud"/> closed for the emitter/
/// mana HUD). One root GameObject per live building, primitive-kit
/// dressing (<see cref="RuntimeCityBuilder.SpawnPrim"/>), synced every
/// frame against <see cref="SimBridge.BuildingCount"/>/<see
/// cref="SimBridge.BuildingAt"/> since match-core's own building list
/// only ever grows (destroyed buildings stay in it, state flips instead)
/// -- this is the one place that list is walked and turned into/out of
/// existence as GameObjects.
///
/// Renders docs/23 §2's own lifecycle: UnderConstruction scales up a
/// small translucent single-cube "scaffold" toward full size as
/// <see cref="SimBuilding.TicksUntilComplete"/> counts down against its
/// <see cref="BuildingDef.BuildTimeTicks"/> (deliberately kept as one
/// generic shape regardless of kind -- you can't tell what's being
/// built from a construction site, only once it's actually built);
/// Complete swaps to a real per-<see cref="BuildingKind"/> SILHOUETTE
/// (see <see cref="BuildCompleteShape"/>); <see cref="SimBuilding.
/// IsDamaged"/> darkens the same tint (docs/18 §3's "Damaged" visual
/// state, derived from HP, never its own persisted state); Destroyed
/// despawns the GameObject, fires a one-time <see cref="DamageFx.
/// BuildingRubble"/> wreck (scaled off the building's own footprint) and
/// disgorges <see cref="BuildingDef.Occupants"/> fleeing Citizens near the
/// wreck via <see cref="RuntimeCityBuilder.SpawnFleeingOccupant"/> (2026-07
/// creator direction: "when they are destroyed they disgorge their human
/// occupants that flee" -- the first link of the worker-economy epic's
/// causal chain: building destroyed -> occupants flee -> a future
/// Collector unit captures and possesses them into Workers).
///
/// 2026-07 follow-up: real per-kind building art, closing the "every
/// kind is the same cube, just tinted" v1 gap. Per the maddr-aesthetic-
/// preferences skill's own §5 ("shape communicates origin/function,
/// color communicates contents/state" -- don't let one visual property
/// carry two different facts): the ORIGINAL v1 used color to distinguish
/// KIND (an HSV hue keyed off `BuildingKind`), which is exactly the
/// channel conflation that skill flags. Now shape carries kind (seven
/// distinct two-primitive silhouettes below) and color instead carries
/// OWNER (which player built it) + damaged state -- a more useful
/// gameplay signal anyway ("which of these are mine") than "what does
/// storage vs a factory look like," which the silhouette now answers on
/// its own. Owner colors are a deliberate approximation of docs/17's own
/// per-faction palette register (organic/gothic for the Doctor,
/// olive-drab tin-toy-robot for the Human Army) keyed by PLAYER INDEX,
/// not a real `FactionId` lookup -- `SimBridge` doesn't expose a
/// player's faction yet, a real, separate gap flagged rather than
/// silently worked around.
///
/// Still does NOT skin the HQ per-faction beyond that same owner-color
/// approximation (docs/23 §2's own "HQ dressing per faction" phrase) --
/// it gets its own distinct silhouette (a keep + turret, biggest scale)
/// but not faction-specific named-archetype variety the way
/// <see cref="BuildingDresser"/> gives the CITY generator's own
/// landmarks. That richness is real, separate scope, not attempted here.
/// </summary>
public class BaseDresser : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;

    private static Material _scaffoldMat;
    private static readonly Dictionary<int, Material> SolidMatsByOwner = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material> DamagedMatsByOwner = new Dictionary<int, Material>();

    // UnderConstruction: one scaffold GameObject per building, a single
    // scaling cube (see the class header for why shape stays generic here).
    private readonly Dictionary<uint, GameObject> _scaffolds = new Dictionary<uint, GameObject>();
    // Complete/Damaged: one ROOT GameObject per building, holding the
    // real per-kind silhouette as children -- built ONCE (buildings never
    // move once placed), only re-tinted afterward.
    private readonly Dictionary<uint, GameObject> _completed = new Dictionary<uint, GameObject>();

    // Destroyed is fired-once per EntityId (2026-07: rubble FX + occupant
    // disgorge) -- match-core's own building list only grows, so without
    // this a Destroyed building would re-trigger every frame forever.
    private readonly HashSet<uint> _destroyedHandled = new HashSet<uint>();

    // 2026-07 (creator direction: "Building need decent amount of HPs and
    // should show damage and some low-poly fire when being attacked"):
    // Intact -> Damaged fires the smoke+fire attach exactly once per
    // EntityId, same "match-core's building list only grows" reasoning
    // as _destroyedHandled above -- there is no repair mechanic yet, so
    // IsDamaged never regresses back to false for a live building.
    private readonly HashSet<uint> _damagedHandled = new HashSet<uint>();

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

            var def = BuildingDef.Get(b.Kind);
            var hexWorld = builder.WorldOf(b.Hex);
            var groundY = builder.GroundHeightAt(hexWorld);
            var fullScale = FullScaleFor(def);

            if (b.State == BuildingState.Destroyed)
            {
                DestroyIfPresent(_scaffolds, b.EntityId);
                DestroyIfPresent(_completed, b.EntityId);
                if (_destroyedHandled.Add(b.EntityId))
                {
                    var wreckWorld = new Vector3(hexWorld.x, groundY, hexWorld.z);
                    DamageFx.BuildingRubble(wreckWorld, transform, fullScale.x);
                    for (var occ = 0; occ < def.Occupants; occ++)
                        builder.SpawnFleeingOccupant(b.Hex);
                }
                continue;
            }

            if (b.State == BuildingState.UnderConstruction)
            {
                // defensive only -- SimBuilding's own state machine never
                // regresses from Complete back to UnderConstruction, so
                // this should never actually fire.
                DestroyIfPresent(_completed, b.EntityId);
                UpdateScaffold(b, def, fullScale, hexWorld, groundY);
                continue;
            }

            // Complete
            DestroyIfPresent(_scaffolds, b.EntityId);
            if (!_completed.TryGetValue(b.EntityId, out var root))
            {
                root = new GameObject("Building_" + b.Kind + "_" + b.EntityId);
                root.transform.SetParent(transform, false);
                root.transform.position = new Vector3(hexWorld.x, groundY, hexWorld.z);
                BuildCompleteShape(root, b.Kind, fullScale);
                _completed[b.EntityId] = root;
            }
            TintShape(root, b.PlayerIndex, b.IsDamaged);
            if (b.IsDamaged && _damagedHandled.Add(b.EntityId))
            {
                DamageFx.AttachSmoke(root.transform, fullScale.y);
                DamageFx.AttachFire(root.transform, fullScale.y);
            }
        }
    }

    private static void DestroyIfPresent(Dictionary<uint, GameObject> dict, uint entityId)
    {
        if (dict.TryGetValue(entityId, out var go))
        {
            Object.Destroy(go);
            dict.Remove(entityId);
        }
    }

    private void UpdateScaffold(SimBuilding b, BuildingDef def, Vector3 fullScale, Vector3 hexWorld, float groundY)
    {
        if (!_scaffolds.TryGetValue(b.EntityId, out var go))
        {
            go = builder.SpawnPrim(PrimitiveType.Cube, Vector3.zero, Vector3.one, ScaffoldMat(), transform);
            go.name = "Building_" + b.Kind + "_" + b.EntityId + "_Scaffold";
            _scaffolds[b.EntityId] = go;
        }
        var progress = def.BuildTimeTicks > 0 ? 1f - (float)b.TicksUntilComplete / def.BuildTimeTicks : 1f;
        progress = Mathf.Clamp01(progress);
        var scale = Vector3.Lerp(fullScale * 0.15f, fullScale, progress);
        go.transform.localScale = scale;
        go.transform.position = new Vector3(hexWorld.x, groundY + scale.y * 0.5f, hexWorld.z);
    }

    /// <summary>docs/18 §3 tiers reused as a scale proxy, unchanged from
    /// v1 -- Landmark-tier (the HQ) reads visibly larger than the
    /// Small/Medium storage-and-utility roster.</summary>
    private static Vector3 FullScaleFor(BuildingDef def)
    {
        if (def.MaxHp >= 3000) return new Vector3(18f, 14f, 18f);   // Landmark (Hq)
        if (def.MaxHp >= 1500) return new Vector3(15f, 10f, 15f);   // Large
        if (def.MaxHp >= 600) return new Vector3(13f, 7f, 13f);     // Medium
        return new Vector3(11f, 5f, 11f);                            // Small
    }

    /// <summary>2026-07 (GrabCursor's post-clone "lands on the roof" beat):
    /// the world-space height of a building's own roof above its
    /// footprint's ground point -- the SAME tier-height table <see
    /// cref="FullScaleFor"/> already uses for rendering, exposed here so
    /// nothing outside this class has to duplicate (and risk drifting
    /// from) those numbers.</summary>
    public static float RoofHeightFor(BuildingKind kind) => FullScaleFor(BuildingDef.Get(kind)).y;

    // ---- per-kind silhouettes (all children of `root`, which is
    // already positioned at the hex's ground point) ----

    private void BuildCompleteShape(GameObject root, BuildingKind kind, Vector3 fullScale)
    {
        switch (kind)
        {
            case BuildingKind.BloodStorage:
            case BuildingKind.FuelStorage:
                BuildTankShape(root, fullScale);
                break;
            case BuildingKind.FuelPump:
                BuildPumpShape(root, fullScale);
                break;
            case BuildingKind.PartsStorage:
                BuildWarehouseShape(root, fullScale);
                break;
            case BuildingKind.HarvestPost:
                BuildWatchtowerShape(root, fullScale);
                break;
            case BuildingKind.Factory:
                BuildFactoryShape(root, fullScale);
                break;
            case BuildingKind.Defense:
                BuildBunkerShape(root, fullScale);
                break;
            case BuildingKind.Hq:
                BuildHqShape(root, fullScale);
                break;
            default:
                BuildGenericBoxShape(root, fullScale);
                break;
        }
    }

    /// <summary>BloodStorage/FuelStorage -- a real storage vessel: a
    /// cylindrical body plus a domed cap, narrower than the full
    /// footprint so it reads as a tank sitting on a hex, not a building
    /// filling it. Unity's built-in Cylinder is 1 unit diameter x 2
    /// units tall at scale 1 (unlike Cube's 1x1x1) -- localScale.y here
    /// is deliberately halved from the desired world height to account
    /// for that.</summary>
    private void BuildTankShape(GameObject root, Vector3 fullScale)
    {
        var radius = Mathf.Min(fullScale.x, fullScale.z) * 0.28f;
        var bodyHeight = fullScale.y * 0.85f;
        builder.SpawnPrim(PrimitiveType.Cylinder, root.transform.position + Vector3.up * (bodyHeight * 0.5f),
            new Vector3(radius * 2f, bodyHeight * 0.5f, radius * 2f), Placeholder(), root.transform);
        builder.SpawnPrim(PrimitiveType.Sphere, root.transform.position + Vector3.up * bodyHeight,
            new Vector3(radius * 1.7f, radius * 1.7f, radius * 1.7f), Placeholder(), root.transform);
    }

    /// <summary>FuelPump -- a small pump house plus an upright nozzle
    /// pole offset to one side, distinct from the tank's centered,
    /// vessel-shaped read.</summary>
    private void BuildPumpShape(GameObject root, Vector3 fullScale)
    {
        var houseH = fullScale.y * 0.5f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (houseH * 0.5f),
            new Vector3(fullScale.x * 0.55f, houseH, fullScale.z * 0.55f), Placeholder(), root.transform);
        var poleRadius = fullScale.x * 0.06f;
        var poleHeight = fullScale.y * 0.9f;
        builder.SpawnPrim(PrimitiveType.Cylinder,
            root.transform.position + Vector3.right * (fullScale.x * 0.3f) + Vector3.up * (poleHeight * 0.5f),
            new Vector3(poleRadius * 2f, poleHeight * 0.5f, poleRadius * 2f), Placeholder(), root.transform);
    }

    /// <summary>PartsStorage -- a wide low warehouse body plus a smaller
    /// raised roof vent off-center, the "long low industrial shed" read.</summary>
    private void BuildWarehouseShape(GameObject root, Vector3 fullScale)
    {
        var bodyH = fullScale.y * 0.7f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (bodyH * 0.5f),
            new Vector3(fullScale.x * 0.9f, bodyH, fullScale.z * 0.9f), Placeholder(), root.transform);
        var ventH = fullScale.y * 0.35f;
        builder.SpawnPrim(PrimitiveType.Cube,
            root.transform.position + Vector3.right * (fullScale.x * 0.2f) + Vector3.up * (bodyH + ventH * 0.5f),
            new Vector3(fullScale.x * 0.25f, ventH, fullScale.z * 0.25f), Placeholder(), root.transform);
    }

    /// <summary>HarvestPost -- a thin tall pole with a platform near the
    /// top, a lookout-tower read matching its "collection point" fiction.</summary>
    private void BuildWatchtowerShape(GameObject root, Vector3 fullScale)
    {
        var poleRadius = fullScale.x * 0.12f;
        var poleHeight = fullScale.y * 0.95f;
        builder.SpawnPrim(PrimitiveType.Cylinder, root.transform.position + Vector3.up * (poleHeight * 0.5f),
            new Vector3(poleRadius * 2f, poleHeight * 0.5f, poleRadius * 2f), Placeholder(), root.transform);
        var platH = fullScale.y * 0.12f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (poleHeight * 0.85f),
            new Vector3(fullScale.x * 0.8f, platH, fullScale.z * 0.8f), Placeholder(), root.transform);
    }

    /// <summary>Factory -- a large body plus a tall thin smokestack
    /// offset to one corner, the classic factory silhouette.</summary>
    private void BuildFactoryShape(GameObject root, Vector3 fullScale)
    {
        var bodyH = fullScale.y * 0.65f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (bodyH * 0.5f),
            new Vector3(fullScale.x * 0.9f, bodyH, fullScale.z * 0.9f), Placeholder(), root.transform);
        var stackRadius = fullScale.x * 0.09f;
        builder.SpawnPrim(PrimitiveType.Cylinder,
            root.transform.position + Vector3.right * (fullScale.x * 0.32f) + Vector3.forward * (fullScale.z * 0.32f) + Vector3.up * (fullScale.y * 0.5f),
            new Vector3(stackRadius * 2f, fullScale.y * 0.5f, stackRadius * 2f), Placeholder(), root.transform);
    }

    /// <summary>Defense -- a low wide bunker plus a domed turret centered
    /// on top, a pillbox read.</summary>
    private void BuildBunkerShape(GameObject root, Vector3 fullScale)
    {
        var bodyH = fullScale.y * 0.5f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (bodyH * 0.5f),
            new Vector3(fullScale.x * 0.95f, bodyH, fullScale.z * 0.95f), Placeholder(), root.transform);
        var domeRadius = fullScale.x * 0.3f;
        builder.SpawnPrim(PrimitiveType.Sphere, root.transform.position + Vector3.up * (bodyH + domeRadius * 0.6f),
            new Vector3(domeRadius * 2f, domeRadius * 1.3f, domeRadius * 2f), Placeholder(), root.transform);
    }

    /// <summary>Hq -- a tall keep plus a smaller turret perched off-center
    /// on top, the biggest silhouette in the roster (Landmark-tier scale
    /// already makes it the largest footprint too).</summary>
    private void BuildHqShape(GameObject root, Vector3 fullScale)
    {
        var bodyH = fullScale.y * 0.8f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (bodyH * 0.5f),
            new Vector3(fullScale.x * 0.75f, bodyH, fullScale.z * 0.75f), Placeholder(), root.transform);
        var turretH = fullScale.y * 0.35f;
        builder.SpawnPrim(PrimitiveType.Cube,
            root.transform.position + Vector3.right * (fullScale.x * 0.18f) + Vector3.up * (bodyH + turretH * 0.5f),
            new Vector3(fullScale.x * 0.3f, turretH, fullScale.z * 0.3f), Placeholder(), root.transform);
    }

    /// <summary>Defensive fallback only -- every real `BuildingKind`
    /// value is handled above; this never fires unless a new kind is
    /// added to match-core without a matching case here.</summary>
    private void BuildGenericBoxShape(GameObject root, Vector3 fullScale)
    {
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (fullScale.y * 0.5f),
            fullScale, Placeholder(), root.transform);
    }

    /// <summary>Every spawned primitive starts on this single shared
    /// placeholder material -- <see cref="TintShape"/> immediately
    /// overwrites every child's material by owner/damaged state right
    /// after a shape is built, so this is never actually seen; it exists
    /// purely so `SpawnPrim` (which requires a material) has one to
    /// hand it.</summary>
    private static Material _placeholderMat;
    private static Material Placeholder()
    {
        if (_placeholderMat == null) _placeholderMat = new Material(ShaderUtil.FindRenderableShader());
        return _placeholderMat;
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

    /// <summary>A deliberate approximation of docs/17's own per-faction
    /// palette register, keyed by PLAYER INDEX rather than a real
    /// `FactionId` lookup -- `SimBridge` has no player-faction accessor
    /// yet. Index 0 (today's demo always fields MadDoctor there) reads
    /// as a sickly organic/gothic green; index 1 (Human Army) reads as
    /// olive-drab military; anything else is a neutral gray rather than
    /// guessing a faction it has no data for.</summary>
    private static Color OwnerBaseColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.42f, 0.55f, 0.4f);
            case 1: return new Color(0.48f, 0.46f, 0.32f);
            default: return new Color(0.55f, 0.55f, 0.6f);
        }
    }

    private static Material SolidMatFor(int playerIndex)
    {
        if (SolidMatsByOwner.TryGetValue(playerIndex, out var mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = OwnerBaseColor(playerIndex);
        SolidMatsByOwner[playerIndex] = mat;
        return mat;
    }

    private static Material DamagedMatFor(int playerIndex)
    {
        if (DamagedMatsByOwner.TryGetValue(playerIndex, out var mat) && mat != null) return mat;
        var c = OwnerBaseColor(playerIndex);
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(c.r * 0.45f, c.g * 0.3f, c.b * 0.3f, 1f);   // darker + a scorched red-shift, same idiom v1 used
        DamagedMatsByOwner[playerIndex] = mat;
        return mat;
    }

    private static void TintShape(GameObject root, int playerIndex, bool damaged)
    {
        var mat = damaged ? DamagedMatFor(playerIndex) : SolidMatFor(playerIndex);
        var t = root.transform;
        for (var i = 0; i < t.childCount; i++)
        {
            var rend = t.GetChild(i).GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;
        }
    }
}
