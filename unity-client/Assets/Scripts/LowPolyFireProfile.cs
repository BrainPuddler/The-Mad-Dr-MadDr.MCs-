using UnityEngine;

/// <summary>
/// Tunable knobs for the standalone low-poly fire system (LowPolyFire.cs /
/// LowPolyFireMeshKit.cs). Deliberately its own asset type -- NOT a field
/// added to DamageFxProfile -- so this system carries zero dependency on
/// the smoke/fire profile it must stay independent of. Same "unassigned =
/// safe defaults" contract as DamageFxProfile/CityLightingProfile: nothing
/// requires an asset to be created or assigned; <see cref="Active"/> falls
/// back to <see cref="Default"/> when nothing is wired up.
///
/// Create via Assets > Create > MadDr > Low Poly Fire Profile.
/// </summary>
[CreateAssetMenu(fileName = "LowPolyFireProfile", menuName = "MadDr/Low Poly Fire Profile")]
public class LowPolyFireProfile : ScriptableObject
{
    [Header("Capacity")]
    [Tooltip("Hard cap on simultaneously-live fires (a fire = one surface-anchored energy/fuel emitter, not one flame tongue -- each fire draws multiple tongue instances once it's past stage 1). Ignitions/spread beyond this cap are silently skipped, matching the project's 'no per-frame allocations' rule -- pool size is fixed once at startup, never grown.")]
    [Range(8, 1024)]
    public int MaxActiveFires = 256;

    [Tooltip("Hard cap on simultaneously-live ember particles across all fires combined.")]
    [Range(0, 4096)]
    public int MaxActiveEmbers = 512;

    [Header("Surface placement")]
    [Tooltip("Distance (world units) a fire's visual root hovers off the exterior surface it's anchored to, along that surface's own normal -- keeps the flame's base geometry from z-fighting the wall it's gripping.")]
    [Range(0.01f, 0.5f)]
    public float SurfaceOffset = 0.06f;

    [Header("Growth")]
    [Tooltip("Roughly how many seconds an average fire takes to climb from freshly-ignited (stage 1 flicker) to fully-grown (stage 5 roaring flame). Individual fires vary +-25% off this around their own random seed, so a cluster of ignitions doesn't advance through stages in lockstep.")]
    [Range(2f, 180f)]
    public float BaseGrowthDurationSeconds = 24f;

    [Tooltip("How many seconds a fire takes to shrink from its current energy down to fully extinguished once its fuel runs out.")]
    [Range(0.5f, 30f)]
    public float ExtinguishSeconds = 5f;

    [Header("Fuel")]
    [Tooltip("Fuel units a freshly-ignited fire starts with (jittered +-30% per fire). Larger fuel means a fire burns longer before it's forced to shrink and extinguish.")]
    [Range(0.2f, 20f)]
    public float BaseFuel = 3f;

    [Tooltip("Fuel units consumed per second at energy 1.0 (roughly -- actual burn also scales down with a fire's own moisture resistance and current energy). Higher burns through BaseFuel faster.")]
    [Range(0.01f, 2f)]
    public float BurnRatePerSecond = 0.09f;

    [Header("Surface crawling / spread")]
    [Tooltip("Energy (0..1) a fire must reach before it's even eligible to spawn a child fire on nearby exterior geometry.")]
    [Range(0f, 1f)]
    public float SpreadEnergyThreshold = 0.55f;

    [Tooltip("How often (seconds) an eligible fire re-rolls its chance to spread, jittered +-30% per fire so a whole burning face doesn't spread in the same frame.")]
    [Range(0.3f, 6f)]
    public float SpreadCheckInterval = 0.7f;

    [Tooltip("Chance (0..1) an eligible fire actually spreads on any given spread check, once it clears SpreadEnergyThreshold -- keeps spread from firing the instant a fire crosses the threshold every single check.")]
    [Range(0f, 1f)]
    public float SpreadChancePerCheck = 0.3f;

    [Tooltip("A spread candidate point is searched within this radius (world units) of the parent fire's own surface position, along that surface's own tangent plane -- NOT a random world offset. See LowPolyFireManager.TryFindNearbySurfacePoint.")]
    [Range(0.2f, 10f)]
    public float CrawlRadiusMax = 2.2f;

    [Tooltip("Fraction of a parent fire's CURRENT energy a freshly-spawned child fire inherits as its own starting energy.")]
    [Range(0f, 1f)]
    public float SpawnEnergyFraction = 0.35f;

    [Tooltip("Cap on how many children any single fire may ever spawn over its lifetime -- bounds branching so one ignition point can't runaway-fill the entire fire pool.")]
    [Range(0, 12)]
    public int MaxChildrenPerFire = 3;

    [Header("Wind")]
    [Tooltip("Upper bound (degrees) wind is allowed to lean a flame off vertical. Upward buoyancy always stays the dominant motion -- wind can never fully flatten a flame, it only ever leans within this cap.")]
    [Range(0f, 60f)]
    public float MaxWindLeanDegrees = 28f;

    [Header("Lighting")]
    [Tooltip("Hard cap on simultaneous real-time point Lights across every fire combined (pooled, reused, never one Light per flame). Fires beyond this budget still render their emissive geometry, they just don't cast dynamic light.")]
    [Range(0, 128)]
    public int MaxLights = 24;

    [Tooltip("Fires farther than this from the main camera never get a pooled Light, regardless of budget -- rely on emissive material alone at range (LOD).")]
    [Range(5f, 300f)]
    public float MaxLightDistance = 70f;

    private static LowPolyFireProfile _default;

    /// <summary>Safe in-code fallback -- same lazy-CreateInstance idiom as
    /// DamageFxProfile.Default/CityLightingProfile.Default.</summary>
    public static LowPolyFireProfile Default
    {
        get
        {
            if (_default == null) _default = CreateInstance<LowPolyFireProfile>();
            return _default;
        }
    }

    private static LowPolyFireProfile _active;

    /// <summary>The profile currently in effect. Same loose static-holder
    /// idiom the rest of this codebase's tunable profiles use.</summary>
    public static LowPolyFireProfile Active
    {
        get { return _active != null ? _active : Default; }
        set { _active = value; }
    }
}
