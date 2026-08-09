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
    // 2026-08: keyed by FactionId, not player index -- see OwnerBaseColor's
    // own doc comment for why the old per-slot keying was a real gap.
    private static readonly Dictionary<FactionId, Material> SolidMatsByOwner = new Dictionary<FactionId, Material>();
    private static readonly Dictionary<FactionId, Material> DamagedMatsByOwner = new Dictionary<FactionId, Material>();
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
                BuildCompleteShape(root, b.Kind, fullScale, b.PlayerIndex);
                _completed[b.EntityId] = root;
            }
            TintShape(root, PlayerFactionFor(b.PlayerIndex), b.IsDamaged);
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

    private void BuildCompleteShape(GameObject root, BuildingKind kind, Vector3 fullScale, int playerIndex)
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
                BuildFactoryShape(root, fullScale, playerIndex);
                break;
            case BuildingKind.Defense:
                BuildBunkerShape(root, fullScale);
                break;
            case BuildingKind.Hq:
                BuildHqShape(root, fullScale, playerIndex);
                break;
            case BuildingKind.BigBrain:
                BuildBigBrainShape(root, fullScale);
                break;
            default:
                BuildGenericBoxShape(root, fullScale);
                break;
        }
    }

    /// <summary>2026-08 ("apply the same level of visual refinement...
    /// to the Factory and Control Centre for every race"): the ONE
    /// accessor this whole pass needed that didn't exist yet --
    /// SimBridge.PlayerFaction (a thin passthrough to match-core's own
    /// already-tracked PlayerState.Faction, see that method's own doc
    /// comment). Falls back to FactionId.Mixed -- this project's own
    /// established "don't guess a specific faction's look" bucket, same
    /// one PlayerFaction itself falls back to -- if `bridge` is somehow
    /// unset, so a null reference here can never crash building
    /// dressing; Mixed-faction buildings (and this defensive fallback)
    /// render the ORIGINAL, undecorated shape rather than a bespoke
    /// fourth architectural style nobody asked for.</summary>
    private FactionId PlayerFactionFor(int playerIndex)
    {
        return bridge != null ? bridge.PlayerFaction(playerIndex) : FactionId.Mixed;
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
    /// offset to one corner, the classic factory silhouette. This
    /// silhouette itself is UNCHANGED by the 2026-08 per-faction pass
    /// (creator direction: "do not change... overall silhouette... do
    /// not redesign the buildings from scratch") -- every faction's
    /// treatment below dresses this exact body+offset-element massing
    /// rather than replacing it, so "instantly recognizable... even
    /// before noticing color" has to come from material/detail
    /// differences, not from three different footprints.</summary>
    private void BuildFactoryShape(GameObject root, Vector3 fullScale, int playerIndex)
    {
        switch (PlayerFactionFor(playerIndex))
        {
            case FactionId.MadDoctor: BuildDoctorFactory(root, fullScale); return;
            case FactionId.AlienHive: BuildAlienFactory(root, fullScale); return;
            case FactionId.HumanArmy: BuildHumanFactory(root, fullScale); return;
            default: BuildGenericFactoryShape(root, fullScale); return;   // Mixed / unrecognized -- see PlayerFactionFor's own comment
        }
    }

    /// <summary>The pre-2026-08 plain shape, kept verbatim as the Mixed-
    /// faction/fallback path -- no bespoke fourth architectural style was
    /// asked for, so Mixed keeps reading exactly as it always has rather
    /// than guessing at one.</summary>
    private void BuildGenericFactoryShape(GameObject root, Vector3 fullScale)
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
    /// already makes it the largest footprint too). Same "silhouette
    /// unchanged, dressed differently per faction" contract as
    /// BuildFactoryShape above.</summary>
    private void BuildHqShape(GameObject root, Vector3 fullScale, int playerIndex)
    {
        switch (PlayerFactionFor(playerIndex))
        {
            case FactionId.MadDoctor: BuildDoctorControlCentre(root, fullScale); return;
            case FactionId.AlienHive: BuildAlienControlCentre(root, fullScale); return;
            case FactionId.HumanArmy: BuildHumanControlCentre(root, fullScale); return;
            default: BuildGenericHqShape(root, fullScale); return;   // Mixed / unrecognized -- see PlayerFactionFor's own comment
        }
    }

    /// <summary>The pre-2026-08 plain shape, kept verbatim as the Mixed-
    /// faction/fallback path -- same reasoning as
    /// BuildGenericFactoryShape above.</summary>
    private void BuildGenericHqShape(GameObject root, Vector3 fullScale)
    {
        var bodyH = fullScale.y * 0.8f;
        builder.SpawnPrim(PrimitiveType.Cube, root.transform.position + Vector3.up * (bodyH * 0.5f),
            new Vector3(fullScale.x * 0.75f, bodyH, fullScale.z * 0.75f), Placeholder(), root.transform);
        var turretH = fullScale.y * 0.35f;
        builder.SpawnPrim(PrimitiveType.Cube,
            root.transform.position + Vector3.right * (fullScale.x * 0.18f) + Vector3.up * (bodyH + turretH * 0.5f),
            new Vector3(fullScale.x * 0.3f, turretH, fullScale.z * 0.3f), Placeholder(), root.transform);
    }

    // ---- 2026-08 per-faction Factory/Control Centre treatments
    // ("apply the same level of visual refinement and thematic
    // storytelling used for the upgraded Big Brain Building to the
    // Factory and Control Centre for every race") --------------------
    //
    // Shared design rule across all six methods below, worth stating
    // once rather than in every method: the BODY (and, for Factory, the
    // chimney-slot cylinder / for Hq, the turret cube) stays a DIRECT
    // child of `root` using Placeholder() -- exactly like every other
    // kind in this file -- so TintShape's existing owner/faction-color
    // sweep keeps working on it completely unchanged (this is what
    // keeps "whose building is this" reading correctly at a glance,
    // silhouette and color both unchanged from before). Every
    // faction-flavored material below (brass, iron, crystal, aluminum,
    // carbon fiber, glass, glow) goes on ADDITIONAL detail geometry
    // parented under a per-building "Trim" holder transform instead --
    // a GRANDCHILD of root, not a direct child, so TintShape's own
    // single-level GetChild sweep never reaches it and overwrites it
    // with the flat owner color. This is the exact same jarHolder/
    // pedestalTrim split BuildBigBrainShape/BuildPedestal already
    // established; nothing new here, just applied to two more kinds.

    /// <summary>Mad Doctor faction Factory -- "massive industrial
    /// laboratory": dark brick body (owner-tinted, unchanged shape) with
    /// a cast-iron chimney banded in brass, stone corner pilasters,
    /// gothic window voids, a brass pressure tank, copper pipework, a
    /// slowly spinning flywheel on a small housing, and a softly
    /// pulsing green glass tube (a real Light, not just an emissive
    /// material -- see EerieChamberGlow.cs's own header for why).</summary>
    private void BuildDoctorFactory(GameObject root, Vector3 fullScale)
    {
        var origin = root.transform.position;
        var bodyH = fullScale.y * 0.65f;
        var bodyW = fullScale.x * 0.9f;
        var bodyD = fullScale.z * 0.9f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.5f),
            new Vector3(bodyW, bodyH, bodyD), Placeholder(), root.transform);

        // 2026-08 (creator direction: "the Factory smoke stack needs to be
        // longer and narrower"): radius cut from 0.09 to 0.065 of fullScale.x
        // (narrower), height raised from a flat fullScale.y (flush with the
        // building's own roofline) to 1.4x that (visibly taller than the
        // building it rises from) -- same "diameter"/"height*0.5 = center
        // offset" Cylinder convention every other primitive in this method
        // already uses.
        var stackRadius = fullScale.x * 0.065f;
        var stackHeight = fullScale.y * 1.4f;
        var stackXZ = new Vector3(fullScale.x * 0.32f, 0f, fullScale.z * 0.32f);
        var stackTop = origin + stackXZ + Vector3.up * stackHeight;
        builder.SpawnPrim(PrimitiveType.Cylinder, origin + stackXZ + Vector3.up * (stackHeight * 0.5f),
            new Vector3(stackRadius * 2f, stackHeight * 0.5f, stackRadius * 2f), Placeholder(), root.transform);

        var trim = new GameObject("FactoryTrim").transform;
        trim.SetParent(root.transform, false);
        var brassMat = Brass();

        // 2026-08 (creator direction: "the metal edging on the building need
        // to extend further out of the building add rivets"): band diameter
        // multiplier raised from 2.3 to 2.8x the (now narrower) stack radius
        // -- a bigger overhang past the stack's own surface than before, not
        // just an unchanged ring shrunk along with the stack -- and each band
        // gets its own scattered rivet ring via the same SpawnRivets helper
        // the jar lid/base rings already use, at that band's own outer
        // radius so the studs sit flush on the band's rim.
        var bandMult = 2.8f;
        var rivetSize = fullScale.x * 0.012f;
        var steelMatStack = Steel();
        for (var i = 1; i <= 3; i++)
        {
            var bandY = stackHeight * (i / 4f);
            var bandCenter = origin + stackXZ + Vector3.up * bandY;
            builder.SpawnPrim(PrimitiveType.Cylinder, bandCenter,
                new Vector3(stackRadius * bandMult, fullScale.y * 0.018f, stackRadius * bandMult), brassMat, trim);
            SpawnRivets(bandCenter, stackRadius * bandMult * 0.5f, rivetSize, steelMatStack, trim, 10, 500 + i * 10);
        }

        // 2026-08 (creator direction: "add smoke emitter coming from it"):
        // a continuous ambient plume at the stack's own opening -- reuses
        // SmokePlume directly (it is a plain public MonoBehaviour with its
        // own Update-driven spawn loop; DamageFx.AttachSmoke wraps it with
        // wall-based, damage-triggered positioning this doesn't want) so the
        // Factory reads as a running industrial chimney from the moment
        // construction completes, independent of the building's HP/damage
        // state entirely.
        var smokeGo = new GameObject("ChimneySmoke");
        smokeGo.transform.SetParent(trim, false);
        smokeGo.transform.position = stackTop;
        var smokeAngle = ((root.GetInstanceID() & 0xFFFF) % 360) * Mathf.Deg2Rad;
        // 2026-08 (creator direction: "thicker smoke, larger"): scale raised
        // 1.4->2.2 (bigger puffs) and a 1.25x alpha multiplier opted in via
        // SmokePlume's new optional 3rd param (0.8 base * 1.25 clamps to a
        // fully opaque 1.0 at each puff's freshest/densest moment, fading
        // out as it disperses same as before) -- denser near the stack,
        // thinning with distance, the same way a real chimney's smoke reads.
        smokeGo.AddComponent<SmokePlume>().Init(2.2f, smokeAngle, 1.25f);

        var stoneMat = DoctorStone();
        // 2026-08 (creator direction: "the edge objects need to be thicker
        // and protrude more" -- confirmed as the corner pilasters): width
        // raised 0.07->0.10 of fullScale.x, and the old flush mount (centered
        // so the pilaster's OUTER face landed exactly at the wall, i.e. it
        // sat fully embedded/inside the wall with nothing sticking out) is
        // replaced with a real protrusion -- shifted outward by 40% of the
        // pilaster's own width, so ~60% of it still overlaps the wall (reads
        // as attached, not floating) while the remaining ~40% now genuinely
        // sticks out past the building's own silhouette.
        var pilasterH = bodyH * 0.92f;
        var pilasterW = fullScale.x * 0.1f;
        var pilasterProtrude = pilasterW * 0.4f;
        float[] signs = { 1f, -1f };
        foreach (var cx in signs)
        foreach (var cz in signs)
        {
            builder.SpawnPrim(PrimitiveType.Cube,
                origin + new Vector3(cx * (bodyW * 0.5f - pilasterW * 0.5f + pilasterProtrude), pilasterH * 0.5f, cz * (bodyD * 0.5f - pilasterW * 0.5f + pilasterProtrude)),
                new Vector3(pilasterW, pilasterH, pilasterW), stoneMat, trim);
        }

        var windowMat = PedestalWindowMat();
        var windowH = bodyH * 0.5f;
        var windowW = fullScale.x * 0.08f;
        float[] windowXFrac = { -0.28f, 0f, 0.28f };
        foreach (var xf in windowXFrac)
        {
            builder.SpawnPrim(PrimitiveType.Cube,
                origin + new Vector3(xf * bodyW, bodyH * 0.5f, bodyD * 0.5f * 0.99f),
                new Vector3(windowW, windowH, fullScale.x * 0.02f), windowMat, trim);
        }

        var tankRadius = fullScale.x * 0.11f;
        var tankH = bodyH * 0.7f;
        var tankCenter = origin + Vector3.right * (bodyW * 0.5f + tankRadius * 0.9f);
        builder.SpawnPrim(PrimitiveType.Cylinder, tankCenter + Vector3.up * (tankH * 0.5f),
            new Vector3(tankRadius * 2f, tankH * 0.5f, tankRadius * 2f), brassMat, trim);
        builder.SpawnPrim(PrimitiveType.Sphere, tankCenter + Vector3.up * (tankH * 0.94f),
            Vector3.one * (tankRadius * 1.6f), brassMat, trim);

        var copperMat = DoctorCopper();
        var pipeRadius = fullScale.x * 0.018f;
        float[] pipeXFrac = { -0.2f, 0.2f };
        foreach (var xf in pipeXFrac)
        {
            builder.SpawnPrim(PrimitiveType.Cylinder,
                origin + new Vector3(xf * bodyW, bodyH * 0.5f, -bodyD * 0.5f - pipeRadius * 1.5f),
                new Vector3(pipeRadius * 2f, bodyH * 0.5f, pipeRadius * 2f), copperMat, trim);
        }

        var ironMat = DoctorIron();
        var housingSize = fullScale.x * 0.22f;
        var housingCenter = origin + Vector3.forward * (bodyD * 0.5f + housingSize * 0.4f) + Vector3.up * (housingSize * 0.5f);
        builder.SpawnPrim(PrimitiveType.Cube, housingCenter, Vector3.one * housingSize, ironMat, trim);
        var wheelD = housingSize * 0.85f;
        var wheelCenter = housingCenter + Vector3.up * (housingSize * 0.5f + 0.01f);
        var wheelGo = builder.SpawnPrim(PrimitiveType.Cylinder, wheelCenter,
            new Vector3(wheelD, housingSize * 0.06f, wheelD), ironMat, trim);
        wheelGo.AddComponent<SlowSpin>().degreesPerSecond = 25f;
        SpawnRivets(wheelCenter, wheelD * 0.42f, housingSize * 0.06f, Steel(), trim, 8, 401);

        var tubeCenter = origin + Vector3.left * (bodyW * 0.5f + fullScale.x * 0.03f) + Vector3.up * (bodyH * 0.55f);
        var glowGo = builder.SpawnPrim(PrimitiveType.Cylinder, tubeCenter,
            new Vector3(fullScale.x * 0.05f, bodyH * 0.35f, fullScale.x * 0.05f), DoctorGlowMat(), trim);
        SpawnPulseLight(trim, tubeCenter, glowGo, new Color(0.3f, 1f, 0.5f), new Color(0.3f, 1.1f, 0.5f) * 1.2f, fullScale.x * 0.8f, 2.6f);
    }

    /// <summary>Mad Doctor faction Control Centre -- "headquarters of a
    /// brilliant but unstable scientist": dark brick keep (owner-tinted,
    /// unchanged shape) topped by a stone turret with an iron observatory
    /// dome, a brass Tesla rod arcing to the dome (a real LineRenderer,
    /// see TeslaArc.cs), mechanical antenna rods, corner pilasters, and
    /// an illuminated green core near the base.</summary>
    private void BuildDoctorControlCentre(GameObject root, Vector3 fullScale)
    {
        var origin = root.transform.position;
        var bodyH = fullScale.y * 0.8f;
        var bodyW = fullScale.x * 0.75f;
        var bodyD = fullScale.z * 0.75f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.5f),
            new Vector3(bodyW, bodyH, bodyD), Placeholder(), root.transform);

        var turretH = fullScale.y * 0.35f;
        var turretSize = fullScale.x * 0.3f;
        var turretCenter = origin + Vector3.right * (fullScale.x * 0.18f) + Vector3.up * (bodyH + turretH * 0.5f);
        builder.SpawnPrim(PrimitiveType.Cube, turretCenter,
            new Vector3(turretSize, turretH, turretSize), Placeholder(), root.transform);

        var trim = new GameObject("HqTrim").transform;
        trim.SetParent(root.transform, false);
        var ironMat = DoctorIron();
        var stoneMat = DoctorStone();
        var brassMat = Brass();

        var domeTop = turretCenter + Vector3.up * (turretH * 0.5f);
        var domeRadius = turretSize * 0.55f;
        builder.SpawnPrim(PrimitiveType.Sphere, domeTop + Vector3.up * (domeRadius * 0.3f),
            new Vector3(domeRadius * 2f, domeRadius * 1.3f, domeRadius * 2f), ironMat, trim);

        // Tesla rod + a second anchor on the dome surface -- a real
        // jittering arc between them (TeslaArc.cs).
        var rodBase = domeTop + Vector3.up * (domeRadius * 0.9f);
        var rodTip = rodBase + Vector3.up * (turretH * 0.5f);
        builder.SpawnPrim(PrimitiveType.Cylinder, (rodBase + rodTip) * 0.5f,
            new Vector3(fullScale.x * 0.012f, (rodTip.y - rodBase.y) * 0.5f, fullScale.x * 0.012f), brassMat, trim);
        var arcAnchor = domeTop + new Vector3(domeRadius * 0.7f, domeRadius * 0.2f, 0f);
        builder.SpawnPrim(PrimitiveType.Sphere, arcAnchor, Vector3.one * (fullScale.x * 0.02f), brassMat, trim);
        SpawnArc(trim, rodTip, arcAnchor, DoctorGlowMat(), new Color(0.55f, 1f, 0.6f, 0.85f), fullScale.x * 0.015f);

        // mechanical antenna rods around the turret roofline
        var antennaCount = 4;
        var antennaR = turretSize * 0.42f;
        for (var i = 0; i < antennaCount; i++)
        {
            var angleDeg = i / (float)antennaCount * 360f;
            var rad = angleDeg * Mathf.Deg2Rad;
            var basePos = turretCenter + Vector3.up * (turretH * 0.5f) + new Vector3(Mathf.Sin(rad) * antennaR, 0f, Mathf.Cos(rad) * antennaR);
            var tipPos = basePos + Vector3.up * (fullScale.y * 0.06f);
            builder.SpawnPrim(PrimitiveType.Cylinder, (basePos + tipPos) * 0.5f,
                new Vector3(fullScale.x * 0.01f, (tipPos.y - basePos.y) * 0.5f, fullScale.x * 0.01f), brassMat, trim);
            builder.SpawnPrim(PrimitiveType.Sphere, tipPos, Vector3.one * (fullScale.x * 0.018f), brassMat, trim);
        }

        // 2026-08 (creator direction: "the edge objects need to be thicker
        // and protrude more" -- confirmed as the corner pilasters, same fix
        // as BuildDoctorFactory's own matching pilasters): width raised
        // 0.06->0.085 of fullScale.x, plus the same outward-protrusion shift
        // (40% of the pilaster's own width) replacing the old flush mount
        // that left the whole pilaster embedded inside the wall.
        var pilasterH = bodyH * 0.92f;
        var pilasterW = fullScale.x * 0.085f;
        var pilasterProtrude = pilasterW * 0.4f;
        float[] signs = { 1f, -1f };
        foreach (var cx in signs)
        foreach (var cz in signs)
        {
            builder.SpawnPrim(PrimitiveType.Cube,
                origin + new Vector3(cx * (bodyW * 0.5f - pilasterW * 0.5f + pilasterProtrude), pilasterH * 0.5f, cz * (bodyD * 0.5f - pilasterW * 0.5f + pilasterProtrude)),
                new Vector3(pilasterW, pilasterH, pilasterW), stoneMat, trim);
        }

        var windowMat = PedestalWindowMat();
        var windowH = bodyH * 0.35f;
        var windowW = fullScale.x * 0.07f;
        float[] windowXFrac = { -0.2f, 0.2f };
        foreach (var xf in windowXFrac)
        {
            builder.SpawnPrim(PrimitiveType.Cube,
                origin + new Vector3(xf * bodyW, bodyH * 0.55f, bodyD * 0.5f * 0.99f),
                new Vector3(windowW, windowH, fullScale.x * 0.02f), windowMat, trim);
        }

        var coreCenter = origin + Vector3.up * (bodyH * 0.22f) + Vector3.forward * (bodyD * 0.5f * 0.98f);
        var coreGo = builder.SpawnPrim(PrimitiveType.Sphere, coreCenter, Vector3.one * (fullScale.x * 0.09f), DoctorGlowMat(), trim);
        SpawnPulseLight(trim, coreCenter, coreGo, new Color(0.3f, 1f, 0.5f), new Color(0.3f, 1.1f, 0.5f) * 1.4f, fullScale.x * 1.1f, 3.6f);
    }

    /// <summary>Alien faction Factory -- "a living energy organism":
    /// translucent membrane hull DETAIL (the owner-tinted body cube
    /// underneath stays the same shape/silhouette; the organic read
    /// comes from bulging energy sacs, ribs, and a crystal growth
    /// replacing the chimney slot), no visible bolts/rivets anywhere
    /// (per the brief: "avoid visible bolts and human engineering"),
    /// gentle hovering (Bob.cs) and slow rotation (SlowSpin.cs) instead
    /// of anything mechanical-looking.</summary>
    private void BuildAlienFactory(GameObject root, Vector3 fullScale)
    {
        var origin = root.transform.position;
        var bodyH = fullScale.y * 0.65f;
        var bodyW = fullScale.x * 0.9f;
        var bodyD = fullScale.z * 0.9f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.5f),
            new Vector3(bodyW, bodyH, bodyD), Placeholder(), root.transform);

        var trim = new GameObject("FactoryTrim").transform;
        trim.SetParent(root.transform, false);
        var crystalMat = AlienCrystalMat();
        var membraneMat = AlienMembraneMat();

        // crystal growth replacing the chimney slot -- same offset/height
        // envelope the original silhouette used there, reshaped.
        var spikeXZ = new Vector3(fullScale.x * 0.32f, 0f, fullScale.z * 0.32f);
        var spikeH = fullScale.y * 0.9f;
        var spikeGo = PropLibrary.Spawn(builder, "alien-crystal-spike", PrimitiveType.Cylinder,
            origin + spikeXZ + Vector3.up * (spikeH * 0.5f),
            new Vector3(fullScale.x * 0.22f, spikeH, fullScale.x * 0.22f), crystalMat, trim);
        spikeGo.AddComponent<SlowSpin>().degreesPerSecond = 8f;

        // pulsing energy sacs bulging off two faces
        var sacPositions = new[]
        {
            origin + Vector3.right * (bodyW * 0.5f * 0.9f) + Vector3.up * (bodyH * 0.6f),
            origin + Vector3.left * (bodyW * 0.5f * 0.9f) + Vector3.up * (bodyH * 0.4f),
            origin + Vector3.forward * (bodyD * 0.5f * 0.9f) + Vector3.up * (bodyH * 0.5f),
        };
        var sacRadius = fullScale.x * 0.14f;
        for (var i = 0; i < sacPositions.Length; i++)
        {
            var sacGo = builder.SpawnPrim(PrimitiveType.Sphere, sacPositions[i], Vector3.one * (sacRadius * 2f), membraneMat, trim);
            sacGo.AddComponent<Bob>().amplitude = sacRadius * 0.25f;
            if (i == 0)
                SpawnPulseLight(trim, sacPositions[i], sacGo, new Color(0.6f, 0.3f, 1f), new Color(0.6f, 0.3f, 1f) * 1.3f, fullScale.x * 0.7f, 3.1f);
        }

        // organic ribs -- thin vertical crystal struts around the body
        var ribCount = 6;
        var ribR = Mathf.Max(bodyW, bodyD) * 0.52f;
        for (var i = 0; i < ribCount; i++)
        {
            var angleDeg = i / (float)ribCount * 360f;
            var rad = angleDeg * Mathf.Deg2Rad;
            var pos = origin + new Vector3(Mathf.Sin(rad) * ribR, bodyH * 0.5f, Mathf.Cos(rad) * ribR);
            builder.SpawnPrim(PrimitiveType.Cylinder, pos,
                new Vector3(fullScale.x * 0.025f, bodyH * 0.48f, fullScale.x * 0.025f), crystalMat, trim);
        }

        // hovering crystal growths near the roofline
        for (var i = 0; i < 3; i++)
        {
            var angleDeg = (i / 3f) * 360f + 40f;
            var rad = angleDeg * Mathf.Deg2Rad;
            var r = Mathf.Min(bodyW, bodyD) * 0.3f;
            var pos = origin + new Vector3(Mathf.Sin(rad) * r, bodyH + fullScale.x * 0.1f, Mathf.Cos(rad) * r);
            var growthH = fullScale.x * 0.28f;
            var growthGo = PropLibrary.Spawn(builder, "alien-crystal-spike", PrimitiveType.Cylinder, pos,
                new Vector3(fullScale.x * 0.08f, growthH, fullScale.x * 0.08f), crystalMat, trim);
            growthGo.AddComponent<Bob>().amplitude = fullScale.x * 0.04f;
        }
    }

    /// <summary>Alien faction Control Centre -- "the hive mind": a
    /// massive floating central crystal (replacing the turret slot,
    /// same offset/height envelope) with orbiting energy rings, curved
    /// support struts, crystalline antennae, and a pulsing psychic core.
    /// No rivets/bolts anywhere, matching the Factory's own restraint.</summary>
    private void BuildAlienControlCentre(GameObject root, Vector3 fullScale)
    {
        var origin = root.transform.position;
        var bodyH = fullScale.y * 0.8f;
        var bodyW = fullScale.x * 0.75f;
        var bodyD = fullScale.z * 0.75f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.5f),
            new Vector3(bodyW, bodyH, bodyD), Placeholder(), root.transform);

        var trim = new GameObject("HqTrim").transform;
        trim.SetParent(root.transform, false);
        var crystalMat = AlienCrystalMat();

        var turretH = fullScale.y * 0.35f;
        var crystalCenter = origin + Vector3.right * (fullScale.x * 0.18f) + Vector3.up * (bodyH + turretH * 0.5f);
        var crystalH = turretH * 1.05f;
        var crystalGo = PropLibrary.Spawn(builder, "alien-crystal-spike", PrimitiveType.Cylinder, crystalCenter,
            new Vector3(fullScale.x * 0.26f, crystalH, fullScale.x * 0.26f), crystalMat, trim);
        crystalGo.AddComponent<Bob>().amplitude = fullScale.x * 0.05f;
        crystalGo.AddComponent<SlowSpin>().degreesPerSecond = 6f;

        // Curved supports, approximated as vertical struts from the roof
        // up to a point PARTWAY up the crystal (0.25 of its own height
        // above center-minus-half, not the crystal's exact bottom tip).
        // Checked numerically before picking that fraction: the crystal
        // is only slightly taller than the turret slot it replaces
        // (crystalH = turretH * 1.05) at the SAME vertical center the
        // turret used, so its own bottom tip sits barely BELOW the
        // roofline (bodyH) -- attaching a strut there would compute a
        // negative height (Unity Cylinder with a negative Y-scale
        // renders inverted/degenerate, not just "wrong looking," a real
        // bug, not a style nitpick). Attaching 0.25 of crystalH above
        // the true bottom instead makes the strut length exactly
        // `turretH * (0.5 - crystalH/turretH * 0.25)`, which stays
        // positive for any crystalH up to 2x turretH -- comfortably
        // covers the actual 1.05x used here with real margin, not by
        // coincidence.
        var strutAttachY = crystalCenter.y - crystalH * 0.25f;
        float[] strutXFrac = { -0.12f, 0.12f };
        foreach (var xf in strutXFrac)
        {
            var top = new Vector3(crystalCenter.x, strutAttachY, crystalCenter.z);
            var basePos = origin + new Vector3(fullScale.x * 0.18f + xf * fullScale.x, bodyH, 0f);
            builder.SpawnPrim(PrimitiveType.Cylinder, (top + basePos) * 0.5f,
                new Vector3(fullScale.x * 0.018f, (top.y - basePos.y) * 0.5f, fullScale.x * 0.018f), crystalMat, trim);
        }

        // two orbiting energy rings at different heights, spinning opposite directions
        var ringRadii = new[] { fullScale.x * 0.42f, fullScale.x * 0.3f };
        var ringHeights = new[] { crystalCenter.y - crystalH * 0.1f, crystalCenter.y + crystalH * 0.25f };
        var ringSpeeds = new[] { 14f, -10f };
        for (var i = 0; i < 2; i++)
        {
            var ringGo = builder.SpawnPrim(PrimitiveType.Cylinder,
                new Vector3(crystalCenter.x, ringHeights[i], crystalCenter.z),
                new Vector3(ringRadii[i] * 2f, fullScale.x * 0.012f, ringRadii[i] * 2f), crystalMat, trim);
            ringGo.AddComponent<SlowSpin>().degreesPerSecond = ringSpeeds[i];
        }

        // crystalline antennae around the roofline
        var antennaCount = 5;
        var antennaR = Mathf.Min(bodyW, bodyD) * 0.4f;
        for (var i = 0; i < antennaCount; i++)
        {
            var angleDeg = i / (float)antennaCount * 360f;
            var rad = angleDeg * Mathf.Deg2Rad;
            var pos = origin + new Vector3(Mathf.Sin(rad) * antennaR, bodyH, Mathf.Cos(rad) * antennaR);
            var spikeH = fullScale.x * 0.16f;
            PropLibrary.Spawn(builder, "alien-crystal-spike", PrimitiveType.Cylinder, pos + Vector3.up * (spikeH * 0.5f),
                new Vector3(fullScale.x * 0.05f, spikeH, fullScale.x * 0.05f), crystalMat, trim);
        }

        // pulsing psychic core at the crystal's own heart
        var coreGo = builder.SpawnPrim(PrimitiveType.Sphere, crystalCenter, Vector3.one * (fullScale.x * 0.1f), AlienGlowMat(), trim);
        SpawnPulseLight(trim, crystalCenter, coreGo, new Color(0.62f, 0.3f, 1f), new Color(0.62f, 0.3f, 1f) * 1.5f, fullScale.x * 1.2f, 4.2f);
    }

    /// <summary>Human Alliance faction Factory -- "advanced automated
    /// manufacturing": aluminum body detail with a carbon-fiber
    /// conveyor strip (a real scrolling-texture belt, ScrollingTexture.cs
    /// -- zero extra geometry), a banded aluminum cooling tower
    /// replacing the chimney slot, a simple loading crane, and a steady
    /// (non-pulsing -- see HumanBlueLightMat's own comment) illuminated
    /// maintenance-bay window. Deliberately NO rivets anywhere ("avoid
    /// unnecessary ornamentation"), the one faction here that skips
    /// them entirely.</summary>
    private void BuildHumanFactory(GameObject root, Vector3 fullScale)
    {
        var origin = root.transform.position;
        var bodyH = fullScale.y * 0.65f;
        var bodyW = fullScale.x * 0.9f;
        var bodyD = fullScale.z * 0.9f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.5f),
            new Vector3(bodyW, bodyH, bodyD), Placeholder(), root.transform);

        var stackRadius = fullScale.x * 0.09f;
        var stackXZ = new Vector3(fullScale.x * 0.32f, 0f, fullScale.z * 0.32f);
        builder.SpawnPrim(PrimitiveType.Cylinder, origin + stackXZ + Vector3.up * (fullScale.y * 0.5f),
            new Vector3(stackRadius * 2f, fullScale.y * 0.5f, stackRadius * 2f), Placeholder(), root.transform);

        var trim = new GameObject("FactoryTrim").transform;
        trim.SetParent(root.transform, false);
        var aluminumMat = HumanAluminum();
        var carbonMat = HumanCarbon();

        // cooling-tower banding around the chimney-slot cylinder
        for (var i = 1; i <= 2; i++)
        {
            var bandY = fullScale.y * (i / 3f);
            builder.SpawnPrim(PrimitiveType.Cylinder, origin + stackXZ + Vector3.up * bandY,
                new Vector3(stackRadius * 2.25f, fullScale.y * 0.02f, stackRadius * 2.25f), aluminumMat, trim);
        }

        // conveyor strip along one side, scrolling
        var conveyorW = bodyD * 0.7f;
        var conveyorGo = builder.SpawnPrim(PrimitiveType.Cube,
            origin + Vector3.right * (bodyW * 0.5f + fullScale.x * 0.04f) + Vector3.up * (fullScale.x * 0.06f),
            new Vector3(fullScale.x * 0.08f, fullScale.x * 0.05f, conveyorW), carbonMat, trim);
        conveyorGo.AddComponent<ScrollingTexture>().speed = new Vector2(0f, 0.6f);

        // cooling towers near a corner
        float[] towerXFrac = { -0.32f };
        foreach (var xf in towerXFrac)
        {
            var towerH = bodyH * 0.55f;
            PropLibrary.Spawn(builder, "human-cooling-tower", PrimitiveType.Cylinder,
                origin + new Vector3(xf * bodyW, towerH * 0.5f, -bodyD * 0.5f - fullScale.x * 0.08f),
                new Vector3(fullScale.x * 0.16f, towerH, fullScale.x * 0.16f), aluminumMat, trim);
        }

        // loading crane -- a post + a horizontal arm, both axis-aligned
        var craneH = bodyH * 1.15f;
        var cranePos = origin + Vector3.left * (bodyW * 0.5f + fullScale.x * 0.05f);
        builder.SpawnPrim(PrimitiveType.Cube, cranePos + Vector3.up * (craneH * 0.5f),
            new Vector3(fullScale.x * 0.04f, craneH, fullScale.x * 0.04f), aluminumMat, trim);
        builder.SpawnPrim(PrimitiveType.Cube, cranePos + Vector3.up * craneH + Vector3.forward * (fullScale.z * 0.15f),
            new Vector3(fullScale.x * 0.04f, fullScale.x * 0.04f, fullScale.z * 0.3f), aluminumMat, trim);

        // illuminated maintenance-bay window -- steady, not pulsing
        var windowH = bodyH * 0.4f;
        var windowW = fullScale.x * 0.22f;
        builder.SpawnPrim(PrimitiveType.Cube,
            origin + Vector3.up * (bodyH * 0.5f) + Vector3.forward * (bodyD * 0.5f * 0.99f),
            new Vector3(windowW, windowH, fullScale.x * 0.02f), HumanBlueLightMat(), trim);

        // roof ventilation
        float[] ventXFrac = { -0.15f, 0.15f };
        foreach (var xf in ventXFrac)
        {
            builder.SpawnPrim(PrimitiveType.Cylinder,
                origin + new Vector3(xf * bodyW, bodyH + fullScale.x * 0.03f, 0f),
                new Vector3(fullScale.x * 0.05f, fullScale.x * 0.03f, fullScale.x * 0.05f), aluminumMat, trim);
        }
    }

    /// <summary>Human Alliance faction Control Centre -- "modern orbital
    /// command headquarters": aluminum keep with a slowly rotating
    /// communication dish on the reinforced tower (SlowSpin.cs -- a safe
    /// vertical spin, no tilted mount, so it reads as a radar sweep
    /// without needing any static rotation this environment has no
    /// Editor to render-verify), sensor towers, an antenna cluster, and
    /// a steady illuminated blue observation-deck band.</summary>
    private void BuildHumanControlCentre(GameObject root, Vector3 fullScale)
    {
        var origin = root.transform.position;
        var bodyH = fullScale.y * 0.8f;
        var bodyW = fullScale.x * 0.75f;
        var bodyD = fullScale.z * 0.75f;
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.5f),
            new Vector3(bodyW, bodyH, bodyD), Placeholder(), root.transform);

        var turretH = fullScale.y * 0.35f;
        var turretSize = fullScale.x * 0.3f;
        var turretCenter = origin + Vector3.right * (fullScale.x * 0.18f) + Vector3.up * (bodyH + turretH * 0.5f);
        builder.SpawnPrim(PrimitiveType.Cube, turretCenter,
            new Vector3(turretSize, turretH, turretSize), Placeholder(), root.transform);

        var trim = new GameObject("HqTrim").transform;
        trim.SetParent(root.transform, false);
        var aluminumMat = HumanAluminum();

        // communication dish -- a shallow, wide, flat-facing-up disc
        var dishR = turretSize * 0.6f;
        var dishGo = builder.SpawnPrim(PrimitiveType.Cylinder, turretCenter + Vector3.up * (turretH * 0.5f + fullScale.x * 0.02f),
            new Vector3(dishR * 2f, fullScale.x * 0.02f, dishR * 2f), aluminumMat, trim);
        dishGo.AddComponent<SlowSpin>().degreesPerSecond = 18f;
        builder.SpawnPrim(PrimitiveType.Cylinder, turretCenter + Vector3.up * (turretH * 0.5f + fullScale.x * 0.05f),
            new Vector3(fullScale.x * 0.02f, fullScale.x * 0.03f, fullScale.x * 0.02f), aluminumMat, dishGo.transform);

        // sensor towers around the roofline
        var sensorCount = 3;
        var sensorR = turretSize * 0.5f;
        for (var i = 0; i < sensorCount; i++)
        {
            var angleDeg = i / (float)sensorCount * 360f + 60f;
            var rad = angleDeg * Mathf.Deg2Rad;
            var basePos = turretCenter + Vector3.up * (turretH * 0.5f) + new Vector3(Mathf.Sin(rad) * sensorR, 0f, Mathf.Cos(rad) * sensorR);
            var h = fullScale.y * 0.05f;
            builder.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (h * 0.5f),
                new Vector3(fullScale.x * 0.012f, h * 0.5f, fullScale.x * 0.012f), aluminumMat, trim);
            builder.SpawnPrim(PrimitiveType.Sphere, basePos + Vector3.up * h, Vector3.one * (fullScale.x * 0.018f), aluminumMat, trim);
        }

        // antenna cluster near the turret base
        var antennaCount = 4;
        var antennaR = fullScale.x * 0.04f;
        for (var i = 0; i < antennaCount; i++)
        {
            var angleDeg = i / (float)antennaCount * 360f;
            var rad = angleDeg * Mathf.Deg2Rad;
            var basePos = turretCenter - Vector3.up * (turretH * 0.5f) + new Vector3(Mathf.Sin(rad) * antennaR, 0f, Mathf.Cos(rad) * antennaR);
            var h = fullScale.y * 0.09f;
            builder.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (h * 0.5f),
                new Vector3(fullScale.x * 0.008f, h * 0.5f, fullScale.x * 0.008f), aluminumMat, trim);
        }

        // illuminated blue observation-deck band -- steady
        var deckH = bodyH * 0.16f;
        var deckMat = HumanBlueLightMat();
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.62f) + Vector3.forward * (bodyD * 0.5f * 0.99f),
            new Vector3(bodyW * 0.6f, deckH, fullScale.x * 0.015f), deckMat, trim);
        builder.SpawnPrim(PrimitiveType.Cube, origin + Vector3.up * (bodyH * 0.62f) + Vector3.right * (bodyW * 0.5f * 0.99f),
            new Vector3(fullScale.x * 0.015f, deckH, bodyD * 0.6f), deckMat, trim);
    }

    /// <summary>Shared by every faction's pulsing-light detail (the
    /// Doctor green tube/core, the Alien energy sac/psychic core) --
    /// wires up a real Light + EerieChamberGlow the same way
    /// BuildBigBrainShape's own bottom-glow does, so this isn't
    /// reimplemented six times with slightly different boilerplate.
    /// `glowGo` is the visible emissive source object (already spawned
    /// by the caller); the Light itself is a new child of `parent`.</summary>
    private void SpawnPulseLight(Transform parent, Vector3 worldPos, GameObject glowGo, Color lightColor, Color emissionPeak, float range, float intensity)
    {
        var lightGo = new GameObject("PulseLight");
        lightGo.transform.SetParent(parent, false);
        lightGo.transform.position = worldPos;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.range = range;
        light.color = lightColor;
        lightGo.AddComponent<EerieChamberGlow>().Init(light, glowGo.GetComponent<Renderer>(), emissionPeak, intensity);
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

        // Moved up from where the brain mesh itself is actually spawned
        // further below -- the bubble orbit math needs the brain's real
        // world radius before it's created, not just its own later
        // SpawnPrim call. Same value either way, just computed earlier.
        var brainRadius = radius * 0.75f;

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
        // physical"; follow-up: "round the brain not below the brain"):
        // see BrainJarBubbles.cs's own class header for why this is
        // hand-rolled primitives, not a ParticleSystem, and for the
        // orbit-around-the-brain motion model. `brainRadius`/`radius`
        // here are SCALE (diameter) values, matching every other prim
        // in this file (see BuildTankShape's own comment) -- their
        // REAL world radii (what the orbit math actually needs) are
        // exactly half that, computed here rather than passing the raw
        // scale values and risking BrainJarBubbles silently
        // misinterpreting which convention it received. Seeded from
        // this building's own world position so every Big Brain
        // building's bubbles are independently phased rather than
        // identical clones of each other, without needing any per-match
        // random state.
        var bubblesGo = new GameObject("Bubbles");
        bubblesGo.transform.SetParent(jarHolder.transform, false);
        bubblesGo.transform.position = jarCenter;
        var bubbles = bubblesGo.AddComponent<BrainJarBubbles>();
        var bubbleSeed = Mathf.RoundToInt(root.transform.position.x * 131f + root.transform.position.z * 977f);
        var fluidWorldRadius = radius * 1.7f * 0.5f;
        var brainWorldRadius = brainRadius * 0.5f;
        bubbles.Init(BubbleMaterial(), fluidWorldRadius, brainWorldRadius,
            -fluidHalfHeight, fluidHalfHeight * 2f, 8, bubbleSeed);

        // 2026-07 (creator direction, replacing the pink-sphere-cluster
        // placeholder above with a real low-poly brain + PBR material
        // set): "geometry stays simple while the materials do the heavy
        // lifting... rely on textures to convey the intricate anatomy."
        // See BrainMesh.cs (the mesh -- 2026-08: now real geometric
        // gyri/sulci, not just a squashed sphere pair, see that file's
        // own addendum) and BrainTextureKit.cs (the shared-heightfield
        // normal/AO/smoothness/height set) for the actual technique --
        // this is just the placement + material wiring. `brainRadius`
        // itself is declared earlier now (bubbles need it too).
        var brainMat = BrainMaterial();
        PropLibrary.Spawn(builder, "big-brain-mass", PrimitiveType.Sphere, jarCenter,
            Vector3.one * brainRadius, brainMat, jarHolder.transform);

        var stemMat = new Material(ShaderUtil.FindRenderableShader());
        stemMat.color = new Color(0.68f, 0.55f, 0.55f);
        var stemCenter = jarCenter - Vector3.up * (brainRadius * 0.85f);
        PropLibrary.Spawn(builder, "big-brain-stem", PrimitiveType.Cylinder, stemCenter,
            new Vector3(brainRadius * 0.28f, brainRadius * 0.5f, brainRadius * 0.28f), stemMat, jarHolder.transform);

        // 2026-08 (creator direction: "make the roof of the big brain the
        // same material as the base"): the lid -- the building's own
        // roof, capping the jar the way a roof caps everything below it
        // -- used to be a fixed chrome-gray, deliberately NOT owner-
        // tinted. That's reversed here: Placeholder() (a throwaway
        // material, same as every owner-tinted piece in this file) AND
        // `root.transform` as the parent instead of `jarHolder.transform`
        // -- the jarHolder holder tree is specifically what TintShape's
        // single-level GetChild sweep never reaches (see this method's
        // own class header), so a fixed match to just one player's color
        // wouldn't actually stay matched for every OTHER player/damaged
        // state; parenting the lid where the pedestal's own tiers live
        // instead means it always resolves to whatever SolidMatFor/
        // DamagedMatFor the base is ACTUALLY wearing that frame, not an
        // approximation of it.
        builder.SpawnPrim(PrimitiveType.Cylinder, jarCenter + Vector3.up * (jarH * 0.52f),
            new Vector3(radius * 2f, jarH * 0.06f, radius * 2f), Placeholder(), root.transform);

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

    /// <summary>A small pocket of air/gas, not a solid ball -- light
    /// green, mostly clear. One shared material for every bubble on
    /// every Big Brain building (no per-instance color variation needed,
    /// unlike the glow disc above), so BrainJarBubbles never has to
    /// create its own Material instances.
    ///
    /// 2026-08 (creator report: "I can't see them" -- the other half of
    /// the fix, alongside BrainJarBubbles.cs's own size fix): a bubble
    /// sitting fully INSIDE the fluid cylinder is a second, independent
    /// transparent object nested inside a first one, both alpha-blended
    /// with ZWrite off (LabMeshBuilder.MakeTransparent). Unity sorts
    /// same-queue transparent objects back-to-front by each renderer's
    /// own bounds-center distance from camera -- for a small bubble deep
    /// inside a much larger fluid cylinder, that distance is often
    /// nearly identical to the fluid's own, so which one wins the
    /// draw-order tiebreak is effectively undefined and can consistently
    /// go the WRONG way every frame rather than merely flicker.
    /// `renderQueue` is set AFTER `MakeTransparent` (which sets it to the
    /// Transparent queue's default 3000) specifically so this later
    /// assignment isn't clobbered by it, forcing bubbles to always
    /// composite on top of the glass/fluid/rims regardless of that
    /// distance-sort ambiguity.
    ///
    /// 2026-08 follow-up (creator direction: "light green and mostly
    /// clear"): the near-white fill from the earlier visibility fix is
    /// replaced with an actual light-green tint at a low alpha -- a real
    /// air bubble is barely-there fill plus a bright specular glint, not
    /// a solid tinted sphere, so Smoothness is nudged up a touch (0.85
    /// -> 0.9) to keep that glint reading clearly now that the fill
    /// itself is more transparent.</summary>
    private static Material _bubbleMat;
    private static Material BubbleMaterial()
    {
        if (_bubbleMat != null) return _bubbleMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.55f, 0.92f, 0.62f, 0.3f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);
        LabMeshBuilder.MakeTransparent(mat);
        mat.renderQueue = 3010;   // after the glass/fluid/rims (queue 3000) -- always composites on top of them
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

    // ---- 2026-08 per-faction Factory/Control Centre materials
    // ("apply the same level of visual refinement... to the Factory and
    // Control Centre for every race") ----------------------------------

    /// <summary>Mad Doctor faction: dark heavy cast-iron framework
    /// (chimneys, machinery housings).</summary>
    private static Material DoctorIron() => MTextured("faction-doctor-iron", 0.22f, 0.22f, 0.23f, PbrTextureAtlas.CastIron, 0.32f, 0.55f);

    /// <summary>Mad Doctor faction: oxidized copper pipework.</summary>
    private static Material DoctorCopper() => MTextured("faction-doctor-copper", 0.6f, 0.36f, 0.22f, PbrTextureAtlas.OxidizedCopper, 0.4f, 0.7f);

    /// <summary>Mad Doctor faction: dark brick walls -- reuses
    /// PbrTextureAtlas.Brick (the same texture the CITY's own civilian
    /// buildings use, BuildingDresser.Brick) tinted darker/cooler, not a
    /// fresh texture -- brick coursing reads the same regardless of
    /// which building it's on; only the tone needs to shift toward
    /// "old, mysterious, gothic" instead of a lived-in row house.</summary>
    private static Material DoctorDarkBrick() => MTextured("faction-doctor-brick", 0.3f, 0.24f, 0.22f, PbrTextureAtlas.Brick, 0.12f);

    /// <summary>Mad Doctor faction: pale weathered stone for buttresses/
    /// window surrounds -- reuses PbrTextureAtlas.Limestone (same
    /// texture PedestalPlaqueMat/BuildingDresser.Concrete both already
    /// share) at a cooler, more weathered tone than either.</summary>
    private static Material DoctorStone() => MTextured("faction-doctor-stone", 0.52f, 0.51f, 0.48f, PbrTextureAtlas.Limestone, 0.18f);

    /// <summary>Mad Doctor faction: the "green illuminated tubes" --
    /// opaque, strongly emissive, paired with a real EerieChamberGlow
    /// pulse the same way the Big Brain jar's own glow disc is (see that
    /// class's own header for why a real Light, not just emissive
    /// material, matters here).</summary>
    private static Material _doctorGlowMat;
    private static Material DoctorGlowMat()
    {
        if (_doctorGlowMat != null) return _doctorGlowMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        var glow = new Color(0.25f, 0.95f, 0.45f);
        mat.color = glow;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glow * 1.3f);
        _doctorGlowMat = mat;
        return _doctorGlowMat;
    }

    /// <summary>Human Alliance faction: brushed aluminum panels.</summary>
    private static Material HumanAluminum() => MTextured("faction-human-aluminum", 0.82f, 0.84f, 0.86f, PbrTextureAtlas.BrushedAluminum, 0.62f, 0.5f);

    /// <summary>Human Alliance faction: carbon-fiber accent panels.</summary>
    private static Material HumanCarbon() => MTextured("faction-human-carbon", 0.5f, 0.52f, 0.55f, PbrTextureAtlas.CarbonFiberPanel, 0.55f, 0.1f);

    /// <summary>Human Alliance faction: "illuminated blue glass" --
    /// deliberately STEADY, not pulsing (no EerieChamberGlow pairing,
    /// unlike Doctor's/Alien's own glow materials) -- "lighting should
    /// be clean and functional" / "communicate precision" is read as a
    /// genuine faction-differentiating choice: Doctor pulses slow and
    /// eerie, Alien pulses organic, Human stays constant. A steady light
    /// literally reads as more "precise" than a wavering one.</summary>
    private static Material _humanBlueLightMat;
    private static Material HumanBlueLightMat()
    {
        if (_humanBlueLightMat != null) return _humanBlueLightMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        var glow = new Color(0.3f, 0.6f, 1f);
        mat.color = glow;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glow * 1.2f);
        _humanBlueLightMat = mat;
        return _humanBlueLightMat;
    }

    /// <summary>Alien faction: translucent glowing purple crystal --
    /// transparent (LabMeshBuilder.MakeTransparent, same technique the
    /// Big Brain jar's own glass uses) with a faint baked-in emission on
    /// top of whatever real light lands on it, since "glowing crystal"
    /// should read even in shadow, not just under direct light.</summary>
    private static Material _alienCrystalMat;
    private static Material AlienCrystalMat()
    {
        if (_alienCrystalMat != null) return _alienCrystalMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.6f, 0.4f, 0.85f, 0.55f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", PbrTextureAtlas.AlienCrystal);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.55f, 0.25f, 0.9f) * 0.35f);
        LabMeshBuilder.MakeTransparent(mat);
        _alienCrystalMat = mat;
        return _alienCrystalMat;
    }

    /// <summary>Alien faction: "living surfaces" -- a softer, blobbier,
    /// less-faceted organic membrane than AlienCrystalMat's own crisp
    /// crystal read, for hull/body surfaces rather than growths/spikes.</summary>
    private static Material _alienMembraneMat;
    private static Material AlienMembraneMat()
    {
        if (_alienMembraneMat != null) return _alienMembraneMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.5f, 0.28f, 0.58f, 0.85f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", PbrTextureAtlas.AlienMembrane);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.5f, 0.2f, 0.65f) * 0.15f);
        LabMeshBuilder.MakeTransparent(mat);
        _alienMembraneMat = mat;
        return _alienMembraneMat;
    }

    /// <summary>Alien faction: pure bright emissive purple -- the visible
    /// SOURCE paired with EerieChamberGlow's real Light, same "avoid...
    /// a generic video-game point light, pair it with something visible"
    /// reasoning as the Big Brain jar's own glow disc.</summary>
    private static Material _alienGlowMat;
    private static Material AlienGlowMat()
    {
        if (_alienGlowMat != null) return _alienGlowMat;
        var mat = new Material(ShaderUtil.FindRenderableShader());
        var glow = new Color(0.62f, 0.25f, 0.95f);
        mat.color = glow;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glow * 1.5f);
        _alienGlowMat = mat;
        return _alienGlowMat;
    }

    /// <summary>Shared by every faction's Tesla-coil/energy-arc mount --
    /// creates a LineRenderer + TeslaArc between two freshly-created
    /// anchor points (`fromLocal`/`toLocal`, positions relative to
    /// `root`'s own origin) and returns the arc's own GameObject in case
    /// a caller wants to parent it under a non-owner-tinted holder.
    /// `arcMat` should already be transparent/emissive-appropriate (see
    /// TeslaArc.cs's own class header on why a real LineRenderer, not a
    /// ParticleSystem, is used here).</summary>
    private GameObject SpawnArc(Transform parent, Vector3 fromWorld, Vector3 toWorld, Material arcMat, Color color, float width)
    {
        var fromGo = new GameObject("ArcFrom");
        fromGo.transform.SetParent(parent, false);
        fromGo.transform.position = fromWorld;
        var toGo = new GameObject("ArcTo");
        toGo.transform.SetParent(parent, false);
        toGo.transform.position = toWorld;

        var arcGo = new GameObject("Arc");
        arcGo.transform.SetParent(parent, false);
        var line = arcGo.AddComponent<LineRenderer>();
        line.sharedMaterial = arcMat;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        var arc = arcGo.AddComponent<TeslaArc>();
        arc.from = fromGo.transform;
        arc.to = toGo.transform;
        return arcGo;
    }

    /// <summary>docs/17's own per-faction palette register, now keyed by
    /// a REAL `FactionId` (via <see cref="PlayerFactionFor"/> ->
    /// `SimBridge.PlayerFaction`) instead of the old PLAYER-INDEX
    /// approximation this method's own comment used to flag as a known
    /// gap ("SimBridge has no player-faction accessor yet"). That gap
    /// mattered more than it looked: a 3+ player match with two Alien
    /// players, say, would have shown the second one as flat neutral
    /// gray under the old index-keyed scheme -- not a hypothetical
    /// edge case, but exactly the kind of mismatch that would have
    /// undermined 2026-08's own "maintaining the existing color
    /// language: Mad Doctor green / Alien purple / Human blue" direction
    /// for anyone not sitting in slot 0 or 1. Human Alliance's own tone
    /// changes here too, from the old olive-drab military approximation
    /// to actual blue -- an explicit, in-scope correction per that same
    /// direction, not an accidental drift.</summary>
    private static Color OwnerBaseColor(FactionId faction)
    {
        switch (faction)
        {
            case FactionId.MadDoctor: return new Color(0.42f, 0.55f, 0.4f);
            case FactionId.HumanArmy: return new Color(0.34f, 0.5f, 0.64f);
            case FactionId.AlienHive: return new Color(0.5f, 0.36f, 0.62f);
            default: return new Color(0.55f, 0.55f, 0.6f);   // Mixed / unrecognized -- unchanged neutral gray
        }
    }

    private static Material SolidMatFor(FactionId faction)
    {
        if (SolidMatsByOwner.TryGetValue(faction, out var mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = OwnerBaseColor(faction);
        SolidMatsByOwner[faction] = mat;
        return mat;
    }

    private static Material DamagedMatFor(FactionId faction)
    {
        if (DamagedMatsByOwner.TryGetValue(faction, out var mat) && mat != null) return mat;
        var c = OwnerBaseColor(faction);
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(c.r * 0.45f, c.g * 0.3f, c.b * 0.3f, 1f);   // darker + a scorched red-shift, same idiom v1 used
        DamagedMatsByOwner[faction] = mat;
        return mat;
    }

    private static void TintShape(GameObject root, FactionId faction, bool damaged)
    {
        var mat = damaged ? DamagedMatFor(faction) : SolidMatFor(faction);
        var t = root.transform;
        for (var i = 0; i < t.childCount; i++)
        {
            var rend = t.GetChild(i).GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;
        }
    }
}
