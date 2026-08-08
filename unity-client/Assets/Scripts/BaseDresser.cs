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
    // 2026-08 (Big Brain jar "Major Improvement"): same cache-by-key
    // idiom RoadDresser.cs/BuildingDresser.cs already each keep their own
    // private copy of -- this file didn't need one until now.
    private static readonly Dictionary<string, Material> TexturedCache = new Dictionary<string, Material>();

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
    /// `FullScaleFor` itself already set for this exact size proxy).
    ///
    /// 2026-08 follow-up (creator direction: "2-4 depending on the size
    /// of the building"): mirrors `BuildingStats.FireCount`'s own same
    /// follow-up -- 1-8 replaced with 2-4.</summary>
    private static int FireCountFor(BuildingDef def)
    {
        if (def.MaxHp >= 3000) return 4;   // Landmark (Hq)
        if (def.MaxHp >= 1500) return 4;   // Large
        if (def.MaxHp >= 600) return 3;    // Medium
        return 2;                           // Small
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
    /// silently invented.
    ///
    /// 2026-08 ("Major Improvement" creator direction -- explicit: "Do
    /// not redesign the Big Brain Building or replace its existing
    /// visual language... modify the existing visual without changing
    /// its overall shape, proportions, or core architectural design"):
    /// every dimension that existed before (pedestal size, jar radius/
    /// height, fluid fill level, brain size, stem, lid) is UNCHANGED
    /// below -- this pass only upgrades materials in place (glass,
    /// brain -- see BrainMesh.cs's own 2026-08 addendum) and ADDS new
    /// elements the brief calls for (glass edge-highlight rims, the
    /// bottom glow light, rising bubbles, brass rings + steel rivets)
    /// without touching any existing size/position number.</summary>
    private void BuildBigBrainShape(GameObject root, Vector3 fullScale)
    {
        var radius = Mathf.Min(fullScale.x, fullScale.z) * 0.26f;
        var pedestalH = fullScale.y * 0.4f;
        BuildPedestal(root, radius, pedestalH);

        var jarHolder = new GameObject("BrainJar");
        jarHolder.transform.SetParent(root.transform, false);

        var jarBaseY = pedestalH;
        var jarH = fullScale.y * 0.45f;
        var jarCenter = root.transform.position + Vector3.up * (jarBaseY + jarH * 0.5f);

        // ---- glass (2026-08: material upgraded in place, same radius/
        // height as before -- see GlassMaterial's own comment) ----
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter,
            new Vector3(radius * 1.9f, jarH * 0.5f, radius * 1.9f), GlassMaterial(), jarHolder.transform);

        // 2026-08 ("Add subtle glass thickness, reflections, refraction,
        // and edge highlights"): a thin bright band right at the glass's
        // own top/bottom lip -- real glass concentrates and catches
        // light hardest at its edge, and this is the reliable, cheap way
        // to sell that without a custom refraction/fresnel shader this
        // environment has no Editor to compile or verify (same standing
        // caution as every other custom-shader temptation in this
        // codebase). Radius is a hair larger than the glass itself so it
        // reads as sitting ON the rim, not inside it.
        var rimMat = GlassRimMaterial();
        var rimHalfHeight = jarH * 0.018f;
        var rimRadius = radius * 1.93f;
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter + Vector3.up * (jarH * 0.5f - rimHalfHeight),
            new Vector3(rimRadius, rimHalfHeight, rimRadius), rimMat, jarHolder.transform);
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter - Vector3.up * (jarH * 0.5f - rimHalfHeight),
            new Vector3(rimRadius, rimHalfHeight, rimRadius), rimMat, jarHolder.transform);

        var fluidMat = new Material(ShaderUtil.FindRenderableShader());
        var fluidColor = new Color(0.35f, 0.85f, 0.6f);
        fluidMat.color = new Color(fluidColor.r, fluidColor.g, fluidColor.b, 0.55f);
        LabMeshBuilder.MakeTransparent(fluidMat);
        fluidMat.EnableKeyword("_EMISSION");
        fluidMat.SetColor("_EmissionColor", fluidColor * 0.6f);
        var fluidHalfHeight = jarH * 0.46f;
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter,
            new Vector3(radius * 1.7f, fluidHalfHeight, radius * 1.7f), fluidMat, jarHolder.transform);

        // 2026-08 ("Add a very subtle, slow-pulsing light source at the
        // bottom of the chamber... illuminate the underside and lower
        // folds of the brain"): a small emissive glow-disc prop (the
        // visible SOURCE -- "avoid... a generic video-game point light"
        // is read as needing something visible to have caused the glow,
        // not a bare invisible light) plus a real, short-range,
        // shadowless Light so it actually illuminates the brain mesh's
        // own underside -- see EerieChamberGlow.cs's own class header
        // for why this is deliberately NOT routed through
        // DynamicLightBudget/GlowPointRegistry.
        var glowCenter = new Vector3(jarCenter.x, jarCenter.y - fluidHalfHeight * 0.82f, jarCenter.z);
        var glowDiscRadius = radius * 0.5f;
        var glowGo = builder.SpawnPrim(PrimitiveType.Sphere, glowCenter,
            new Vector3(glowDiscRadius, glowDiscRadius * 0.3f, glowDiscRadius), EerieGlowMaterial(), jarHolder.transform);

        var lightGo = new GameObject("EerieGlowLight");
        lightGo.transform.SetParent(jarHolder.transform, false);
        lightGo.transform.position = glowCenter;
        var glowLight = lightGo.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.shadows = LightShadows.None;
        glowLight.lightmapBakeType = LightmapBakeType.Realtime;
        glowLight.range = jarH * 0.95f;
        glowLight.color = new Color(0.35f, 1f, 0.55f);
        var glowAnim = lightGo.AddComponent<EerieChamberGlow>();
        glowAnim.Init(glowLight, glowGo.GetComponent<Renderer>(), new Color(0.3f, 1.1f, 0.6f) * 1.4f, 3.4f);

        // 2026-08 ("Add small bubbles suspended within the green
        // liquid... slowly and randomly rise... sparse enough to feel
        // physical"): see BrainJarBubbles.cs's own class header for why
        // this is hand-rolled primitives, not a ParticleSystem. Seeded
        // from this building's own world position so every Big Brain
        // building's bubbles are independently phased rather than
        // identical clones of each other, without needing any per-match
        // random state.
        var bubblesGo = new GameObject("Bubbles");
        bubblesGo.transform.SetParent(jarHolder.transform, false);
        bubblesGo.transform.position = jarCenter;
        var bubbles = bubblesGo.AddComponent<BrainJarBubbles>();
        var bubbleSeed = Mathf.RoundToInt(root.transform.position.x * 131f + root.transform.position.z * 977f);
        bubbles.Init(BubbleMaterial(), radius, -fluidHalfHeight, fluidHalfHeight * 2f, 8, bubbleSeed);

        // 2026-07 (creator direction, replacing the pink-sphere-cluster
        // placeholder above with a real low-poly brain + PBR material
        // set): "geometry stays simple while the materials do the heavy
        // lifting... rely on textures to convey the intricate anatomy."
        // See BrainMesh.cs (the mesh -- 2026-08: now real geometric
        // gyri/sulci, not just a squashed sphere pair, see that file's
        // own addendum) and BrainTextureKit.cs (the shared-heightfield
        // normal/AO/smoothness/height set) for the actual technique --
        // this is just the placement + material wiring.
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

        // 2026-08 ("Add substantial structural rings around the glass:
        // brass ring around the top, brass ring around the bottom,
        // visible steel rivets/bolts... make the chamber feel heavy,
        // industrial, and physically constructed"): squat solid
        // cylinders, not true hollow torus geometry -- from any normal
        // camera angle a jar-clamp ring reads identically either way
        // (you'd never see "through" the hole), and a solid band is far
        // cheaper. Sits right where the rim highlight above already is,
        // reading as "the ring is what's clamping the glass rim in
        // place" rather than two unrelated decorations stacked there.
        var ringHalfHeight = jarH * 0.05f;
        var ringRadius = radius * 1.98f;
        var topRingCenter = jarCenter + Vector3.up * (jarH * 0.5f - ringHalfHeight * 0.4f);
        var bottomRingCenter = jarCenter - Vector3.up * (jarH * 0.5f - ringHalfHeight * 0.4f);
        var brassMat = Brass();
        builder.SpawnPrim(PrimitiveType.Cylinder, topRingCenter,
            new Vector3(ringRadius, ringHalfHeight, ringRadius), brassMat, jarHolder.transform);
        builder.SpawnPrim(PrimitiveType.Cylinder, bottomRingCenter,
            new Vector3(ringRadius, ringHalfHeight, ringRadius), brassMat, jarHolder.transform);

        var rivetSeedSalt = Mathf.RoundToInt(root.transform.position.x * 53f + root.transform.position.z * 197f);
        var rivetSize = ringHalfHeight * 1.7f;
        var rivetPlacementRadius = ringRadius * 0.5f;
        var steelMat = Steel();
        SpawnRivets(topRingCenter, rivetPlacementRadius, rivetSize, steelMat, jarHolder.transform, 14, rivetSeedSalt);
        SpawnRivets(bottomRingCenter, rivetPlacementRadius, rivetSize, steelMat, jarHolder.transform, 14, rivetSeedSalt + 1000);
    }

    /// <summary>2026-08 (creator direction: "Make the base more ornate
    /// yet should feel like a building"): the original pedestal was one
    /// plain drum -- reads as a plinth/sculpture stand, not architecture.
    /// Subdivides the SAME height budget (`pedestalH`, unchanged -- the
    /// jar still sits at exactly `pedestalH` above the ground, so the
    /// overall silhouette/proportions this building already reads at
    /// are untouched) into five stacked tiers a real stone tower base
    /// actually has: a flared FOOTING (the part that visibly meets the
    /// ground), the main BODY, a thin MOLDING band, a projecting
    /// CORNICE, and a CAP ring the jar visually rests on -- plus engaged
    /// COLUMNS around the body (the single detail that reads
    /// "architecture" fastest at a glance) and dark door/window recesses
    /// (a real entrance implies a real interior, which is what makes a
    /// shape read as a BUILDING rather than a monument).
    ///
    /// Every tier stays a DIRECT child of `root` (not a separate holder
    /// like `jarHolder`), so <see cref="TintShape"/>'s existing owner-
    /// color/damaged-state sweep covers all of it automatically, same as
    /// every other multi-primitive silhouette in this roster (HQ's own
    /// keep+turret, for instance) -- "shape communicates kind, color
    /// communicates owner" (maddr-aesthetic-preferences skill, §5) means
    /// a flat single hue across an ornate shape is the CORRECT choice
    /// here, not a compromise; the tiering/columns/reliefs read from
    /// their own geometry and lighting, not from color contrast. Only
    /// the door/window recesses break from owner color -- real
    /// window/door openings read as voids into the interior, not as the
    /// building's own painted material, the same distinction
    /// BuildingDresser.WindowBand already draws for the city's own
    /// buildings -- so they're parented under `pedestalTrim`, a small
    /// holder exempt from `TintShape` the same way `jarHolder` already
    /// is.</summary>
    private void BuildPedestal(GameObject root, float radius, float pedestalH)
    {
        var origin = root.transform.position;

        // Tier heights sum to exactly pedestalH -- the jar (placed at
        // `pedestalH` by the caller) still sits precisely on top of the
        // cap ring, whatever the tier split.
        var footingH = pedestalH * 0.14f;
        var bodyH = pedestalH * 0.58f;
        var moldingH = pedestalH * 0.06f;
        var corniceH = pedestalH * 0.12f;
        var capH = pedestalH * 0.10f;

        // Diameters (SpawnPrim's Cylinder scale IS the world diameter --
        // see BuildTankShape's own comment on this convention). The
        // cornice/footing project slightly wider than the body, same as
        // a real stone base's foundation course and crown molding both
        // overhang the plain wall between them -- a purely decorative
        // overhang, not a footprint/collision change (this roster's
        // hex-based footprint is unrelated to dressing geometry).
        var footingD = radius * 2.55f;
        var bodyD = radius * 2.1f;
        var moldingD = radius * 2.25f;
        var corniceD = radius * 2.6f;
        var capD = radius * 2.15f;

        // Placeholder(), not a real material: every tier below is a
        // DIRECT child of `root`, so TintShape overwrites this with the
        // owner-color material immediately after -- same "the initial
        // material is never actually seen" convention every other
        // owner-tinted silhouette in this file already relies on.
        var stoneMat = Placeholder();
        var y = 0f;
        builder.SpawnPrim(PrimitiveType.Cylinder, origin + Vector3.up * (y + footingH * 0.5f),
            new Vector3(footingD, footingH * 0.5f, footingD), stoneMat, root.transform);
        y += footingH;

        var bodyBaseY = y;
        var bodyCenterY = y + bodyH * 0.5f;
        builder.SpawnPrim(PrimitiveType.Cylinder, origin + Vector3.up * bodyCenterY,
            new Vector3(bodyD, bodyH * 0.5f, bodyD), stoneMat, root.transform);
        y += bodyH;

        builder.SpawnPrim(PrimitiveType.Cylinder, origin + Vector3.up * (y + moldingH * 0.5f),
            new Vector3(moldingD, moldingH * 0.5f, moldingD), stoneMat, root.transform);
        y += moldingH;

        builder.SpawnPrim(PrimitiveType.Cylinder, origin + Vector3.up * (y + corniceH * 0.5f),
            new Vector3(corniceD, corniceH * 0.5f, corniceD), stoneMat, root.transform);
        y += corniceH;

        builder.SpawnPrim(PrimitiveType.Cylinder, origin + Vector3.up * (y + capH * 0.5f),
            new Vector3(capD, capH * 0.5f, capD), stoneMat, root.transform);
        y += capH;   // y now == pedestalH exactly -- the jar's own math is untouched

        // Engaged columns around the body -- the single fastest "this is
        // architecture, not a plinth" cue. Placed right at the body's
        // own outer surface so they read as attached pilasters, not
        // free-standing posts.
        const int columnCount = 8;
        var columnDiameter = radius * 0.22f;
        var columnPlacementRadius = bodyD * 0.52f;
        for (var i = 0; i < columnCount; i++)
        {
            var angle = i / (float)columnCount * 360f * Mathf.Deg2Rad;
            var pos = origin + new Vector3(Mathf.Sin(angle) * columnPlacementRadius, bodyCenterY,
                Mathf.Cos(angle) * columnPlacementRadius);
            builder.SpawnPrim(PrimitiveType.Cylinder, pos,
                new Vector3(columnDiameter, bodyH * 0.5f, columnDiameter), stoneMat, root.transform);
        }

        // A real entrance (front, +Z) plus three smaller windows on the
        // other faces -- door/window VOIDS, not the building's own
        // stone, so these go under `pedestalTrim` rather than `root`
        // directly (see this method's own doc comment for why).
        var pedestalTrim = new GameObject("PedestalTrim");
        pedestalTrim.transform.SetParent(root.transform, false);
        var windowMat = PedestalWindowMat();
        var bodyWorldRadius = bodyD * 0.5f;

        var doorH = bodyH * 0.55f;
        var doorW = radius * 0.5f;
        var doorDepth = radius * 0.1f;
        var doorCenterY = bodyBaseY + doorH * 0.5f + bodyH * 0.06f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.forward * (bodyWorldRadius * 0.99f) + Vector3.up * doorCenterY,
            new Vector3(doorW, doorH, doorDepth), windowMat, pedestalTrim.transform);

        var windowH = bodyH * 0.28f;
        var windowW = radius * 0.3f;
        var windowDepth = radius * 0.08f;
        var windowCenterY = bodyCenterY + bodyH * 0.08f;
        float[] windowAnglesDeg = { 90f, 180f, 270f };
        foreach (var deg in windowAnglesDeg)
        {
            var rad = deg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            var pos = origin + dir * (bodyWorldRadius * 0.99f) + Vector3.up * windowCenterY;
            builder.SpawnPrim(PrimitiveType.Cube, pos,
                new Vector3(windowW, windowH, windowDepth), windowMat, pedestalTrim.transform);
        }

        // A small cornerstone-plaque slab beside the door -- reads as
        // carved/cast stone or bronze regardless of owner (a real
        // cornerstone is never painted the building's own color), so it
        // gets its own light stone tone rather than sharing the dark
        // window material.
        var plaqueW = radius * 0.34f;
        var plaqueH = bodyH * 0.16f;
        var plaqueDepth = radius * 0.05f;
        var plaqueCenterY = bodyBaseY + plaqueH * 0.5f + bodyH * 0.04f;
        var plaquePos = origin + Vector3.forward * (bodyWorldRadius * 0.99f) + Vector3.right * (doorW * 0.5f + plaqueW * 0.7f)
            + Vector3.up * plaqueCenterY;
        builder.SpawnPrim(PrimitiveType.Cube, plaquePos, new Vector3(plaqueW, plaqueH, plaqueDepth),
            PedestalPlaqueMat(), pedestalTrim.transform);
    }

    /// <summary>Evenly spaced around `ringCenter`'s own circumference at
    /// `placementRadius` (in the ring's local XZ plane, i.e. straight out
    /// from the jar's own central axis), with small per-rivet angle/
    /// radius/size jitter -- "mechanically embedded rather than
    /// decorative" is read as real dimensional studs (small spheres, so
    /// they read correctly as domed rivet heads from any camera angle
    /// with zero orientation math needed) that aren't laser-perfectly
    /// spaced, matching this project's own "irregular, naturally
    /// distributed" world-dressing convention rather than a sterile
    /// grid. Reuses PbrTextureAtlas.Jitter for the jitter hash rather
    /// than inventing a third copy of the same hash function.</summary>
    private void SpawnRivets(Vector3 ringCenter, float placementRadius, float rivetSize,
        Material rivetMat, Transform parent, int count, int seedSalt)
    {
        for (var i = 0; i < count; i++)
        {
            var baseAngleDeg = i / (float)count * 360f;
            var angleJitter = (PbrTextureAtlas.Jitter(i, seedSalt, 101) - 0.5f) * (360f / count) * 0.3f;
            var angleRad = (baseAngleDeg + angleJitter) * Mathf.Deg2Rad;
            var radiusJitter = 1f + (PbrTextureAtlas.Jitter(i, seedSalt, 102) - 0.5f) * 0.08f;
            var r = placementRadius * radiusJitter;
            var pos = ringCenter + new Vector3(Mathf.Sin(angleRad) * r, 0f, Mathf.Cos(angleRad) * r);
            var sizeJitter = 1f + (PbrTextureAtlas.Jitter(i, seedSalt, 103) - 0.5f) * 0.12f;
            builder.SpawnPrim(PrimitiveType.Sphere, pos, Vector3.one * (rivetSize * sizeJitter), rivetMat, parent);
        }
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

    /// <summary>2026-08 ("Major Improvement": "Make the existing chamber
    /// glass much clearer and more realistic, while retaining an old-
    /// fashioned, slightly imperfect laboratory-glass appearance").
    /// Lower alpha than the original (0.28 -> 0.22) so it reads as
    /// genuinely see-through, high Smoothness so URP Lit's own built-in
    /// dielectric fresnel response (Metallic stays 0 -- glass isn't a
    /// metal) gives sharp specular highlights and environment
    /// reflections without any custom shader. The faint pale green-blue
    /// tint is a deliberate nod to real antique lab glass (often
    /// slightly green from iron impurities) -- a happy authentic
    /// coincidence with the jar's own "eerie green" theme, not invented
    /// to match it. PbrTextureAtlas.Glass (previously unused for actual
    /// transparency -- that texture's own comment calling it "no real
    /// transparency, this project has no transparent-material
    /// convention yet" predates LabMeshBuilder.MakeTransparent) supplies
    /// its diagonal sheen band as the "slightly imperfect, hand-blown"
    /// read the brief asks for, tiled low (2x1) so it reads as a broad
    /// warp down the cylinder rather than a repeating pattern.
    ///
    /// True refraction (bending what's behind the glass) has no slot in
    /// URP Lit and isn't attempted via a custom Shader Graph -- same
    /// standing "can't verify a hand-authored shader compiles or renders
    /// correctly with no Editor" caution BrainTextureKit's own header
    /// already applies elsewhere in this file's material stack.</summary>
    private static Material _glassMat;
    private static Material GlassMaterial()
    {
        if (_glassMat != null) return _glassMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.8f, 0.9f, 0.86f, 0.22f);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", PbrTextureAtlas.Glass);
            mat.SetTextureScale("_BaseMap", new Vector2(2f, 1f));
        }
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.93f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        LabMeshBuilder.MakeTransparent(mat);
        _glassMat = mat;
        return _glassMat;
    }

    /// <summary>The thin bright band at the glass's own top/bottom lip
    /// (see BuildBigBrainShape's own comment on why this, not a custom
    /// fresnel shader, is how this codebase sells "edge highlight").</summary>
    private static Material _glassRimMat;
    private static Material GlassRimMaterial()
    {
        if (_glassRimMat != null) return _glassRimMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.92f, 0.97f, 0.95f, 0.5f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
        LabMeshBuilder.MakeTransparent(mat);
        _glassRimMat = mat;
        return _glassRimMat;
    }

    /// <summary>The visible emissive SOURCE object EerieChamberGlow's own
    /// class header explains pairing with the real Light -- a plain,
    /// opaque, strongly emissive material; EerieChamberGlow drives its
    /// per-instance brightness via MaterialPropertyBlock the same way
    /// EmissiveAnimator already does for every other emissive prop in
    /// this codebase, so this shared/cached Material instance never gets
    /// mutated directly (SRP-batcher-friendly, same reasoning as
    /// EmissiveAnimator's own class header).</summary>
    private static Material _eerieGlowMat;
    private static Material EerieGlowMaterial()
    {
        if (_eerieGlowMat != null) return _eerieGlowMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        var glow = new Color(0.3f, 1f, 0.55f);
        mat.color = glow;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glow * 1.4f);
        _eerieGlowMat = mat;
        return _eerieGlowMat;
    }

    /// <summary>Pale, faintly luminous, moderately transparent -- reads
    /// as a small pocket of air/gas rather than a solid ball. One shared
    /// material for every bubble on every Big Brain building (no per-
    /// instance color variation needed, unlike the glow disc above), so
    /// BrainJarBubbles never has to create its own Material instances.</summary>
    private static Material _bubbleMat;
    private static Material BubbleMaterial()
    {
        if (_bubbleMat != null) return _bubbleMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.85f, 0.98f, 0.9f, 0.5f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        LabMeshBuilder.MakeTransparent(mat);
        _bubbleMat = mat;
        return _bubbleMat;
    }

    /// <summary>Same cache-by-key/base-color/texture/tiling idiom
    /// RoadDresser.cs's and BuildingDresser.cs's own private MTextured
    /// copies already use -- extended with an optional Metallic
    /// parameter (unused by either of those files' own callers so far)
    /// since brass genuinely is a metal, unlike wet asphalt or painted
    /// equipment. `smoothness`/`metallic` < 0 means "leave the shader's
    /// own default alone," matching MTextured's existing sentinel
    /// convention in the other two files.</summary>
    private static Material MTextured(string key, float r, float g, float b, Texture2D tex,
        float smoothness = -1f, float metallic = -1f)
    {
        Material mat;
        if (TexturedCache.TryGetValue(key, out mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(r, g, b);
        if (tex != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
        }
        if (smoothness >= 0f && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (metallic >= 0f && mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        TexturedCache[key] = mat;
        return mat;
    }

    /// <summary>The jar's top/bottom structural rings -- moderately
    /// smooth/metallic so it reads as worn, handled metal rather than a
    /// mirror-polish (see BuildBigBrainShape's own comment for the
    /// ring geometry itself, and PbrTextureAtlas.Brass's own comment for
    /// the patina/scratch texture).</summary>
    private static Material Brass()
    {
        return MTextured("big-brain-brass", 0.72f, 0.56f, 0.26f, PbrTextureAtlas.Brass, 0.55f, 0.75f);
    }

    /// <summary>Rivet studs -- flat, untextured steel-gray. At the small
    /// size a single rivet actually renders (a few dozen pixels at most,
    /// typical RTS camera height), texture detail wouldn't even be
    /// visible; the real geometry (BaseDresser.SpawnRivets) is what
    /// sells "mechanically embedded," not surface texture.</summary>
    private static Material Steel()
    {
        return MTextured("big-brain-steel-rivet", 0.55f, 0.56f, 0.58f, null, 0.55f, 0.85f);
    }

    /// <summary>2026-08 (ornate pedestal: "Make the base more ornate yet
    /// should feel like a building"). Dark, faintly glassy voids for the
    /// door/window recesses -- these read as openings into an interior,
    /// not as the building's own owner-colored stone (see BuildPedestal's
    /// own doc comment for the full reasoning), so they're deliberately
    /// NOT one of this file's owner-tint materials.</summary>
    private static Material _pedestalWindowMat;
    private static Material PedestalWindowMat()
    {
        if (_pedestalWindowMat != null) return _pedestalWindowMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.05f, 0.06f, 0.08f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
        _pedestalWindowMat = mat;
        return _pedestalWindowMat;
    }

    /// <summary>The cornerstone plaque's own light stone/bronze tone,
    /// distinct from both the owner-colored masonry and the dark window
    /// voids -- reuses PbrTextureAtlas.Limestone (already established as
    /// this project's shared "carved stone" texture, BuildingDresser's
    /// own Concrete() material) rather than inventing a fresh one.</summary>
    private static Material PedestalPlaqueMat()
    {
        return MTextured("big-brain-pedestal-plaque", 0.8f, 0.77f, 0.68f, PbrTextureAtlas.Limestone, 0.4f);
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
