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
    // fires the smoke+fire attach exactly once per EntityId, same
    // "match-core's building list only grows" reasoning as
    // _destroyedHandled above -- there is no repair mechanic yet, so a
    // building's HP never regresses back up once it starts taking
    // damage.
    //
    // 2026-08 (creator direction: "as soon as a building is in combat we
    // need to see the smoke and fire"): the trigger condition itself
    // moved from `b.IsDamaged` (<=50% HP, docs/18 SS3) to "has taken any
    // damage at all" (see Dress() below) -- the name stays
    // `_damagedHandled` since it's still the same "fired exactly once"
    // guard, just gated earlier now. `TintShape`'s own Damaged-tier
    // darkening is UNCHANGED and still keyed on the real `b.IsDamaged`
    // threshold -- these are deliberately two independent triggers now.
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
            // 2026-08 (creator direction: "as soon as a building is in
            // combat we need to see the smoke and fire"): was `b.IsDamaged`
            // (<=50% HP) -- now any HP loss at all, so fire/smoke shows on
            // the very first hit instead of waiting for the Damaged
            // threshold. `b.Hp < b.MaxHp` can only newly become true once
            // per building (no repair path), so `_damagedHandled` still
            // guards this to exactly one fire per building.
            if (b.State == BuildingState.Complete && b.Hp < b.MaxHp && _damagedHandled.Add(b.EntityId))
            {
                var footprintRadius = fullScale.x * 0.5f;
                DamageFx.AttachSmoke(root.transform, fullScale.y, footprintRadius, SmokeScaleFor(def));
                DamageFx.AttachFireCluster(root.transform, fullScale.y, footprintRadius, FireCountFor(def));
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

    /// <summary>2026-08 (creator direction: "it should start with 1 but
    /// then others popup in different places based on the building size
    /// up to 8"): the RTS roster's own tier proxy for <see
    /// cref="DamageFx.AttachFireCluster"/>'s `targetCount` -- SAME size
    /// boundaries `FullScaleFor` already draws (an RTS `BuildingDef` has
    /// no `MadDr.CityGen.BuildingTier` of its own to hand
    /// `BuildingStats.FireCount` directly, so this mirrors that table's
    /// NUMBERS rather than sharing its code, the same "duplicate the
    /// tier boundary constants, not a cross-package type" precedent
    /// `FullScaleFor` itself already set for this exact size proxy).</summary>
    private static int FireCountFor(BuildingDef def)
    {
        if (def.MaxHp >= 3000) return 8;   // Landmark (Hq)
        if (def.MaxHp >= 1500) return 5;   // Large
        if (def.MaxHp >= 600) return 3;    // Medium
        return 1;                           // Small
    }

    /// <summary>2026-08 (creator report: "I've never seen the smoke
    /// either"): the RTS roster's own tier proxy for <see
    /// cref="DamageFx.AttachSmoke"/>'s `scale` -- same size boundaries
    /// as <see cref="FireCountFor"/> and <see cref="FullScaleFor"/>,
    /// same "duplicate the tier boundary constants, not a cross-package
    /// type" precedent, mirroring <see cref="MadDr.CityGen.
    /// BuildingStats.SmokeScale"/>'s own numbers.</summary>
    private static float SmokeScaleFor(BuildingDef def)
    {
        if (def.MaxHp >= 3000) return 3.0f;   // Landmark (Hq)
        if (def.MaxHp >= 1500) return 2.2f;   // Large
        if (def.MaxHp >= 600) return 1.5f;    // Medium
        return 1.0f;                           // Small
    }

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
            case BuildingKind.BigBrain:
                BuildBigBrainShape(root, fullScale);
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

    /// <summary>BigBrain -- creator direction, 2026-07: "The big brain
    /// base... should have a big brain in a glass jar on it." A squat
    /// owner-tinted pedestal (same "shape=kind, color=owner" language
    /// every other silhouette here follows) topped by a glass jar with a
    /// glowing organic brain suspended inside it -- the jar assembly is
    /// deliberately NOT owner-tinted (unlike the pedestal underneath):
    /// it's parented under a plain holder transform with no `Renderer`
    /// of its own, so `TintShape`'s single-level `GetChild` sweep (which
    /// only ever touches `root`'s DIRECT children) never reaches these
    /// grandchildren and overwrites their glass/brain materials with the
    /// flat owner color the way it would if they sat directly under
    /// `root`. Same live-in-code placeholder this whole roster's numbers
    /// are (CLAUDE.md's standing v0.1 policy) -- "used for tech
    /// upgrades" per the creator's own framing is a real, NOT-yet-
    /// designed system (no tech-tree/upgrade mechanic exists anywhere in
    /// match-core today, confirmed by a fresh grep before writing this)
    /// -- this method is the visual half only, flagged rather than
    /// silently invented.</summary>
    private void BuildBigBrainShape(GameObject root, Vector3 fullScale)
    {
        var radius = Mathf.Min(fullScale.x, fullScale.z) * 0.26f;
        var pedestalH = fullScale.y * 0.4f;
        builder.SpawnPrim(PrimitiveType.Cylinder, root.transform.position + Vector3.up * (pedestalH * 0.5f),
            new Vector3(radius * 2.3f, pedestalH * 0.5f, radius * 2.3f), Placeholder(), root.transform);

        var jarHolder = new GameObject("BrainJar");
        jarHolder.transform.SetParent(root.transform, false);

        var jarBaseY = pedestalH;
        var jarH = fullScale.y * 0.45f;
        var jarCenter = root.transform.position + Vector3.up * (jarBaseY + jarH * 0.5f);

        var glassMat = new Material(ShaderUtil.FindRenderableShader());
        glassMat.color = new Color(0.75f, 0.88f, 0.85f, 0.28f);
        LabMeshBuilder.MakeTransparent(glassMat);
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter,
            new Vector3(radius * 1.9f, jarH * 0.5f, radius * 1.9f), glassMat, jarHolder.transform);

        var fluidMat = new Material(ShaderUtil.FindRenderableShader());
        var fluidColor = new Color(0.35f, 0.85f, 0.6f);
        fluidMat.color = new Color(fluidColor.r, fluidColor.g, fluidColor.b, 0.55f);
        LabMeshBuilder.MakeTransparent(fluidMat);
        fluidMat.EnableKeyword("_EMISSION");
        fluidMat.SetColor("_EmissionColor", fluidColor * 0.6f);
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter,
            new Vector3(radius * 1.7f, jarH * 0.46f, radius * 1.7f), fluidMat, jarHolder.transform);

        // 2026-07 (creator direction, replacing the pink-sphere-cluster
        // placeholder above with a real low-poly brain + PBR material
        // set): "geometry stays simple while the materials do the heavy
        // lifting... 500-2,000 triangles... rely on textures to convey
        // the intricate anatomy." See BrainMesh.cs (the mesh: two
        // hemispheres + a central fissure + a cerebellum, ~700 tris) and
        // BrainTextureKit.cs (the shared-heightfield normal/AO/
        // smoothness/height set) for the actual technique -- this is
        // just the placement + material wiring.
        var brainRadius = radius * 0.75f;
        var brainMat = BrainMaterial();
        PropLibrary.Spawn(builder, "big-brain-mass", PrimitiveType.Sphere, jarCenter,
            Vector3.one * brainRadius, brainMat, jarHolder.transform);

        var stemMat = new Material(ShaderUtil.FindRenderableShader());
        stemMat.color = new Color(0.68f, 0.55f, 0.55f);
        var stemCenter = jarCenter - Vector3.up * (brainRadius * 0.85f);
        PropLibrary.Spawn(builder, "big-brain-stem", PrimitiveType.Cylinder, stemCenter,
            new Vector3(brainRadius * 0.28f, brainRadius * 0.5f, brainRadius * 0.28f), stemMat, jarHolder.transform);

        // chrome lid, sealing the jar -- deliberately a fixed metal color
        // (not owner-tinted, not glass) rather than a third arbitrary
        // material choice, since a real jar lid IS just plain metal.
        var lidMat = new Material(ShaderUtil.FindRenderableShader());
        lidMat.color = new Color(0.72f, 0.74f, 0.78f);
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter + Vector3.up * (jarH * 0.52f),
            new Vector3(radius * 2f, jarH * 0.06f, radius * 2f), lidMat, jarHolder.transform);
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

    /// <summary>The Big Brain jar's brain material: BrainTextureKit's
    /// whole PBR set wired into URP/Lit's own standard property/keyword
    /// names -- `_BaseMap` (albedo) and `_Smoothness` are already
    /// verified working elsewhere in this file/RoadDresser, but
    /// `_BumpMap`/`_NORMALMAP`, `_OcclusionMap`/`_OCCLUSIONMAP`,
    /// `_MetallicGlossMap`/`_METALLICSPECGLOSSMAP`, and `_ParallaxMap`/
    /// `_PARALLAXMAP`/`_Parallax` are all NEW here -- correct per Unity's
    /// own long-stable URP Lit shader source, but genuinely unconfirmed
    /// in THIS project (no prior usage to check against, and no Editor
    /// here to compile/render it), same "flag a property, can't verify
    /// the keyword string is exactly right" risk docs/28's whole bug
    /// history is full of. Metallic stays 0 (organic tissue, no metal
    /// response); a faint warm low-level emission approximates "soft,
    /// organic, not plastic" -- URP's Lit shader has no real subsurface-
    /// scattering slot at all (that's an HDRP-only feature), and a custom
    /// Shader Graph approximation isn't attempted here for the same
    /// "can't verify it compiles/renders blind" reason -- see
    /// BrainTextureKit's own class header for the fuller account.</summary>
    private static Material _brainMat;
    private static Material BrainMaterial()
    {
        if (_brainMat != null) return _brainMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", BrainTextureKit.Albedo);
        if (mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", BrainTextureKit.Normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mat.HasProperty("_OcclusionMap"))
        {
            mat.SetTexture("_OcclusionMap", BrainTextureKit.Occlusion);
            if (mat.HasProperty("_OcclusionStrength")) mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }
        if (mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", BrainTextureKit.MetallicGloss);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (mat.HasProperty("_ParallaxMap"))
        {
            mat.SetTexture("_ParallaxMap", BrainTextureKit.Height);
            if (mat.HasProperty("_Parallax")) mat.SetFloat("_Parallax", 0.025f);
            mat.EnableKeyword("_PARALLAXMAP");
        }
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        var warmGlow = new Color(0.5f, 0.26f, 0.24f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", warmGlow * 0.1f);
        _brainMat = mat;
        return _brainMat;
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
