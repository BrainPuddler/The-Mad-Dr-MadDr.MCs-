using System.Collections.Generic;
using MadDr.CityGen;
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
public class RuntimeCityBuilder : MonoBehaviour
{
    public enum PresetChoice { Village, SmallTown, BigCity }

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

    [Tooltip("Traffic field: the target fraction of the fleet actively driving at any moment. The rest sit parked at the curb between bounded trips (drive N hops, park a while, repeat). 1 = every car always driving, never parks. Long-run average, not a per-frame guarantee -- see HudStatus for the live measured percentage.")]
    [Range(0.05f, 1f)]
    public float trafficMovingPercent = 0.55f;

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
    private int _cityVersion;
    private HashSet<HexCoord> _blockedGroundCache;
    private HashSet<HexCoord> _blockedAmphibiousCache;
    private int _blockedCacheVersion = -1;

    private readonly Dictionary<Collider, Building> _buildingByCollider = new Dictionary<Collider, Building>();
    private readonly Dictionary<Building, List<GameObject>> _cubesByBuilding = new Dictionary<Building, List<GameObject>>();
    private Transform _buildingsHost;
    private readonly List<MonsterAgent> _monsters = new List<MonsterAgent>();
    private readonly List<Citizen> _citizens = new List<Citizen>();
    private readonly List<Tank> _tanks = new List<Tank>();
    private readonly List<UnitCombat> _combatants = new List<UnitCombat>();
    private readonly List<TrafficCar> _trafficCars = new List<TrafficCar>();

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

    public CityModel City { get { return _city; } }
    public int CityVersion { get { return _cityVersion; } }
    public int WalletBlood { get; private set; }
    public int WalletBones { get; private set; }
    public int WalletBrains { get; private set; }
    public int CitizensEaten { get; private set; }

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

        _city = CityGenerator.Generate(unchecked((uint)seed), ResolvePreset());
        _battlefield = BattlefieldState.FreshFrom(_city);
        _terrain = new TerrainField(_city, _origin, unchecked((uint)seed));
        foreach (var lm in _city.Landmarks)
            if (lm.Archetype == "rail_depot") { _railyardCenter = lm.Site; break; }

        BuildGround();
        BuildTableEdge();
        BuildTerrainAndRoads();
        BuildBuildings();
        BuildBridges();
        BuildLandmarkAuras();
        SpawnCitizens();
        SpawnTanks();
        SpawnTraffic();

        if (GetComponent<NightMode>() == null) gameObject.AddComponent<NightMode>();

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

        var hud = gameObject.GetComponent<HudStatus>();
        if (hud == null) hud = gameObject.AddComponent<HudStatus>();
        hud.Init(this, commander);

        var bars = gameObject.GetComponent<HealthBars>();
        if (bars == null) bars = gameObject.AddComponent<HealthBars>();
        bars.Init(this);

        var fog = gameObject.GetComponent<FogOfWar>();
        if (fog == null) fog = gameObject.AddComponent<FogOfWar>();
        fog.Init(this);

        var minimap = gameObject.GetComponent<Minimap>();
        if (minimap == null) minimap = gameObject.AddComponent<Minimap>();
        minimap.Init(this, commander, fog);

