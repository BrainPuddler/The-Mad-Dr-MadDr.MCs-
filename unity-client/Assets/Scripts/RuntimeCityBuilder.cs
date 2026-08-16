using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// The playable battlefield hub: builds the generated city as real
/// geometry, fetches the roster, spawns commanded monsters
/// (MonsterAgent + genome-driven MonsterBody), spawns Citizens to menace
/// (docs/19), wires the camera/orders/HUD, owns the live
/// BattlefieldState (buildings take damage, rubble opens paths), and
/// the session harvest wallet (docs/20 yields).
///
/// Hit Play: left-click your monster, right-click the world.
/// </summary>
public class RuntimeCityBuilder : MonoBehaviour, IHexObstacleQuery
{
    public enum PresetChoice { Village, SmallTown, BigCity, NewYork, Paris, Montreal }

    [Header("City")]
    [Tooltip("City seed: same seed + preset = identical city, every time (docs/18 determinism contract). Ignored if a CityGizmo also sits on this GameObject -- its seed becomes the source of truth, so tuning the Scene-view preview and hitting Play build the same city without retyping.")]
    public int seed = 42;

    [Tooltip("Ignored if a CityGizmo also sits on this GameObject -- see the seed tooltip.")]
    public PresetChoice preset = PresetChoice.Village;

    [Header("Roster")]
    [Tooltip("Where mutator-service is running. Defaults to the same deployed instance the Lab website uses -- see RosterFetcher's tooltip for the localhost alternative.")]
    public string baseUrl = "https://maddr-mutator.onrender.com";

    [Tooltip("Paste this from the Lab website's \"Account ID\" header button.")]
    public string accountId = "";

    [Header("Tuning (v0.1 display placeholders)")]
    [Tooltip("Multiplier applied to the docs/11 hex/min locomotion speeds for on-screen movement. The raw v0.1 numbers read very slowly at real-world scale; docs/04's own Speed stat is hex/SECOND -- a known placeholder-scale inconsistency, logged in docs/12.")]
    public float speedDisplayMultiplier = 5f;

    [Tooltip("How many Citizens wander the streets near the spawn area (docs/19; client-side cosmetic crowd).")]
    public int citizenCount = 24;

    [Tooltip("How many enemy tanks spawn near the city edge to fight the monsters (a combat test harness; half carry flamethrowers).")]
    public int tankCount = 4;

    [Tooltip("How many cars drive the road network (docs/19 traffic) -- they flee monsters like Citizens do.")]
    public int trafficCarCount = 10;

    [Header("Lighting (docs/28)")]
    [Tooltip("Every tunable number for streetlamps/windows/neon/marquee lights -- brightness, real-light budget, flicker/buzz/chase timing. Create one via Assets > Create > MadDr > City Lighting Profile. Left unassigned, everything falls back to CityLightingProfile.Default's own safe values.")]
    public CityLightingProfile lightingProfile;

    [Header("Damage FX (docs/21 batch 2)")]
    [Tooltip("Fire/smoke puff size knobs. Create one via Assets > Create > MadDr > Damage Fx Profile. Left unassigned, everything falls back to DamageFxProfile.Default's own safe values. Unlike the lighting profile, these are read live -- no rebuild needed to see a change.")]
    public DamageFxProfile damageFxProfile;

    [Header("Monster combat AI")]
    [Tooltip("Combat/AI behavior knobs (currently: the mob-mentality bonus for collateral building attacks). Create one via Assets > Create > MadDr > Monster Combat Profile. Left unassigned, everything falls back to MonsterCombatProfile.Default's own safe values -- read live, same as Damage Fx Profile.")]
    public MonsterCombatProfile monsterCombatProfile;

    [Header("Region picker (off by default -- unchanged behavior)")]
    [Tooltip("Shows an in-game 'choose your city' screen before generation instead of using the Inspector's preset field directly. Off by default so every existing scene/workflow (Inspector preset, CityGizmo sync) keeps working byte-for-byte unchanged -- this only changes anything when explicitly turned on.")]
    public bool showRegionPicker = false;

    [Header("Human race (docs/23 §1, plus FactionId.Mixed as of the 2026-07 amendment)")]
    [Tooltip("The human player's faction. Set by MatchSetupHud when showMatchSetupHud is on; otherwise this Inspector value is used directly -- same 'Inspector field is the source of truth until a picker opts in' pattern as `preset`.")]
    public FactionId chosenFaction = FactionId.MadDoctor;

    [Header("Match setup menu (docs/30, off by default -- unchanged behavior)")]
    [Tooltip("Shows the combined 'choose your race + AI opponents' menu (MatchSetupHud) before generation -- own race, 1-4 AI opponents each with a race and personality, then Begin Match. Off by default so every existing scene keeps working byte-for-byte unchanged. Supersedes the old separate FactionPickerHud/OpponentFactionPickerHud screens (docs/30). Shown BEFORE the region picker when both are on, same ordering rationale the old faction pickers already established (both are 'which faction(s)' questions, naturally grouped before 'which city').")]
    public bool showMatchSetupHud = false;

    /// <summary>docs/30 (selectable races + AI opponents): one configured
    /// AI opponent slot -- set by <see cref="MatchSetupHud"/> when
    /// <see cref="showMatchSetupHud"/> is on and the player confirms.
    /// `Personality` is always resolved (never null) by the time <see
    /// cref="BeginMatch"/> reads it -- "Random" in the menu is rolled to a
    /// concrete <see cref="CommanderPersonality"/> at confirm time, not
    /// deferred, so the SAME personality drives both this opponent's
    /// starting army (<see cref="SpawnOpponentStartingArmy"/>) and its
    /// in-match behavior (<see cref="AiMatchDriver"/>) -- a "Berserker"-
    /// labeled opponent fielding a Turtle-weighted starting army would be a
    /// real bug, not a style nitpick, so this struct is the ONE place that
    /// value is decided.</summary>
    [System.Serializable]
    public struct AiOpponentConfig
    {
        public FactionId Faction;
        public CommanderPersonality Personality;

        public AiOpponentConfig(FactionId faction, CommanderPersonality personality)
        {
            Faction = faction;
            Personality = personality;
        }
    }

    /// <summary>Empty by default -- every existing scene/test that never
    /// touches <see cref="MatchSetupHud"/> leaves this empty, and <see
    /// cref="BeginMatch"/> falls back to the original docs/12 Q13 default
    /// single opponent (Army, or Hive if the human picked Army) in that
    /// case, reproducing the exact prior 2-player behavior byte-for-byte.
    /// 1-4 entries once <see cref="MatchSetupHud"/> is used (the menu
    /// itself enforces that range) -- <see cref="BeginMatch"/> places one
    /// player slot per entry, in order, starting at player index 1.</summary>
    public List<AiOpponentConfig> aiOpponents = new List<AiOpponentConfig>();

    [Header("docs/27 Phase A dev check (off by default)")]
    [Tooltip("Wires the FIRST spawned monster to docs/27's SimBridge/interpolated-view pipeline instead of its normal Time.deltaTime movement, so a Move order on it is decided by match-core and rendered by interpolation -- the actual Editor smoke test docs/27 Phase A has been waiting on (nothing else in this environment can check it). Left-click that monster, right-click to move it, same as always. Every other monster (and every other order kind on this one) is completely unaffected.")]
    public bool simDrivenDemo = false;

    private SimBridge _simBridge;

    // 2026-07 (creator direction: harvester monsters "navigate back to
    // the factory dumping their load there"): a monster's OWN per-unit
    // SimBridge field (MonsterAgent's `_simBridge`) is only ever set via
    // EnableSimDriven -- true for at most one demo unit today, null for
    // the rest of the roster (docs/27's own still-limited opt-in scope).
    // BeginMatch's own match/SimBridge, by contrast, always exists once a
    // match has started (task #115: "unify BeginMatch to always create
    // MatchState") -- exposed here so any Unity-side script holding a
    // plain RuntimeCityBuilder reference (which every MonsterAgent
    // already does, sim-driven or not) can query buildings without
    // needing its own separate SimBridge wiring.
    public SimBridge SimBridge { get { return _simBridge; } }

    [Tooltip("Traffic field: the target fraction of the fleet actively driving at any moment. The rest sit parked at the curb between bounded trips (drive N hops, park a while, repeat). 1 = every car always driving, never parks. Long-run average, not a per-frame guarantee -- see HudStatus for the live measured percentage.")]
    [Range(0.05f, 1f)]
    public float trafficMovingPercent = 0.55f;

    [Tooltip("2026-07 creator direction (\"change where cars are driving to the areas close to or near the player view area -- let's not waste processing power\"): cars farther than this from the camera's actual ground focus freeze in place (zero per-frame cost -- driving logic, threat checks, and roundabout math all skip entirely) until the camera comes back near them, and route-picking at every junction is biased toward staying within this radius so an active car's trip naturally curls back toward the view instead of wandering off to a part of the map nobody's looking at. 2026-07 FIX: a first version of this used a FIXED radius against the wrong point entirely (see RefreshTrafficActivity's own doc comment) and froze the whole fleet from the very first refresh on a typical match -- a real, reported regression (\"still none of the cars are moving, in the editor\"), not a tuning nitpick. Now derived LIVE from camera height every refresh, reusing the exact same proven ratio SimpleCameraRig.shadowDistancePerHeight already uses for \"how far the visible ground extends from the look-at point\" -- this field is now a FLOOR (the minimum radius at minimum zoom), not the radius itself.")]
    public float trafficActiveRadiusFloor = 60f;

    [Tooltip("Same ratio as SimpleCameraRig.shadowDistancePerHeight (SnapTo's own camera-to-ground geometry: height * 1.28, plus margin so the covered ground extends to the visible frustum's edge, not just the exact look-at point) -- reused here rather than guessed, since it's the same camera and the same question (\"how far does the visible ground extend from here\").")]
    public float trafficActiveRadiusPerCameraHeight = 1.9f;

    [Tooltip("Same cap philosophy as SimpleCameraRig.shadowDistanceCap -- don't let the active-traffic zone keep growing at extreme zoom-out, where the perf savings matter most and individual cars are visually tiny anyway.")]
    public float trafficActiveRadiusCap = 320f;

    [Tooltip("2026-07 SAFETY NET, added after a SECOND \"still none of the cars are moving\" report even after fixing the camera ground-focus math above: never trust an absolute distance/radius check alone to decide whether ANY traffic is visible -- always keep the fleet's own nearest N cars active regardless of what the radius calculation says, the same \"always promote the closest N, don't rely on a single absolute threshold\" principle DynamicLightBudget already uses for real lights. This makes it structurally impossible for a camera-geometry miscalculation (this one, or a future one this project hasn't hit yet) to ever freeze 100% of visible traffic again -- whatever's actually nearest the camera keeps driving no matter what. Default (12) is deliberately just above the default trafficCarCount (10), so the out-of-the-box scene is entirely unaffected by this whole feature (every car always qualifies as one of the \"nearest 12\" out of only 10) -- the radius-based freeze only ever starts doing anything once a fleet is larger than this floor.")]
    public int trafficActiveMinimumCount = 12;

    [Tooltip("How much clear space (meters) stays between two units' bodies once a group has settled around a shared destination waypoint -- how tightly they pack in around the click point. Added to the pair's own combined body radii (ApplySeparation), so this is extra daylight on top of however big the units themselves are, not the whole gap.")]
    [Range(0f, 5f)]
    public float groupSpacing = 1f;

    // live state
    private CityModel _city;
    private BattlefieldState _battlefield;
    private Vector3 _origin;
    private TerrainField _terrain;
    private HexCoord? _railyardCenter;
    private HashSet<HexCoord> _roadNetwork;
    private RosterFetcher _roster;

    /// <summary>The player's own fetched creatures (docs/07 Menagerie),
    /// kept around after the match-start spawn loop so <see
    /// cref="LabBattalionHud"/> can resolve a Lab-defined battalion
    /// template's creatureIds back into real genomes without a second
    /// fetch -- the SAME roster data, not a duplicate source of truth.
    /// Empty until <see cref="HandleRosterReady"/> fires.</summary>
    public StoredGenomeDto[] RosterCreatures { get; private set; } = new StoredGenomeDto[0];

    /// <summary>2026-08 (docs/12 "Lab stable" half of battalion grouping):
    /// every named battalion template this account has saved in the Lab,
    /// fetched independently of the roster/Menagerie fetch. Empty until
    /// <see cref="HandleBattalionsReady"/> fires (or forever, if the
    /// player has never saved one).</summary>
    public BattalionTemplateDto[] LabBattalions { get; private set; } = new BattalionTemplateDto[0];
    private int _cityVersion;
    private HashSet<HexCoord> _blockedGroundCache;
    private HashSet<HexCoord> _blockedAmphibiousCache;
    private int _blockedCacheVersion = -1;
    private HashSet<HexCoord> _blockedGroundWithSimCache;
    private HashSet<HexCoord> _blockedAmphibiousWithSimCache;
    private int _blockedSimCacheVersion = -1;
    private long _blockedSimSignature = long.MinValue;

    private readonly Dictionary<Collider, Building> _buildingByCollider = new Dictionary<Collider, Building>();
    private readonly Dictionary<Building, List<GameObject>> _cubesByBuilding = new Dictionary<Building, List<GameObject>>();
    // docs/12 Tier 3 of the graphics-upgrade plan: landmark sites + every
    // player's starting HQ/Factory hex, gathered once in BeginMatch
    // (BEFORE BuildBuildings runs) and used as EngagementZoneManager's
    // "where is the fighting" input -- docs/18 SS5's own live-engagement
    // tracking doesn't exist yet (no SimBridge query for "where are units
    // fighting right now"), so these static, decided-once-at-match-start
    // points are a v0.1 stand-in. A building far from EVERY one of these
    // never re-classifies mid-match even if combat drifts there -- see
    // DeBatchBuildingDressingIfNeeded's own doc comment for how that gap
    // is contained rather than ignored.
    private readonly List<HexCoord> _engagementCenters = new List<HexCoord>();
    // Buildings whose DRESSING (not massing cubes -- see BuildBuildings)
    // is currently StaticBatchingUtility.Combine'd because ClassifyBuilding
    // read them as DistantSkyline at build time. Consulted (and cleared)
    // by DeBatchBuildingDressingIfNeeded the moment such a building takes
    // its first damage.
    private readonly HashSet<Building> _batchedDistantDressing = new HashSet<Building>();
    // 2026-08 (creator direction: "as the lot is cleaned of parts the lot
    // debris is decreased"): the individual rubble chunk GameObjects
    // spawned for a building's own collapse (flattened out of
    // RubbleDresser.Shatter/Scatter's returned hosts) -- ScavengeBuildingDebris
    // destroys a proportional slice of this list as the pile depletes, a
    // SEPARATE tracking set from `_cubesByBuilding` (whose entries are
    // either already destroyed or repurposed as squished dressing, not
    // the actual visible rubble silhouette).
    private readonly Dictionary<Building, List<GameObject>> _debrisChunksByBuilding = new Dictionary<Building, List<GameObject>>();
    // 2026-08 (Zombie scavenging redesign, docs/12: "the site must
    // clearly communicate that it is fully cleared and available for
    // construction"): the permanent scorch-mark decals SpawnScorchDecal
    // spawns at destruction time were never tracked anywhere and never
    // removed -- a fully-scavenged lot with every rubble chunk gone
    // still showed dark burn patches, reading as "still damaged," not
    // "buildable." Tracked here so DrainBuildingScavenge can clear them
    // the instant the pile is fully cleared, same lifecycle as
    // `_debrisChunksByBuilding` (both entries removed together so
    // neither dictionary grows unbounded over a long match).
    private readonly Dictionary<Building, List<GameObject>> _scorchDecalsByBuilding = new Dictionary<Building, List<GameObject>>();
    // 2026-08 (creator direction: "spawn fire when under attack"):
    // idempotency guard for IgniteBuildingIfNeeded -- a building ignites
    // at most once, the moment an attacker is confirmed in range and
    // actively fighting it (MonsterAgent.TickAttack), not gated on a hit
    // actually landing. Previously this idempotency was a side effect of
    // "current.CurrentHp == current.MaxHp is only ever true on the first
    // damage application" inside ApplyBuildingDamage; that trick doesn't
    // work once ignition can happen with zero damage yet dealt, so it's
    // now a real tracked set, same shape as BaseDresser's own
    // `_damagedHandled`.
    //
    // 2026-08 follow-up (creator direction: "if an attacked target has
    // multiple building in it's template then any buildings hit my
    // monster's weapon fire should catch fire first"): tracked PER
    // MASSING-CUBE (one entry per footprint hex a multi-hex building
    // owns), not per Building -- so each hex of a Large/Medium multi-hex
    // structure can ignite independently, from whichever one an attacker
    // is actually hitting, instead of every hit collapsing onto the
    // building's first hex. Keying by the cube `GameObject` itself
    // (rather than a (Building, HexCoord) pair) also means a hex that's
    // later cleared and rebuilt automatically starts fresh -- the OLD
    // cube was `Object.Destroy`'d, so its stale HashSet entry can never
    // collide with the new cube's own distinct GameObject reference.
    private readonly HashSet<GameObject> _ignitedCubes = new HashSet<GameObject>();
    private Transform _buildingsHost;
    private Transform _monstersHost;
    private readonly List<MonsterAgent> _monsters = new List<MonsterAgent>();
    private readonly List<Citizen> _citizens = new List<Citizen>();
    private readonly List<Tank> _tanks = new List<Tank>();
    private readonly List<UnitCombat> _combatants = new List<UnitCombat>();
    // 2026-07 worker-economy epic: Collectors capture fleeing Citizens and
    // possess them into Workers -- both new unit kinds, Tank.cs-pattern
    // bespoke MonoBehaviours (non-genome, plain UnitCombat), tracked here
    // the same way _tanks/_combatants are.
    private readonly List<Collector> _collectors = new List<Collector>();
    private readonly List<Worker> _workers = new List<Worker>();
    private readonly List<CollectorClassDef> _collectorClasses = new List<CollectorClassDef>();
    private readonly Dictionary<uint, CollectorBattalionOrder> _collectorOrders = new Dictionary<uint, CollectorBattalionOrder>();
    private bool _collectorClassesLoaded;
    private readonly List<TrafficCar> _trafficCars = new List<TrafficCar>();

    // 2026-07 (creator report: "driving through parked cars" -- realistic
    // driving/collision-avoidance pass): RoadDresser's STATIC decorative
    // parked cars (set-dressing, `RoadDresser.SpawnCar`) were pure visual
    // GameObjects with a `KnockableProp` for physics knockback but no way
    // for a driving TrafficCar's own AI to even know they exist --
    // `DistanceAhead` below only ever checked the moving fleet/tanks/
    // citizens, never this static dressing, so a car had zero reason to
    // slow or steer around one. Registered once at spawn (never moves, so
    // this list never needs updating after city-build), read by
    // DistanceAhead the same way every other obstacle kind already is.
    private readonly List<Transform> _parkedObstacles = new List<Transform>();

    /// <summary>Called once by RoadDresser.SpawnCar for every static
    /// decorative parked car it places, so TrafficCar's own DistanceAhead
    /// obstacle check can see it -- see this list's own field comment for
    /// why that wasn't already true.</summary>
    public void RegisterParkedObstacle(Transform t) { _parkedObstacles.Add(t); }

    // docs/25 Phase A: uniform-grid neighbour lookup behind ApplySeparation/
    // SteerFollowPath, rebuilt lazily on first use each frame (checked via
    // Time.frameCount rather than from Update(), so this has no dependency
    // on script execution order against MonsterAgent.Update()).
    private readonly SpatialGrid<UnitCombat> _combatantGrid = new SpatialGrid<UnitCombat>();
    private int _combatantGridFrame = -1;
    private float _maxCombatantRadius;
    private float _maxCombatantSpeed;   // docs/25 Phase C: widens SteerFollowPath's query for fast-closing neighbours
    private readonly List<UnitCombat> _separationQueryBuffer = new List<UnitCombat>();
    // docs/25 Phase B: shared candidate buffer for SteerFollowPath's combined
    // separation+avoidance query (was avoidance-only under Phase A/AvoidanceDir).
    private readonly List<UnitCombat> _steerQueryBuffer = new List<UnitCombat>();
    private float _trafficCheckTimer;
    private int _trafficWakeCursor;

    // 2026-07: which cars get to actually simulate, gated on camera
    // distance -- see trafficActiveRadius's own tooltip. Refreshed on a
    // SEPARATE, faster cadence than the traffic-band check above (the
    // camera can pan across the whole map in under a second; a 4s check
    // would leave newly-near/newly-far cars mis-classified for a very
    // visible while), same 0.35s cadence DynamicLightBudget's own
    // nearest-camera refresh already uses for the same reason.
    private const float TrafficActivityRefreshInterval = 0.35f;
    private float _trafficActivityTimer;
    private Vector3 _cameraGroundFocus;

    /// <summary>The camera's last-known ground position (X/Z only, Y
    /// zeroed) -- <see cref="TrafficCar.PickNext"/> reads this to bias its
    /// route choice back toward the player's view instead of wandering
    /// off-screen. Updated on the same throttled cadence as the activity
    /// freeze above, not every frame.</summary>
    public Vector3 CameraGroundFocus { get { return _cameraGroundFocus; } }

    // docs/25 Phase D: rare-path stall detection + sidestep grants, polled
    // from the same periodic Update() traffic already uses (see that
    // method) -- not every frame, per the plan's "rare-path only" framing.
    private readonly DeadlockManager _deadlockManager = new DeadlockManager();
    private float _deadlockPollTimer;
    private const float DeadlockPollInterval = 1f;

    public CityModel City { get { return _city; } }
    public int CityVersion { get { return _cityVersion; } }
    public int CitizensEaten { get; private set; }

    // 2026-08 (docs/12 "eating citizens" fix): reservation tracking for
    // TrySpendReal -- see that method's own header for why this exists.
    private readonly Dictionary<ResourceKind, int> _pendingSpend = new Dictionary<ResourceKind, int>();
    private int _pendingSpendFrame = -1;

    // 2026-08 (Zombie/SCV-style construction, docs/12): every player-0
    // building entity id this class has already sent an initial "pause,
    // nobody's staffing you yet" command for -- same once-per-EntityId
    // guard idiom as BaseDresser's own `_destroyedHandled` (match-core's
    // building list only grows, so without this it would refire every
    // frame forever).
    private readonly HashSet<uint> _constructionPauseHandled = new HashSet<uint>();

    /// <summary>Every fighting unit -- monsters and tanks. The health-bar
    /// HUD, enemy targeting, and no-overlap separation all read this.</summary>
    public IReadOnlyList<UnitCombat> Combatants { get { return _combatants; } }

    private void Start()
    {
        _origin = transform.position;

        // CityGizmo is the Scene-view preview for this same city (docs/18
        // SS2 smoke test) -- when both components share a GameObject, the
        // natural workflow is tune-in-Editor then hit Play, and the two
        // components previously had entirely separate seed/preset fields
        // with nothing wiring them together: change one, forget the
        // other, and Play silently builds a DIFFERENT city than the one
        // just previewed. No good reason for that footgun to exist, so
        // the gizmo (if present) becomes the source of truth here.
        var gizmo = GetComponent<CityGizmo>();
        if (gizmo != null)
        {
            seed = gizmo.seed;
            preset = ConvertPreset(gizmo.preset);
        }

        // docs/30: the combined match-setup menu (own race + 1-4 AI
        // opponents) goes FIRST when both it and the region picker are
        // enabled, same ordering rationale the old FactionPickerHud/
        // OpponentFactionPickerHud pair already established -- its own
        // Confirm() chains into the region picker itself when
        // showRegionPicker is also on, so this check must run before the
        // region-picker check below, not after.
        if (showMatchSetupHud)
        {
            var matchSetup = gameObject.GetComponent<MatchSetupHud>();
            if (matchSetup == null) matchSetup = gameObject.AddComponent<MatchSetupHud>();
            matchSetup.Init(this);
            return;
        }

        // docs/23 Phase 8's own still-open "region picker" item: off by
        // default (BeginMatch runs immediately, identical to every prior
        // session's behavior) -- opting in defers generation until
        // RegionPickerHud reports a choice, the same "opt-in, no-op until
        // a scene explicitly turns it on" discipline simDrivenDemo already
        // established for SimBridge.
        if (showRegionPicker)
        {
            var picker = gameObject.GetComponent<RegionPickerHud>();
            if (picker == null) picker = gameObject.AddComponent<RegionPickerHud>();
            picker.Init(this);
            return;
        }

        BeginMatch();
    }

    /// <summary>Everything Start() used to do unconditionally, from city
    /// generation through the roster fetch -- extracted so
    /// <see cref="RegionPickerHud"/> can call it once a player picks a
    /// region, without duplicating any of it. Called immediately from
    /// Start() when <see cref="showRegionPicker"/> is off (today's exact
    /// behavior, unchanged) or once from the picker's own confirm click
    /// otherwise -- never both.</summary>
    public void BeginMatch()
    {
        _city = CityGenerator.Generate(unchecked((uint)seed), ResolvePreset());
        _battlefield = BattlefieldState.FreshFrom(_city);
        _terrain = new TerrainField(_city, _origin, unchecked((uint)seed));
        // docs/12 Tier 3: every landmark site seeds _engagementCenters
        // (below) alongside each player's starting HQ/Factory hex, added
        // as SpawnStartingBases picks them further down this method --
        // no `break` anymore since a rail_depot landmark is no longer
        // the only one worth visiting.
        foreach (var lm in _city.Landmarks)
        {
            if (lm.Archetype == "rail_depot" && !_railyardCenter.HasValue) _railyardCenter = lm.Site;
            _engagementCenters.Add(lm.Site);
        }

        // 2026-07 amendment (docs/12 "give the player one fully functional
        // factory on startup" + the faction picker): a real MatchState now
        // exists for EVERY match, not just when the simDrivenDemo dev
        // toggle happens to be on -- HandleRosterReady's own simDrivenDemo
        // block (below) now just opts the first monster into sim-driven
        // MOVEMENT against this already-running match, rather than also
        // creating it. player 0 is the human's chosen faction; player 1 is
        // a simple AI-antagonist default (docs/12 Q13: "AI-only Army/Hive
        // antagonists for single-player skirmish"), deliberately never
        // Mixed -- Mixed is the human's own unlocked reward, not something
        // an AI opponent spontaneously gets.
        _simBridge = gameObject.GetComponent<SimBridge>();
        if (_simBridge == null) _simBridge = gameObject.AddComponent<SimBridge>();

        // docs/30: an explicit MatchSetupHud configuration (aiOpponents,
        // 1-4 entries) wins outright; an EMPTY list (every scene that never
        // touches MatchSetupHud) falls back to the original docs/12 Q13
        // single-opponent default -- same FACTION-SELECTION rule as before
        // (Army, or Hive if the human picked Army), so leaving the new menu
        // off reproduces the exact prior matchup unchanged. NOT byte-for-
        // byte identical at the RNG level, though: SpawnOpponentStartingArmy
        // now folds `playerIndex` into its seed (needed so 2+ opponents
        // don't draw from the identical stream), which shifts the SAME
        // seed's starting-army composition even for this single-opponent
        // fallback case. A deliberate, flagged scope note, not a silent
        // regression -- see docs/30/docs/12 for the full writeup.
        var opponents = aiOpponents.Count > 0
            ? aiOpponents
            : new List<AiOpponentConfig>
              {
                  new AiOpponentConfig(
                      chosenFaction == FactionId.HumanArmy ? FactionId.AlienHive : FactionId.HumanArmy,
                      CommanderPersonality.Generate(unchecked((uint)seed)))
              };

        var factions = new List<FactionId> { chosenFaction };
        var playerSetups = new List<PlayerSetup> { PlayerSetup.Human(chosenFaction) };
        foreach (var ai in opponents)
        {
            factions.Add(ai.Faction);
            playerSetups.Add(PlayerSetup.Ai(ai.Faction, ai.Personality));
        }

        _simBridge.StartMatch(unchecked((uint)seed), playerSetups, _city);
        SpawnStartingBases(factions, opponents);

        // the moon-dial/mana/capture-progress HUD, the build-menu/ghost-
        // cursor/BaseDresser trio, and the component-wallet/supply HUD all
        // read live match-core state through THIS SimBridge -- wired here
        // unconditionally (moved 2026-07 out of the simDrivenDemo-gated
        // roster-ready block below, which used to be the only place a
        // real match was guaranteed to exist) so the player's starting
        // Factory/HQ are actually visible without needing that dev toggle
        // on.
        var lumenHud = gameObject.GetComponent<LumenHud>();
        if (lumenHud == null) lumenHud = gameObject.AddComponent<LumenHud>();
        lumenHud.Init(_simBridge, this, playerIndex: 0);

        var buildMenu = gameObject.GetComponent<BuildMenuHud>();
        if (buildMenu == null) buildMenu = gameObject.AddComponent<BuildMenuHud>();
        buildMenu.Init(_simBridge, playerIndex: 0);

        var ghostCursor = gameObject.GetComponent<BuildGhostCursor>();
        if (ghostCursor == null) ghostCursor = gameObject.AddComponent<BuildGhostCursor>();
        ghostCursor.Init(_simBridge, this, buildMenu, playerIndex: 0);

        var baseDresser = gameObject.GetComponent<BaseDresser>();
        if (baseDresser == null) baseDresser = gameObject.AddComponent<BaseDresser>();
        baseDresser.Init(_simBridge, this);

        // 2026-08 (Barracks/infantry roster pass): same wiring shape as
        // baseDresser just above -- see RosterInfantryView's own header
        // for why this is a separate manager rather than folded into
        // BaseDresser (units, not buildings; a different sim-side list).
        var rosterInfantryView = gameObject.GetComponent<RosterInfantryView>();
        if (rosterInfantryView == null) rosterInfantryView = gameObject.AddComponent<RosterInfantryView>();
        rosterInfantryView.Init(_simBridge, this);

        var resourceHud = gameObject.GetComponent<ResourceHud>();
        if (resourceHud == null) resourceHud = gameObject.AddComponent<ResourceHud>();
        resourceHud.Init(_simBridge, playerIndex: 0);

        var buildingNavHud = gameObject.GetComponent<BuildingNavHud>();
        if (buildingNavHud == null) buildingNavHud = gameObject.AddComponent<BuildingNavHud>();
        buildingNavHud.Init(_simBridge, this, playerIndex: 0);

        var grabCursor = gameObject.GetComponent<GrabCursor>();
        if (grabCursor == null) grabCursor = gameObject.AddComponent<GrabCursor>();
        grabCursor.Init(_simBridge, this, playerIndex: 0);

        // docs/28: set BEFORE any dresser runs -- BuildingDresser/RoadDresser
        // are static generators that mint their cached emissive materials
        // (Bulb(), window glow) ONCE at build time, reading whatever
        // profile is active right now.
        CityLightingProfile.Active = lightingProfile != null ? lightingProfile : CityLightingProfile.Default;
        // read LIVE by DamageFx (not just at build time) -- set here anyway
        // so a match that never touches the Inspector still gets Default's
        // safe values instead of a null Active on the very first puff.
        DamageFxProfile.Active = damageFxProfile != null ? damageFxProfile : DamageFxProfile.Default;
        MonsterCombatProfile.Active = monsterCombatProfile != null ? monsterCombatProfile : MonsterCombatProfile.Default;

        // 2026-08 perf (Tier 0 of the graphics-upgrade plan, docs/12):
        // "no performance measurement exists anywhere in this project" was
        // that plan's own headline finding -- no profiler capture, no
        // frame time, no object count, in any doc or comment. This is the
        // scaffolding to fix that, not the measurement itself: there's no
        // Unity Editor in the environment these changes were written in,
        // so the actual before/after numbers still have to come from the
        // creator's own Profiler window. ProfilerMarkers around each build
        // phase below label the Profiler timeline instead of leaving it as
        // undifferentiated call-stack noise; the Debug.Log after
        // BuildLandmarkAuras prints a one-time object/renderer/collider
        // census plus total build time, both cheap enough to leave on
        // permanently (a handful of GetComponentsInChildren calls once per
        // match, not a per-frame cost).
        var buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
        UnityEngine.Profiling.Profiler.BeginSample("RuntimeCityBuilder.BuildGround");
        BuildGround();
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("RuntimeCityBuilder.BuildTableEdge");
        BuildTableEdge();
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("RuntimeCityBuilder.BuildTerrainAndRoads");
        BuildTerrainAndRoads();
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("RuntimeCityBuilder.BuildBuildings");
        BuildBuildings();
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("RuntimeCityBuilder.BuildBridges");
        BuildBridges();
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("RuntimeCityBuilder.BuildLandmarkAuras");
        BuildLandmarkAuras();
        UnityEngine.Profiling.Profiler.EndSample();
        buildStopwatch.Stop();
        LogCityBuildCensus(buildStopwatch.ElapsedMilliseconds);

        // seed the camera-ground focus BEFORE any traffic spawns -- the
        // real camera doesn't exist/get snapped to the city center until
        // later in this method, but TrafficCar.Init's own PickNext calls
        // (inside SpawnTraffic below) already want a sensible bias target
        // rather than defaulting to world origin for however many hexes
        // that happens not to coincide with the actual map.
        _cameraGroundFocus = WorldOf(_city.CenterHex);

        SpawnCitizens();
        SpawnTanks();
        SpawnTraffic();
        SpawnTram();

        // docs/23 Phase 10: supersedes the old NightMode binary day/dusk
        // toggle with a continuous Lumen-clock-driven cycle + post stack.
        if (GetComponent<LumenCycleController>() == null)
        {
            var lumen = gameObject.AddComponent<LumenCycleController>();
            lumen.ApplyProfile(lightingProfile);   // no-op when unassigned -- keeps the component's own Inspector values
            lumen.Init(_city.Region);
        }
        // docs/28: generalized from the streetlamp-only budget -- now
        // spends one shared real-light budget across every registered
        // glow point (streetlamps, windows, neon, marquee), whichever kind
        // they are. Its tuning fields live on the component itself (live
        // in Play mode); an assigned profile just seeds them.
        if (GetComponent<DynamicLightBudget>() == null)
            gameObject.AddComponent<DynamicLightBudget>().ApplyProfile(lightingProfile);
        if (GetComponent<EmissiveAnimatorDriver>() == null)
            gameObject.AddComponent<EmissiveAnimatorDriver>();
        // docs/33: pushes the handful of global shader uniforms every
        // BuildingWindowGrid-dressed building's windows read (day/night
        // cycle position, flicker tuning, the WindowScheduleEnabled
        // toggle) once per frame -- same one-driver-per-scene pattern as
        // EmissiveAnimatorDriver above.
        if (GetComponent<BuildingWindowGridDriver>() == null)
            gameObject.AddComponent<BuildingWindowGridDriver>();

        // camera: frame the spawn area so Play starts looking at the action
        var cam = Camera.main;
        if (cam != null)
        {
            var rig = cam.GetComponent<SimpleCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<SimpleCameraRig>();
            rig.SnapTo(WorldOf(_city.CenterHex), 70f);
        }

        var commander = gameObject.GetComponent<WaypointCommander>();
        if (commander == null) commander = gameObject.AddComponent<WaypointCommander>();
        commander.Init(this);
        grabCursor.commander = commander;
        // 2026-08 (creator direction: "when cursor is in grab mode,
        // disable lasso rectangle select"): see WaypointCommander's own
        // `grabCursor` field doc for why it needs this back-reference.
        commander.grabCursor = grabCursor;

        var hud = gameObject.GetComponent<HudStatus>();
        if (hud == null) hud = gameObject.AddComponent<HudStatus>();
        hud.Init(this, commander);

        // 2026-08 (creator direction: "add a toggle window lights on
        // off"): no Init() needed -- reads EmissiveAnimator's own static
        // toggle directly, same "no data source" simplicity HudStatus's
        // instructions text has.
        if (gameObject.GetComponent<WindowLightsHud>() == null)
            gameObject.AddComponent<WindowLightsHud>();

        var bars = gameObject.GetComponent<HealthBars>();
        if (bars == null) bars = gameObject.AddComponent<HealthBars>();
        bars.Init(this);

        var harvesterMarkers = gameObject.GetComponent<HarvesterMarkerHud>();
        if (harvesterMarkers == null) harvesterMarkers = gameObject.AddComponent<HarvesterMarkerHud>();
        harvesterMarkers.Init(this);

        var fog = gameObject.GetComponent<FogOfWar>();
        if (fog == null) fog = gameObject.AddComponent<FogOfWar>();
        fog.Init(this);

        var minimap = gameObject.GetComponent<Minimap>();
        if (minimap == null) minimap = gameObject.AddComponent<Minimap>();
        minimap.Init(this, commander, fog);

        var selectionHud = gameObject.GetComponent<SelectionHud>();
        if (selectionHud == null) selectionHud = gameObject.AddComponent<SelectionHud>();
        selectionHud.Init(commander, minimap);

        var recallHud = gameObject.GetComponent<RecallHud>();
        if (recallHud == null) recallHud = gameObject.AddComponent<RecallHud>();
        recallHud.Init(commander, minimap);

        var battalionHud = gameObject.GetComponent<BattalionHud>();
        if (battalionHud == null) battalionHud = gameObject.AddComponent<BattalionHud>();
        battalionHud.Init(commander, minimap, recallHud, grabCursor);

        var labBattalionHud = gameObject.GetComponent<LabBattalionHud>();
        if (labBattalionHud == null) labBattalionHud = gameObject.AddComponent<LabBattalionHud>();
        labBattalionHud.Init(this, minimap, battalionHud, grabCursor);

        var collectorLabHud = gameObject.GetComponent<CollectorLabHud>();
        if (collectorLabHud == null) collectorLabHud = gameObject.AddComponent<CollectorLabHud>();
        collectorLabHud.Init(this, playerIndex: 0);

        var barracksHud = gameObject.GetComponent<BarracksHud>();
        if (barracksHud == null) barracksHud = gameObject.AddComponent<BarracksHud>();
        barracksHud.Init(this, playerIndex: 0);

        // 2026-08 (creator direction: "I need to see the image rotating
        // on the roof"): a real 3D hologram of the queue's own portrait,
        // spinning above the Factory roof for as long as production
        // runs -- see RoofPortraitHologram's own doc comment. Built
        // BEFORE ProductionQueueHud (was after) so its own reference can
        // be handed straight into that Init call below -- ProductionQueueHud
        // needs it to float the build label/progress bar at the exact
        // same world anchor the hologram itself uses (2026-08 follow-up,
        // creator report: "the Battalion label that should [be] with the
        // portrait is not visible").
        var roofPortraitHologram = gameObject.GetComponent<RoofPortraitHologram>();
        if (roofPortraitHologram == null) roofPortraitHologram = gameObject.AddComponent<RoofPortraitHologram>();
        roofPortraitHologram.Init(grabCursor);

        var productionQueueHud = gameObject.GetComponent<ProductionQueueHud>();
        if (productionQueueHud == null) productionQueueHud = gameObject.AddComponent<ProductionQueueHud>();
        productionQueueHud.Init(grabCursor, roofPortraitHologram);
        // 2026-08 (creator direction: "I would also like to be able to
        // drop monster on the factory HUD display on the bottom right"):
        // same reverse-reference reasoning as orderSheetHud just below --
        // GrabCursor reads ProductionQueueHud.HoveredTileIndex every
        // frame while Carrying.
        grabCursor.productionQueueHud = productionQueueHud;

        // 2026-08 follow-up (creator report: "the clipboard interface
        // isn't working disable it. Replace it with... press the C key
        // near the factory a order sheet will open"): opened/toggled by
        // GrabCursor's own C-key handler now, not a clipboard click --
        // built after ProductionQueueHud so its reference can be handed
        // in for the "dock right above the tile row" anchor
        // (ProductionQueueHud.TileRowTop). The reverse reference just
        // below is new for this pivot: GrabCursor needs to both call INTO
        // this HUD (open/toggle) and read FROM it (which Factory is open,
        // which slot is hovered) every frame -- see
        // GrabCursor.orderSheetHud's own doc comment.
        var factoryOrdersHud = gameObject.GetComponent<FactoryOrdersHud>();
        if (factoryOrdersHud == null) factoryOrdersHud = gameObject.AddComponent<FactoryOrdersHud>();
        factoryOrdersHud.Init(grabCursor, productionQueueHud);
        grabCursor.orderSheetHud = factoryOrdersHud;

        var clock = gameObject.GetComponent<AnalogClockHud>();
        if (clock == null) gameObject.AddComponent<AnalogClockHud>();

        _roster = gameObject.GetComponent<RosterFetcher>();
        if (_roster == null) _roster = gameObject.AddComponent<RosterFetcher>();
        _roster.baseUrl = baseUrl;
        _roster.accountId = accountId;
        _roster.OnRosterReady += HandleRosterReady;
        _roster.OnRosterFailed += HandleRosterFailed;
        _roster.OnBattalionsReady += HandleBattalionsReady;

        var deployingArmyHud = gameObject.GetComponent<DeployingArmyHud>();
        if (deployingArmyHud == null) deployingArmyHud = gameObject.AddComponent<DeployingArmyHud>();
        deployingArmyHud.Init(_roster);

        _roster.FetchRoster();
        _roster.FetchBattalions();
    }

    private const float TrafficCheckInterval = 4f;
    private const float TrafficBandTolerance = 0.2f; // +-20% of trafficMovingPercent, creator direction

    /// <summary>Two independent rare-path timers share this MonoBehaviour's
    /// only Update(): (1) docs/19 traffic field, corrective half -- each
    /// car's OWN independent park timer (rolled at Init/ParkHere) already
    /// targets trafficMovingPercent on average, but a run of bad luck can
    /// drift the LIVE fraction driving well below it for a while with
    /// nobody due to depart soon. Rather than forcing an exact per-park-
    /// event swap (too rigid -- creator direction: "the next car(s) do not
    /// have to start immediately, we can have more cars on longer
    /// journeys"), this periodically checks the measured fraction and,
    /// only once it's dropped more than TrafficBandTolerance below target,
    /// wakes ONE currently-parked car early -- a loose band, not a
    /// lockstep swap. A rotating cursor spreads the early wake-ups across
    /// the fleet instead of always picking the same car. (2) docs/25 Phase
    /// D -- polls DeadlockManager on its own separate interval, unguarded
    /// by traffic-car count (monsters can jam each other with zero cars
    /// anywhere nearby).</summary>
    private void Update()
    {
        TickCollectorProduction(Time.deltaTime);
        TickConstructionStaffing();

        if (_trafficCars.Count > 0)
        {
            _trafficCheckTimer -= Time.deltaTime;
            if (_trafficCheckTimer <= 0f)
            {
                _trafficCheckTimer = TrafficCheckInterval;

                var target = trafficMovingPercent;
                var lowBand = target * (1f - TrafficBandTolerance);
                if (TrafficMovingFraction < lowBand)
                {
                    var n = _trafficCars.Count;
                    for (var i = 0; i < n; i++)
                    {
                        var idx = (_trafficWakeCursor + i) % n;
                        var c = _trafficCars[idx];
                        if (c == null || c.IsDriving) continue;
                        _trafficWakeCursor = (idx + 1) % n;
                        c.DepartNow();
                        break;   // one car per check -- see the "don't have to start immediately" note above
                    }
                }
            }
        }

        // docs/25 Phase D: rare-path deadlock poll, independent of the
        // traffic-car timer above (must still run in a scene with zero
        // traffic cars -- monsters can jam each other with no cars
        // anywhere nearby).
        _deadlockPollTimer -= Time.deltaTime;
        if (_deadlockPollTimer <= 0f)
        {
            _deadlockPollTimer = DeadlockPollInterval;
            _deadlockManager.Poll(_monsters, DeadlockPollInterval, Time.time, this);
        }

        // 2026-07 creator direction: only simulate traffic near the
        // player's view. A third independent rare-path timer, same
        // shared-Update() convention as the two above.
        if (_trafficCars.Count > 0)
        {
            _trafficActivityTimer -= Time.deltaTime;
            if (_trafficActivityTimer <= 0f)
            {
                _trafficActivityTimer = TrafficActivityRefreshInterval;
                RefreshTrafficActivity();
            }
        }
    }

    /// <summary>Marks every traffic car near/far based on ground distance
    /// from where the camera is actually LOOKING (not the camera rig's own
    /// transform, which sits well off to the side of that -- see the
    /// 2026-07 FIX note below), and caches that ground point for <see
    /// cref="TrafficCar.PickNext"/>'s route bias to read. A no-op (leaves
    /// every car in whatever state it already had) if there's no main
    /// camera yet -- matches DynamicLightBudget.Refresh's own defensive
    /// "cam == null: bail" convention. Same "fail open" posture if the
    /// look-ray never crosses the ground plane either (camera pitched up
    /// at the sky, a degenerate case SnapTo's own fixed 50-degree pitch
    /// never actually produces, but scroll-zoom/rotation are player
    /// input): keeps the LAST known good focus rather than computing
    /// nonsense from it, so a bad frame can only ever leave cars in their
    /// previous (working) state, never actively break them.
    ///
    /// 2026-07 FIX, real reported regression ("still none of the cars are
    /// moving, in the editor"): the very first version of this used
    /// `cam.transform.position` directly as the "ground focus" -- but
    /// `SimpleCameraRig.SnapTo` places the camera RIG itself at `focus +
    /// (0, height, -height*0.8)`, i.e. well ABOVE and BEHIND the actual
    /// point it's looking at, never AT it. Treating that offset, elevated
    /// rig position as "where the player is looking" put the reference
    /// point tens of meters off from the real one on every single match,
    /// and since traffic spawns scattered across the WHOLE road network
    /// (not clustered near the start camera position), this likely froze
    /// the entire default 10-car fleet the instant the very first refresh
    /// ran (0.35s into the match) -- a real, severe regression, not a
    /// tuning nitpick. Fixed by raycasting from the camera through the
    /// VIEWPORT CENTER to the ground plane (y=0) -- the actual "what is
    /// the player looking at" point, the same question <see
    /// cref="SimpleCameraRig.FocusOn"/> already answers for its own
    /// G-key-jump feature via a very similar ground-raycast, just done
    /// here without a dependency on that component (RuntimeCityBuilder
    /// only needs a math answer, not SimpleCameraRig's own drag-state).
    /// Also switched the active radius from a single fixed guess to one
    /// derived LIVE from camera height every refresh (see <see
    /// cref="trafficActiveRadiusPerCameraHeight"/>'s own doc comment) --
    /// the wrong-point bug was the primary cause, but a live, camera-
    /// aware radius is far more robust across zoom levels than any one
    /// fixed number could be.</summary>
    private void RefreshTrafficActivity()
    {
        var cam = Camera.main;
        if (cam == null) return;

        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Mathf.Abs(ray.direction.y) > 1e-5f)
        {
            var t = -ray.origin.y / ray.direction.y;
            if (t > 0f)
            {
                var ground = ray.origin + ray.direction * t;
                ground.y = 0f;
                _cameraGroundFocus = ground;
            }
            // t <= 0: the ground plane is BEHIND the camera along this
            // ray (shouldn't happen at SnapTo's fixed downward pitch, but
            // don't silently compute a nonsense point if it ever does) --
            // keep the last known good focus.
        }
        // ray.direction.y == 0: looking perfectly level, no ground
        // intersection at all -- same "keep the last known good focus"
        // fallback.

        var camHeight = cam.transform.position.y;
        var activeRadius = Mathf.Min(camHeight * trafficActiveRadiusPerCameraHeight + trafficActiveRadiusFloor, trafficActiveRadiusCap);
        var activeSq = activeRadius * activeRadius;

        var count = _trafficCars.Count;
        var sqDistances = new float[count];
        for (var i = 0; i < count; i++)
        {
            var car = _trafficCars[i];
            if (car == null) { sqDistances[i] = float.MaxValue; continue; }
            var p = car.transform.position;
            p.y = 0f;
            sqDistances[i] = (p - _cameraGroundFocus).sqrMagnitude;
        }

        // SAFETY NET (see trafficActiveMinimumCount's own doc comment):
        // never rely on the radius/focus calculation ALONE to decide
        // whether any traffic is visible -- always keep the nearest N
        // cars active regardless of what that calculation says. An O(n^2)
        // partial selection is plenty fast at the fleet sizes this
        // project actually has (tens, not thousands); not worth pulling
        // in a full sort for a single threshold value.
        var minCount = Mathf.Min(trafficActiveMinimumCount, count);
        var thresholdSq = float.MaxValue;
        if (minCount > 0 && minCount < count)
        {
            var sorted = (float[])sqDistances.Clone();
            System.Array.Sort(sorted);
            thresholdSq = sorted[minCount - 1];
        }