        _roster = gameObject.GetComponent<RosterFetcher>();
        if (_roster == null) _roster = gameObject.AddComponent<RosterFetcher>();
        _roster.baseUrl = baseUrl;
        _roster.accountId = accountId;
        _roster.OnRosterReady += HandleRosterReady;
        _roster.OnRosterFailed += HandleRosterFailed;
        _roster.FetchRoster();
    }

    private const float TrafficCheckInterval = 4f;
    private const float TrafficBandTolerance = 0.2f; // +-20% of trafficMovingPercent, creator direction

    /// <summary>Docs/19 traffic field, corrective half: each car's OWN
    /// independent park timer (rolled at Init/ParkHere) already targets
    /// trafficMovingPercent on average, but a run of bad luck can drift
    /// the LIVE fraction driving well below it for a while with nobody
    /// due to depart soon. Rather than forcing an exact per-park-event
    /// swap (too rigid -- creator direction: "the next car(s) do not
    /// have to start immediately, we can have more cars on longer
    /// journeys"), this periodically checks the measured fraction and,
    /// only once it's dropped more than TrafficBandTolerance below
    /// target, wakes ONE currently-parked car early -- a loose band, not
    /// a lockstep swap. A rotating cursor spreads the early wake-ups
    /// across the fleet instead of always picking the same car.</summary>
    private void Update()
    {
        if (_trafficCars.Count == 0) return;
        _trafficCheckTimer -= Time.deltaTime;
        if (_trafficCheckTimer > 0f) return;
        _trafficCheckTimer = TrafficCheckInterval;

        var target = trafficMovingPercent;
        var lowBand = target * (1f - TrafficBandTolerance);
        if (TrafficMovingFraction >= lowBand) return;

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

    private HashSet<HexCoord> _waterSet;
    private HashSet<HexCoord> _roundaboutSet;

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

    /// <summary>The circulating-lane radius traffic follows around a
    /// roundabout island -- kept in sync with RoadDresser's ring so cars
    /// drive on the asphalt, not over the curb or off the edge.</summary>
    public const float RoundaboutLaneRadius = 7.4f;

    /// <summary>Distance to the nearest thing (traffic car, tank, or
    /// citizen) sitting AHEAD of `pos` along `dir` and within
    /// `laneHalfWidth` of that line, up to `maxRange`; `maxRange` if the
    /// lane is clear. A car reads this to keep a following gap (creator
    /// direction, 2026-07: "slow down if there is a human, car, tank
    /// something in front of them"). O(fleet) per call -- fine at
    /// tens of cars.</summary>
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

    private CityPreset ResolvePreset()
    {
        switch (preset)
        {
            case PresetChoice.SmallTown: return CityPreset.SmallTown();
            case PresetChoice.BigCity: return CityPreset.BigCity();
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
    /// city version (each BlockedTo*() call walks every building).</summary>
    public HashSet<HexCoord> BlockedFor(bool amphibious)
    {
        if (_blockedCacheVersion != _cityVersion)
        {
            _blockedGroundCache = _battlefield.BlockedToGround();
            _blockedAmphibiousCache = _battlefield.BlockedToAmphibious();
            _blockedCacheVersion = _cityVersion;
        }
        return amphibious ? _blockedAmphibiousCache : _blockedGroundCache;
    }

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
    /// winged unit perches on. Same tier table the visuals use.</summary>
    public float BuildingHeight(Building building)
    {
        return HeightForTier(building.Tier);
    }

    private Dictionary<HexCoord, float> _roofCache;
    private int _roofCacheVersion = -1;

    /// <summary>The standing surface at a world position: a STANDING
    /// building's roof height on its footprint hexes, 0 (street level)
    /// everywhere else -- including on rubble, so a perch whose building
    /// gets destroyed under it eases back down to the ground. Cached per
    /// city version; called per idle flyer per frame, so it has to be a
    /// dictionary hit, not a building-list walk.</summary>
    public float SurfaceHeightAt(Vector3 worldPos)
    {
        if (_roofCacheVersion != _cityVersion || _roofCache == null)
        {
            _roofCache = new Dictionary<HexCoord, float>();
            foreach (var b in _battlefield.Buildings)
            {
                if (!b.BlocksMovement) continue;
                var h = HeightForTier(b.Building.Tier);
                foreach (var hex in b.Building.Footprint) _roofCache[hex] = h;
            }
            _roofCacheVersion = _cityVersion;
        }
        float height;
        return _roofCache.TryGetValue(HexAt(worldPos), out height) ? height : 0f;
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
        RoadDresser.Build(this, _city, terrain, _railyardCenter);
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
        if (renderer != null) renderer.sharedMaterial = mat;
        return go;
    }

    private static int Mod(int x, int m)
    {
        return ((x % m) + m) % m;
    }

    private void BuildBuildings()
    {
        var buildings = new GameObject("Buildings").transform;
        buildings.SetParent(transform, false);
        _buildingsHost = buildings;

        // downtown vs suburb massing tint (docs/21 batch 2, item 10): a
        // building's hex distance from CenterHex stands in for road-graph
        // radius (the generator seeds density outward from the same
        // center) -- close in reads cooler/institutional, the outskirts
        // read warmer/residential
        var districtRadius = Mathf.Max(1, (_city.WidthHexes + _city.HeightHexes) / 4);
        var smallDowntown = NewMaterial(new Color(0.72f, 0.72f, 0.74f));
        var smallSuburb = NewMaterial(new Color(0.83f, 0.78f, 0.64f));
        var mediumDowntown = NewMaterial(new Color(0.5f, 0.52f, 0.62f));
        var mediumSuburb = NewMaterial(new Color(0.72f, 0.6f, 0.48f));
        var large = NewMaterial(new Color(0.35f, 0.35f, 0.7f));
        var landmark = NewMaterial(new Color(0.9f, 0.75f, 0.2f));

        foreach (var building in _city.Buildings)
        {
            var height = HeightForTier(building.Tier);
            var suburb = building.Footprint[0].DistanceTo(_city.CenterHex) > districtRadius * 0.55f;
            var industrial = _railyardCenter.HasValue
                && building.Footprint[0].DistanceTo(_railyardCenter.Value) <= RoadDresser.RailyardRadius;
            Material mat;
            switch (building.Tier)
            {
                case BuildingTier.Medium: mat = suburb ? mediumSuburb : mediumDowntown; break;
                case BuildingTier.Large: mat = large; break;
                case BuildingTier.Landmark: mat = landmark; break;
                default: mat = suburb ? smallSuburb : smallDowntown; break;
            }
            var cubes = new List<GameObject>();
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
            BuildingDresser.Dress(this, building, height, cubes, buildings, industrial, suburb);
            _cubesByBuilding[building] = cubes;
        }
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
        cube.GetComponent<Renderer>().sharedMaterial = mat;
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

    public void ApplyBuildingDamage(Building building, int amount)
    {
        BuildingRuntimeState current = null;
        foreach (var state in _battlefield.Buildings)
            if (ReferenceEquals(state.Building, building)) { current = state; break; }
        if (current == null || current.Stage == DamageStage.Destroyed) return;

        var next = current.ApplyDamage(amount);
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
                    if (_buildingsHost != null) RubbleDresser.Shatter(this, hex, pos0, rubbleMat, _buildingsHost);
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
                RubbleDresser.Scatter(this, building, rubbleMat, _buildingsHost);
                DamageFx.DustBurst(WorldOf(building.Footprint[0]), _buildingsHost);
                SpawnScorchDecal(building, _buildingsHost);
            }
            _cityVersion++;
            Debug.Log("Building destroyed -- rubble is now walkable.");
        }
        else if (next.Stage == DamageStage.Damaged && current.Stage == DamageStage.Intact)
        {
            // Intact -> Damaged visual: darken (docs/18's cracked state),
            // dressing included -- per-renderer material INSTANCES here,
            // never a tint on the shared cached dresser materials (that
            // would darken every building in the city at once)
            foreach (var cube in cubes)
            {
                foreach (var renderer in cube.GetComponentsInChildren<Renderer>())
                {
                    var mat = new Material(ShaderUtil.FindRenderableShader());
                    var c = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.gray;
                    mat.color = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f);
                    renderer.sharedMaterial = mat;
                }
            }
            // a lazy smoke plume for as long as the building stands damaged
            // (docs/21 batch 2, item 3)
            if (cubes.Count > 0) DamageFx.AttachSmoke(cubes[0].transform, BuildingHeight(building));
        }
    }

    /// <summary>A dark, flat, near-ground scorch mark under each footprint
    /// hex of a just-destroyed building -- the rubble pass darkens the
    /// wreckage itself, but left the ground it fell on unmarked. Terrain-
    /// following (GroundHeightAt), colliderless -- purely a scorched-earth
    /// read, no gameplay weight.</summary>
    private void SpawnScorchDecal(Building building, Transform parent)
    {
        // several small, irregular-sized patches per hex, NOT one big
        // disc spanning the whole footprint -- a single hex-wide circle
        // read as a "radiating puddle" from the RTS camera rather than
        // a scorch accent under the (now-shattered) rubble
        var mat = NewMaterial(new Color(0.12f, 0.11f, 0.1f));
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
            }
        }
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

    /// <summary>A harvester unloads its onboard tank into the session
    /// wallet (docs/22): the carried load is banked as Blood (fuel), the
    /// dominant lane a harvest tool draws. Called when a laden harvester
    /// idles near its home/Vat.</summary>
    public void BankHarvestLoad(float load)
    {
        var banked = Mathf.RoundToInt(load);
        if (banked <= 0) return;
        WalletBlood += banked;
        Debug.Log("Harvester banked " + banked + " blood. Wallet: " + WalletBlood + " blood.");
    }

    public void OnCitizenEaten(Citizen citizen)
    {
        // docs/20 per-citizen yield: Blood 2 / Bones 1 / Brains 1
        WalletBlood += 2;
        WalletBones += 1;
        WalletBrains += 1;
        CitizensEaten++;
        _citizens.Remove(citizen);
        if (citizen != null && _buildingsHost != null)
        {
            var pos = citizen.transform.position;
            pos.y = GroundHeightAt(pos);
            DamageFx.BloodSplatter(pos, _buildingsHost);
        }
        if (citizen != null) Object.Destroy(citizen.gameObject);
        Debug.Log("Citizen eaten. Wallet: " + WalletBlood + " blood / " + WalletBones + " bones / " + WalletBrains + " brains.");
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

        var monsters = new GameObject("Monsters").transform;
        monsters.SetParent(transform, false);

        var center = _city.CenterHex;
        var blockedToGround = BlockedFor(false);
        var landingSpots = new List<HexCoord>();
        foreach (var hex in center.Range(6))
            if (_city.Contains(hex) && !blockedToGround.Contains(hex)) landingSpots.Add(hex);

        for (var i = 0; i < cache.Creatures.Length; i++)
        {
            var creature = cache.Creatures[i];
            var home = landingSpots.Count > 0 ? landingSpots[i % landingSpots.Count] : center;

            var root = new GameObject("Monster_" + creature.Id);
            root.transform.SetParent(monsters, false);
            var agent = root.AddComponent<MonsterAgent>();
            agent.Init(this, creature, home);
            _monsters.Add(agent);
            if (agent.Fighter != null) _combatants.Add(agent.Fighter);
        }
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

    /// <summary>FollowPath's steering entry point (docs/25 Phase B,
    /// extended by Phase C), replacing the old plain AvoidanceDir call.
    /// Rebuilds/queries the same neighbour grid Phase A introduced, then
    /// hands the candidate list to MonsterSteeringController.Combine to
    /// blend seek against a softened separation force and (Phase C)
    /// time-to-collision predictive avoidance in one pass -- see that
    /// class's header for why (docs/25 section 2 root cause #1: seek and
    /// separation used to fight sequentially instead of blending). Query
    /// reach now covers three things at once: separation's own range, the
    /// avoidance padding, and however far a neighbour closing at
    /// `_maxCombatantSpeed` plus this unit's own speed could travel within
    /// `MonsterSteeringController.Horizon` seconds -- a purely spatial
    /// reach (Phase B's) would miss a fast-closing neighbour that's still
    /// distant right now.</summary>
    public MonsterSteeringController.SteeringResult SteerFollowPath(UnitCombat self, Vector3 desiredDir, float speed)
    {
        if (self == null) return new MonsterSteeringController.SteeringResult { Direction = desiredDir, SpeedScale = 1f };
        RebuildCombatantGridIfNeeded();
        _steerQueryBuffer.Clear();
        var closingReach = (speed + _maxCombatantSpeed) * MonsterSteeringController.Horizon;
        var reach = self.Radius + _maxCombatantRadius
            + Mathf.Max(groupSpacing, MonsterSteeringController.AvoidancePadding)
            + closingReach;
        _combatantGrid.QueryRadius(self.transform.position, reach, _steerQueryBuffer);
        return MonsterSteeringController.Combine(self, desiredDir, speed, _steerQueryBuffer, groupSpacing);
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
}