        for (var i = 0; i < count; i++)
        {
            var car = _trafficCars[i];
            if (car == null) continue;
            car.SetNearCamera(sqDistances[i] <= activeSq || sqDistances[i] <= thresholdSq);
        }
    }

    private HashSet<HexCoord> _waterSet;
    private HashSet<HexCoord> _roundaboutSet;
    private HashSet<HexCoord> _arterialSet;

    /// <summary>O(1) water-hex lookup, lazily built from CityModel.Water
    /// (never changes after generation).</summary>
    public bool IsWaterHex(HexCoord hex)
    {
        if (_waterSet == null) _waterSet = new HashSet<HexCoord>(_city.Water);
        return _waterSet.Contains(hex);
    }

    /// <summary>O(1) roundabout-hex lookup (CityModel.Roundabouts) -- a
    /// junction hex rendered as a circular roundabout. TrafficCar reads
    /// this to circulate around the central island instead of driving
    /// straight through (creator direction, 2026-07: "Cars must follow
    /// the curve proper curves of the road").</summary>
    public bool IsRoundabout(HexCoord hex)
    {
        if (_roundaboutSet == null) _roundaboutSet = new HashSet<HexCoord>(_city.Roundabouts);
        return _roundaboutSet.Contains(hex);
    }

    /// <summary>O(1) arterial-hex lookup (CityModel.ArterialRoads),
    /// lazily built the same way IsWaterHex/IsRoundabout already are --
    /// `CityModel.ArterialRoads` is only an `IReadOnlyList`, not something
    /// safe to `.Contains()` directly every call. TrafficCar.ParkHere
    /// reads this so a dynamically-parked car's curb offset scales with
    /// the SAME road width RoadDresser's own dressing already uses
    /// (creator report, 2026-07: cars parking mid-lane on the wide
    /// arterial, because ParkHere's old curb offset was a flat constant
    /// only ever tuned for the 7.5m residential width).</summary>
    public bool IsArterial(HexCoord hex)
    {
        if (_arterialSet == null) _arterialSet = new HashSet<HexCoord>(_city.ArterialRoads);
        return _arterialSet.Contains(hex);
    }

    /// <summary>The circulating-lane radius traffic follows around a
    /// roundabout island -- kept in sync with RoadDresser's ring so cars
    /// drive on the asphalt, not over the curb or off the edge.</summary>
    public const float RoundaboutLaneRadius = 7.4f;

    /// <summary>Distance to the nearest thing (traffic car, parked-car
    /// dressing prop, tank, or citizen) sitting AHEAD of `pos` along
    /// `dir` and within `laneHalfWidth` of that line, up to `maxRange`;
    /// `maxRange` if the lane is clear. A car reads this to keep a
    /// following gap (creator direction, 2026-07: "slow down if there is
    /// a human, car, tank something in front of them") and, via the same
    /// check on the opposite-lane offset, to decide whether it's safe to
    /// pass. Static parked-car dressing added 2026-07 (creator report:
    /// "driving through parked cars") -- see `_parkedObstacles`' own
    /// field comment for why it wasn't already covered. O(fleet +
    /// parked-dressing-count) per call -- fine at the scale this project
    /// runs.</summary>
    public float DistanceAhead(Vector3 pos, Vector3 dir, float maxRange, float laneHalfWidth, TrafficCar self)
    {
        var best = maxRange;
        void Consider(Vector3 p)
        {
            var to = p - pos;
            to.y = 0f;
            var along = Vector3.Dot(to, dir);
            if (along <= 0.2f || along >= best) return;   // behind me, or farther than the current nearest
            var lateral = (to - dir * along).magnitude;
            if (lateral > laneHalfWidth) return;          // not in my lane
            best = along;
        }
        foreach (var c in _trafficCars) if (c != null && c != self) Consider(c.transform.position);
        foreach (var p in _parkedObstacles) if (p != null) Consider(p.position);
        foreach (var t in _tanks) if (t != null && t.Combat != null && t.Combat.Alive) Consider(t.transform.position);
        foreach (var z in _citizens) if (z != null) Consider(z.transform.position);
        return best;
    }

    /// <summary>How deep the water sits above the carved bed at a hex's
    /// centre -- TerrainField.WaterLevel minus the actual terrain height
    /// there. Continuous, not a flat per-hex value: TerrainField blends
    /// height by inverse distance, so this reads shallow near a bank and
    /// deep mid-channel, same curve the visible shoreline follows. 0 for
    /// a non-water hex. Tanks use this to decide whether a crossing is
    /// fordable (see Tank.cs).</summary>
    public float WaterDepthAt(HexCoord hex)
    {
        if (!IsWaterHex(hex)) return 0f;
        return TerrainField.WaterLevel - GroundHeightAt(WorldOf(hex));
    }

    /// <summary>A road hex counts as a pedestrian-legal crossing point
    /// ("corner") if it ISN'T a plain straight mid-block segment -- i.e.
    /// its road-neighbors aren't just two hexes roughly opposite each
    /// other. A junction (3+ road neighbors), a bend (2 neighbors that
    /// aren't opposite), or a dead end (0-1) all count; only a genuine
    /// through-stretch of street doesn't. Citizens use this to cross at
    /// corners instead of jaywalking mid-block (see Citizen.cs).</summary>
    public bool IsRoadCorner(HexCoord hex)
    {
        var roads = RoadNetworkHexes();
        var here = WorldOf(hex);
        var roadNeighbors = 0;
        Vector3? firstDir = null;
        foreach (var n in hex.Neighbors())
        {
            if (!roads.Contains(n)) continue;
            roadNeighbors++;
            var dir = (WorldOf(n) - here).normalized;
            if (firstDir == null) { firstDir = dir; continue; }
            if (Vector3.Dot(firstDir.Value, dir) > -0.5f) return true; // not roughly opposite -> a bend/junction
        }
        return roadNeighbors <= 1; // dead end (or isolated): also crossable, only a straight run isn't
    }

    private HashSet<HexCoord> _sidewalkSet;

    /// <summary>Sidewalk hexes: an in-city, non-road, non-water,
    /// non-building hex that BORDERS a road -- the walkable strip along a
    /// block's street frontage. Citizens live on these (creator
    /// direction, 2026-07: pedestrians "MUST stay on Sidewalk unless
    /// they are crossing the road or avoiding monsters"). Built once and
    /// cached; the road/building layout is fixed after generation (only
    /// damage changes passability, which doesn't create sidewalks).</summary>
    public bool IsSidewalkHex(HexCoord hex)
    {
        if (_sidewalkSet == null) BuildSidewalkSet();
        return _sidewalkSet.Contains(hex);
    }

    private void BuildSidewalkSet()
    {
        _sidewalkSet = new HashSet<HexCoord>();
        var roads = RoadNetworkHexes();
        var blockedGround = BlockedFor(false);   // buildings + water
        foreach (var hex in roads)
        {
            foreach (var n in hex.Neighbors())
            {
                if (roads.Contains(n)) continue;              // the road itself, not a sidewalk
                if (!_city.Contains(n)) continue;             // off-map
                if (blockedGround.Contains(n)) continue;      // building footprint or water
                _sidewalkSet.Add(n);
            }
        }
    }

    /// <summary>A random sidewalk hex within `radius` hexes of `near`,
    /// for a citizen to head toward -- a real destination instead of an
    /// aimless wander (creator direction, 2026-07). Falls back to any
    /// sidewalk hex, then to `near` itself, so it never returns
    /// off-sidewalk.</summary>
    public HexCoord RandomSidewalkNear(HexCoord near, int radius, int salt)
    {
        if (_sidewalkSet == null) BuildSidewalkSet();
        if (_sidewalkSet.Count == 0) return near;
        HexCoord best = near;
        var bestScore = -1;
        var i = 0;
        foreach (var s in _sidewalkSet)
        {
            i++;
            if (s.DistanceTo(near) > radius) continue;
            // deterministic-ish pick: hash each candidate, keep the top
            var score = unchecked((s.Q * 73856093) ^ (s.R * 19349663) ^ (salt * 83492791)) & 0x7FFFFFFF;
            if (score > bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    /// <summary>CityGizmo.PresetChoice -> RuntimeCityBuilder.PresetChoice.
    /// The two enums are distinct nested types with (today) matching
    /// declaration order, but mapping by NAME here means a future reorder
    /// of either one can't silently swap presets underneath the other.</summary>
    private static PresetChoice ConvertPreset(CityGizmo.PresetChoice p)
    {
        switch (p)
        {
            case CityGizmo.PresetChoice.SmallTown: return PresetChoice.SmallTown;
            case CityGizmo.PresetChoice.BigCity: return PresetChoice.BigCity;
            default: return PresetChoice.Village;
        }
    }

    private CityPreset ResolvePreset() => ResolvePreset(preset);

    /// <summary>Static so <see cref="RegionPickerHud"/> can resolve any
    /// candidate choice for its own preview thumbnails without needing a
    /// live instance's <see cref="preset"/> field set to it first --
    /// pure function of the enum value, same as it always was, just no
    /// longer implicitly reading `this`.</summary>
    public static CityPreset ResolvePreset(PresetChoice choice)
    {
        switch (choice)
        {
            case PresetChoice.SmallTown: return CityPreset.SmallTown();
            case PresetChoice.BigCity: return CityPreset.BigCity();
            // docs/23 Phase 8: region presets (citygen-core-only when that
            // phase shipped -- this is the first Unity-side consumer of
            // any of the three).
            case PresetChoice.NewYork: return CityPreset.NewYork();
            case PresetChoice.Paris: return CityPreset.Paris();
            case PresetChoice.Montreal: return CityPreset.Montreal();
            default: return CityPreset.Village();
        }
    }

    // ---- coordinate bridge ---------------------------------------------------

    public Vector3 WorldOf(HexCoord hex)
    {
        var (x, z) = hex.ToWorld();
        return _origin + new Vector3((float)x, 0f, (float)z);
    }

    /// <summary>World position -> hex, via exact fractional-axial cube
    /// rounding (the standard algorithm; nearest-center by construction).</summary>
    public HexCoord HexAt(Vector3 world)
    {
        var local = world - _origin;
        var size = HexCoord.HexMeters / 1.7320508075688772; // hexMeters / sqrt(3)
        var fq = (0.57735026918962576 * local.x - local.z / 3.0) / size;  // (sqrt(3)/3 x - z/3) / size
        var fr = (2.0 / 3.0 * local.z) / size;

        // cube round
        var fs = -fq - fr;
        var q = System.Math.Round(fq, System.MidpointRounding.AwayFromZero);
        var r = System.Math.Round(fr, System.MidpointRounding.AwayFromZero);
        var s = System.Math.Round(fs, System.MidpointRounding.AwayFromZero);
        var dq = System.Math.Abs(q - fq);
        var dr = System.Math.Abs(r - fr);
        var ds = System.Math.Abs(s - fs);
        if (dq > dr && dq > ds) q = -r - s;
        else if (dr > ds) r = -q - s;
        return new HexCoord((int)q, (int)r);
    }

    /// <summary>Current blocked set for a movement class, cached per
    /// city version (each BlockedTo*() call walks every building).
    ///
    /// 2026-08 (creator direction: "fix that bug", following up on a
    /// report investigation that found ground units can walk straight
    /// through a Factory/HQ): `_battlefield` is built ONCE from the
    /// procedural `_city` at `BeginMatch` and only ever updated for
    /// EXISTING procedural buildings taking damage
    /// (`ApplyBuildingDamage` -&gt; `WithBuildingDamage`) -- an RTS
    /// building placed mid-match via `SimBridge`/`MatchState`
    /// (`SpawnHqForPlayer`/`SpawnFactoryForPlayer`/worker construction)
    /// was NEVER added to it, so it had zero footprint in every
    /// pathfinding/collision query this file answers (a real,
    /// architectural gap, not a narrow bug -- `SpawnStartingBases`'s own
    /// doc comment already flagged half of this: "match-core's own
    /// building-blocked set... isn't visible to Unity's own BlockedFor
    /// query," but nothing had closed the gap it named).
    ///
    /// Standing (non-`Destroyed`) `SimBuilding`s are now unioned in on
    /// top of the cached procedural set -- same "destruction reopens the
    /// hex" policy `BattlefieldState.BlockedToGround`'s own doc already
    /// states for procedural buildings, applied consistently. Kept as a
    /// SEPARATE cache layer (not merged into `_blockedGroundCache`
    /// itself) so the common no-active-match case (menus, the Lab, any
    /// caller before `BeginMatch`) stays the exact original zero-copy
    /// cached-reference return; once a match exists, a cheap signature
    /// over every `SimBuilding`'s (EntityId, State) -- NOT `BuildingCount`
    /// alone, which wouldn't change when a building merely transitions
    /// UnderConstruction -&gt; Complete -&gt; Destroyed -- decides whether the
    /// (real, but still small: a handful to dozens of bases, not
    /// hundreds) combined-set rebuild is actually needed this call.
    ///
    /// Honest scope boundary: only ground/amphibious blocking. Flight
    /// blocking (`BlockedForFlight`) and the footprint-overhang/roof-
    /// height systems (`InsideBuildingFootprint`, `_roofCache`) are
    /// UNTOUCHED -- flyers still cruise over RTS buildings exactly as
    /// before, and TickSettle's corner-overhang check still only reasons
    /// about procedural buildings. Extending either is a real, separate,
    /// larger design question (does a flyer treat an RTS building's roof
    /// like any other perchable roof? unanswered), not silently folded in
    /// here.</summary>
    public HashSet<HexCoord> BlockedFor(bool amphibious)
    {
        if (_blockedCacheVersion != _cityVersion)
        {
            _blockedGroundCache = _battlefield.BlockedToGround();
            _blockedAmphibiousCache = _battlefield.BlockedToAmphibious();
            _blockedCacheVersion = _cityVersion;
        }
        if (_simBridge == null || !_simBridge.HasMatch)
            return amphibious ? _blockedAmphibiousCache : _blockedGroundCache;

        var sig = SimBuildingBlockedSignature();
        if (_blockedSimCacheVersion != _cityVersion || _blockedSimSignature != sig)
        {
            _blockedGroundWithSimCache = new HashSet<HexCoord>(_blockedGroundCache);
            _blockedAmphibiousWithSimCache = new HashSet<HexCoord>(_blockedAmphibiousCache);
            for (var i = 0; i < _simBridge.BuildingCount; i++)
            {
                var b = _simBridge.BuildingAt(i);
                if (b.State == BuildingState.Destroyed) continue;
                _blockedGroundWithSimCache.Add(b.Hex);
                _blockedAmphibiousWithSimCache.Add(b.Hex);
            }
            _blockedSimCacheVersion = _cityVersion;
            _blockedSimSignature = sig;
        }
        return amphibious ? _blockedAmphibiousWithSimCache : _blockedGroundWithSimCache;
    }

    /// <summary>Cheap change-detector for <see cref="BlockedFor"/>'s sim-
    /// building union: folds every building's (EntityId, State) into one
    /// running value so a construction completing, a building being
    /// destroyed, or a brand-new one appearing all change the result --
    /// `BuildingCount` alone would miss the first two (the count doesn't
    /// change when a building merely changes STATE).</summary>
    private long SimBuildingBlockedSignature()
    {
        long sig = _simBridge.BuildingCount;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            sig = sig * 1000003L + b.EntityId * 3L + (int)b.State;
        }
        return sig;
    }

    // docs/25 Phase D: IHexObstacleQuery, the narrow slice DeadlockManager
    // needs to pick a sidestep hex. IsBlocked deliberately always uses the
    // GROUND (non-amphibious) blocked set -- a conservative choice so a
    // sidestep target is never water, regardless of which creature is
    // actually being asked to yield.
    public bool CityContains(HexCoord hex) { return _city.Contains(hex); }
    public bool IsBlocked(HexCoord hex) { return BlockedFor(false).Contains(hex); }

    /// <summary>Building height by tier -- the SAME numbers BuildBuildings
    /// renders with, so a flyer's "can I clear this roof" math can never
    /// drift from what's actually on screen.</summary>
    private static float HeightForTier(BuildingTier tier)
    {
        switch (tier)
        {
            case BuildingTier.Medium: return 12f;
            case BuildingTier.Large: return 30f;
            case BuildingTier.Landmark: return 40f;
            default: return 6f;
        }
    }

    /// <summary>Blocked hexes for a WINGED unit cruising at `clearAltitude`
    /// -- only buildings TALLER than that altitude actually block (water
    /// never blocks flight, same as amphibious ground movement); a short
    /// building simply gets flown over. Not cached like BlockedFor since
    /// it varies continuously by altitude rather than a fixed handful of
    /// movement classes, and it's only ever called at path-compute time
    /// (new orders, re-paths on city change), never per frame.</summary>
    public HashSet<HexCoord> BlockedForFlight(float clearAltitude)
    {
        var blocked = new HashSet<HexCoord>();
        foreach (var b in _battlefield.Buildings)
        {
            if (!b.BlocksMovement) continue;
            if (HeightForTier(b.Building.Tier) <= clearAltitude) continue;
            foreach (var hex in b.Building.Footprint) blocked.Add(hex);
        }
        return blocked;
    }

    /// <summary>Rendered height of this building right now -- the roof a
    /// winged unit perches on. Tier table the visuals use, MAX'd against
    /// any registered roof-landing override (2026-07 bug fix, see
    /// `_roofHeightOverrides`'s doc comment) so a gable/setback building's
    /// approach-altitude decision (`TickPerch`'s low/high cruise-tier
    /// pick) sees the same real roof height `SurfaceHeightAt` will
    /// eventually land the creature on, not the flatter tier number.</summary>
    public float BuildingHeight(Building building)
    {
        var height = HeightForTier(building.Tier);
        foreach (var hex in building.Footprint)
        {
            float overrideHeight;
            if (_roofHeightOverrides.TryGetValue(hex, out overrideHeight) && overrideHeight > height)
                height = overrideHeight;
        }
        return height;
    }

    private Dictionary<HexCoord, float> _roofCache;
    private int _roofCacheVersion = -1;

    /// <summary>Bug fix (2026-07, creator report): a flyer couldn't land on
    /// an A-frame/gable roof (`BuildingDresser.DressSmall`'s pitched-roof
    /// pick, Small tier) or the stacked deco setback every Large-tier
    /// "high-rise" gets (`DressOffice`) -- both are real, contiguous roof
    /// MASSING drawn on top of `HeightForTier`'s flat per-tier cube, and
    /// nothing fed their extra height back into the landing-height table,
    /// so a perch settled the creature down at the flat tier height,
    /// visually below/inside that geometry. `BuildingDresser` -- the only
    /// code that knows which specific hex got which roof shape (a
    /// per-hex hash, not stored anywhere else) -- registers the actual
    /// solid-roof height here as it dresses each hex; deliberately NOT
    /// used for the small decorative "rooftop kit" (water towers, vents,
    /// antennas, chimneys, signage, landmark set pieces) -- those stay
    /// colliderless clutter, exactly as the creator asked.</summary>
    private readonly Dictionary<HexCoord, float> _roofHeightOverrides = new Dictionary<HexCoord, float>();

    /// <summary>Register hex's actual landable roof height, if it's taller
    /// than the flat per-tier massing height -- called once per hex by
    /// `BuildingDresser` while dressing (never overwritten downward: a
    /// multi-hex building's hex is only ever registered once, but `Max`
    /// keeps this safe regardless of call order). Reset naturally on the
    /// next `BuildBuildings()` full rebuild along with everything else
    /// this class owns; a since-destroyed building's hex is excluded by
    /// `EnsureRoofCache`'s own `BlocksMovement` check below, same as the
    /// flat-tier case, so a rubble pancake correctly ignores whatever was
    /// registered for the roof that no longer exists.</summary>
    public void RegisterRoofLandingHeight(HexCoord hex, float landableHeight)
    {
        float existing;
        _roofHeightOverrides[hex] = _roofHeightOverrides.TryGetValue(hex, out existing)
            ? Mathf.Max(existing, landableHeight)
            : landableHeight;
    }

    // ---- roof "parking": same distribution discipline as ground formations
    // (FormationHexes/AssignFormation), extended to rooftops (creator
    // direction, 2026-07: "Same parking, distributions rules should apply
    // to roof features. If there is not enough space... it should pick a
    // different roof nearby before landing.") ------------------------------

    /// <summary>How many flyers this building's roof can hold at once --
    /// one perch slot per footprint hex, the same "one parking slot per
    /// unit" idea `FormationHexes` uses for ground destinations, just
    /// keyed to the roof's own area instead of an open hex neighbourhood
    /// (a roof has no neighbourhood to search outward into -- it IS the
    /// footprint).</summary>
    public int RoofCapacity(Building building)
    {
        return building.Footprint.Count;
    }

    /// <summary>This building's footprint hexes that are NOT currently
    /// occupied by an already-perched flyer, nearest-to-the-building-
    /// origin-hex first (a stable, deterministic order -- callers assign
    /// nearest-unit-to-nearest-slot on top of this). Occupancy is read
    /// live off <see cref="Monsters"/> (`MonsterAgent.PerchedOn`), not a
    /// separate counter, so it can never drift from what's actually
    /// standing up there.</summary>
    public List<HexCoord> AvailableRoofSlots(Building building)
    {
        var occupied = new HashSet<HexCoord>();
        foreach (var m in _monsters)
        {
            if (m == null) continue;
            var on = m.PerchedOn;
            if (on == null || !ReferenceEquals(on, building)) continue;
            occupied.Add(HexAt(m.transform.position));
        }
        var free = new List<HexCoord>();
        foreach (var hex in building.Footprint)
            if (!occupied.Contains(hex)) free.Add(hex);
        return free;
    }

    /// <summary>The nearest OTHER standing building (to `preferred`'s own
    /// footprint) with at least `neededSlots` free roof slots right now --
    /// "pick a different roof nearby" when the clicked one is full.
    /// `exclude` skips buildings already tried this order (so a caller
    /// walking overflow through several candidates in one click never
    /// loops back to one it already rejected). Returns null if nothing
    /// standing nearby has room -- the caller decides what "give up"
    /// means (docs precedent: `FormationHexes` pads onto the original
    /// spot rather than leaving a unit with no order at all).</summary>
    public Building FindNearbyPerchableBuilding(Building preferred, int neededSlots, HashSet<Building> exclude)
    {
        if (preferred == null || preferred.Footprint.Count == 0) return null;
        var origin = preferred.Footprint[0];

        Building best = null;
        var bestDist = int.MaxValue;
        foreach (var state in _battlefield.Buildings)
        {
            if (!state.BlocksMovement) continue;   // destroyed/rubble -- no roof to land on
            var candidate = state.Building;
            if (ReferenceEquals(candidate, preferred)) continue;
            if (exclude != null && exclude.Contains(candidate)) continue;
            if (candidate.Footprint.Count == 0) continue;
            if (AvailableRoofSlots(candidate).Count < neededSlots) continue;

            var dist = origin.DistanceTo(candidate.Footprint[0]);
            if (dist < bestDist) { bestDist = dist; best = candidate; }
        }
        return best;
    }

    /// <summary>Rebuilds `_roofCache` (standing-building footprint hex ->
    /// roof height) if the city has changed since the last build. Shared by
    /// `SurfaceHeightAt` and `InsideBuildingFootprint` -- both need exactly
    /// "which hexes currently have a standing building on them," just for
    /// different questions (how tall / does this point land inside).
    /// Folds in `_roofHeightOverrides` (see its own doc comment) so a
    /// flyer's landing height matches whatever solid roof massing is
    /// actually on screen, not just the flat per-tier number.</summary>
    private void EnsureRoofCache()
    {
        if (_roofCacheVersion == _cityVersion && _roofCache != null) return;
        _roofCache = new Dictionary<HexCoord, float>();
        foreach (var b in _battlefield.Buildings)
        {
            if (!b.BlocksMovement) continue;
            var h = HeightForTier(b.Building.Tier);
            foreach (var hex in b.Building.Footprint)
            {
                float overrideHeight;
                _roofCache[hex] = _roofHeightOverrides.TryGetValue(hex, out overrideHeight)
                    ? Mathf.Max(h, overrideHeight)
                    : h;
            }
        }
        _roofCacheVersion = _cityVersion;
    }

    /// <summary>The standing surface at a world position: a STANDING
    /// building's roof height on its footprint hexes, 0 (street level)
    /// everywhere else -- including on rubble, so a perch whose building
    /// gets destroyed under it eases back down to the ground. Cached per
    /// city version; called per idle flyer per frame, so it has to be a
    /// dictionary hit, not a building-list walk.</summary>
    public float SurfaceHeightAt(Vector3 worldPos)
    {
        EnsureRoofCache();
        float height;
        return _roofCache.TryGetValue(HexAt(worldPos), out height) ? height : 0f;
    }

    // Half the square footprint SpawnCube actually renders a building
    // cube at (localScale.x/z = HexCoord.HexMeters * 0.9, axis-aligned, no
    // rotation) -- NOT the hex's own inradius. A hex's circumradius
    // (~11.55m) is smaller than this cube's half-diagonal (~12.73m), so a
    // building's rendered corners poke a bit past its own hex boundary
    // into a neighbouring hex's space. Anything treating "this hex isn't
    // in the blocked set" as "this exact point is clear" can be fooled by
    // that overhang -- see InsideBuildingFootprint's own header for where
    // this bit a real call site.
    private const float BuildingFootprintHalfExtent = (float)(HexCoord.HexMeters * 0.9) / 2f;

    /// <summary>True if `worldPos` falls inside any standing building's
    /// ACTUAL rendered footprint -- checks the candidate's own hex plus its
    /// six neighbours (the only hexes close enough for a building cube to
    /// reach, per `BuildingFootprintHalfExtent`'s comment), not a hex-set
    /// membership test alone. Exists because docs/12 (2026-07) traced a
    /// real bug to exactly this gap: `MonsterAgent.TickSettle`'s per-step
    /// "is the next step's hex blocked" check let a unit creeping toward a
    /// group ring-settle target (its radius grows with group size, with no
    /// upper bound tied to the city layout) walk into a building's
    /// corner overhang without the step's own hex ever being flagged
    /// blocked. Cheap (<=7 dictionary lookups), meant for occasional
    /// spot-picking/step-validation call sites, not a per-frame-per-unit
    /// hot path.
    ///
    /// `exclude` (2026-08, creator report "monster went into the factory
    /// and never left it"): the ONE hex this check should never flag,
    /// even if a building genuinely covers it -- for a unit that's
    /// already standing there. Without it, a clone spawned directly on
    /// the Factory's own hex (`GrabCursor.CloneOnto`'s "it visibly comes
    /// out of the building that made it") or a roof occupant evicted back
    /// onto that same hex (`MonsterAgent.BootFromRoof`) fails THIS
    /// footprint check on its own first settle-creep step: `next` is only
    /// centimetres from where it's already standing, still well inside
    /// the SAME building's `BuildingFootprintHalfExtent` square it just
    /// spawned in, so `TickSettle` reads its own spawn point as "walked
    /// into a building" and nulls the settle target before a single step
    /// ever lands -- the unit is stranded exactly on/inside the Factory's
    /// rendered footprint forever, matching the report exactly. Excluding
    /// the unit's own current hex only forgives THAT specific hex for
    /// THIS step; every other building (including this one's OTHER
    /// footprint hexes, and its neighbour-overhang reach from elsewhere)
    /// is still checked exactly as before, so a real walk-into-a-building
    /// step from open ground is still caught.</summary>
    public bool InsideBuildingFootprint(Vector3 worldPos, HexCoord? exclude = null)
    {
        EnsureRoofCache();
        var hex = HexAt(worldPos);
        if (hex != exclude && BuildingCovers(hex, worldPos)) return true;
        foreach (var n in hex.Neighbors())
            if (n != exclude && BuildingCovers(n, worldPos)) return true;
        return false;
    }

    private bool BuildingCovers(HexCoord hex, Vector3 worldPos)
    {
        if (!_roofCache.ContainsKey(hex)) return false;
        var c = WorldOf(hex);
        return Mathf.Abs(worldPos.x - c.x) <= BuildingFootprintHalfExtent
            && Mathf.Abs(worldPos.z - c.z) <= BuildingFootprintHalfExtent;
    }

    // ---- terrain ---------------------------------------------------------------

    /// <summary>Ground elevation at a world position -- the sculpted
    /// miniature-set surface (docs/21): 0 under every building plot,
    /// road, and bridge (the flat-lock rule that keeps roof heights and
    /// flight math intact), rolling on open ground, mounded on the
    /// generator's ridge hexes, carved below zero in river/pond beds.
    /// Units terrain-follow this each frame.</summary>
    public float GroundHeightAt(Vector3 world)
    {
        return _terrain != null ? _terrain.HeightAt(world) : 0f;
    }

    // ---- static city geometry --------------------------------------------------

    private void BuildGround()
    {
        // The CLICK surface stays a flat invisible plane at y=0 (ground
        // right-clicks, docs/21 accepted tradeoff: <=3m hills skew a
        // click by well under half a hex). The VISIBLE ground is the
        // sculpted mesh below.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GroundClickPlane";
        ground.transform.SetParent(transform, false);
        var center = WorldOf(_city.CenterHex);
        ground.transform.position = new Vector3(center.x, 0f, center.z);
        // a unity plane is 10x10 at scale 1; cover the map with margin
        var w = _city.WidthHexes * (float)HexCoord.HexMeters / 10f * 1.3f;
        var h = _city.HeightHexes * (float)HexCoord.HexMeters / 10f * 1.3f;
        ground.transform.localScale = new Vector3(w, 1f, h);
        var planeRenderer = ground.GetComponent<Renderer>();
        if (planeRenderer != null) Object.Destroy(planeRenderer);

        BuildTerrainMesh();
    }

    /// <summary>The sculpted miniature-table surface: chunked grid meshes
    /// sampling TerrainField. Resolution auto-scales so big maps stay
    /// within a sane vertex budget (docs/21: BigCity trades detail for
    /// scale). One shared material -- SRP-batcher friendly.</summary>
    private void BuildTerrainMesh()
    {
        var parent = new GameObject("TerrainMesh").transform;
        parent.SetParent(transform, false);
        var grass = NewMaterial(new Color(0.42f, 0.47f, 0.36f));

        var hexM = (float)HexCoord.HexMeters;
        var mapW = _city.WidthHexes * hexM * 1.15f;
        var mapH = _city.HeightHexes * hexM * 1.15f;
        var center = WorldOf(_city.CenterHex);
        var minX = center.x - mapW / 2f;
        var minZ = center.z - mapH / 2f;

        // quad edge: fine enough to show hex-scale banks on the normal
        // presets, coarsening on huge maps to hold the vertex budget
        var quad = Mathf.Max(hexM / 3f, Mathf.Max(mapW, mapH) / 220f);
        const int chunkQuads = 48;
        var chunkSize = chunkQuads * quad;
        var chunksX = Mathf.CeilToInt(mapW / chunkSize);
        var chunksZ = Mathf.CeilToInt(mapH / chunkSize);

        for (var cz = 0; cz < chunksZ; cz++)
            for (var cx = 0; cx < chunksX; cx++)
            {
                var go = new GameObject("Chunk_" + cx + "_" + cz);
                go.transform.SetParent(parent, false);
                var mesh = new Mesh();
                var verts = new Vector3[(chunkQuads + 1) * (chunkQuads + 1)];
                var tris = new int[chunkQuads * chunkQuads * 6];
                var ox = minX + cx * chunkSize;
                var oz = minZ + cz * chunkSize;
                for (var j = 0; j <= chunkQuads; j++)
                    for (var i = 0; i <= chunkQuads; i++)
                    {
                        var p = new Vector3(ox + i * quad, 0f, oz + j * quad);
                        p.y = _terrain.HeightAt(p);
                        verts[j * (chunkQuads + 1) + i] = p;
                    }
                var t = 0;
                for (var j = 0; j < chunkQuads; j++)
                    for (var i = 0; i < chunkQuads; i++)
                    {
                        var v0 = j * (chunkQuads + 1) + i;
                        var v1 = v0 + 1;
                        var v2 = v0 + chunkQuads + 1;
                        var v3 = v2 + 1;
                        tris[t++] = v0; tris[t++] = v2; tris[t++] = v1;
                        tris[t++] = v1; tris[t++] = v2; tris[t++] = v3;
                    }
                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = grass;
            }
    }

    /// <summary>The miniature-set border (docs/21 batch 2, item 8): a
    /// raised wooden table rim just past the sculpted terrain, and a
    /// painted flat-color backdrop ring further out, so the map reads as
    /// a diorama on a table rather than trailing off into the void at its
    /// edge. Purely decorative -- outside every gameplay hex range.</summary>
    private void BuildTableEdge()
    {
        var host = new GameObject("TableEdge").transform;
        host.SetParent(transform, false);

        var hexM = (float)HexCoord.HexMeters;
        var mapW = _city.WidthHexes * hexM * 1.15f;
        var mapH = _city.HeightHexes * hexM * 1.15f;
        var center = WorldOf(_city.CenterHex);
        var wood = NewMaterial(new Color(0.36f, 0.24f, 0.14f));
        var sky = NewMaterial(new Color(0.62f, 0.75f, 0.86f));

        const float rimThickness = 6f;
        const float rimHeight = 1.6f;
        var rimY = rimHeight * 0.5f;
        var outerW = mapW + rimThickness * 2f;
        var outerH = mapH + rimThickness * 2f;

        SpawnEdgeBar(host, wood, new Vector3(center.x, rimY, center.z - mapH / 2f - rimThickness / 2f), new Vector3(outerW, rimHeight, rimThickness));
        SpawnEdgeBar(host, wood, new Vector3(center.x, rimY, center.z + mapH / 2f + rimThickness / 2f), new Vector3(outerW, rimHeight, rimThickness));
        SpawnEdgeBar(host, wood, new Vector3(center.x - mapW / 2f - rimThickness / 2f, rimY, center.z), new Vector3(rimThickness, rimHeight, mapH));
        SpawnEdgeBar(host, wood, new Vector3(center.x + mapW / 2f + rimThickness / 2f, rimY, center.z), new Vector3(rimThickness, rimHeight, mapH));

        // painted backdrop: tall inward-facing walls well past the rim, a
        // flat "sky" standing in for a skybox so the table doesn't trail
        // off into empty space at the RTS camera's typical framing
        const float backdropHeight = 140f;
        const float backdropDistance = 60f;
        var by = backdropHeight * 0.5f;
        var bw = outerW + backdropDistance * 2f;
        var bh = outerH + backdropDistance * 2f;
        SpawnEdgeBar(host, sky, new Vector3(center.x, by, center.z - bh / 2f), new Vector3(bw, backdropHeight, 1f));
        SpawnEdgeBar(host, sky, new Vector3(center.x, by, center.z + bh / 2f), new Vector3(bw, backdropHeight, 1f));
        SpawnEdgeBar(host, sky, new Vector3(center.x - bw / 2f, by, center.z), new Vector3(1f, backdropHeight, bh));
        SpawnEdgeBar(host, sky, new Vector3(center.x + bw / 2f, by, center.z), new Vector3(1f, backdropHeight, bh));
    }

    private static void SpawnEdgeBar(Transform parent, Material mat, Vector3 pos, Vector3 scale)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent, false);
        cube.transform.position = pos;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = mat;
        var collider = cube.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
    }

    private void BuildTerrainAndRoads()
    {
        var terrain = new GameObject("Terrain").transform;
        terrain.SetParent(transform, false);

        // water: one continuous, gently flowing surface -- NOT a grid of
        // painted blue tiles. A translucent sheet welded across whole water
        // bodies (river and each pond) rides at TerrainField.WaterLevel and
        // undulates (WaterSurface); a dark murky bed sheet sits just above
        // the carved riverbed so real depth reads down THROUGH the surface.
        BuildWater(terrain);

        // ridges are now SCULPTED by the terrain mesh; what they get here
        // is the miniature-set read: model-railroad puffball trees
        ScatterVegetation(terrain);

        // roads: the connected 1950s street network (RoadDresser draws
        // pads + connector strips + sidewalks + furniture + railyard
        // siding near a rail_depot landmark, if this preset has one)
        var roadsHost = RoadDresser.Build(this, _city, terrain, _railyardCenter);
        CombineStaticRoadSurfaces(roadsHost);
    }

    /// <summary>Tier 0 of the graphics-upgrade plan (docs/12, 2026-08):
    /// the project's own diagnosis found zero static batching, mesh
    /// combining, or LOD anywhere in the city-building path, at BigCity
    /// scale (~490k renderers). Building dressing can't safely take this
    /// treatment -- ApplyBuildingDamage's Damaged/Destroyed transitions
    /// rewrite every dressing renderer's transform and material in place,
    /// which static batching (geometry baked into a combined buffer at
    /// combine time) cannot tolerate. Road FURNITURE can't either --
    /// RoadDresser wraps every knockable prop (parked cars, hydrants,
    /// poles) in a "Knockable" holder with a <see cref="KnockableProp"/>
    /// that physically tips it at runtime, same problem.
    ///
    /// What's left over -- and what this combines -- is the raw road
    /// SURFACE: pads, connector strips, sidewalks, curbs, center dashes,
    /// crosswalk stripes. None of it is ever destroyed, moved, or
    /// reparented after RoadDresser.Build returns (confirmed: no
    /// Object.Destroy call anywhere in RoadDresser.cs, and it's
    /// deliberately colliderless per that file's own header comment), so
    /// it's genuinely permanent for the life of the city. Detected by
    /// absence of a KnockableProp ancestor rather than by threading a
    /// separate "is furniture" flag through RoadDresser's many draw
    /// call sites -- every furniture piece already funnels through
    /// KnockHolder, so this is a correct filter without touching that
    /// file at all.</summary>
    private void CombineStaticRoadSurfaces(Transform roadsHost)
    {
        if (roadsHost == null) return;
        var surfaceObjects = new List<GameObject>();
        foreach (var renderer in roadsHost.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<KnockableProp>() != null) continue;
            renderer.gameObject.isStatic = true;
            surfaceObjects.Add(renderer.gameObject);
        }
        if (surfaceObjects.Count > 0)
            StaticBatchingUtility.Combine(surfaceObjects.ToArray(), roadsHost.gameObject);
    }

    /// <summary>Model-railroad vegetation, deterministically scattered:
    /// tree clusters on ridge hexes (the high ground should read green
    /// and bumpy from the RTS camera), single trees rarely on open
    /// grass, bushes hugging pond/river shores.</summary>
    private void ScatterVegetation(Transform parent)
    {
        var trunk = NewMaterial(new Color(0.36f, 0.27f, 0.18f));
        var canopy = NewMaterial(new Color(0.30f, 0.44f, 0.22f));
        var bush = NewMaterial(new Color(0.36f, 0.5f, 0.28f));
        var rock = NewMaterial(new Color(0.55f, 0.53f, 0.5f));
        var lilyPad = NewMaterial(new Color(0.22f, 0.42f, 0.24f));
        var reedStem = NewMaterial(new Color(0.32f, 0.42f, 0.22f));
        var reedHead = NewMaterial(new Color(0.36f, 0.23f, 0.15f));

        var water = new HashSet<HexCoord>(_city.Water);
        var blocked = BlockedFor(false);
        var ponds = PondHexes();

        foreach (var hex in _city.Ridges)
        {
            // CityModel.Ridges is only filtered against roads/water in
            // the generator ("Ridges never coincide with roads or
            // water") -- buildings are placed in a LATER pass that
            // treats ridge hexes as ordinary buildable open land, so a
            // ridge hex can end up with a building footprint on it
            // (TerrainField correctly flat-locks that hex to 0, buildings
            // win over the mound -- but this tree pass never checked, so
            // it kept sprouting trees through the building standing there)
            if (blocked.Contains(hex)) continue;
            var n = 2 + Mod(hex.Q * 31 + hex.R * 17, 2);
            for (var i = 0; i < n; i++)
                SpawnTree(hex, i, trunk, canopy, parent);
            // occasional rock outcrops break up an all-trees hillside
            if (Mod(hex.Q * 41 + hex.R * 19, 4) == 0)
                SpawnRocks(hex, rock, parent);
        }

        foreach (var hex in _city.Water)
        {
            var isPond = ponds.Contains(hex);

            // still water grows lily pads on its own surface; the river
            // (the map-spanning connected component, see PondHexes) stays
            // clear -- flowing water doesn't carry pads
            if (isPond && Mod(hex.Q * 17 + hex.R * 37, 3) != 0)
                SpawnLilyPads(hex, lilyPad, parent);

            // shoreline dressing: cattail reeds ringing a pond, the
            // existing bushes along a river bank -- the two water bodies
            // read differently at their edges, not just on the water
            foreach (var nb in hex.Neighbors())
            {
                if (!_city.Contains(nb) || water.Contains(nb) || blocked.Contains(nb)) continue;
                if (isPond)
                {
                    if (Mod(nb.Q * 31 + nb.R * 11, 5) == 0) SpawnCattails(nb, reedStem, reedHead, parent);
                    continue;
                }
                if (Mod(nb.Q * 53 + nb.R * 29, 7) != 0) continue;
                var w = WorldOf(nb);
                var off = new Vector3(Mod(nb.Q * 13 + nb.R * 7, 9) - 4f, 0f, Mod(nb.Q * 5 + nb.R * 23, 9) - 4f);
                var p = w + off;
                p.y = GroundHeightAt(p);
                var b = SpawnPrim(PrimitiveType.Sphere, p + Vector3.up * 0.5f,
                    new Vector3(1.6f, 1.0f, 1.6f), bush, parent);
                b.name = "Bush";
            }
        }
    }

    /// <summary>The battlefield's water: ONE subdivided translucent sheet
    /// resting at TerrainField.WaterLevel over the whole water region,
    /// animated by WaterSurface, over a dark murky bed hugging the carved
    /// riverbed. The waterline is not drawn at all -- it EMERGES: the
    /// terrain mesh is opaque and sits above the waterline everywhere
    /// except the carved beds and their blended banks, so the depth test
    /// hides every submerged part of the sheet and the visible edge is
    /// exactly where each bank crosses the waterline. That contour follows
    /// TerrainField's smoothed noise, so the shore is an organic curve --
    /// no hex outline, no tiles. Depth reads as a gradient for free:
    /// near the shore the sheet is tinted over the bank's own sunken
    /// grass (light shallows), mid-river it's over the dark bed (deeps).
    /// Colliderless; pathing still reads the hex set, never the visuals.</summary>
    private void BuildWater(Transform parent)
    {
        if (_city.Water == null) return;

        // bridge/road/building hexes are flat-locked to 0 by TerrainField,
        // so the sheet is depth-hidden under them automatically; the bed
        // skips them so no dark slab pokes up through a bridge deck
        var flat = new HashSet<HexCoord>();
        foreach (var h in _city.Roads) flat.Add(h);
        foreach (var b in _city.Buildings) foreach (var h in b.Footprint) flat.Add(h);
        foreach (var br in _city.Bridges) foreach (var h in br.Footprint) flat.Add(h);

        var wet = new List<HexCoord>();
        foreach (var hex in _city.Water) if (!flat.Contains(hex)) wet.Add(hex);
        if (wet.Count == 0) return;

        var bedMat = NewMaterial(new Color(0.05f, 0.11f, 0.12f));
        var bedMesh = HexFanMesh(wet, p => _terrain.HeightAt(p) + 0.05f);
        var bedGo = new GameObject("WaterBed");
        bedGo.transform.SetParent(parent, false);
        bedGo.AddComponent<MeshFilter>().sharedMesh = bedMesh;
        bedGo.AddComponent<MeshRenderer>().sharedMaterial = bedMat;

        // translucent and glossy: the murky bed and sunken banks showing
        // through ARE the depth cue, and high smoothness lets the waves'
        // recomputed normals roll light glints across the surface
        var surfaceMat = NewMaterial(new Color(0.16f, 0.38f, 0.45f, 0.6f));
        if (surfaceMat.HasProperty("_Smoothness")) surfaceMat.SetFloat("_Smoothness", 0.92f);
        else if (surfaceMat.HasProperty("_Glossiness")) surfaceMat.SetFloat("_Glossiness", 0.92f);
        LabMeshBuilder.MakeTransparent(surfaceMat);
        BuildWaterSheet(wet, parent, surfaceMat);
    }

    /// <summary>The animated surface: a regular grid over the water
    /// region's bounds (padded a hex and a half so it always reaches past
    /// the waterline into the banks, where the depth test trims it). Grid
    /// spacing stays well under WaterSurface's wavelength so the travelling
    /// waves are properly sampled, coarsening only on huge maps to hold
    /// the vertex budget (docs/21: BigCity trades detail for scale).</summary>
    private void BuildWaterSheet(List<HexCoord> wet, Transform parent, Material surfaceMat)
    {
        var hexM = (float)HexCoord.HexMeters;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var hex in wet)
        {
            var w = WorldOf(hex);
            if (w.x < minX) minX = w.x;
            if (w.x > maxX) maxX = w.x;
            if (w.z < minZ) minZ = w.z;
            if (w.z > maxZ) maxZ = w.z;
        }
        minX -= hexM * 1.5f; maxX += hexM * 1.5f;
        minZ -= hexM * 1.5f; maxZ += hexM * 1.5f;

        var spanX = maxX - minX;
        var spanZ = maxZ - minZ;
        var quad = Mathf.Max(4f, Mathf.Max(spanX, spanZ) / 200f);
        var nx = Mathf.CeilToInt(spanX / quad);
        var nz = Mathf.CeilToInt(spanZ / quad);

        var verts = new Vector3[(nx + 1) * (nz + 1)];
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
                verts[j * (nx + 1) + i] = new Vector3(minX + i * quad, TerrainField.WaterLevel, minZ + j * quad);
        var tris = new int[nx * nz * 6];
        var t = 0;
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                // same up-facing winding as the terrain chunk mesh
                var v0 = j * (nx + 1) + i;
                var v1 = v0 + 1;
                var v2 = v0 + nx + 1;
                var v3 = v2 + 1;
                tris[t++] = v0; tris[t++] = v2; tris[t++] = v1;
                tris[t++] = v1; tris[t++] = v2; tris[t++] = v3;
            }

        var mesh = new Mesh();
        if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("WaterSheet");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = surfaceMat;
        go.AddComponent<WaterSurface>().Init(mesh, verts, RiverFlow(wet), 0f);
    }

    /// <summary>A welded hex-fan mesh over `hexes`: per hex a centre vertex
    /// plus its six pointy-top corners (at HexCoord's own circumradius),
    /// six triangles wound to face up. `heightAt` maps a world-xz point to
    /// its y. Because every hex computes corners from its own centre with
    /// the identical offset, two neighbours' shared corners land on exactly
    /// the same world position -- the sheet tiles with no seam.</summary>
    private Mesh HexFanMesh(List<HexCoord> hexes, System.Func<Vector3, float> heightAt)
    {
        var size = (float)(HexCoord.HexMeters / 1.7320508075688772); // circumradius
        var verts = new List<Vector3>(hexes.Count * 7);
        var tris = new List<int>(hexes.Count * 18);

        foreach (var hex in hexes)
        {
            var c = WorldOf(hex);
            var baseIdx = verts.Count;
            var cp = new Vector3(c.x, 0f, c.z);
            cp.y = heightAt(cp);
            verts.Add(cp);
            for (var i = 0; i < 6; i++)
            {
                var ang = Mathf.Deg2Rad * (60f * i - 30f); // pointy-top corner
                var p = new Vector3(c.x + size * Mathf.Cos(ang), 0f, c.z + size * Mathf.Sin(ang));
                p.y = heightAt(p);
                verts.Add(p);
            }
            for (var i = 0; i < 6; i++)
            {
                // (centre, next, cur) winds the top face up -- same up-normal
                // convention the terrain chunk mesh uses
                tris.Add(baseIdx);
                tris.Add(baseIdx + 1 + (i + 1) % 6);
                tris.Add(baseIdx + 1 + i);
            }
        }

        var mesh = new Mesh();
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>The water's downstream direction, approximated as the
    /// region's longer world axis -- the generator carves the river as a
    /// band spanning the map, so the bounding box of all water is reliably
    /// long along the river. The sheet's waves march this way; ponds ride
    /// the same sheet and just read as gentle chop.</summary>
    private Vector2 RiverFlow(List<HexCoord> river)
    {
        if (river == null || river.Count == 0) return Vector2.zero;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var hex in river)
        {
            var w = WorldOf(hex);
            if (w.x < minX) minX = w.x;
            if (w.x > maxX) maxX = w.x;
            if (w.z < minZ) minZ = w.z;
            if (w.z > maxZ) maxZ = w.z;
        }
        return (maxX - minX) >= (maxZ - minZ) ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
    }

    /// <summary>Splits `_city.Water` into connected components (plain hex
    /// adjacency BFS) and calls every hex OUTSIDE the largest one a
    /// "pond" -- the generator's river is carved as one band spanning
    /// the full map width, so it's reliably the biggest component; ponds
    /// are separate, smaller, local blobs. citygen-core doesn't tag which
    /// is which, so this infers it purely for presentation (lily pads vs.
    /// open water) without touching the shared schema.</summary>
    private HashSet<HexCoord> PondHexes()
    {
        var water = new HashSet<HexCoord>(_city.Water);
        var seen = new HashSet<HexCoord>();
        List<HexCoord> largest = null;

        foreach (var start in _city.Water)
        {
            if (seen.Contains(start)) continue;
            var component = new List<HexCoord>();
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);
            seen.Add(start);
            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();
                component.Add(hex);
                foreach (var n in hex.Neighbors())
                {
                    if (!water.Contains(n) || seen.Contains(n)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            if (largest == null || component.Count > largest.Count) largest = component;
        }

        var ponds = new HashSet<HexCoord>(_city.Water);
        if (largest != null) foreach (var h in largest) ponds.Remove(h);
        return ponds;
    }

    private void SpawnLilyPads(HexCoord hex, Material pad, Transform parent)
    {
        var w = WorldOf(hex);
        var count = 2 + Mod(hex.Q * 11 + hex.R * 7, 3);
        for (var i = 0; i < count; i++)
        {
            var off = new Vector3(Mod(hex.Q * 19 + hex.R * 5 + i * 29, 13) - 6f, 0f,
                Mod(hex.Q * 7 + hex.R * 23 + i * 17, 13) - 6f);
            // float on the flowing surface sheet (TerrainField.WaterLevel),
            // proud of the wave amplitude so a pad reads as sitting ON the
            // film rather than being periodically swallowed by a crest
            var p = w + off + Vector3.up * (TerrainField.WaterLevel + 0.11f);
            var size = 0.6f + Mod(hex.Q + hex.R + i * 3, 3) * 0.25f;
            var lp = SpawnPrim(PrimitiveType.Cylinder, p, new Vector3(size, 0.04f, size), pad, parent);
            lp.name = "LilyPad";
        }
    }

    private void SpawnCattails(HexCoord hex, Material stem, Material head, Transform parent)
    {
        var w = WorldOf(hex);
        var count = 2 + Mod(hex.Q * 5 + hex.R * 31, 3);
        for (var i = 0; i < count; i++)
        {
            var off = new Vector3(Mod(hex.Q * 13 + hex.R * 7 + i * 19, 9) - 4f, 0f,
                Mod(hex.Q * 3 + hex.R * 17 + i * 23, 9) - 4f);
            var baseP = w + off;
            baseP.y = GroundHeightAt(baseP);
            var height = 1.4f + Mod(hex.Q + hex.R + i * 5, 3) * 0.3f;
            SpawnPrim(PrimitiveType.Cylinder, baseP + Vector3.up * (height * 0.5f),
                new Vector3(0.05f, height * 0.5f, 0.05f), stem, parent).name = "Reed";
            SpawnPrim(PrimitiveType.Cylinder, baseP + Vector3.up * (height + 0.15f),
                new Vector3(0.09f, 0.18f, 0.09f), head, parent).name = "CattailHead";
        }
    }

    private void SpawnTree(HexCoord hex, int index, Material trunk, Material canopy, Transform parent)
    {
        var w = WorldOf(hex);
        var off = new Vector3(Mod(hex.Q * 19 + hex.R * 7 + index * 41, 13) - 6f, 0f,
            Mod(hex.Q * 3 + hex.R * 31 + index * 17, 13) - 6f);
        var baseP = w + off;
        baseP.y = GroundHeightAt(baseP);
        var height = 2.4f + Mod(hex.Q + hex.R + index, 3) * 0.7f;
        SpawnPrim(PrimitiveType.Cylinder, baseP + Vector3.up * (height * 0.25f),
            new Vector3(0.35f, height * 0.25f, 0.35f), trunk, parent).name = "Trunk";
        SpawnPrim(PrimitiveType.Sphere, baseP + Vector3.up * (height * 0.75f),
            new Vector3(height * 0.7f, height * 0.62f, height * 0.7f), canopy, parent).name = "Canopy";
    }

    /// <summary>A small cluster of tumbled boulders on a ridge hex --
    /// deterministic, tilted at odd angles, terrain-following. Gated to
    /// a quarter of ridge hexes (see caller) so hillsides read as mostly
    /// trees with the occasional rocky outcrop, not a gravel yard.</summary>
    private void SpawnRocks(HexCoord hex, Material rock, Transform parent)
    {
        var w = WorldOf(hex);
        var count = 1 + Mod(hex.Q * 7 + hex.R * 13, 2);
        for (var i = 0; i < count; i++)
        {
            var off = new Vector3(Mod(hex.Q * 17 + hex.R * 11 + i * 23, 15) - 7f, 0f,
                Mod(hex.Q * 29 + hex.R * 3 + i * 37, 15) - 7f);
            var baseP = w + off;
            baseP.y = GroundHeightAt(baseP);
            var size = 0.8f + Mod(hex.Q + hex.R + i * 5, 3) * 0.35f;
            var boulder = SpawnPrim(PrimitiveType.Cube, baseP + Vector3.up * (size * 0.4f),
                new Vector3(size * 1.3f, size * 0.8f, size), rock, parent);
            boulder.transform.rotation = Quaternion.Euler(
                Mod(hex.Q * 13 + i * 7, 20) - 10f,
                Mod(hex.Q * 31 + hex.R * 5 + i * 19, 360),
                Mod(hex.R * 17 + i * 11, 20) - 10f);
            boulder.name = "Rock";
        }
    }

    /// <summary>2026-08 (docs/30 Tier 1, "per-object UV tiling"): how many
    /// real-world meters ONE full texture repeat should span. A shared
    /// cached material's `_BaseMap` tiling used to be a single fixed value
    /// (`MTextured`'s own doc comment called this out: "No per-object UV
    /// tiling scaled to world size... the SAME 0..1 UV rect stretches
    /// across a 1m curb prop or a 30m building wall equally"), so brick
    /// coursing that reads correctly on a wall reads absurdly dense on a
    /// small prop using the same shared material. 6f is chosen so a
    /// typical ~18m building wall repeats about 3 times -- matching the
    /// OLD fixed (3,3) baseline for the case it was actually tuned
    /// against -- while a 1-2m prop now tiles down instead of stretching
    /// up. A v0.1 placeholder like every other tuning number in this
    /// project, not a measured/authored value.</summary>
    private const float TileWorldMeters = 6f;

    private static MaterialPropertyBlock _tilingBlock;

    /// <summary>Per-instance `_BaseMap` tiling via MaterialPropertyBlock,
    /// derived from this object's own world scale -- deliberately NOT a
    /// per-instance Material (that would defeat SRP batching on every
    /// shared dresser material, the exact regression this project's own
    /// material-caching convention exists to avoid). Only touches
    /// materials that actually carry a `_BaseMap` texture (`MTextured`'s
    /// output) -- a flat `M()` color has no texture to tile, so this is a
    /// silent no-op for it either way.
    ///
    /// Uses the LARGEST scale component as the size proxy rather than an
    /// average of all three: most dressed geometry here is a thin wall
    /// panel (a large width/height, a small depth), and the depth
    /// component would otherwise pull a wall's effective tile count down
    /// toward the thin axis. Approximate -- Unity's built-in primitive UV
    /// layout isn't reasoned about per-face here -- but it is a real,
    /// documented improvement over the flat constant it replaces, not a
    /// claim of exactness.</summary>
    private static void ApplyWorldScaledTiling(Renderer renderer, Material mat, Vector3 scale)
    {
        if (renderer == null || mat == null) return;
        if (!mat.HasProperty("_BaseMap")) return;
        if (mat.GetTexture("_BaseMap") == null) return;

        var size = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        if (size < 0.01f) return;
        var tiles = Mathf.Max(0.35f, size / TileWorldMeters);

        if (_tilingBlock == null) _tilingBlock = new MaterialPropertyBlock();
        else _tilingBlock.Clear();
        renderer.GetPropertyBlock(_tilingBlock);
        _tilingBlock.SetVector("_BaseMap_ST", new Vector4(tiles, tiles, 0f, 0f));
        renderer.SetPropertyBlock(_tilingBlock);
    }

    /// <summary>Colliderless styled primitive -- the dresser workhorse.</summary>
    public GameObject SpawnPrim(PrimitiveType type, Vector3 position, Vector3 scale, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            ApplyWorldScaledTiling(renderer, mat, scale);
        }
        return go;
    }

    private static MaterialPropertyBlock _matteBlock;

    /// <summary>2026-08 (creator direction: "the triangle roofs should be
    /// matt not shiny"): per-instance zero-smoothness override via
    /// MaterialPropertyBlock -- same "never fork a per-instance Material"
    /// discipline <see cref="ApplyWorldScaledTiling"/> already
    /// established, so a shared cached roof-color Material (<see
    /// cref="BuildingDresser"/>'s own `M()` cache, keyed purely by color
    /// -- other props reusing that exact color stay untouched) doesn't
    /// have to fork into a matte-specific variant just for the new
    /// <see cref="ProceduralMeshKit.GableRoof"/> shape. Sets both
    /// `_Smoothness` (URP Lit) and `_Glossiness` (Built-in Standard) --
    /// same "set both, the shader that doesn't have one simply ignores
    /// it" precedent Tier 0's own damage-darken override already uses
    /// for `_BaseColor`/`_Color`.</summary>
    private static void ApplyMatteFinish(Renderer renderer)
    {
        if (renderer == null) return;
        if (_matteBlock == null) _matteBlock = new MaterialPropertyBlock();
        else _matteBlock.Clear();
        renderer.GetPropertyBlock(_matteBlock);
        _matteBlock.SetFloat("_Smoothness", 0f);
        _matteBlock.SetFloat("_Glossiness", 0f);
        renderer.SetPropertyBlock(_matteBlock);
    }

    /// <summary>Colliderless styled hand-authored mesh -- <see
    /// cref="SpawnPrim"/>'s sibling for shapes `CreatePrimitive` doesn't
    /// offer (see <see cref="ProceduralMeshKit"/>'s own header), e.g.
    /// <see cref="ProceduralMeshKit.GableRoof"/> for a real triangular
    /// roof instead of a rotated cube. Same calling convention as
    /// `SpawnPrim` -- position/scale/material/parent, no collider.
    /// `matte` (default false, so this stays a no-op for any future
    /// caller that doesn't ask for it) applies <see
    /// cref="ApplyMatteFinish"/>.</summary>
    public GameObject SpawnMesh(Mesh mesh, Vector3 position, Vector3 scale, Material mat, Transform parent, bool matte = false)
    {
        var go = new GameObject("Mesh");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        ApplyWorldScaledTiling(renderer, mat, scale);
        if (matte) ApplyMatteFinish(renderer);
        return go;
    }

    private static int Mod(int x, int m)
    {
        return ((x % m) + m) % m;
    }

    /// <summary>Same suburb/industrial classification every dressing call
    /// needs -- factored out (2026-08, docs/12 Tier 3) so
    /// DeBatchBuildingDressingIfNeeded's later re-Dress call reproduces
    /// byte-identical dressing to what BuildBuildings originally chose,
    /// instead of drifting from a second hand-copied formula.</summary>
    private void ComputeDistrictFlags(Building building, out bool suburb, out bool industrial)
    {
        // downtown vs suburb massing tint (docs/21 batch 2, item 10): a
        // building's hex distance from CenterHex stands in for road-graph
        // radius (the generator seeds density outward from the same
        // center) -- close in reads cooler/institutional, the outskirts
        // read warmer/residential
        var districtRadius = Mathf.Max(1, (_city.WidthHexes + _city.HeightHexes) / 4);
        suburb = building.Footprint[0].DistanceTo(_city.CenterHex) > districtRadius * 0.55f;
        industrial = _railyardCenter.HasValue
            && building.Footprint[0].DistanceTo(_railyardCenter.Value) <= RoadDresser.RailyardRadius;
    }

    private void BuildBuildings()
    {
        var buildings = new GameObject("Buildings").transform;
        buildings.SetParent(transform, false);
        _buildingsHost = buildings;

        // 2026-08 (creator direction: "apply all texture and
        // displacement map details to city building"): these six flat
        // colors are the actual WALL every building shows to the world
        // -- BuildingDresser only adds detail ON TOP of this massing
        // cube (trim, windows, roof), and the facade-grammar system
        // (docs/30) only re-skins Medium/Large's STREET-facing faces,
        // leaving every Small-tier building (the overwhelming majority
        // of the city -- houses, gas stations, diners) and every alley/
        // party-wall face of Medium/Large completely untextured. Reusing
        // `PbrTextureAtlas.Limestone` -- the same neutral, general-
        // purpose "worn surface" texture `Concrete()`/`DressedStone()`/
        // the faction-stone materials already tint for their own
        // purposes -- rather than inventing a seventh texture, and
        // keeping the EXACT same tuned colors as before (only the
        // surface gains detail, the palette itself doesn't change) so
        // this can't regress the district-tint/region-tint reads
        // already built on top of these six colors elsewhere.
        var smallDowntown = NewTexturedMaterial(new Color(0.72f, 0.72f, 0.74f), PbrTextureAtlas.Limestone);
        var smallSuburb = NewTexturedMaterial(new Color(0.83f, 0.78f, 0.64f), PbrTextureAtlas.Limestone);
        var mediumDowntown = NewTexturedMaterial(new Color(0.5f, 0.52f, 0.62f), PbrTextureAtlas.Limestone);
        var mediumSuburb = NewTexturedMaterial(new Color(0.72f, 0.6f, 0.48f), PbrTextureAtlas.Limestone);
        var large = NewTexturedMaterial(new Color(0.35f, 0.35f, 0.7f), PbrTextureAtlas.Limestone);
        var landmark = NewTexturedMaterial(new Color(0.9f, 0.75f, 0.2f), PbrTextureAtlas.Limestone);

        // docs/12 Tier 3 of the graphics-upgrade plan: dressing (not
        // massing -- see the loop below) belonging to a building
        // classified DistantSkyline gets StaticBatchingUtility.Combine'd
        // in ONE pass after every building is built, same "batch
        // everything under this host in a single Combine call" shape
        // CombineStaticRoadSurfaces already uses for road surfaces.
        // EngagementZoneConfig.Default is citygen-core's own docs/18 SS5
        // v0.1 numbers (175m/1000m) -- no separate Unity-side knob yet.
        var zoneConfig = EngagementZoneConfig.Default;
        var pendingDistantDressing = new List<GameObject>();

        foreach (var building in _city.Buildings)
        {
            var height = HeightForTier(building.Tier);
            bool suburb, industrial;
            ComputeDistrictFlags(building, out suburb, out industrial);
            Material mat;
            switch (building.Tier)
            {
                case BuildingTier.Medium: mat = suburb ? mediumSuburb : mediumDowntown; break;
                case BuildingTier.Large: mat = large; break;
                case BuildingTier.Landmark: mat = landmark; break;
                default: mat = suburb ? smallSuburb : smallDowntown; break;
            }
            var cubes = new List<GameObject>();
            var footprintCount = building.Footprint.Count;
            foreach (var hex in building.Footprint)
            {
                var cube = SpawnCube(hex, height / 2f, height, mat, buildings, true);
                cubes.Add(cube);
                var collider = cube.GetComponent<Collider>();
                if (collider != null) _buildingByCollider[collider] = building;
            }
            // 1950s dressing (docs/21 Phase 3): holders are REGISTERED in
            // the same cubes list, so the damage pipeline below crushes
            // and tints the water towers/signs/fire escapes along with
            // the massing they belong to
            BuildingDresser.Dress(this, building, height, cubes, buildings, industrial, suburb, _city.Region);
            _cubesByBuilding[building] = cubes;

            // docs/12 Tier 3: a building whose CLOSEST footprint hex to
            // every engagement center (landmarks + starting HQs/Factories)
            // is beyond EngagementZoneConfig's LocalCity radius is
            // DistantSkyline -- its dressing (the many small window/
            // cornice/water-tower props Tier 0 found blocking batching)
            // is queued for one shared Combine call below. Massing cubes
            // are deliberately excluded: they're the ones ApplyBuildingDamage
            // Object.Destroy()s outright on collapse rather than mutating
            // in place, so batching them buys nothing and would only add
            // more surface area to the de-batch path for no benefit.
            var zone = EngagementZoneManager.ClassifyBuilding(building, _engagementCenters, zoneConfig);
            if (zone == EngagementZone.DistantSkyline)
            {
                _batchedDistantDressing.Add(building);
                for (var i = footprintCount; i < cubes.Count; i++)
                    pendingDistantDressing.Add(cubes[i]);
            }
        }

        CombineDistantSkylineDressing(pendingDistantDressing, buildings);
    }

    /// <summary>The docs/12 Tier 3 counterpart to CombineStaticRoadSurfaces
    /// -- same "mark isStatic, one shared Combine call" shape, applied to
    /// DistantSkyline building-dressing holders instead of road-surface
    /// renderers. Building dressing has no KnockableProp equivalent (only
    /// RoadDresser's street furniture tips over), so no exclusion filter
    /// is needed here.</summary>
    private void CombineDistantSkylineDressing(List<GameObject> dressingHolders, Transform buildingsHost)
    {
        if (dressingHolders.Count == 0) return;
        var dressingObjects = new List<GameObject>();
        foreach (var holder in dressingHolders)
        {
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.isStatic = true;
                dressingObjects.Add(renderer.gameObject);
            }
        }
        if (dressingObjects.Count > 0)
            StaticBatchingUtility.Combine(dressingObjects.ToArray(), buildingsHost.gameObject);
    }

    /// <summary>docs/12 Tier 3: undoes CombineDistantSkylineDressing for
    /// ONE building the instant it takes its first damage. Static batching
    /// bakes each renderer's vertices into a shared world-space buffer at
    /// Combine time -- Unity's own docs are explicit that transform moves/
    /// scales afterward are silently ignored by the renderer, which is
    /// exactly what ApplyBuildingDamage's Destroyed-stage squish does to a
    /// dressing holder (`cube.transform.localScale`/`position` rewritten
    /// in place). The Damaged-stage darken pass is NOT the problem --
    /// that's a per-renderer MaterialPropertyBlock override, which static
    /// batching tolerates fine (same trick Tier 0's own damage-material
    /// fix already relies on) -- but de-batching unconditionally on first
    /// damage rather than trying to special-case "only Destroyed needs
    /// this" keeps the two damage-stage code paths below unaware this
    /// mechanism exists at all, rather than threading a batched/unbatched
    /// distinction through both of them.
    ///
    /// There's no Unity API to reverse StaticBatchingUtility.Combine on a
    /// live renderer, so this destroys the (now possibly stale, batched)
    /// dressing holders for this building and re-runs BuildingDresser.Dress
    /// for it alone -- ComputeDistrictFlags reproduces the exact suburb/
    /// industrial inputs BuildBuildings used, so the respawned dressing is
    /// visually identical to what was just destroyed, just unbatched.
    /// EmissiveAnimator/GlowPointRegistry both already null-check their
    /// entries every tick (a knocked-over/destroyed prop "simply drops
    /// out", their own comments say) -- the stale entries this destroy
    /// leaves behind self-prune on the next tick, no explicit unregister
    /// needed here.</summary>
    private void DeBatchBuildingDressingIfNeeded(Building building)
    {
        if (!_batchedDistantDressing.Remove(building)) return;
        List<GameObject> cubes;
        if (!_cubesByBuilding.TryGetValue(building, out cubes)) return;

        var footprintCount = building.Footprint.Count;
        for (var i = cubes.Count - 1; i >= footprintCount; i--)
        {
            Object.Destroy(cubes[i]);
            cubes.RemoveAt(i);
        }

        bool suburb, industrial;
        ComputeDistrictFlags(building, out suburb, out industrial);
        var height = HeightForTier(building.Tier);
        BuildingDresser.Dress(this, building, height, cubes, _buildingsHost, industrial, suburb, _city.Region);
    }

    /// <summary>Play-mode read for the landmark mechanics' radii --
    /// docs/03's 3-hex emitter aura and docs/18/20's 5-hex Collection
    /// Station harvest radius. CityGizmo draws these as wire spheres in
    /// the Scene view, but the actual GAME never showed them; a ring of
    /// short emissive pylons (teal = emitter, red = station, the gizmo's
    /// own color code) marks each radius on the ground. Pylons that
    /// would land inside a building or in water are skipped -- the ring
    /// reads through the gap, and a post poking out of a roof would
    /// read as a glitch. Registered with NeonRegistry, so they glow
    /// properly at night like all other emissives.</summary>
    private void BuildLandmarkAuras()
    {
        var host = new GameObject("LandmarkAuras").transform;
        host.SetParent(transform, false);

        var emitterMat = NewMaterial(new Color(0.2f, 0.85f, 0.85f));
        emitterMat.EnableKeyword("_EMISSION");
        emitterMat.SetColor("_EmissionColor", new Color(0.2f, 0.85f, 0.85f) * 0.9f);
        NeonRegistry.Register(emitterMat, new Color(0.2f, 0.85f, 0.85f) * 0.9f);

        var hubMat = NewMaterial(new Color(0.85f, 0.25f, 0.25f));
        hubMat.EnableKeyword("_EMISSION");
        hubMat.SetColor("_EmissionColor", new Color(0.85f, 0.25f, 0.25f) * 0.9f);
        NeonRegistry.Register(hubMat, new Color(0.85f, 0.25f, 0.25f) * 0.9f);

        var water = new HashSet<HexCoord>(_city.Water);
        var blocked = BlockedFor(false);

        foreach (var landmark in _city.Landmarks)
        {
            var mat = landmark.Kind == LandmarkKind.Emitter ? emitterMat : hubMat;
            var center = WorldOf(landmark.Site);
            var radius = landmark.RadiusHexes * (float)HexCoord.HexMeters;
            const int posts = 18;
            for (var i = 0; i < posts; i++)
            {
                var angle = i * (2f * Mathf.PI / posts);
                var p = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var hex = HexAt(p);
                if (!_city.Contains(hex) || blocked.Contains(hex) || water.Contains(hex)) continue;
                p.y = GroundHeightAt(p);
                var pylon = SpawnPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.8f,
                    new Vector3(0.22f, 0.8f, 0.22f), mat, host);
                pylon.name = "AuraPylon";
            }
        }
    }

    /// <summary>Tier 0 of the graphics-upgrade plan (docs/12, 2026-08):
    /// one-time census after the city finishes building, so a real
    /// BigCity run finally has SOME written-down number to compare a
    /// future optimization pass against -- docs/30's ~19k buildings /
    /// ~530k GameObjects / ~490k renderers / ~39k colliders were
    /// estimates from a re-implemented placement pass, not a measurement,
    /// because nothing existed to measure with. `GetComponentsInChildren`
    /// over the whole scene is a one-shot cost paid once per match, not a
    /// per-frame one, so this is safe to leave on permanently rather than
    /// gating it behind a debug flag.</summary>
    private void LogCityBuildCensus(long buildMilliseconds)
    {
        var gameObjectCount = FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        var rendererCount = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
        var colliderCount = FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
        Debug.Log(string.Format(
            "City build census -- preset={0} seed={1}: {2}ms, {3} GameObjects, {4} renderers, {5} colliders.",
            preset, seed, buildMilliseconds, gameObjectCount, rendererCount, colliderCount));
    }

    private void BuildBridges()
    {
        // trestle piers, guardrails, through-truss arches (docs/21 batch 2,
        // item 1) -- colliderless, same as the flat deck it replaces;
        // BridgeDresser makes its own "Bridges" host under `transform`
        BridgeDresser.Build(this, _city, transform);
    }

    private GameObject SpawnCube(HexCoord hex, float y, float height, Material mat, Transform parent, bool keepCollider)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent, false);
        var hexSize = (float)HexCoord.HexMeters;
        var world = WorldOf(hex);
        cube.transform.position = new Vector3(world.x, y, world.z);
        cube.transform.localScale = new Vector3(hexSize * 0.9f, height, hexSize * 0.9f);
        var cubeRenderer = cube.GetComponent<Renderer>();
        cubeRenderer.sharedMaterial = mat;
        // 2026-08 ("apply all texture and displacement map details"):
        // per-instance tiling via MaterialPropertyBlock, same Tier 1a
        // technique (docs/12) every other textured prop in this file
        // already uses -- a no-op for the six massing materials' old
        // untextured incarnation (ApplyWorldScaledTiling bails out on
        // any material with no `_BaseMap` texture assigned), so this is
        // purely additive now that NewTexturedMaterial gives them one.
        ApplyWorldScaledTiling(cubeRenderer, mat, cube.transform.localScale);
        if (!keepCollider)
        {
            var collider = cube.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }
        return cube;
    }

    private static Material NewMaterial(Color color)
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = color;
        return mat;
    }

    /// <summary>Same shape as `BuildingDresser`/`RoadDresser`'s own
    /// `MTextured` helpers (this file didn't have one until now) --
    /// tints a shared atlas texture rather than inventing a flat color.
    /// A default fallback tiling is set here; `ApplyWorldScaledTiling`
    /// (called from <see cref="SpawnCube"/>) overrides it per-instance
    /// via MaterialPropertyBlock once the actual building height/hex
    /// size is known, so a small house and a Landmark tower sharing this
    /// SAME Material don't share the same stretched-or-tiny texture
    /// scale.</summary>
    private static Material NewTexturedMaterial(Color color, Texture2D tex)
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = color;
        if (mat.HasProperty("_BaseMap") && tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
        }
        return mat;
    }

    // ---- live destruction -------------------------------------------------------

    public Building BuildingFromCollider(Collider collider)
    {
        Building b;
        return collider != null && _buildingByCollider.TryGetValue(collider, out b) ? b : null;
    }

    public bool IsDestroyed(Building building)
    {
        foreach (var state in _battlefield.Buildings)
            if (ReferenceEquals(state.Building, building)) return state.Stage == DamageStage.Destroyed;
        return false;
    }

    /// <summary>2026-08 (creator direction: "if an attacked target has
    /// multiple building in it's template then any buildings hit my
    /// monster's weapon fire should catch fire first"): the specific
    /// footprint hex `MonsterAgent.TickAttack` resolved this hit against
    /// (`NearestFootprintPoint`, the same point its own weapon FX beam/
    /// shot converges on) -- see `FootprintIndexOf`'s own doc comment for
    /// how it picks which of a multi-hex building's own massing cubes
    /// this hit's fire feedback (ignition + `RegisterHit`) applies to.</summary>
    public void ApplyBuildingDamage(Building building, int amount, HexCoord hitHex)
    {
        BuildingRuntimeState current = null;
        foreach (var state in _battlefield.Buildings)
            if (ReferenceEquals(state.Building, building)) { current = state; break; }
        if (current == null || current.Stage == DamageStage.Destroyed) return;

        // docs/12 Tier 3: un-batch BEFORE any stage transition below runs
        // -- see DeBatchBuildingDressingIfNeeded's own doc comment for why
        // this has to happen on first damage rather than only right before
        // the Destroyed-stage squish that actually needs it.
        DeBatchBuildingDressingIfNeeded(building);

        // 2026-08 (creator direction: "as the lot is cleaned of parts the
        // lot debris is decreased... available to build on it"): stamp
        // the destroy-transition frame off match-core's own running
        // clock (0 if no match is up yet, same degenerate-but-harmless
        // fallback UnblockProceduralBuildingHex's own callers already
        // accept) -- TryReclaimHex's decay fallback below needs SOME
        // elapsed-time reference, and reusing match-core's Frame avoids
        // introducing a second clock.
        var next = current.ApplyDamage(amount, _simBridge?.CurrentFrame ?? 0);
        _battlefield = _battlefield.WithBuildingDamage(next);

        List<GameObject> cubes;
        if (!_cubesByBuilding.TryGetValue(building, out cubes)) return;

        if (next.Stage == DamageStage.Destroyed)
        {
            // docs/18 SS3: collapse to walkable rubble; its hexes leave
            // the pathing index -- flag agents to re-path. `cubes` holds
            // the massing cube for each footprint hex FIRST (added in
            // BuildBuildings' footprint loop), then one dressing holder
            // per hex (added once by BuildingDresser.Dress right after)
            // -- exactly footprint.Count of each, in that order. That
            // structural invariant is what lets this tell them apart
            // below without a separate marker.
            var rubbleMat = new Material(ShaderUtil.FindRenderableShader());
            rubbleMat.color = new Color(0.3f, 0.28f, 0.26f);
            var footprintCount = building.Footprint.Count;
            // 2026-08 (creator direction: "as the lot is cleaned of parts
            // the lot debris is decreased"): collect every spawned rubble
            // host's children into one flat per-building list so
            // ScavengeBuildingDebris can later destroy a proportional
            // slice of it as the pile depletes.
            var debrisChunks = new List<GameObject>();
            for (var i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];
                if (i < footprintCount)
                {
                    // massing cube: squishing an 18m-wide cube flat in
                    // place read as a uniform stain from the RTS camera
                    // ("radiating puddles", not broken masonry) -- destroy
                    // it and replace with several big tilted slab pieces
                    var hex = building.Footprint[i];
                    var pos0 = cube.transform.position;
                    var massingCollider = cube.GetComponent<Collider>();
                    if (massingCollider != null) _buildingByCollider.Remove(massingCollider);
                    Object.Destroy(cube);
                    if (_buildingsHost != null)
                    {
                        var shatterHost = RubbleDresser.Shatter(this, hex, pos0, rubbleMat, _buildingsHost);
                        for (var c = 0; c < shatterHost.childCount; c++) debrisChunks.Add(shatterHost.GetChild(c).gameObject);
                    }
                    continue;
                }
                // dressing holder: still squish in place -- it's already a
                // cluster of smaller, varied pieces (windows, cornices,
                // water towers), so flattening reads as debris, not a slab
                var s = cube.transform.localScale;
                cube.transform.localScale = new Vector3(s.x, s.y * 0.12f, s.z);
                var p = cube.transform.position;
                cube.transform.position = new Vector3(p.x, p.y * 0.12f, p.z);
                // GetComponentsInChildren includes the cube's own renderer
                foreach (var renderer in cube.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = rubbleMat;
                var collider = cube.GetComponent<Collider>();
                if (collider != null)
                {
                    _buildingByCollider.Remove(collider);
                    Object.Destroy(collider); // rubble: clicks fall through to the ground
                }
            }
            // small debris chunks scattered over the shattered slabs
            // (docs/21 batch 2, item 5) and a one-shot dust puff burst
            // for the collapse beat (item 3)
            if (_buildingsHost != null)
            {
                var scatterHost = RubbleDresser.Scatter(this, building, rubbleMat, _buildingsHost);
                for (var c = 0; c < scatterHost.childCount; c++) debrisChunks.Add(scatterHost.GetChild(c).gameObject);
                DamageFx.DustBurst(WorldOf(building.Footprint[0]), _buildingsHost);
                _scorchDecalsByBuilding[building] = SpawnScorchDecal(building, _buildingsHost);
                // 2026-08 (creator report: "destroyed collapsed building
                // do not have lights"): rubble had zero emissive surfaces
                // anywhere until now -- see DamageFx.CollapseEmbers' own
                // header. Radius scales with footprint size, same "bigger
                // building, bigger wreck" idea BuildingRubble already uses.
                DamageFx.CollapseEmbers(WorldOf(building.Footprint[0]), _buildingsHost,
                    Mathf.Max(3f, building.Footprint.Count * 3f));
            }
            _debrisChunksByBuilding[building] = debrisChunks;
            // 2026-08 (creator report: "I don't see people fleeing from
            // the wreckage of the building"): disgorge was wired ONLY to
            // the separate RTS-building roster (BaseDresser watching
            // SimBuilding.State) -- a PROCEDURAL building (this method's
            // own kind, the vast majority of the map, and fully
            // attackable via TickAttack) never disgorged anyone at all.
            // Same "when they are destroyed they disgorge their human
            // occupants that flee" contract, same SpawnFleeingOccupant
            // call BaseDresser already uses, just from the destruction
            // path a house/shop actually dies through.
            for (var occ = 0; occ < BuildingStats.Occupants(building.Tier); occ++)
                SpawnFleeingOccupant(building.Footprint[0]);
            _cityVersion++;
            // 2026-08 bugfix (creator report: "areas are NOT being
            // reclaimed... for new player to create new buildings"):
            // this procedural building has no SimBuilding entity of its
            // own, so match-core's OWN blocked-hex set (CanPlaceBuilding's
            // gate) never learned it was destroyed -- only THIS file's
            // own BlockedFor cache (used for movement/pathing, already
            // correct via `_cityVersion++` above) reopened.
            //
            // 2026-08 follow-up (creator direction: "assign some salvage
            // parts based on the building size... as the lot is cleaned
            // of parts the lot debris is decreased until it is completely
            // cleared and is available to build on it"): the original fix
            // here unblocked the hex IMMEDIATELY on destruction. That's
            // now superseded -- build-placement availability is gated on
            // TryReclaimHex's own dual check (fully scavenged, or a decay
            // fallback if nobody ever does), same shape as the separate
            // RTS SimBuilding roster's own RubbleClearTicks/
            // DebrisDecayTicks gate. Deliberately NOT called here: nothing
            // needs to know "can I build on this fresh wreck" until
            // someone actually tries, and TryReclaimHex is cheap to call
            // lazily right at that moment (BuildGhostCursor's own live
            // preview, and ScavengeBuildingDebris on full clear) rather
            // than proactively swept every frame for every destroyed
            // building on the map.
            Debug.Log("Building destroyed -- rubble is now walkable.");
        }
        else
        {
            if (next.Stage == DamageStage.Damaged && current.Stage == DamageStage.Intact)
            {
                // Intact -> Damaged visual: darken (docs/18's cracked state),
                // dressing included -- PER-RENDERER OVERRIDE here, never a
                // tint on the shared cached dresser materials (that would
                // darken every building in the city at once).
                //
                // 2026-08 perf (Tier 0 of the graphics-upgrade plan,
                // docs/12): this used to be `new Material(...)` per
                // renderer -- ~56 heap-allocated, never-Destroy()'d Material
                // instances for a single Large office, on the frame combat
                // is heaviest. A MaterialPropertyBlock override gets the
                // same "this renderer only" isolation without allocating or
                // leaking a Material, and without touching the shared
                // sharedMaterial other buildings still reference. Reused
                // across the whole loop -- GetPropertyBlock/SetPropertyBlock
                // copy in and out, so one block instance is enough.
                //
                // 2026-08 (creator report: "goes solid at about 50%
                // destruction"): GetComponentsInChildren is a RECURSIVE
                // sweep -- since fire/smoke now spawn as children of THIS
                // SAME cube transform (DamageFx.AttachSmoke/AttachFireCluster
                // both take cubes[0].transform as their holder, several
                // entries back), whatever puffs happened to be alive at
                // the exact instant a building crossed the Damaged
                // threshold got their transparent, per-frame-updated
                // material silently REPLACED by this loop's new opaque
                // (never MakeTransparent'd), one-time, never-updated-again
                // Material -- reading as a puff freezing solid, since the
                // SmokePuff component's own `_mat` field goes on mutating
                // an now-orphaned Material nothing renders anymore. Skip
                // any renderer whose ancestor is a live FX root -- this
                // loop was written before fire/smoke existed under this
                // same hierarchy and was never audited against it.
                //
                // 2026-08 follow-up (SmokeCluster follow-up, see that
                // class's own header): the damage-triggered smoke path's
                // wrapper type changed from `SmokePlume` to `SmokeCluster`
                // -- this skip-check is keyed off component TYPE, so it
                // would otherwise silently stop protecting damage smoke
                // (while still protecting `SmokePlume`'s one remaining
                // caller, the standalone chimney) and reopen the exact
                // "goes solid" bug this comment describes, just for the
                // new class instead of the old one.
                var damagedBlock = new MaterialPropertyBlock();
                foreach (var cube in cubes)
                {
                    foreach (var renderer in cube.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer.GetComponentInParent<SmokePlume>() != null) continue;
                        if (renderer.GetComponentInParent<SmokeCluster>() != null) continue;
                        if (renderer.GetComponentInParent<FireCluster>() != null) continue;
                        var c = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.gray;
                        var darkened = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f);
                        renderer.GetPropertyBlock(damagedBlock);
                        // both names set: URP/Lit's [MainColor] is
                        // `_BaseColor`, Built-in/Standard's is `_Color` --
                        // ShaderUtil documents this project can end up on
                        // either, and an override for a property the active
                        // shader doesn't have is simply unused, not an error.
                        damagedBlock.SetColor("_BaseColor", darkened);
                        damagedBlock.SetColor("_Color", darkened);
                        renderer.SetPropertyBlock(damagedBlock);
                    }
                }
            }

            // 2026-08 (creator direction: "spawn fire when under attack"):
            // ignition itself moved OUT of this method -- see
            // IgniteBuildingIfNeeded's own doc comment for why (TickAttack
            // now calls it directly the instant an attacker is in range,
            // instead of waiting for a hit to land here). Still called
            // here too, as a defensive fallback: any damage source that
            // reaches ApplyBuildingDamage without having gone through
            // TickAttack's in-range check first still ignites correctly,
            // and IgniteBuildingIfNeeded is a no-op past the first call
            // either way.
            IgniteBuildingIfNeeded(building, hitHex);

            // 2026-08 (creator direction: "monsters should attack all
            // buildings that will be destroyed in the attack. goal make
            // it realistic that the building are logically destroyed"):
            // HP is shared across a multi-hex building's WHOLE footprint
            // (`current.ApplyDamage` above, `_battlefield.Buildings` is
            // keyed by `Building`, not by hex) -- every hex of a
            // Large/Medium multi-hex structure is already GOING to
            // collapse together the instant HP hits zero (the Destroyed
            // branch above already shatters every cube in `cubes` at
            // once). Up to now, only the SPECIFIC hex actually being hit
            // ever showed fire -- meaning a whole multi-hex footprint
            // could suddenly turn to rubble while only one corner had
            // ever visibly burned. As this building's own HP fraction
            // falls, progressively ignite MORE of its own other hexes
            // too (still capped by each hex's own idempotent
            // `_ignitedCubes` guard, so this is a cheap no-op past the
            // first time any given hex lights up) -- by the time it's
            // truly near death, the whole structure is burning, not just
            // the one point actually under fire, so the eventual full-
            // footprint collapse reads as earned rather than a surprise.
            if (building.Footprint.Count > 1)
            {
                var hpFraction = next.MaxHp > 0 ? (float)next.CurrentHp / next.MaxHp : 0f;
                var urgency = Mathf.Clamp01(1f - hpFraction);
                var targetIgnitedHexes = Mathf.Clamp(Mathf.CeilToInt(building.Footprint.Count * urgency), 1, building.Footprint.Count);
                for (var i = 0; i < targetIgnitedHexes; i++)
                    IgniteBuildingIfNeeded(building, building.Footprint[i]);
            }

            // 2026-08 (fire-propagation rewrite, creator's own brief:
            // "Fire always begins at one or more weapon impact locations.
            // The impact injects an initial burst of heat proportional to
            // weapon energy" -- follow-up: "increase the speed of the
            // spread based on ... amount of time before building is
            // destroyed. shorter time more spawns"): every hit that lands
            // here (armed and unarmed alike -- MonsterAgent.TickAttack's
            // own two call sites both funnel through this one method)
            // feeds the SPECIFIC hex's FireCluster (`FootprintIndexOf`,
            // 2026-08 follow-up: "any buildings hit... should catch fire
            // first" -- a multi-hex building's other, un-hit hexes must
            // NOT hear about a hit that landed on a different one) its own
            // damage amount as "weapon energy," plus this building's OWN
            // current HP fraction (`next`, the just-computed post-damage
            // state) so the cluster can speed up as the building nears
            // destruction. `cubes` was already resolved at the top of this
            // method; only runs once per landed hit, not per frame.
            var hitCubeIndex = FootprintIndexOf(building, hitHex);
            var hitCube = hitCubeIndex < cubes.Count ? cubes[hitCubeIndex] : cubes[0];
            var cluster = hitCube.GetComponentInChildren<FireCluster>();
            if (cluster != null)
                cluster.RegisterHit(amount, next.MaxHp > 0 ? (float)next.CurrentHp / next.MaxHp : 0f);
        }
    }

    /// <summary>2026-08 (creator direction: "if an attacked target has
    /// multiple building in it's template then any buildings hit my
    /// monster's weapon fire should catch fire first"): which of a
    /// building's own footprint hexes (and therefore which massing cube
    /// -- `RuntimeCityBuilder`'s `cubes` list holds exactly one per
    /// footprint hex, same index order as `building.Footprint`, per
    /// `ApplyBuildingDamage`'s own Destroyed-branch comment) a given hit
    /// hex corresponds to. A plain linear scan over `IReadOnlyList
    /// &lt;HexCoord&gt;` (no `IndexOf` on that interface) -- footprints
    /// here top out at a handful of hexes (Large tier: 4), so this is
    /// nowhere near the "expensive search" territory the visual-variation
    /// follow-up warns against; it only ever runs once per landed hit,
    /// not per frame. Falls back to index 0 if `hitHex` somehow isn't
    /// part of this building's own footprint -- defensive only, since
    /// every real caller derives `hitHex` from `NearestFootprintPoint` on
    /// this SAME building.</summary>
    private static int FootprintIndexOf(Building building, HexCoord hitHex)
    {
        for (var i = 0; i < building.Footprint.Count; i++)
            if (building.Footprint[i] == hitHex) return i;
        return 0;
    }

    /// <summary>2026-08 (creator direction: "spawn fire when under
    /// attack"): ignites the SPECIFIC hex's smoke+fire cluster the moment
    /// it's under active assault, called from <see cref="MonsterAgent.
    /// TickAttack"/> the instant an attacker is confirmed in range and
    /// begins fighting -- BEFORE any damage has necessarily landed
    /// (weapon cooldown/travel time could otherwise delay ignition by up
    /// to a second or more past the moment combat visibly starts, per the
    /// creator's own prior direction: "as soon as a building is in combat
    /// we need to see the smoke and fire"). Idempotent via <see
    /// cref="_ignitedCubes"/> -- a given hex ignites at most once, same
    /// "never re-fire" contract the old HP-based gate had, just tracked
    /// explicitly (and now per-cube, not per-building -- see that field's
    /// own doc comment) now that ignition can happen with zero damage yet
    /// dealt.
    ///
    /// 2026-08 follow-up (creator direction: "if an attacked target has
    /// multiple building in it's template then any buildings hit my
    /// monster's weapon fire should catch fire first"): `hitHex` picks
    /// WHICH of this building's own footprint hexes (via
    /// `FootprintIndexOf`) actually catches fire -- a Large/Medium
    /// multi-hex structure no longer always ignites its first hex
    /// regardless of where an attacker is actually standing; each hex
    /// ignites independently, from whichever one is actually under
    /// fire.</summary>
    public void IgniteBuildingIfNeeded(Building building, HexCoord hitHex)
    {
        if (building == null) return;
        List<GameObject> cubes;
        if (!_cubesByBuilding.TryGetValue(building, out cubes) || cubes.Count == 0) return;

        var cubeIndex = FootprintIndexOf(building, hitHex);
        if (cubeIndex >= cubes.Count) cubeIndex = 0;
        var cube = cubes[cubeIndex];
        if (!_ignitedCubes.Add(cube)) return;

        BuildingRuntimeState current = null;
        foreach (var state in _battlefield.Buildings)
            if (ReferenceEquals(state.Building, building)) { current = state; break; }
        if (current == null || current.Stage == DamageStage.Destroyed) return;

        var height = BuildingHeight(building);
        // 2026-08 (creator report: "what happen to my low poly fire for
        // when buildings were under attack" -> traced to AttachFire
        // having only ever been wired to the SEPARATE RTS-building
        // roster, BaseDresser.cs, never to THIS path -- the one monsters
        // actually damage via TickAttack/ApplyBuildingDamage, the vast
        // majority of the map. Same fire-cluster call BaseDresser makes,
        // scaled off this building's own real footprint size (hex count)
        // instead of the RTS roster's fixed-size silhouette table).
        var footprintRadius = Mathf.Sqrt(building.Footprint.Count) * (float)HexCoord.HexMeters * 0.4f;
        // 2026-08 follow-up BUGFIX (creator report: "I still do not see
        // the fire"): cube.transform.position.y is NOT ground level --
        // SpawnCube(hex, height/2f, height, ...) centers the massing
        // cube at HALF the building's height (a primitive "sitting on
        // the ground" is positioned at its own vertical middle). Every
        // height-fraction offset AttachSmoke/AttachFireCluster compute
        // was therefore landing half a building-height too high -- fire
        // in particular ended up floating ~50% of the building's own
        // height above its actual roofline. See DamageFx.AttachSmoke's
        // own doc comment for the full writeup; -height*0.5f is the
        // correction back to true ground level.
        var groundOffset = -height * 0.5f;
        // 2026-08 (SmokeCluster follow-up): fire attaches FIRST now --
        // SmokeCluster reads the FireCluster it's given, so that
        // FireCluster has to exist (and be Init'd) before AttachSmoke can
        // wire it up. See DamageFx.AttachFireCluster's own doc comment.
        var fire = DamageFx.AttachFireCluster(cube.transform, height, footprintRadius, BuildingStats.FireCount(building.Tier), groundOffset);
        DamageFx.AttachSmoke(cube.transform, fire, BuildingStats.SmokeScale(building.Tier), groundOffset);
    }

    /// <summary>2026-08 (creator direction: "assign some salvage parts
    /// based on the building size... as the lot is cleaned of parts the
    /// lot debris is decreased until it is completely cleared and is
    /// available to build on it"), 2026-08 follow-up (creator direction:
    /// "check that monsters can harvest metal and other building
    /// salvage" -&gt; "carry it home," same tank <see
    /// cref="MonsterAgent"/>'s Blood/Bones/Brains already use): drains up
    /// to `amount` of `building`'s own remaining scavenge pile and
    /// RETURNS the actual amount drained -- the caller (a harvester's own
    /// <c>CreditHarvestForScavengedDebris</c>) carries that home and
    /// banks it later via the existing <see cref="BankHarvestLoad"/> path,
    /// same as an eaten citizen's yield. Deliberately does NOT credit a
    /// wallet directly anymore (an earlier draft of this method did,
    /// before any AI actually called it) -- Parts now flows through the
    /// SAME onboard-tank/deliver-to-Factory loop every other harvested
    /// resource already uses, not a separate instant-credit shortcut.
    /// Still shrinks the visible rubble proportionally and reclaims the
    /// hex the instant the pile is fully cleared. Silent no-op (returns
    /// 0) if the building isn't actually Destroyed, has nothing left to
    /// loot, or `amount` isn't positive -- same bad-input contract as
    /// <see cref="ApplyBuildingDamage(Building, int, HexCoord)"/>.
    ///
    /// 2026-08 (Zombie scavenging redesign, docs/12: "the site must
    /// clearly communicate that it is fully cleared and available for
    /// construction"): callers are now expected to invoke this
    /// REPEATEDLY with small per-tick amounts (concurrent Workers each
    /// requesting their own share every tick -- see <see
    /// cref="Worker.TickScavenging"/>) rather than once with the whole
    /// remaining pile, so the proportional chunk-shrink below now
    /// actually animates instead of jumping straight from full to empty
    /// in one call. On the tick that fully clears the pile, also
    /// destroys any still-standing scorch decals (<see
    /// cref="SpawnScorchDecal"/>) and drops both this building's tracking
    /// dictionary entries -- previously only the rubble CHUNKS were ever
    /// removed; the permanent scorch marks stayed forever, so a "fully
    /// cleared" lot still visibly read as damaged/burnt rather than
    /// buildable.</summary>
    public int DrainBuildingScavenge(Building building, int amount)
    {
        if (_simBridge == null || amount <= 0) return 0;
        BuildingRuntimeState current = null;
        foreach (var state in _battlefield.Buildings)
            if (ReferenceEquals(state.Building, building)) { current = state; break; }
        if (current == null || current.Stage != DamageStage.Destroyed || current.ScavengeRemaining <= 0) return 0;

        var before = current.ScavengeRemaining;
        var next = current.WithScavengeConsumed(amount);
        _battlefield = _battlefield.WithBuildingDamage(next);
        var actuallyConsumed = before - next.ScavengeRemaining;

        List<GameObject> chunks;
        if (_debrisChunksByBuilding.TryGetValue(building, out chunks) && chunks.Count > 0 && next.ScavengeValue > 0)
        {
            var targetVisible = Mathf.CeilToInt(chunks.Count * (next.ScavengeRemaining / (float)next.ScavengeValue));
            while (chunks.Count > targetVisible)
            {
                var last = chunks[chunks.Count - 1];
                chunks.RemoveAt(chunks.Count - 1);
                if (last != null) Object.Destroy(last);
            }
        }

        if (next.IsFullyScavenged)
        {
            foreach (var hex in building.Footprint) _simBridge.UnblockProceduralBuildingHex(hex);

            List<GameObject> decals;
            if (_scorchDecalsByBuilding.TryGetValue(building, out decals))
            {
                foreach (var d in decals) if (d != null) Object.Destroy(d);
                _scorchDecalsByBuilding.Remove(building);
            }
            _debrisChunksByBuilding.Remove(building);
        }

        return actuallyConsumed;
    }

    /// <summary>2026-08 (creator direction: "check that monsters can
    /// harvest metal and other building salvage"): the debris counterpart
    /// to <see cref="NearestCitizenTo"/> -- nearest DESTROYED procedural
    /// building with a nonzero <see cref="BuildingRuntimeState.
    /// ScavengeRemaining"/> pile, or null if none stands within `within`
    /// meters of `position`. Called from <see cref="MonsterAgent.
    /// AcquireTarget"/>'s foraging fallback once a citizen search comes
    /// up empty. Checks every footprint hex of every destroyed building
    /// (footprints top out at a handful of hexes, same "nowhere near
    /// expensive search territory" reasoning <see cref="FootprintIndexOf"/>'s
    /// own doc comment already makes) rather than just one representative
    /// point, so a large Landmark wreck is found from whichever edge is
    /// actually closest.</summary>
    public Building NearestScavengeableBuildingTo(Vector3 position, float within)
    {
        Building best = null;
        var bestSq = within * within;
        foreach (var state in _battlefield.Buildings)
        {
            if (state.Stage != DamageStage.Destroyed || state.ScavengeRemaining <= 0) continue;
            foreach (var hex in state.Building.Footprint)
            {
                var d = WorldOf(hex) - position;
                d.y = 0f;
                if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = state.Building; }
            }
        }
        return best;
    }

    /// <summary>2026-08 (creator direction: "check that monsters can
    /// harvest metal and other building salvage"): which destroyed,
    /// still-scavengeable building (if any) owns `hex` -- lets
    /// <see cref="WaypointCommander"/> route a right-click on a wreck's
    /// own footprint to <see cref="MonsterAgent.OrderScavenge"/>, the
    /// same way a right-click on a standing building already routes to
    /// <see cref="MonsterAgent.OrderAttack"/>. A destroyed building's own
    /// massing-cube collider is gone by the time this would ever be
    /// asked (see <see cref="ApplyBuildingDamage(Building, int, HexCoord)"/>'s
    /// Destroyed branch: rubble is deliberately collider-less, "clicks
    /// fall through to the ground") -- so unlike <see
    /// cref="BuildingFromCollider"/>, this is a plain hex-membership scan,
    /// not a collider lookup.</summary>
    public Building ScavengeableBuildingAt(HexCoord hex)
    {
        foreach (var state in _battlefield.Buildings)
        {
            if (state.Stage != DamageStage.Destroyed || state.ScavengeRemaining <= 0) continue;
            foreach (var h in state.Building.Footprint)
                if (h == hex) return state.Building;
        }
        return null;
    }

    /// <summary>2026-08 (Zombie/SCV-style construction, docs/12): the
    /// local human player's own nearest <c>UnderConstruction</c> RTS
    /// building that ISN'T currently staffed by a Worker -- the
    /// construction counterpart to <see
    /// cref="NearestScavengeableBuildingTo"/> above, same nearest-of-
    /// state-Y idiom <see cref="BaseDresser"/>'s own UnderConstruction
    /// enumeration already establishes, just querying <see
    /// cref="SimBridge.BuildingAt"/> (real match-core `SimBuilding`s)
    /// instead of the procedural civilian `Building` list those two
    /// query -- a genuinely different building type, not a typo.</summary>
    public SimBuilding NearestUnstaffedConstructionSite(Vector3 position, float within)
    {
        if (_simBridge == null || !_simBridge.HasMatch) return null;
        SimBuilding best = null;
        var bestSq = within * within;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            if (b.PlayerIndex != 0 || b.State != BuildingState.UnderConstruction || b.IsStaffed) continue;
            var d = WorldOf(b.Hex) - position;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = b; }
        }
        return best;
    }

    /// <summary>2026-08 (creator direction: "wander... radius around our
    /// buildings of 2 km" -- <see cref="Worker.TickWander"/>'s leash):
    /// nearest PLAYER-0 building of any kind/state -- HQ, Factory, mid-
    /// construction, everything -- unlike <see
    /// cref="NearestUnstaffedConstructionSite"/> above, which only counts
    /// unstaffed sites. Returns `position` itself (distance 0) if this
    /// player somehow owns no buildings at all, so callers never need a
    /// null case.</summary>
    public Vector3 NearestOwnBuildingPosition(Vector3 position)
    {
        if (_simBridge == null || !_simBridge.HasMatch) return position;
        var best = position;
        var bestSq = float.MaxValue;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            if (b.PlayerIndex != 0) continue;
            var w = WorldOf(b.Hex);
            var d = w - position;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = w; }
        }
        return best;
    }

    /// <summary>True if `position` is within `radius` of ANY player-0
    /// building -- the wander leash check. A player with multiple bases
    /// (expanded past their starting HQ/Factory) gets a leash around
    /// each one, not just the original spawn point.</summary>
    public bool IsWithinRangeOfOwnBuildings(Vector3 position, float radius)
    {
        if (_simBridge == null || !_simBridge.HasMatch) return false;
        var radiusSq = radius * radius;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            if (b.PlayerIndex != 0) continue;
            var d = WorldOf(b.Hex) - position;
            d.y = 0f;
            if (d.sqrMagnitude <= radiusSq) return true;
        }
        return false;
    }

    /// <summary>2026-08 (creator direction: Workers "must deliver
    /// scavenged parts to factories or whatever we have for collections
    /// centres"): nearest COMPLETE player-0 Factory to deliver an
    /// onboard load to -- same building-kind/state filter and "approach
    /// the rim, not the blocked center hex" fallback <see
    /// cref="MonsterAgent"/>'s own private `FindOwnFactory`/
    /// `FindOwnFactoryApproachHex` already established for harvester
    /// Monsters, mirrored here rather than shared/refactored so
    /// `Worker`'s delivery trip reaches the same real destination
    /// without touching that already-working Monster code path. Falls
    /// back to the nearest own building of ANY kind if no Factory exists
    /// yet (a fresh match, or one just got destroyed) -- deliver
    /// SOMEWHERE rather than never deliver at all.</summary>
    public Vector3 NearestOwnFactoryApproachPosition(Vector3 from)
    {
        if (_simBridge == null || !_simBridge.HasMatch) return NearestOwnBuildingPosition(from);
        HexCoord? factoryHex = null;
        var bestSq = float.MaxValue;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            if (b.PlayerIndex != 0 || b.Kind != BuildingKind.Factory || b.State != BuildingState.Complete) continue;
            var d = (WorldOf(b.Hex) - from).sqrMagnitude;
            if (d < bestSq) { bestSq = d; factoryHex = b.Hex; }
        }
        return factoryHex.HasValue ? ApproachPositionFor(factoryHex.Value, from) : NearestOwnBuildingPosition(from);
    }

    /// <summary>2026-08 bugfix (creator report: "do humans have workers?
    /// building are not getting built" -- root-caused to exactly this gap):
    /// a building's own hex is ALWAYS in <see cref="BlockedFor"/>'s ground
    /// set from the instant it's placed (every non-Destroyed `SimBuilding`,
    /// UnderConstruction included -- see <see cref="BlockedFor"/>'s own
    /// union), so a Worker can never actually STAND on a construction
    /// site's hex; <see cref="GroundPathFollower.SetGoal"/> already knows
    /// this and silently substitutes the nearest open NEIGHBOR hex as the
    /// real walkable destination. <see cref="Worker.TickSeekBuild"/> used
    /// to compare its own position against the site's blocked hex CENTER
    /// instead of that same substituted neighbor -- two adjacent hex
    /// centers are `HexCoord.HexMeters` (20m) apart, permanently outside
    /// `Worker.BuildReach` (3.5m), so a Worker parked on the correct
    /// neighbor hex could NEVER satisfy the arrival check, never issued
    /// `SetBuildingStaffed(true)`, and the site sat permanently paused
    /// (Unity's own `TickConstructionStaffing` unstaffs every fresh human
    /// build the instant it appears -- see that method's own comment) --
    /// worse, the Worker itself stayed permanently `BusyWorkers`-occupied
    /// (only released on the Complete transition, which could now never
    /// happen), so enough queued buildings would eventually exhaust
    /// `PlayerState.AvailableWorkers` and `CanPlaceBuilding` would start
    /// rejecting EVERY further placement too -- the "buildings are not
    /// getting built" report's other likely half. This is the SAME
    /// "approach the rim, not the blocked center hex" fix <see
    /// cref="NearestOwnFactoryApproachPosition"/> already had (extracted
    /// here as a general helper -- construction sites can be ANY
    /// `BuildingKind`, not just Factory, so this can't stay Factory-
    /// specific), just never applied to the build-staffing path at
    /// all.</summary>
    public Vector3 ApproachPositionFor(HexCoord hex, Vector3 from)
    {
        var blocked = BlockedFor(false);
        if (!blocked.Contains(hex)) return WorldOf(hex);
        HexCoord? best = null;
        var bestNeighborSq = float.MaxValue;
        foreach (var n in hex.Neighbors())
        {
            if (!_city.Contains(n) || blocked.Contains(n)) continue;
            var d = (WorldOf(n) - from).sqrMagnitude;
            if (d < bestNeighborSq) { bestNeighborSq = d; best = n; }
        }
        return WorldOf(best ?? hex);
    }

    /// <summary>2026-08 root-cause redesign (creator debug report:
    /// "Circular Following... Workers continuously attempt to follow one
    /// another, eventually forming circles... walking toward each other
    /// forever"; "Workers Stop After 30-40 Seconds"). This REPLACES the
    /// original herding mechanism (`TryFindJoinableHerd`, git history),
    /// which had no concept of a stable leader: every wandering Worker,
    /// on every re-pick, just copied a SNAPSHOT of whichever nearby
    /// wandering peer's CURRENT target it happened to see. Once two or
    /// more Workers clustered, there was no Worker left that was ever
    /// guaranteed to pick a genuinely fresh, independent point again --
    /// they just kept re-deriving targets from each other indefinitely,
    /// with no anchor. Depending on timing, a copied target could drift
    /// BACKWARD toward wherever the group had just come from, which is
    /// exactly what reads as circling/oscillating; in a tight cluster
    /// this degenerates toward near-zero net movement, which is what
    /// "stops after 30-40 seconds" looks like from outside. Provable
    /// from the old code as written, not something that needed a debug
    /// harness to find.
    ///
    /// The fix: a real two-role hierarchy, LEADER (picks its own
    /// independent wander targets, never follows anyone) and FOLLOWER
    /// (continuously tracks its leader's LIVE position every tick, never
    /// leads anyone) -- see <see cref="Worker.HerdLeaderId"/>/<see
    /// cref="Worker.IsHerdLeader"/>. The single invariant that makes
    /// A-follows-B-follows-C chains and A-follows-B/B-follows-A cycles
    /// STRUCTURALLY IMPOSSIBLE, not just unlikely: a Worker that
    /// currently HAS FOLLOWERS (<see cref="HasHerdFollowers"/>) must
    /// never itself become a follower (enforced entirely in
    /// `Worker.BeginWander`, this method only ever returns LEADERLESS
    /// candidates). Proof: define a directed edge X-&gt;Y whenever X
    /// follows Y. Every node with an in-edge (a leader, has a follower)
    /// has no out-edge (never follows anyone) by that one rule -- a
    /// graph where every node is EITHER a pure source (followers: one
    /// out-edge, never an in-edge) OR a pure sink (leaders: only
    /// in-edges, never an out-edge) cannot contain a cycle, because a
    /// cycle requires at least one node with both in-degree &gt; 0 and
    /// out-degree &gt; 0, which this rule forbids outright.</summary>
    public bool TryFindHerdLeader(Vector3 near, int excludeInstanceId, float joinRadius, int maxFollowers, out Worker leader)
    {
        leader = null;
        var joinRadiusSq = joinRadius * joinRadius;
        foreach (var w in _workers)
        {
            if (w == null || !w.IsWandering || !w.IsHerdLeader) continue;
            if (w.GetInstanceID() == excludeInstanceId) continue;
            var d = w.transform.position - near;
            d.y = 0f;
            if (d.sqrMagnitude > joinRadiusSq) continue;
            if (HasHerdFollowers(w.GetInstanceID(), maxFollowers)) continue;
            leader = w;
            return true;
        }
        return false;
    }

    /// <summary>True once `leaderInstanceId` already has `cap` or more
    /// followers -- keeps a herd bounded (the brief's own "groups of 3
    /// to 10," now an actual enforced cap on a real, stable leader,
    /// rather than the old mechanism's soft, no-anchor approximation of
    /// one). Also doubles as the "does this Worker currently have ANY
    /// followers at all" check `Worker.BeginWander` uses to decide
    /// whether it's allowed to become a follower itself -- call with
    /// `cap = 1` for that (true the instant there's at least one).</summary>
    public bool HasHerdFollowers(int leaderInstanceId, int cap = int.MaxValue)
    {
        var count = 0;
        foreach (var w in _workers)
        {
            if (w == null || w.HerdLeaderId != leaderInstanceId) continue;
            count++;
            if (count >= cap) return true;
        }
        return false;
    }

    /// <summary>A follower re-validates its leader by identity every
    /// tick (never a stale snapshot) -- looks it up by Unity's own
    /// per-instance ID rather than keeping a direct `Worker` reference,
    /// so a destroyed leader naturally resolves to "not found" (Unity's
    /// `==null` override on a destroyed MonoBehaviour would already
    /// catch a stale reference too, but going through <see
    /// cref="Workers"/> by ID also self-heals if the SAME instance ID
    /// were ever reused, which a raw cached reference could not).</summary>
    public Worker FindWorkerByInstanceId(int id)
    {
        foreach (var w in _workers)
            if (w != null && w.GetInstanceID() == id) return w;
        return null;
    }

    /// <summary>Ticked every <see cref="Update"/>: the instant a player-0
    /// building enters <c>UnderConstruction</c>, queues one <see
    /// cref="SimBridge.QueueSetBuildingStaffedCommand"/> pausing it
    /// (`IsStaffed` defaults true match-core-side, exactly the pre-2026-08
    /// behavior, so it has to be Unity that actively pauses a fresh
    /// human build the instant it notices one -- see <see
    /// cref="SimBuilding.IsStaffed"/>'s own header for why match-core
    /// itself can't default this per-player). A Worker/Zombie's own AI
    /// (<see cref="Worker.TickSeekBuild"/>) then un-pauses it once one
    /// physically arrives. AI opponents (any other PlayerIndex) never
    /// receive this command at all, so their construction is completely
    /// unaffected.</summary>
    private void TickConstructionStaffing()
    {
        if (_simBridge == null || !_simBridge.HasMatch) return;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            if (b.PlayerIndex != 0 || b.State != BuildingState.UnderConstruction) continue;
            if (!_constructionPauseHandled.Add(b.EntityId)) continue;
            _simBridge.QueueSetBuildingStaffedCommand(0, b.EntityId, false);
        }
    }

    /// <summary>docs/12 follow-up: the reclaim-eligibility gate for a
    /// PROCEDURAL building's own hex -- true once its wreck is either
    /// fully scavenged (<see cref="BuildingRuntimeState.IsFullyScavenged"/>)
    /// or <see cref="MatchState.DebrisDecayTicks"/> has passed unscavenged
    /// (the same decaying-if-unlooted fallback the RTS SimBuilding
    /// roster's own dual gate already uses, reusing ITS constant directly
    /// rather than duplicating the number -- see that gate's own doc
    /// comment for why an unlooted wreck can never block a hex forever).
    /// False (not yet eligible) for anything not actually Destroyed, or
    /// with no `DestroyedAtFrame` stamp to measure decay against.</summary>
    private bool IsReclaimEligible(BuildingRuntimeState state)
    {
        if (state.Stage != DamageStage.Destroyed) return false;
        if (state.IsFullyScavenged) return true;
        if (!state.DestroyedAtFrame.HasValue || _simBridge == null) return false;
        return _simBridge.CurrentFrame - state.DestroyedAtFrame.Value >= MatchState.DebrisDecayTicks;
    }

    /// <summary>Lazily resolves and applies <see cref="IsReclaimEligible"/>
    /// for whichever Destroyed procedural building (if any) owns `hex`,
    /// unblocking its WHOLE footprint in match-core's own build-placement
    /// gate the instant it becomes eligible. Called reactively right
    /// before a live placement check (<see cref="BuildGhostCursor"/>'s own
    /// per-frame preview) rather than proactively swept every frame for
    /// every destroyed building on the map -- nothing needs this hex's
    /// answer until someone is actually about to build there. Idempotent
    /// and cheap to call repeatedly (<see cref="SimBridge.
    /// UnblockProceduralBuildingHex"/> is itself a harmless no-op on an
    /// already-unblocked hex).</summary>
    public void TryReclaimHex(HexCoord hex)
    {
        if (_simBridge == null) return;
        foreach (var state in _battlefield.Buildings)
        {
            if (state.Stage != DamageStage.Destroyed) continue;
            var footprint = state.Building.Footprint;
            var owns = false;
            for (var i = 0; i < footprint.Count; i++) if (footprint[i] == hex) { owns = true; break; }
            if (!owns) continue;   // not this building's footprint -- keep scanning the rest
            if (!IsReclaimEligible(state)) return;
            foreach (var h in footprint) _simBridge.UnblockProceduralBuildingHex(h);
            return;
        }
    }

    /// <summary>A dark, flat, near-ground scorch mark under each footprint
    /// hex of a just-destroyed building -- the rubble pass darkens the
    /// wreckage itself, but left the ground it fell on unmarked. Terrain-
    /// following (GroundHeightAt), colliderless -- purely a scorched-earth
    /// read, no gameplay weight.</summary>
    private List<GameObject> SpawnScorchDecal(Building building, Transform parent)
    {
        // several small, irregular-sized patches per hex, NOT one big
        // disc spanning the whole footprint -- a single hex-wide circle
        // read as a "radiating puddle" from the RTS camera rather than
        // a scorch accent under the (now-shattered) rubble
        var mat = NewMaterial(new Color(0.12f, 0.11f, 0.1f));
        var decals = new List<GameObject>();
        foreach (var hex in building.Footprint)
        {
            var center = WorldOf(hex);
            var h = (hex.Q * 41 + hex.R * 17) & 0x7FFFFFFF;
            var patches = 2 + h % 2;
            for (var i = 0; i < patches; i++)
            {
                var hi = (h + i * 733) & 0x7FFFFFFF;
                var off = new Vector3((hi % 9) - 4f, 0f, ((hi >> 4) % 9) - 4f) * 0.6f;
                var pos = center + off;
                pos.y = GroundHeightAt(pos) + 0.05f;
                var size = 2.2f + (hi % 3) * 0.8f;
                var decal = SpawnPrim(PrimitiveType.Cylinder, pos,
                    new Vector3(size, 0.05f, size * (0.75f + (hi % 3) * 0.1f)), mat, parent);
                decal.name = "Scorch";
                decals.Add(decal.gameObject);
            }
        }
        return decals;
    }

    // ---- population ---------------------------------------------------------------

    private void SpawnCitizens()
    {
        var parent = new GameObject("Citizens").transform;
        parent.SetParent(transform, false);
        var blocked = BlockedFor(false);
        var spawned = 0;

        foreach (var hex in _city.CenterHex.Range(14))
        {
            if (spawned >= citizenCount) break;
            if (!_city.Contains(hex) || blocked.Contains(hex)) continue;
            // scatter: skip most candidates deterministically
            if ((hex.Q * 31 + hex.R * 17) % 5 != 0) continue;

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Citizen_" + spawned;
            capsule.transform.SetParent(parent, false);
            var citizen = capsule.AddComponent<Citizen>();
            citizen.Init(this, hex);
            _citizens.Add(citizen);
            spawned++;
        }
    }

    /// <summary>Every spawned monster -- the commander walks this for
    /// box-select and double-click select-all-of-type.</summary>
    public IReadOnlyList<MonsterAgent> Monsters { get { return _monsters; } }

    /// <summary>Every spawned citizen -- the minimap plots these as
    /// small cosmetic blips (docs/19, client-side crowd).</summary>
    public IReadOnlyList<Citizen> Citizens { get { return _citizens; } }

    /// <summary>Spawns a single Citizen at `hex`, already in its forced
    /// panic-flee state (2026-07: a destroyed base building "disgorges
    /// its human occupants that flee"). Called once per occupant by
    /// EITHER of this game's two separate building systems' own
    /// destruction path: <see cref="BaseDresser"/> the instant an RTS
    /// <see cref="SimBuilding"/> flips to Destroyed, or (2026-08, creator
    /// report: "I don't see people fleeing from the wreckage of the
    /// building" -- the gap was that only the RTS path called this)
    /// <see cref="ApplyBuildingDamage"/> the instant a PROCEDURAL
    /// civilian `Building` does, using `BuildingStats.Occupants` for the
    /// count instead of `BuildingDef.Occupants`. Same Citizen creation
    /// shape as <see cref="SpawnCitizens"/>'s match-start scatter, just
    /// triggered mid-match at a specific point instead of scattered at
    /// start.</summary>
    public Citizen SpawnFleeingOccupant(HexCoord hex)
    {
        var blocked = BlockedFor(false);
        var spawnHex = _city.Contains(hex) && !blocked.Contains(hex) ? hex : NearestOpenHex(hex, blocked);

        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Citizen_Occupant_" + _citizens.Count;
        var citizen = capsule.AddComponent<Citizen>();
        citizen.Init(this, spawnHex);
        citizen.InitFleeingFrom(WorldOf(hex));
        _citizens.Add(citizen);
        return citizen;
    }

    /// <summary>Nearest hex to `from` (including `from` itself) that's
    /// both inside the city and not blocked -- a destroyed building's own
    /// hex is blocked terrain, so a disgorged occupant needs somewhere
    /// open next to the wreck to actually stand on.</summary>
    private HexCoord NearestOpenHex(HexCoord from, HashSet<HexCoord> blocked)
    {
        if (_city.Contains(from) && !blocked.Contains(from)) return from;
        for (var ring = 1; ring <= 4; ring++)
        {
            foreach (var n in from.Ring(ring))
            {
                if (_city.Contains(n) && !blocked.Contains(n)) return n;
            }
        }
        return from;
    }

    /// <summary>2026-07 amendment: same shape as <see cref="NearestOpenHex"/>
    /// but a much wider ring search (base placement needs to reach across
    /// a whole map, e.g. an AI opponent's base near a map edge, unlike
    /// NearestOpenHex's own nearby-disgorge-point use case) and an
    /// EXCLUDE set on top of the terrain-blocked set, so two hexes picked
    /// in the same <see cref="SpawnStartingBases"/> call never collide
    /// even though match-core's own building-blocked set (mutated by
    /// SpawnHqForPlayer/SpawnFactoryForPlayer) isn't visible to Unity's
    /// own <see cref="BlockedFor"/> query. Falls back to `from` itself if
    /// nothing opens up within range -- same honest "don't invent a
    /// placement, just use the best guess available" contract
    /// <see cref="MatchState.FindOpenHexNear"/>'s sim-side twin
    /// documents for its own analogous silent-fallback case.</summary>
    private HexCoord FindOpenHexWide(HexCoord from, HashSet<HexCoord> blocked, HashSet<HexCoord> exclude, int maxRing)
    {
        if (_city.Contains(from) && !blocked.Contains(from) && !exclude.Contains(from)) return from;
        for (var ring = 1; ring <= maxRing; ring++)
            foreach (var n in from.Ring(ring))
                if (_city.Contains(n) && !blocked.Contains(n) && !exclude.Contains(n)) return n;
        return from;
    }

    /// <summary>docs/30 (selectable races + AI opponents): N points around
    /// `center` for N-1 AI opponents (player 0, the human, always seeds
    /// from `center` itself unchanged -- see <see
    /// cref="SpawnStartingBases"/>) -- evenly spaced on a ring so 1-4
    /// opponents spread out around the human's own start instead of
    /// stacking toward one fixed offset the way the original single-
    /// opponent placeholder (`center.Q+18, center.R-9`) did. `radius` is a
    /// v0.1 placeholder (same order of magnitude as that original offset,
    /// ~20 hex units out) -- flagged, not claimed balanced, same standing
    /// policy as every other invented number in this file. The 0.6 R-axis
    /// scale corrects for this hex grid's own axial-to-roughly-square
    /// aspect (matching the shape of the original single-opponent offset's
    /// own Q:R ratio) so the ring reads round on the actual map, not
    /// squashed.</summary>
    private static HexCoord[] AiOpponentSeedRing(HexCoord center, int aiOpponentCount, int radius)
    {
        var seeds = new HexCoord[aiOpponentCount];
        for (var i = 0; i < aiOpponentCount; i++)
        {
            var angle = (2.0 * System.Math.PI / aiOpponentCount) * i;
            var q = center.Q + Mathf.RoundToInt(radius * (float)System.Math.Cos(angle));
            var r = center.R + Mathf.RoundToInt(radius * (float)System.Math.Sin(angle) * 0.6f);
            seeds[i] = new HexCoord(q, r);
        }
        return seeds;
    }

    /// <summary>v0.1 placeholder seed-ring radius (CLAUDE.md's standing
    /// "flag the invented number" policy) -- see <see
    /// cref="AiOpponentSeedRing"/>'s own doc comment.</summary>
    private const int AiOpponentSeedRingRadius = 20;

    /// <summary>2026-07 amendment (docs/12 "give the player one fully
    /// functional factory on startup"): place every configured player's
    /// starting HQ + Factory the instant a match exists, bypassing the
    /// worker-economy epic's own Collector->Worker->Factory bootstrap chain
    /// entirely for this ONE starting building per kind per player (see
    /// <see cref="MatchState.SpawnFactoryForPlayer"/>'s own doc comment).
    /// Site selection is a real, flagged v0.1 placeholder (CLAUDE.md's
    /// standing policy): player 0 (always human) near the city center,
    /// each AI opponent seeded from its own point on <see
    /// cref="AiOpponentSeedRing"/> so 1-4 opponents spread out around the
    /// human's start instead of crowding one one fixed offset -- not the
    /// "themed landmark site" docs/23 §2 eventually describes (no such
    /// landmark-selection logic exists anywhere yet), just distinct, valid,
    /// non-overlapping hexes.
    ///
    /// docs/30: generalizes the original 2-player-only version (player 0 +
    /// one hardcoded player-1 opponent) to loop over `playerFactions.Count`
    /// players -- `opponents[i]` (0-indexed) corresponds to player index
    /// `i + 1`, matching `playerFactions[i + 1]`.</summary>
    private void SpawnStartingBases(IReadOnlyList<FactionId> playerFactions, IReadOnlyList<AiOpponentConfig> opponents)
    {
        if (_simBridge == null) return;

        // 2026-07 fix (creator report: "the factory and central base is
        // in the middle of a road"): BlockedFor(false) only ever carries
        // water/rubble/standing-building footprints (BattlefieldState.
        // BlockedToGround -- roads are DELIBERATELY passable to units, so
        // they were never in that set), and FindOpenHexWide had nothing
        // else checking road overlap either -- a starting base could
        // genuinely land square on a road hex, no bug in the ring-search
        // itself, just a missing exclusion. Road (and bridge-deck) hexes
        // are unbuildable ground the same way water already is for this
        // specific placement, so they're unioned into `blocked` here
        // rather than touching BlockedToGround's own broader "can a unit
        // WALK here" contract, which must stay road-permissive.
        var blocked = new HashSet<HexCoord>(BlockedFor(false));
        blocked.UnionWith(RoadNetworkHexes());
        var claimed = new HashSet<HexCoord>();
        var center = _city.CenterHex;

        var p0Hq = FindOpenHexWide(center, blocked, claimed, 24);
        claimed.Add(p0Hq);
        _simBridge.SpawnHqForPlayer(0, p0Hq);
        _engagementCenters.Add(p0Hq); // docs/12 Tier 3
        var p0Factory = FindOpenHexWide(p0Hq, blocked, claimed, 24);
        claimed.Add(p0Factory);
        _simBridge.SpawnFactoryForPlayer(0, p0Factory);
        _engagementCenters.Add(p0Factory); // docs/12 Tier 3

        // 2026-08 (docs/12 tech-wing epic, Phase 1): CanPlaceBuilding now
        // requires an available Worker for EVERY human build, not just
        // Factory -- but Collector (the only way to ever GET a Worker in
        // a real match today) has no auto-spawn trigger of its own yet
        // (see SpawnCollector's own doc comment; its real production
        // path is a future Mad-Doctor mechanic that doesn't exist). Real
        // Worker gating with zero real way to ever earn a Worker would
        // brick the ENTIRE build menu for the rest of the match, not
        // just Factory -- same bootstrap-grant precedent
        // SpawnFactoryForPlayer itself already set (docs/12 "give the
        // player one fully functional factory on startup"), extended
        // here so that grant is actually usable.
        SpawnStartingWorkers(p0Hq, blocked, claimed);

        // 2026-08 (creator direction: "Human Army is from army barracks
        // -- part of the basic kit for Human army"): unlike the old
        // Soldier spawn this superseded, NOT scoped to the local human
        // only -- see the opponent loop below for the AI-opponent half.
        if (chosenFaction == FactionId.HumanArmy) SpawnStartingBarracks(0, p0Hq, blocked, claimed);

        var opponentSeeds = AiOpponentSeedRing(center, opponents.Count, AiOpponentSeedRingRadius);
        for (var i = 0; i < opponents.Count; i++)
        {
            var playerIndex = i + 1;
            var hq = FindOpenHexWide(opponentSeeds[i], blocked, claimed, 24);
            claimed.Add(hq);
            _simBridge.SpawnHqForPlayer(playerIndex, hq);
            _engagementCenters.Add(hq); // docs/12 Tier 3
            var factory = FindOpenHexWide(hq, blocked, claimed, 24);
            claimed.Add(factory);
            _simBridge.SpawnFactoryForPlayer(playerIndex, factory);
            _engagementCenters.Add(factory); // docs/12 Tier 3

            var faction = opponents[i].Faction;
            if (faction == FactionId.HumanArmy || faction == FactionId.AlienHive)
                SpawnOpponentStartingArmy(playerIndex, faction, opponents[i].Personality, hq, blocked, claimed);

            // 2026-08 (creator direction: "Human Army is from army
            // barracks -- part of the basic kit for Human army"): every
            // HumanArmy player gets one, opponents included -- without
            // this, an AI opponent's ProductionAdvisor (which now treats
            // Barracks as a valid idle producer alongside Factory) would
            // have no Barracks to ever actually train Rifleman/
            // FlamethrowerTrooper from mid-match.
            if (faction == FactionId.HumanArmy) SpawnStartingBarracks(playerIndex, hq, blocked, claimed);
        }

        SpawnHostileCivilians(center, blocked, claimed);
        SpawnAngryMob(center, blocked, claimed);
    }

    /// <summary>v0.1 placeholder (CLAUDE.md's standing "flag the invented
    /// number, don't pretend it's balanced" policy) -- see <see
    /// cref="SpawnStartingBases"/>'s own call site for why this bootstrap
    /// exists at all. Not claimed balanced against anything; just enough
    /// that the build menu is usable from turn one. 2026-08 creator
    /// direction: "The game needs to initialize with 30 works so player
    /// can build a new building" -- raised from the original 2.</summary>
    private const int StartingWorkerCount = 30;

    /// <summary>docs/12 tech-wing epic, Phase 1: spawn the human player's
    /// free starting Workers near their HQ, real Unity `Worker` units
    /// registered with match-core exactly like <see
    /// cref="OnCitizenPossessed"/>'s own possess-arrival path (same
    /// `_workers`/`_combatants` bookkeeping, same <see
    /// cref="SimBridge.QueueRegisterWorkerCommand"/> call) -- just
    /// skipping the Collector-capture chain, the same "grant the end
    /// state directly" precedent <see cref="SpawnFactoryForPlayer"/>
    /// already set for the starting Factory itself.</summary>
    private void SpawnStartingWorkers(HexCoord nearHex, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed)
    {
        for (var i = 0; i < StartingWorkerCount; i++)
        {
            var hex = FindOpenHexWide(nearHex, blocked, claimed, 24);
            claimed.Add(hex);
            var go = new GameObject("Worker_" + _workers.Count);
            go.transform.position = WorldOf(hex);
            var worker = go.AddComponent<Worker>();
            worker.Init(this);
            _workers.Add(worker);
            if (worker.Combat != null) _combatants.Add(worker.Combat);
            if (_simBridge != null) _simBridge.QueueRegisterWorkerCommand(0);
        }
    }

    /// <summary>v0.1 placeholder (same "flag the invented number" status
    /// as <see cref="StartingWorkerCount"/>) -- how many Rifleman come
    /// pre-trained the instant a Barracks is placed, same "one fully
    /// functional X on startup, plus a little on top" bootstrap shape
    /// <see cref="SpawnFactoryForPlayer"/> already established for
    /// Factory.</summary>
    private const int StartingRiflemanCount = 4;

    /// <summary>2026-08 (creator direction: "Human Army is from army
    /// barracks -- part of the basic kit for Human army", confirmed as a
    /// real production building, not a cosmetic prop -- see <see
    /// cref="BuildingKind.Barracks"/>'s own doc comment). SUPERSEDES the
    /// old `SpawnStartingSoldiers` (deleted, not kept alongside this --
    /// the cosmetic-only-adjacent `HumanCombatProfile.Soldier` local-AI
    /// garrison it spawned is now redundant with a REAL, visible,
    /// ongoing-producible Rifleman via <see cref="RosterInfantryView"/>,
    /// and the "correction, not retraction" convention this project
    /// follows (CLAUDE.md) means replacing it outright rather than
    /// running two parallel "Human Army infantry" spawns side by side).
    /// Places one free starting Barracks (same bootstrap-grant contract
    /// as <see cref="SpawnFactoryForPlayer"/> -- Complete immediately, no
    /// cost) and seeds it with <see cref="StartingRiflemanCount"/>
    /// already-trained Rifleman via <see
    /// cref="SimBridge.SpawnRosterUnit"/> (the SAME bootstrap path <see
    /// cref="SpawnOpponentStartingArmy"/> already uses for an AI
    /// opponent's own opening force), so a fresh match doesn't wait on a
    /// real training queue just to have SOME visible infantry. Called for
    /// EVERY HumanArmy player -- the local human here, and every
    /// HumanArmy AI opponent from <see cref="SpawnStartingBases"/>'s own
    /// opponent loop -- unlike the old Soldier spawn, which was
    /// deliberately local-human-only.</summary>
    private void SpawnStartingBarracks(int playerIndex, HexCoord nearHex, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed)
    {
        var barracksHex = FindOpenHexWide(nearHex, blocked, claimed, 24);
        claimed.Add(barracksHex);
        _simBridge.SpawnBarracksForPlayer(playerIndex, barracksHex);

        for (var i = 0; i < StartingRiflemanCount; i++)
        {
            var hex = FindOpenHexWide(barracksHex, blocked, claimed, 24);
            claimed.Add(hex);
            _simBridge.SpawnRosterUnit(playerIndex, hex, RosterUnitKind.Rifleman);
        }
    }

    // 2026-08 (creator brief: "Refactor Human Soldiers & Armed Citizens
    // into Monster Variants" -- Grandma-in-a-wheelchair and Armed
    // Civilian): v0.1 placeholder counts (CLAUDE.md's standing "flag the
    // invented number" policy), same status as every other spawn-count
    // constant in this file. Neutral to every player -- these are city
    // threats, not aligned with whichever faction the human or any AI
    // opponent picked, so this spawns once per match regardless of
    // faction choice, unlike SpawnStartingWorkers/SpawnStartingBarracks.

    /// <summary>2026-08 (creator direction: "start building Citizen with
    /// guns, army etc"): how many hypothetical Citizens this roll
    /// actually samples per match -- a plausible neighborhood-sized
    /// population, not the whole city's real Citizen count (which this
    /// method doesn't touch at all; `Citizen.cs`'s own harmless
    /// background population is untouched, same docs/34 §0 scope cut as
    /// always). v0.1 placeholder, sized so the EXPECTED number of real
    /// armed-and-aggressive spawns lands in roughly the same range the
    /// old fixed "1 Grandma + 3 Armed Civilian" count did (docs/19 §3's
    /// 15% total armed rate x the 40% Aggressive-band fraction below x
    /// this sample size ≈ 3), not a claim about a real city's actual
    /// armed-citizen rate.</summary>
    private const int CitizenRollSampleSize = 50;

    // docs/19 §2's own aggression bands: Aggressive is 0.6-1.0. Only
    // this band "may attack proactively if armed" (§3) -- Defensive
    // civilians "fight only if cornered or attacked first," which isn't
    // a real spawned THREAT in the sense this method cares about (no
    // proactive HumanoidCombatant to place), and Passive "always flees."
    // A v0.1 simplification: only Aggressive-band armed rolls produce a
    // real spawn here; Defensive/Passive rolls are silently absorbed
    // back into the ordinary, unarmed-reading Citizen population this
    // method never touches.
    private const float AggressiveBandThreshold = 0.6f;

    // docs/19 §3's real weight table, as cumulative thresholds against a
    // single 0..1 roll: Unarmed 85%, Improvised melee +10% (=0.95),
    // Handgun +4% (=0.99), Shotgun/rifle-tier +1% (=1.0).
    private const float ImprovisedMeleeCumulative = 0.85f;
    private const float HandgunCumulative = 0.95f;
    private const float ShotgunTierCumulative = 0.99f;

    /// <summary>docs/19 §3/§4's own real weapon-roll table, wired to an
    /// actual spawn for the first time -- supersedes the old flat,
    /// always-present "1 Grandma + 3 Armed Civilian" count (docs/35 §4's
    /// own scope note flagged this as the natural follow-up). Each of
    /// <see cref="CitizenRollSampleSize"/> hypothetical Citizens rolls a
    /// weapon tier AND an independent aggression value (same "no
    /// UnityEngine.Random, deterministic per-index Frac hashing"
    /// convention this codebase uses throughout); only a roll that lands
    /// BOTH armed and Aggressive-band (§3: "may attack proactively if
    /// armed") becomes a real spawn -- Grandma is literally docs/19 §4's
    /// own worked example for the shotgun-tier roll, so that tier maps
    /// to her unconditionally; Handgun maps to Armed Civilian (the
    /// existing generic "ordinary armed civilian" profile); Improvised
    /// melee maps to <see cref="HumanCombatProfile.MobRioterRock"/>
    /// (built for the Angry Civilian Mob below, but its "citizen swinging
    /// something blunt" read fits an ordinary improvised-melee roll just
    /// as well outside a mob context). Scattered across the whole city,
    /// same golden-ratio angle/radius spacing the old fixed-count version
    /// already established.
    ///
    /// Police/SWAT/Hunter/Militia are deliberately NOT part of this roll
    /// -- they're professional/specialist archetypes (docs/35's own
    /// "Refactor Human Soldiers & Armed Citizens" roster), not organic
    /// outcomes of an ordinary citizen's weapon-access roll, so they get
    /// their own small fixed-count scattered spawn instead (same shape
    /// this whole method used before this rewrite), not folded into
    /// docs/19's table.</summary>
    private void SpawnHostileCivilians(HexCoord center, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed)
    {
        if (_simBridge == null) return;

        var spawnIndex = 0;
        for (var i = 0; i < CitizenRollSampleSize; i++)
        {
            var weaponRoll = Frac(i * 0.618034f + 0.05f);
            var aggressionRoll = Frac(i * 0.415236f + 0.71f);
            if (aggressionRoll < AggressiveBandThreshold) continue;   // Defensive/Passive -- no proactive threat, absorbed back into the ordinary population

            HumanCombatProfile profile;
            string label;
            if (weaponRoll < ImprovisedMeleeCumulative) continue;   // Unarmed (85%) -- nothing to spawn
            if (weaponRoll < HandgunCumulative) { profile = HumanCombatProfile.MobRioterRock(); label = "RowdyCitizen"; }
            else if (weaponRoll < ShotgunTierCumulative) { profile = HumanCombatProfile.ArmedCivilian(); label = "ArmedCivilian"; }
            else { profile = HumanCombatProfile.Grandma(); label = "Grandma"; }

            SpawnScatteredThreat(center, blocked, claimed, profile, label, spawnIndex);
            spawnIndex++;
        }

        SpawnNamedThreats(center, blocked, claimed, spawnIndex);
    }

    // v0.1 placeholder counts (CLAUDE.md's standing policy) -- specialist
    // archetypes, deliberately small and fixed rather than rolled (see
    // SpawnHostileCivilians's own doc comment for why).
    private const int PoliceCount = 2;
    private const int SwatCount = 1;
    private const int HunterCount = 2;
    private const int MilitiaCount = 2;

    private void SpawnNamedThreats(HexCoord center, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed, int startIndex)
    {
        var i = startIndex;
        for (var n = 0; n < PoliceCount; n++) SpawnScatteredThreat(center, blocked, claimed, HumanCombatProfile.Police(), "Police", i++);
        for (var n = 0; n < SwatCount; n++) SpawnScatteredThreat(center, blocked, claimed, HumanCombatProfile.Swat(), "Swat", i++);
        for (var n = 0; n < HunterCount; n++) SpawnScatteredThreat(center, blocked, claimed, HumanCombatProfile.Hunter(), "Hunter", i++);
        for (var n = 0; n < MilitiaCount; n++) SpawnScatteredThreat(center, blocked, claimed, HumanCombatProfile.Militia(), "Militia", i++);
    }

    /// <summary>Places one hostile_civilian-track threat, scattered
    /// across the whole city off its own `index` -- the exact golden-
    /// ratio angle/radius formula the old fixed-count SpawnHostileCivilians
    /// used, generalized to any index/count rather than a hardcoded small
    /// total. `AiOpponentSeedRing`'s own R-axis 0.6 correction for this
    /// hex grid's axial-to-square aspect is reused here for the same
    /// reason it always was.</summary>
    private void SpawnScatteredThreat(HexCoord center, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed,
        HumanCombatProfile profile, string label, int index)
    {
        var angle = Frac(index * 0.618034f + 0.13f) * Mathf.PI * 2f;
        var dist = Mathf.Lerp(25f, 90f, Frac(index * 0.381966f + 0.37f));
        var q = center.Q + Mathf.RoundToInt(dist * Mathf.Cos(angle));
        var r = center.R + Mathf.RoundToInt(dist * Mathf.Sin(angle) * 0.6f);
        var seedHex = new HexCoord(q, r);
        if (!_city.Contains(seedHex)) seedHex = center;

        var hex = FindOpenHexWide(seedHex, blocked, claimed, 24);
        claimed.Add(hex);
        var go = new GameObject(label + "_" + index);
        var combatant = go.AddComponent<HumanoidCombatant>();
        combatant.Init(this, profile, WorldOf(hex));
    }

    // v0.1 placeholder (CLAUDE.md's standing policy) -- "10-15 packed
    // close together" (creator direction, verbatim); mid-range of that.
    private const int AngryMobSize = 12;
    // ~30% carry a molotov instead of a rock -- the mob's own minority,
    // more-dangerous role (HumanCombatProfile.MobRioterMolotov's own doc
    // comment).
    private const float MolotovFraction = 0.3f;
    // "packed close together" -- a tight scatter radius, genuinely
    // different from SpawnScatteredThreat's 25-90m whole-city spread
    // (a single rioter is a city-wide threat; a MOB is one dense crowd
    // at one place).
    private const float AngryMobRadius = 6f;

    /// <summary>2026-08 (creator direction: "a angry civilian mob, 10-15
    /// packed close together. but weak citizen with rocks, molotov
    /// cocktails; area attack, but low damage. Visually appealing
    /// tho."). ONE cluster per match (a rare, distinct city event,
    /// unlike the whole-city-scattered singles <see
    /// cref="SpawnHostileCivilians"/> places) -- deterministic offset
    /// from `center`, then every rioter placed by real per-instance
    /// jitter within <see cref="AngryMobRadius"/> so they read as
    /// "packed close together" without literally stacking on one hex.
    /// Each rioter also gets a small per-instance BodyColor jitter (the
    /// maddr-aesthetic-preferences skill's own "group movement should
    /// read as a group of individuals, not a clump" principle, applied
    /// to color since these aren't independently pathed the way a
    /// Worker herd is) so a dozen rioters don't read as identical
    /// clones. No real area-of-effect damage mechanic (see
    /// <see cref="WeaponProfile.MolotovCocktail"/>'s own doc comment for
    /// why, honestly) -- "area attack" is a visual/fictional read here,
    /// not a second damage application to nearby units.</summary>
    private void SpawnAngryMob(HexCoord center, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed)
    {
        if (_simBridge == null) return;

        // A single deterministic offset from center, distinct from any
        // SpawnScatteredThreat index range so the two spawn systems'
        // golden-ratio streams don't accidentally correlate.
        var mobAngle = Frac(0.9017f) * Mathf.PI * 2f;
        var mobDist = Mathf.Lerp(40f, 100f, Frac(0.2601f));
        var mobQ = center.Q + Mathf.RoundToInt(mobDist * Mathf.Cos(mobAngle));
        var mobR = center.R + Mathf.RoundToInt(mobDist * Mathf.Sin(mobAngle) * 0.6f);
        var mobSeedHex = new HexCoord(mobQ, mobR);
        if (!_city.Contains(mobSeedHex)) mobSeedHex = center;
        var mobCenterHex = FindOpenHexWide(mobSeedHex, blocked, claimed, 24);

        for (var i = 0; i < AngryMobSize; i++)
        {
            var jitterAngle = Frac(i * 0.618034f + 0.41f) * Mathf.PI * 2f;
            var jitterDist = Mathf.Lerp(0f, AngryMobRadius, Frac(i * 0.381966f + 0.59f));
            var q = mobCenterHex.Q + Mathf.RoundToInt(jitterDist * Mathf.Cos(jitterAngle) * 0.35f);
            var r = mobCenterHex.R + Mathf.RoundToInt(jitterDist * Mathf.Sin(jitterAngle) * 0.35f * 0.6f);
            var seedHex = new HexCoord(q, r);
            if (!_city.Contains(seedHex)) seedHex = mobCenterHex;
            var hex = FindOpenHexWide(seedHex, blocked, claimed, 6);
            claimed.Add(hex);

            var isMolotov = Frac(i * 0.246f + 0.83f) < MolotovFraction;
            var profile = isMolotov ? HumanCombatProfile.MobRioterMolotov() : HumanCombatProfile.MobRioterRock();
            // per-instance color jitter -- a crowd of individuals, not
            // identical clones (struct copy, mutating the local copy
            // only, the shared preset is unaffected).
            var jitter = Frac(i * 0.539f + 0.17f) * 0.18f - 0.09f;
            profile.Visual.BodyColor = new Color(
                Mathf.Clamp01(profile.Visual.BodyColor.r + jitter),
                Mathf.Clamp01(profile.Visual.BodyColor.g + jitter * 0.8f),
                Mathf.Clamp01(profile.Visual.BodyColor.b + jitter * 0.6f));

            var go = new GameObject((isMolotov ? "MobMolotov_" : "MobRock_") + i);
            var combatant = go.AddComponent<HumanoidCombatant>();
            combatant.Init(this, profile, WorldOf(hex));
        }
    }

    private static float Frac(float v) { return v - Mathf.Floor(v); }

    /// <summary>v0.1 placeholder starting budget (CLAUDE.md's standing
    /// "flag the invented number, don't pretend it's balanced" policy,
    /// same status as every other tuning number in this codebase) -- big
    /// enough that <see cref="ArmyGenerator"/> fields a real, multi-unit
    /// opening force for either roster, not a token single unit.</summary>
    private static readonly Dictionary<ResourceKind, int> OpponentStartingArmyBudget = new Dictionary<ResourceKind, int>
    {
        { ResourceKind.Bones, 200 },
        { ResourceKind.Fuel, 200 },
        { ResourceKind.Ichor, 200 },
    };

    /// <summary>2026-08 (creator direction: "Create a faction based army
    /// generator. To start making opponents for the game"): generate a
    /// composition via <see cref="ArmyGenerator"/> and field it near the
    /// opponent's HQ. `personality` is passed IN (not regenerated here) --
    /// docs/30: this MUST be the exact same value driving this opponent's
    /// in-match behavior (<see cref="AiMatchDriver"/>, wired via <see
    /// cref="PlayerSetup.Ai"/> in <see cref="BeginMatch"/>), or a
    /// "Berserker"-labeled opponent could field a Turtle-weighted starting
    /// army -- see <see cref="AiOpponentConfig"/>'s own doc comment for why
    /// this is a real invariant, not a style preference. Budget-seeded off
    /// this match's own `seed` folded with `playerIndex` (docs/30: N
    /// opponents must NOT all draw from the identical stream, or opponent 1
    /// and opponent 2 field identical armies when both resolved from
    /// "Random") -- same decorrelation formula <see cref="AiMatchDriver"/>
    /// itself uses for its own per-player RNG streams. Each unit gets its
    /// own <see cref="FindOpenHexWide"/> placement, claimed into the same
    /// set the HQ/Factory hexes above already use, so a big army can't
    /// stack units on top of each other or the bases.</summary>
    private void SpawnOpponentStartingArmy(int playerIndex, FactionId opponentFaction, CommanderPersonality personality,
        HexCoord aroundHex, HashSet<HexCoord> blocked, HashSet<HexCoord> claimed)
    {
        var rngSeed = unchecked((uint)seed) ^ unchecked((uint)(playerIndex * 0x9E3779B1));
        var composition = ArmyGenerator.Generate(opponentFaction, personality, OpponentStartingArmyBudget, new SimRng(rngSeed));
        foreach (var (kind, count) in composition)
        {
            for (var i = 0; i < count; i++)
            {
                var hex = FindOpenHexWide(aroundHex, blocked, claimed, 24);
                claimed.Add(hex);
                _simBridge.SpawnRosterUnit(playerIndex, hex, kind);
            }
        }
    }

    /// <summary>Every spawned traffic car -- same minimap use as
    /// Citizens above.</summary>
    public IReadOnlyList<TrafficCar> TrafficCars { get { return _trafficCars; } }

    public MonsterAgent NearestMonsterTo(Vector3 position, float within)
    {
        MonsterAgent best = null;
        var bestSq = within * within;
        foreach (var m in _monsters)
        {
            if (m == null) continue;
            var d = m.transform.position - position;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq)
            {
                bestSq = d.sqrMagnitude;
                best = m;
            }
        }
        return best;
    }

    /// <summary>2026-07 creator direction ("if not attacking and humans
    /// are around monsters will chase and consume them"): the citizen
    /// counterpart to <see cref="NearestMonsterTo"/> and <see
    /// cref="NearestEnemyOf"/> -- same squared-distance nearest-of-type
    /// scan, called from <see cref="MonsterAgent.AcquireTarget"/>'s idle
    /// fallback once combat has nothing to engage.</summary>
    public Citizen NearestCitizenTo(Vector3 position, float within)
    {
        Citizen best = null;
        var bestSq = within * within;
        foreach (var z in _citizens)
        {
            if (z == null) continue;
            var d = z.transform.position - position;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq)
            {
                bestSq = d.sqrMagnitude;
                best = z;
            }
        }
        return best;
    }

    /// <summary>A harvester unloads its onboard tank (docs/22). Called
    /// when a laden harvester idles near its home/Vat or (per the newer
    /// Factory-delivery loop) its own Factory. Credits match-core's real
    /// per-player wallet via <see cref="GrantReal"/> per resource lane --
    /// ResourceHud reads that same wallet.
    ///
    /// 2026-08 (creator direction: "humans have all the resources, make
    /// sure those are properly being harvested... all harvesters can
    /// collect all resources"): previously banked the WHOLE pooled load
    /// as pure Blood regardless of what actually filled the tank --
    /// `HarvestProfile` already computes three separate gather rates
    /// (Blood/Bones/Brains, per the creature's own hand-tool family), but
    /// nothing downstream ever read the other two, so a `bone_saw`-handed
    /// harvester (efficient at Bones, weak at Blood) still only ever
    /// delivered a trickle of pure Blood -- a real "specialization tax"
    /// with no offsetting benefit, not the intended "every harvester
    /// collects everything, just at different rates" design. Now takes
    /// each carried lane separately and banks whichever are actually
    /// nonzero.
    ///
    /// 2026-08 follow-up (creator direction: "check that monsters can
    /// harvest metal and other building salvage" -&gt; "carry it home"):
    /// a fourth, optional `parts` lane -- scavenged building debris
    /// (<see cref="MonsterAgent.CreditHarvestForScavengedDebris"/>) banks
    /// through this SAME method, same as Blood/Bones/Brains. Defaults to
    /// 0 so it's silently a no-op for every OTHER existing call shape,
    /// but this method has exactly one real caller today
    /// (`MonsterAgent`'s own idle-bank check), which was updated to pass
    /// its carried Parts lane alongside the other three.</summary>
    public void BankHarvestLoad(float blood, float bones, float brains, float parts = 0f)
    {
        var bankedBlood = Mathf.RoundToInt(blood);
        var bankedBones = Mathf.RoundToInt(bones);
        var bankedBrains = Mathf.RoundToInt(brains);
        var bankedParts = Mathf.RoundToInt(parts);
        if (bankedBlood <= 0 && bankedBones <= 0 && bankedBrains <= 0 && bankedParts <= 0) return;

        GrantReal(ResourceKind.Blood, bankedBlood);
        GrantReal(ResourceKind.Bones, bankedBones);
        GrantReal(ResourceKind.Brains, bankedBrains);
        GrantReal(ResourceKind.Parts, bankedParts);
        Debug.Log("Harvester banked " + bankedBlood + " blood, " + bankedBones + " bones, " + bankedBrains
            + " brains, " + bankedParts + " parts.");
    }

    // ---- real wallet spend/grant helpers (2026-08, docs/12 "eating
    // citizens" fix) ---------------------------------------------------
    //
    // Every one of these used to mutate a client-side shadow int
    // (WalletBlood/Bones/Brains) synchronously -- correct in isolation,
    // but completely disconnected from match-core's real PlayerState
    // wallet (the one ResourceHud/BuildMenuHud/every real purchase
    // actually reads/writes), which is why eating citizens could show 30
    // in one place and 6 in another (docs/12's dated entry has the full
    // story: "I have 30 bones in inventory screen but Train say I have
    // 6"). All spends/grants below go through the real wallet instead.
    //
    // Spending is a QUEUED command (SimBridge.QueueSpendResourceCommand)
    // that only actually lands on the NEXT sim tick -- so a naive "read
    // PlayerWallet, if enough queue a spend" check, called every
    // Update() frame (multiple frames elapse per tick), would double- or
    // triple-spend the same balance before the first spend actually
    // lands (GrabCursor's clone-production loop calls TrySpendBlood
    // exactly this way, once per frame, until it succeeds).
    // `_pendingSpend` reserves the amount locally the instant it's
    // queued and nets it out of every later same-tick read via <see
    // cref="EffectiveWallet"/>, then clears once <see
    // cref="SimBridge.CurrentFrame"/> actually advances (the queued
    // command has landed for real by then).

    /// <summary>The real wallet amount still available to spend THIS
    /// tick, net of anything already reserved by an earlier <see
    /// cref="TrySpendReal"/> call this same tick that hasn't landed in
    /// match-core yet. Always safe to call even with no live match
    /// (returns 0).</summary>
    public int EffectiveWallet(ResourceKind kind)
    {
        if (_simBridge == null || !_simBridge.HasMatch) return 0;
        if (_simBridge.CurrentFrame != _pendingSpendFrame)
        {
            _pendingSpendFrame = _simBridge.CurrentFrame;
            _pendingSpend.Clear();
        }
        _pendingSpend.TryGetValue(kind, out var reserved);
        return Mathf.Max(0, _simBridge.PlayerWallet(0, kind) - reserved);
    }

    /// <summary>Gated real spend -- false and unchanged if unaffordable
    /// (checked against <see cref="EffectiveWallet"/>, not the raw
    /// wallet, so repeated same-tick calls can't double-spend), true and
    /// queues a real <see cref="SimBridge.QueueSpendResourceCommand"/>
    /// otherwise. Same validation-not-clamping discipline match-core's
    /// own `PlayerState.TrySpend` follows.</summary>
    public bool TrySpendReal(ResourceKind kind, int amount)
    {
        if (amount <= 0 || _simBridge == null || !_simBridge.HasMatch) return false;
        if (EffectiveWallet(kind) < amount) return false;
        _pendingSpend.TryGetValue(kind, out var existing);
        _pendingSpend[kind] = existing + amount;
        _simBridge.QueueSpendResourceCommand(0, amount, kind);
        return true;
    }

    /// <summary>docs/26 Phase 10 (Special Attacks System): unblockable
    /// sink twin of <see cref="TrySpendReal"/> -- spends UP TO `amount`,
    /// clamped to whatever's actually available, never negative, never
    /// refuses. Matches docs/22 §1's "Floors, not stalls" design
    /// contract to the letter: "A depleted resource degrades a unit; it
    /// never disables, strands, or kills it... a player who ignores this
    /// entire system must still have a functional army." An empty
    /// wallet reads as "no more free lunch," never "out of bullets,
    /// can't fire."</summary>
    public void SpendRealClamped(ResourceKind kind, int amount)
    {
        if (amount <= 0) return;
        var spend = Mathf.Min(amount, EffectiveWallet(kind));
        if (spend > 0) TrySpendReal(kind, spend);
    }

    /// <summary>docs/26 Phase 10: cast-cost twin of <see
    /// cref="SpendRealClamped"/>, kept as its own named entry point since
    /// every existing call site already spends Blood+Bones together for
    /// a cast.</summary>
    public void SpendWalletForCast(int blood, int bones)
    {
        SpendRealClamped(ResourceKind.Blood, blood);
        SpendRealClamped(ResourceKind.Bones, bones);
    }

    /// <summary>Credits the real wallet via the same queued command
    /// every other income source (harvester banking, scavenger salvage)
    /// already uses.</summary>
    public void GrantReal(ResourceKind kind, int amount)
    {
        if (amount <= 0 || _simBridge == null || !_simBridge.HasMatch) return;
        _simBridge.QueueBankHarvestLoadCommand(0, amount, kind);
    }

    /// <summary>2026-07 (GrabCursor's clone-onto-Factory feature): a
    /// GATED spend, deliberately the opposite contract of <see
    /// cref="SpendWalletForCast"/>'s own "never blocks, floors at 0"
    /// design -- cloning a whole creature is a real purchase ("spawning
    /// more based on the amount of resources required"), not an
    /// unblockable economy sink. Thin named wrapper over <see
    /// cref="TrySpendReal"/> so <see cref="GrabCursor"/>'s own
    /// once-per-frame retry loop (call again next frame if this returns
    /// false) didn't need to change at all.</summary>
    public bool TrySpendBlood(int amount) => TrySpendReal(ResourceKind.Blood, amount);

    /// <summary>2026-08 fix (creator report: "I have 30 bones in
    /// inventory screen but Train say I have 6" -- traced back to THIS
    /// method, the actual source of the divergence): docs/20's per-
    /// citizen yield (Blood 2 / Bones 1 / Brains 1) now credits the REAL
    /// match-core wallet via <see cref="GrantReal"/>, the same wallet
    /// every purchase in the game actually spends from -- previously it
    /// only ever moved a client-side counter nothing else read.</summary>
    public void OnCitizenEaten(Citizen citizen)
    {
        GrantReal(ResourceKind.Blood, 2);
        GrantReal(ResourceKind.Bones, 1);
        GrantReal(ResourceKind.Brains, 1);
        CitizensEaten++;
        _citizens.Remove(citizen);
        if (citizen != null && _buildingsHost != null)
        {
            var pos = citizen.transform.position;
            pos.y = GroundHeightAt(pos);
            DamageFx.BloodSplatter(pos, _buildingsHost);
        }
        if (citizen != null) Object.Destroy(citizen.gameObject);
        Debug.Log("Citizen eaten (" + CitizensEaten + " total).");
    }

    /// <summary>Every spawned Collector -- for a future selection/order
    /// UI, same accessor shape as <see cref="Monsters"/>/<see cref="TrafficCars"/>.</summary>
    public IReadOnlyList<Collector> Collectors { get { return _collectors; } }

    /// <summary>Every possessed-into-Worker unit (2026-07 epic) -- Phase 3
    /// (worker-gated construction) queries this to check a player has an
    /// available worker before letting a build command through.</summary>
    public IReadOnlyList<Worker> Workers { get { return _workers; } }

    /// <summary>2026-07 epic: a Collector's capture arriving -- POSSESSES
    /// the citizen into a new Worker unit instead of eating it for
    /// resources (<see cref="OnCitizenEaten"/>'s sibling arrival path,
    /// same trigger site in <see cref="Citizen.Update"/>, different
    /// outcome). No wallet credit -- a possessed worker IS the payoff,
    /// not a resource transaction. Spawns the Worker at the citizen's own
    /// position/facing, parented under the same host transform as every
    /// other spawned-mid-match unit kind, then destroys the citizen
    /// GameObject exactly like the eaten path does.</summary>
    public void OnCitizenPossessed(Citizen citizen, UnitCombat collector)
    {
        if (citizen == null) return;
        var pos = citizen.transform.position;
        var go = new GameObject("Worker_" + _workers.Count);
        go.transform.position = pos;
        var worker = go.AddComponent<Worker>();
        worker.Init(this);
        _workers.Add(worker);
        if (worker.Combat != null) _combatants.Add(worker.Combat);
        _citizens.Remove(citizen);
        Object.Destroy(citizen.gameObject);
        // docs/12 tech-wing epic, Phase 1: only the human (player 0) ever
        // reaches this path -- Collector/Worker are Unity MonoBehaviours
        // with no player-index field of their own (see this method's own
        // "no wallet credit" framing above: they exist purely as the
        // local human's own possessed labor pool today). Keeps match-
        // core's PlayerState.WorkerCount in sync so CanPlaceBuilding's
        // new real Worker gate actually reflects this Worker existing.
        if (_simBridge != null) _simBridge.QueueRegisterWorkerCommand(0);
        Debug.Log("Citizen possessed into a Worker. Total workers: " + _workers.Count);
    }

    /// <summary>docs/12 tech-wing epic, Phase 1: a Worker died (<see
    /// cref="Worker.OnDied"/>) -- drops it from <see cref="Workers"/> (it
    /// used to just sit there forever at `Alive == false`, silently
    /// inflating `Workers.Count` for any caller checking it, including
    /// the ghost-cursor preview this same phase is making load-bearing)
    /// and unregisters it with match-core so <see
    /// cref="PlayerState.WorkerCount"/> stays in sync with reality.</summary>
    public void OnWorkerDied(Worker worker)
    {
        _workers.Remove(worker);
        if (_simBridge != null) _simBridge.QueueUnregisterWorkerCommand(0);
    }

    /// <summary>Spawns a Collector -- either a manual test/dev call
    /// (`loadout: null`, unchanged since the 2026-07 epic) or the real
    /// output of a Big Brain battalion order (<see
    /// cref="TickCollectorProduction"/>). Mirrors <see
    /// cref="SpawnFleeingOccupant"/>'s own status as a real, tested
    /// building block.</summary>
    public Collector SpawnCollector(HexCoord hex, CollectorClassDef loadout = null)
    {
        var go = new GameObject("Collector_" + _collectors.Count);
        go.transform.position = WorldOf(hex);
        var collector = go.AddComponent<Collector>();
        collector.Init(this, loadout);
        _collectors.Add(collector);
        if (collector.Combat != null) _combatants.Add(collector.Combat);
        return collector;
    }

    // ---- Collector Lab classes ("define them in the lab, as a class.
    // Like a battalion.") + Big Brain battalion production -- 2026-08,
    // docs/12 decision log. Closes the worker-economy epic's last open
    // thread: a player previously had no live way to ever field a
    // Collector at all (SpawnCollector above was manual-only). -------

    private const string CollectorClassesPrefsKey = "MadDr.CollectorClasses.v1";

    [System.Serializable]
    private class CollectorClassListWrapper
    {
        public System.Collections.Generic.List<CollectorClassDef> Items = new System.Collections.Generic.List<CollectorClassDef>();
    }

    /// <summary>One in-progress Big Brain training order -- the Unity-
    /// side twin of match-core's <c>SimBuilding.TrainingKind</c>/
    /// <c>TrainTicksRemaining</c> (same single-slot-per-building
    /// contract), needed because Collector isn't a match-core
    /// <c>SimUnit</c> (see Collector.cs's own header) so this can't just
    /// be a real <c>CommandKind.TrainUnit</c>.</summary>
    public class CollectorBattalionOrder
    {
        public CollectorClassDef Def;
        public HexCoord BuildingHex;
        public int Remaining;
        public float TimeToNextUnit;
    }

    /// <summary>Every class the player has saved in the Lab -- lazily
    /// loaded from <c>PlayerPrefs</c> on first access (no server round
    /// trip; see <see cref="CollectorClassDef"/>'s own header for why).</summary>
    public IReadOnlyList<CollectorClassDef> CollectorClasses
    {
        get
        {
            EnsureCollectorClassesLoaded();
            return _collectorClasses;
        }
    }

    /// <summary>Every Big Brain building currently mid-battalion, keyed
    /// by that building's entity ID -- read by <see
    /// cref="CollectorLabHud"/> to show live training progress.</summary>
    public IReadOnlyDictionary<uint, CollectorBattalionOrder> CollectorOrders { get { return _collectorOrders; } }

    private void EnsureCollectorClassesLoaded()
    {
        if (_collectorClassesLoaded) return;
        _collectorClassesLoaded = true;
        var json = PlayerPrefs.GetString(CollectorClassesPrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var wrapper = JsonUtility.FromJson<CollectorClassListWrapper>(json);
            if (wrapper != null && wrapper.Items != null) _collectorClasses.AddRange(wrapper.Items);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("CollectorClasses: failed to load saved classes -- " + e.Message);
        }
    }

    private void SaveCollectorClasses()
    {
        var wrapper = new CollectorClassListWrapper { Items = _collectorClasses };
        PlayerPrefs.SetString(CollectorClassesPrefsKey, JsonUtility.ToJson(wrapper));
    }

    /// <summary>"Define them in the lab, as a class" -- the Lab half of
    /// the feature. Upserts by name (case-insensitive), clamps
    /// BatchSize into [MinBatchSize, MaxBatchSize], persists locally.</summary>
    public void DefineCollectorClass(CollectorClassDef def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.Name)) return;
        EnsureCollectorClassesLoaded();
        def.BatchSize = Mathf.Clamp(def.BatchSize, CollectorClassDef.MinBatchSize, CollectorClassDef.MaxBatchSize);
        var idx = _collectorClasses.FindIndex(c => string.Equals(c.Name, def.Name, System.StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _collectorClasses[idx] = def; else _collectorClasses.Add(def);
        SaveCollectorClasses();
    }

    public void DeleteCollectorClass(string name)
    {
        EnsureCollectorClassesLoaded();
        var removed = _collectorClasses.RemoveAll(c => string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase));
        if (removed > 0) SaveCollectorClasses();
    }

    /// <summary>"Also add a way to do it in game" -- spends the class's
    /// TotalBonesCost up front as a real, gated purchase against the
    /// REAL match-core wallet (<see cref="SimBridge.PlayerWallet"/>, the
    /// same one <see cref="ResourceHud"/> displays), then trains the
    /// whole batch one unit at a time from the given Complete,
    /// player-0-owned Big Brain building. One order per building at a
    /// time, same single-in-progress-slot precedent match-core's own
    /// <c>SimBuilding.TrainingKind</c> uses for the real roster
    /// TrainUnit pipeline this mirrors client-side. Returns false
    /// (wallet untouched) on any validation failure -- building missing/
    /// not owned/not a Complete Big Brain, an order already running
    /// there, or simply unaffordable.
    ///
    /// 2026-08 fix (creator report: "I have 30 bones in inventory screen
    /// but Train say I have 6"): this used to check/spend against
    /// `RuntimeCityBuilder.WalletBones` -- a client-side counter that
    /// ONLY ever moved via <see cref="OnCitizenEaten"/> (+1/citizen) and
    /// this method's own old <c>TrySpendBones</c> call, completely
    /// disconnected from the real match-core wallet every other Bones
    /// SOURCE (harvester banking, scavenger salvage) and every other
    /// Bones SINK (building construction) actually reads/writes. Now
    /// spends via <see cref="TrySpendReal"/> (new -- see <see
    /// cref="CommandKind.SpendResource"/>'s own doc comment), the same
    /// real-wallet spend every other purchase in this class now uses, so
    /// "what Collector training can afford" and "what the wallet HUD
    /// shows" are finally the same number.</summary>
    public bool BeginCollectorBattalion(uint bigBrainEntityId, CollectorClassDef def)
    {
        if (def == null || _simBridge == null || !_simBridge.HasMatch) return false;
        if (_collectorOrders.ContainsKey(bigBrainEntityId)) return false;

        SimBuilding building = null;
        for (var i = 0; i < _simBridge.BuildingCount; i++)
        {
            var b = _simBridge.BuildingAt(i);
            if (b.EntityId == bigBrainEntityId) { building = b; break; }
        }
        if (building == null || building.PlayerIndex != 0 || building.Kind != BuildingKind.BigBrain
            || building.State != BuildingState.Complete) return false;

        var clampedBatch = Mathf.Clamp(def.BatchSize, CollectorClassDef.MinBatchSize, CollectorClassDef.MaxBatchSize);
        var cost = def.BonesCostPerUnit * clampedBatch;
        if (!TrySpendReal(ResourceKind.Bones, cost)) return false;

        _collectorOrders[bigBrainEntityId] = new CollectorBattalionOrder
        {
            Def = def,
            BuildingHex = building.Hex,
            Remaining = clampedBatch,
            TimeToNextUnit = def.TrainSecondsPerUnit,
        };
        Debug.Log("Big Brain began training " + clampedBatch + " Collector(s) (\"" + def.Name + "\") for " + cost + " Bones.");
        return true;
    }

    /// <summary>Ticked from <see cref="Update"/> every frame: counts down
    /// each active order's per-unit timer and spawns one Collector (via
    /// the same <see cref="SpawnCollector"/> real production goes
    /// through) the instant it reaches zero, near the training
    /// building's own hex -- same <see cref="NearestOpenHex"/> fallback
    /// <see cref="SpawnFleeingOccupant"/> uses for "the building's own
    /// hex might be blocked terrain." Clears the order once the whole
    /// battalion has spawned, freeing that building's single slot.</summary>
    private void TickCollectorProduction(float dt)
    {
        if (_collectorOrders.Count == 0) return;
        List<uint> finished = null;
        foreach (var kv in _collectorOrders)
        {
            var order = kv.Value;
            order.TimeToNextUnit -= dt;
            if (order.TimeToNextUnit > 0f) continue;

            var blocked = BlockedFor(false);
            var spawnHex = NearestOpenHex(order.BuildingHex, blocked);
            SpawnCollector(spawnHex, order.Def);

            order.Remaining--;
            if (order.Remaining <= 0)
            {
                if (finished == null) finished = new List<uint>();
                finished.Add(kv.Key);
            }
            else
            {
                order.TimeToNextUnit = order.Def.TrainSecondsPerUnit;
            }
        }
        if (finished != null)
        {
            foreach (var id in finished) _collectorOrders.Remove(id);
        }
    }

    public void SpawnWaypointMarker(Vector3 at)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "WaypointMarker";
        marker.transform.position = new Vector3(at.x, GroundHeightAt(at) + 0.15f, at.z);
        marker.transform.localScale = new Vector3(4f, 0.05f, 4f);
        var collider = marker.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(ShaderUtil.FindRenderableShader());
            mat.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            renderer.sharedMaterial = mat;
        }
        Object.Destroy(marker, 1.5f);
    }

    // ---- roster -------------------------------------------------------------------

    private void HandleRosterReady(RosterCache cache, bool wasFromCache)
    {
        Debug.Log("RuntimeCityBuilder: roster ready (" + cache.Creatures.Length + " creatures, "
            + (wasFromCache ? "from local cache, fetched " + cache.FetchedAtUtc : "live") + ")");
        RosterCreatures = cache.Creatures;

        if (_monstersHost == null)
        {
            var monsters = new GameObject("Monsters").transform;
            monsters.SetParent(transform, false);
            _monstersHost = monsters;
        }

        var center = _city.CenterHex;
        var blockedToGround = BlockedFor(false);
        var landingSpots = new List<HexCoord>();
        foreach (var hex in center.Range(6))
            if (_city.Contains(hex) && !blockedToGround.Contains(hex)) landingSpots.Add(hex);

        for (var i = 0; i < cache.Creatures.Length; i++)
        {
            var creature = cache.Creatures[i];
            var home = landingSpots.Count > 0 ? landingSpots[i % landingSpots.Count] : center;
            var agent = SpawnMonster(creature, home);

            // docs/27 Phase A dev check: opt the FIRST spawned monster
            // into sim-driven movement, and nothing else -- left-click it,
            // right-click to move it, exactly the existing workflow this
            // class's own header comment describes. Everything else about
            // it (combat, special attacks, eating, flying) is untouched;
            // only its Move order routes through match-core now.
            // 2026-07 amendment: the HUD wiring (moon dial, build menu,
            // ghost cursor, BaseDresser, resource HUD) used to live in
            // THIS block because it was the only place a real _simBridge
            // was guaranteed to exist. It now happens unconditionally in
            // BeginMatch (see the "match-core state" wiring below this
            // loop) since a real match always exists -- this block is only
            // the docs/27 sim-driven-MOVEMENT demo now, its own original,
            // narrower purpose.
            if (simDrivenDemo && i == 0)
            {
                Debug.Log("docs/27: sim-driven demo active on " + agent.gameObject.name + " -- left-click it, right-click to move it.");
                agent.EnableSimDriven(_simBridge, playerIndex: 0, atHex: home, speed: 6.0);
            }
        }
    }

    /// <summary>Build one live MonsterAgent from a genome, register it with
    /// this builder (`_monsters`/`_combatants`), and return it -- factored
    /// out of `HandleRosterReady`'s own spawn loop (2026-07) so
    /// <see cref="GrabCursor"/>'s clone-onto-Factory feature can spawn a
    /// COPY of an already-live creature's own genome the exact same way
    /// the match-start roster fetch spawns the original, rather than a
    /// second, drifting copy of this logic. Requires <see cref="_monstersHost"/>
    /// to already exist (both current call sites -- the roster-ready loop
    /// and GrabCursor -- only ever run after a match/city exists).</summary>
    public MonsterAgent SpawnMonster(StoredGenomeDto creature, HexCoord home)
    {
        var root = new GameObject("Monster_" + creature.Id);
        root.transform.SetParent(_monstersHost, false);
        var agent = root.AddComponent<MonsterAgent>();
        agent.Init(this, creature, home);
        _monsters.Add(agent);
        if (agent.Fighter != null) _combatants.Add(agent.Fighter);
        return agent;
    }

    /// <summary>Enemy tanks at the city edge -- the combat test dummies.
    /// Half carry a flamethrower, half a cannon; they roll in toward the
    /// nearest monster and open fire.</summary>
    private void SpawnTanks()
    {
        if (tankCount <= 0) return;
        var center = _city.CenterHex;
        var blocked = BlockedFor(false);

        // require the hex AND every immediate neighbor to be clear, not
        // just the hex itself -- a tank spawned right against a building's
        // edge has nowhere to go if ApplySeparation (another tank landing
        // on the same crowded ring slot) shoves it sideways, and the only
        // free direction happens to be into that wall
        var candidates = new List<HexCoord>();
        var maxD = 0;
        foreach (var hex in center.Range(28))
        {
            if (!_city.Contains(hex) || blocked.Contains(hex)) continue;
            var clear = true;
            foreach (var n in hex.Neighbors())
                if (blocked.Contains(n)) { clear = false; break; }
            if (!clear) continue;
            var d = hex.DistanceTo(center);
            if (d > maxD) maxD = d;
            candidates.Add(hex);
        }
        if (candidates.Count == 0) return;

        // prefer the outer ring so tanks advance inward toward the roster
        var ring = new List<HexCoord>();
        foreach (var hex in candidates)
            if (hex.DistanceTo(center) >= maxD - 3) ring.Add(hex);
        if (ring.Count == 0) ring = candidates;

        var host = new GameObject("Tanks").transform;
        host.SetParent(transform, false);
        for (var i = 0; i < tankCount; i++)
        {
            var spot = ring[(i * 7 + 3) % ring.Count];   // spread around the ring, deterministically
            var go = new GameObject("Tank_" + i);
            go.transform.SetParent(host, false);
            go.transform.position = WorldOf(spot);
            var tank = go.AddComponent<Tank>();
            tank.Init(this, i % 2 == 1);   // alternate cannon / flamethrower
            _tanks.Add(tank);
            if (tank.Combat != null) _combatants.Add(tank.Combat);
        }
    }

    /// <summary>Road hexes plus every bridge deck hex, unioned once --
    /// the network TrafficCar drives and RoadDresser's connector math
    /// already computes per-hex; cached since the road layout never
    /// changes after generation (only buildings take damage).</summary>
    public HashSet<HexCoord> RoadNetworkHexes()
    {
        if (_roadNetwork == null)
        {
            _roadNetwork = new HashSet<HexCoord>(_city.Roads);
            foreach (var bridge in _city.Bridges)
                foreach (var hex in bridge.Footprint) _roadNetwork.Add(hex);
        }
        return _roadNetwork;
    }

    /// <summary>Docs/19 traffic (docs/21 batch 2, item 9): cars that
    /// drive the road network in bounded trips, park at the curb between
    /// them (trafficMovingPercent is the target fraction driving at any
    /// moment), and flee monsters like Citizens do. Colliderless --
    /// cosmetic crowd, not an order target or an obstacle.</summary>
    private void SpawnTraffic()
    {
        if (trafficCarCount <= 0) return;
        var network = RoadNetworkHexes();
        var hexes = new List<HexCoord>(network);
        if (hexes.Count == 0) return;

        var host = new GameObject("Traffic").transform;
        host.SetParent(transform, false);
        for (var i = 0; i < trafficCarCount; i++)
        {
            var start = hexes[(i * 37 + 5) % hexes.Count];
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TrafficCar_" + i;
            go.transform.SetParent(host, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var car = go.AddComponent<TrafficCar>();
            var hue = (i * 53 % 100) / 100f;
            car.Init(this, network, start, Color.HSVToRGB(hue, 0.4f, 0.75f), trafficMovingPercent);
            _trafficCars.Add(car);
        }
    }

    private const int TramCarCount = 2;

    /// <summary>docs/23's mood-board streetcar (see TramDresser's own
    /// header for the full "why"), New-York-only per that same mood-
    /// board's own hedge -- a no-op for every other region/preset,
    /// exactly like every other call in this method that's gated behind
    /// its own real prerequisite.</summary>
    private void SpawnTram()
    {
        if (_city.Region != CityRegion.NewYork) return;

        var line = TramDresser.TraceLine(_city);
        if (line.Count < 2) return;
        var path = TramDresser.Build(this, _city, line, transform);
        if (path.Count < 2) return;

        var host = new GameObject("Trams").transform;
        host.SetParent(transform, false);
        for (var i = 0; i < TramCarCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TramCar_" + i;
            go.transform.SetParent(host, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var tram = go.AddComponent<TramCar>();
            // spread cars evenly along the line rather than bunched at one end
            var startIndex = (path.Count * (i + 1)) / (TramCarCount + 1);
            tram.Init(this, path, startIndex);
        }
    }

    /// <summary>Live count of spawned traffic cars (0 if trafficCarCount
    /// is 0) -- HudStatus uses this to decide whether to show the traffic
    /// readout at all.</summary>
    public int TrafficCarCount { get { return _trafficCars.Count; } }

    /// <summary>The traffic field's ACTUAL measured fraction of the fleet
    /// currently driving, vs. trafficMovingPercent's target -- read this
    /// in HudStatus to confirm the target is actually being hit live,
    /// not just assumed from the derivation.</summary>
    public float TrafficMovingFraction
    {
        get
        {
            if (_trafficCars.Count == 0) return 0f;
            var driving = 0;
            foreach (var c in _trafficCars) if (c != null && c.IsDriving) driving++;
            return (float)driving / _trafficCars.Count;
        }
    }

    /// <summary>Nearest living combatant of the OPPOSING faction within
    /// range -- how a tank finds a monster and a monster finds a tank.</summary>
    public UnitCombat NearestEnemyOf(UnitCombat self, float range)
    {
        if (self == null) return null;
        UnitCombat best = null;
        var bestSq = range * range;
        var p = self.transform.position;
        foreach (var c in _combatants)
        {
            if (c == null || !c.Alive || c.Faction == self.Faction) continue;
            var d = c.transform.position - p;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = c; }
        }
        return best;
    }

    /// <summary>Soft body separation so units never stand inside each other
    /// ("creatures should NOT walk through each other"), with at least
    /// <see cref="groupSpacing"/> of daylight between their bodies once it
    /// stops pushing -- the Inspector-exposed knob for "how far apart do
    /// monsters end up spaced around a shared destination waypoint"
    /// (creator direction, 2026-07). Originally a hardcoded 1m constant
    /// (creator direction, 2026-07: "settled units are still too close
    /// together, at least 1 meter apart" -- the original "settles exactly
    /// touching" design read as bodies stacked with zero gap, worst right
    /// after a group creeps in via TickSettle); now a public field so a
    /// developer can retune the pack-in tightness without touching code.
    /// Each unit pushes HALF the overlap; the neighbor pushes its own half
    /// next frame, so a pair settles at exactly Radius + Radius +
    /// groupSpacing apart. Citizens are excluded on purpose -- they're
    /// prey, and monsters must be able to reach them.</summary>
    /// <summary>Rebuilds the docs/25 Phase A neighbour grid at most once per
    /// frame -- lazily, on whichever of ApplySeparation/SteerFollowPath runs
    /// first, so this has no dependency on Unity's per-component script
    /// execution order. `_maxCombatantRadius`/`_maxCombatantSpeed` are
    /// computed in the same pass (O(N), no extra scan) so query radii can
    /// grow to fit the largest/fastest combatant without re-walking the
    /// list -- `_maxCombatantSpeed` (docs/25 Phase C, from each unit's
    /// published `LastVelocity`) is what lets SteerFollowPath's query
    /// reach far enough to catch a neighbour that's still distant but
    /// closing fast, which a purely spatial reach would miss.</summary>
    private void RebuildCombatantGridIfNeeded()
    {
        if (_combatantGridFrame == Time.frameCount) return;
        _combatantGridFrame = Time.frameCount;
        _combatantGrid.Clear();
        _maxCombatantRadius = 0f;
        _maxCombatantSpeed = 0f;
        foreach (var c in _combatants)
        {
            if (c == null || !c.Alive) continue;
            _combatantGrid.Insert(c, c.transform.position);
            if (c.Radius > _maxCombatantRadius) _maxCombatantRadius = c.Radius;
            var speed = c.LastVelocity.magnitude;
            if (speed > _maxCombatantSpeed) _maxCombatantSpeed = speed;
        }
    }

    /// <summary>docs/26: everyone in `_combatants` within `radius` of
    /// `center`, via the SAME lazily-rebuilt neighbour grid ApplySeparation/
    /// SteerFollowPath already use -- for a special attack's
    /// area-of-effect resolution, not a second grid. `results` is cleared
    /// and filled; matches every other QueryRadius call in this file in
    /// returning a bounding-square superset, not an exact circle -- the
    /// caller still does its own exact-distance filter on the results.</summary>
    public void QueryCombatantsInRadius(Vector3 center, float radius, List<UnitCombat> results)
    {
        RebuildCombatantGridIfNeeded();
        results.Clear();
        _combatantGrid.QueryRadius(center, radius, results);
    }

    /// <summary>Hard positional correction -- unchanged since before docs/25
    /// (now delegating its per-pair math to
    /// MonsterSteeringController.SeparationForce, a pure extract, same
    /// numbers). Still the ONLY thing enforcing "creatures should NOT walk
    /// through each other" for every non-path-following tick: idle standing,
    /// the group-settle creep, holding position in weapon range -- none of
    /// those call FollowPath/SteerFollowPath, so none of them get the
    /// docs/25 Phase B blend below. Also called directly by Tank.cs
    /// (tanks are out of scope for the docs/25 migration; this stays their
    /// separation too).</summary>
    public void ApplySeparation(UnitCombat self)
    {
        if (self == null) return;
        RebuildCombatantGridIfNeeded();
        _separationQueryBuffer.Clear();
        _combatantGrid.QueryRadius(self.transform.position, self.Radius + _maxCombatantRadius + groupSpacing, _separationQueryBuffer);
        var push = MonsterSteeringController.SeparationForce(self, _separationQueryBuffer, groupSpacing);
        if (push.sqrMagnitude > 1e-8f) self.transform.position += push;   // push is XZ-only, y untouched
    }

    public void OnCombatantDied(UnitCombat c)
    {
        if (c != null) _combatants.Remove(c);
    }

    /// <summary>2026-08 (`HumanoidCombatant`, the shared armed-human kit):
    /// every existing spawner (Worker/Tank/MonsterAgent/Collector) adds
    /// itself to `_combatants` inline, from within this class's own
    /// spawn methods, since they already have direct field access there.
    /// `HumanoidCombatant` is a genuinely different class registering
    /// several variants from several call sites, so a small public
    /// counterpart to the already-public `OnCombatantDied` (registration
    /// in, not just death out) is worth having rather than requiring
    /// every future spawn call site to remember the one-liner itself.</summary>
    public void RegisterCombatant(UnitCombat c)
    {
        if (c != null && !_combatants.Contains(c)) _combatants.Add(c);
    }

    /// <summary>FollowPath's steering entry point (docs/25 Phase B,
    /// extended by Phases C and D), replacing the old plain AvoidanceDir
    /// call. Rebuilds/queries the same neighbour grid Phase A introduced,
    /// then hands the candidate list to MonsterSteeringController.Combine to
    /// blend seek against a softened separation force and (Phase C)
    /// time-to-collision predictive avoidance in one pass -- see that
    /// class's header for why (docs/25 section 2 root cause #1: seek and
    /// separation used to fight sequentially instead of blending). Query
    /// reach now covers three things at once: separation's own range, the
    /// avoidance padding, and however far a neighbour closing at
    /// `_maxCombatantSpeed` plus this unit's own speed could travel within
    /// `MonsterSteeringController.Horizon` seconds -- a purely spatial
    /// reach (Phase B's) would miss a fast-closing neighbour that's still
    /// distant right now. Before any of that: if DeadlockManager (Phase D)
    /// has granted this unit an active yield (`self.YieldUntil` still in
    /// the future), the seek direction fed into Combine is overridden to
    /// point at `self.YieldTarget` instead of wherever FollowPath's own
    /// path node was -- separation/avoidance still run normally against
    /// that redirected heading, so a yielding unit steps aside without
    /// shoving through anyone else to get there. This is the "steering
    /// controller honours [the] flag" half of the plan's architecture; the
    /// grant/expiry bookkeeping lives entirely in DeadlockManager.</summary>
    public MonsterSteeringController.SteeringResult SteerFollowPath(UnitCombat self, Vector3 desiredDir, float speed)
    {
        if (self == null) return new MonsterSteeringController.SteeringResult { Direction = desiredDir, SpeedScale = 1f };

        var effectiveDir = desiredDir;
        if (self.YieldUntil > Time.time && self.YieldTarget.HasValue)
        {
            var toYield = self.YieldTarget.Value - self.transform.position;
            toYield.y = 0f;
            if (toYield.sqrMagnitude > 0.04f) effectiveDir = toYield.normalized;
        }

        RebuildCombatantGridIfNeeded();
        _steerQueryBuffer.Clear();
        var closingReach = (speed + _maxCombatantSpeed) * MonsterSteeringController.Horizon;
        var reach = self.Radius + _maxCombatantRadius
            + Mathf.Max(groupSpacing, MonsterSteeringController.AvoidancePadding)
            + closingReach;
        _combatantGrid.QueryRadius(self.transform.position, reach, _steerQueryBuffer);
        // 2026-08: Time.time is passed in rather than read inside Combine so
        // the steering layer keeps its no-engine-dependency shape and stays
        // drivable from Tools~/SteerVerify. It backs the side-commitment
        // hold and the deadlock push-through window.
        return MonsterSteeringController.Combine(self, effectiveDir, speed, _steerQueryBuffer, groupSpacing, Time.time);
    }

    /// <summary>Distinct passable hexes clustered around `center`,
    /// nearest-first -- one parking slot per unit so a group ordered to a
    /// spot spreads out around it (each on its own hex, ~a hex apart)
    /// instead of stacking on one point. Pads with the center hex if the
    /// area is too hemmed-in to seat everyone.</summary>
    public List<HexCoord> FormationHexes(HexCoord center, int count)
    {
        var result = new List<HexCoord>();
        if (count <= 0) return result;
        var blocked = BlockedFor(false);

        var pool = new List<HexCoord>();
        var radius = 1;
        while (pool.Count < count && radius <= 6)
        {
            pool.Clear();
            foreach (var hex in center.Range(radius))
                if (_city.Contains(hex) && !blocked.Contains(hex)) pool.Add(hex);
            radius++;
        }
        pool.Sort((a, b) => center.DistanceTo(a).CompareTo(center.DistanceTo(b)));

        for (var i = 0; i < count; i++)
            result.Add(i < pool.Count ? pool[i] : center);
        return result;
    }

    private void HandleRosterFailed(string reason)
    {
        Debug.LogWarning("RuntimeCityBuilder: could not load a roster (" + reason + "). "
            + "Spawn a creature in the Lab, click Save to stable, and paste your Account ID into this component.");
    }

    private void HandleBattalionsReady(BattalionTemplateDto[] templates)
    {
        Debug.Log("RuntimeCityBuilder: " + templates.Length + " Lab battalion template(s) loaded.");
        LabBattalions = templates;
    }
}
