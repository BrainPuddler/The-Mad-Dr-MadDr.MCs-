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
    [Tooltip("City seed: same seed + preset = identical city, every time (docs/18 determinism contract).")]
    public int seed = 42;

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

    // live state
    private CityModel _city;
    private BattlefieldState _battlefield;
    private Vector3 _origin;
    private RosterFetcher _roster;
    private int _cityVersion;
    private HashSet<HexCoord> _blockedGroundCache;
    private HashSet<HexCoord> _blockedAmphibiousCache;
    private int _blockedCacheVersion = -1;

    private readonly Dictionary<Collider, Building> _buildingByCollider = new Dictionary<Collider, Building>();
    private readonly Dictionary<Building, List<GameObject>> _cubesByBuilding = new Dictionary<Building, List<GameObject>>();
    private readonly List<MonsterAgent> _monsters = new List<MonsterAgent>();
    private readonly List<Citizen> _citizens = new List<Citizen>();
    private readonly List<Tank> _tanks = new List<Tank>();
    private readonly List<UnitCombat> _combatants = new List<UnitCombat>();

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
        _city = CityGenerator.Generate(unchecked((uint)seed), ResolvePreset());
        _battlefield = BattlefieldState.FreshFrom(_city);

        BuildGround();
        BuildTerrainAndRoads();
        BuildBuildings();
        BuildBridges();
        SpawnCitizens();
        SpawnTanks();

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

        _roster = gameObject.GetComponent<RosterFetcher>();
        if (_roster == null) _roster = gameObject.AddComponent<RosterFetcher>();
        _roster.baseUrl = baseUrl;
        _roster.accountId = accountId;
        _roster.OnRosterReady += HandleRosterReady;
        _roster.OnRosterFailed += HandleRosterFailed;
        _roster.FetchRoster();
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

    // ---- static city geometry --------------------------------------------------

    private void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(transform, false);
        var center = WorldOf(_city.CenterHex);
        ground.transform.position = new Vector3(center.x, -0.05f, center.z);
        // a unity plane is 10x10 at scale 1; cover the map with margin
        var w = _city.WidthHexes * (float)HexCoord.HexMeters / 10f * 1.3f;
        var h = _city.HeightHexes * (float)HexCoord.HexMeters / 10f * 1.3f;
        ground.transform.localScale = new Vector3(w, 1f, h);
        var renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(ShaderUtil.FindRenderableShader());
            mat.color = new Color(0.42f, 0.47f, 0.36f);
            renderer.sharedMaterial = mat;
        }
        // keep the plane's collider: it's what ground right-clicks hit
    }

    private void BuildTerrainAndRoads()
    {
        var terrain = new GameObject("Terrain").transform;
        terrain.SetParent(transform, false);

        var waterMat = NewMaterial(new Color(0.15f, 0.3f, 0.9f));
        foreach (var hex in _city.Water) SpawnCube(hex, -0.4f, 0.8f, waterMat, terrain, false);

        var ridgeMat = NewMaterial(new Color(0.35f, 0.55f, 0.25f));
        foreach (var hex in _city.Ridges) SpawnCube(hex, 1.5f, 3f, ridgeMat, terrain, false);

        var roadMat = NewMaterial(new Color(0.15f, 0.15f, 0.15f));
        foreach (var hex in _city.Roads) SpawnCube(hex, 0.1f, 0.2f, roadMat, terrain, false);
    }

    private void BuildBuildings()
    {
        var buildings = new GameObject("Buildings").transform;
        buildings.SetParent(transform, false);

        var small = NewMaterial(new Color(0.75f, 0.75f, 0.75f));
        var medium = NewMaterial(new Color(0.55f, 0.55f, 0.8f));
        var large = NewMaterial(new Color(0.35f, 0.35f, 0.7f));
        var landmark = NewMaterial(new Color(0.9f, 0.75f, 0.2f));

        foreach (var building in _city.Buildings)
        {
            var height = HeightForTier(building.Tier);
            Material mat;
            switch (building.Tier)
            {
                case BuildingTier.Medium: mat = medium; break;
                case BuildingTier.Large: mat = large; break;
                case BuildingTier.Landmark: mat = landmark; break;
                default: mat = small; break;
            }
            var cubes = new List<GameObject>();
            foreach (var hex in building.Footprint)
            {
                var cube = SpawnCube(hex, height / 2f, height, mat, buildings, true);
                cubes.Add(cube);
                var collider = cube.GetComponent<Collider>();
                if (collider != null) _buildingByCollider[collider] = building;
            }
            _cubesByBuilding[building] = cubes;
        }
    }

    private void BuildBridges()
    {
        var bridges = new GameObject("Bridges").transform;
        bridges.SetParent(transform, false);
        var mat = NewMaterial(new Color(0.5f, 0.33f, 0.15f));
        foreach (var bridge in _city.Bridges)
            foreach (var hex in bridge.Footprint) SpawnCube(hex, 0.6f, 1.2f, mat, bridges, false);
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
            // the pathing index -- flag agents to re-path
            foreach (var cube in cubes)
            {
                var s = cube.transform.localScale;
                cube.transform.localScale = new Vector3(s.x, s.y * 0.12f, s.z);
                var p = cube.transform.position;
                cube.transform.position = new Vector3(p.x, s.y * 0.06f, p.z);
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(ShaderUtil.FindRenderableShader());
                    mat.color = new Color(0.3f, 0.28f, 0.26f);
                    renderer.sharedMaterial = mat;
                }
                var collider = cube.GetComponent<Collider>();
                if (collider != null)
                {
                    _buildingByCollider.Remove(collider);
                    Object.Destroy(collider); // rubble: clicks fall through to the ground
                }
            }
            _cityVersion++;
            Debug.Log("Building destroyed -- rubble is now walkable.");
        }
        else if (next.Stage == DamageStage.Damaged && current.Stage == DamageStage.Intact)
        {
            // Intact -> Damaged visual: darken (docs/18's cracked state)
            foreach (var cube in cubes)
            {
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(ShaderUtil.FindRenderableShader());
                    var c = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.gray;
                    mat.color = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f);
                    renderer.sharedMaterial = mat;
                }
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

    public void OnCitizenEaten(Citizen citizen)
    {
        // docs/20 per-citizen yield: Blood 2 / Bones 1 / Brains 1
        WalletBlood += 2;
        WalletBones += 1;
        WalletBrains += 1;
        CitizensEaten++;
        _citizens.Remove(citizen);
        if (citizen != null) Object.Destroy(citizen.gameObject);
        Debug.Log("Citizen eaten. Wallet: " + WalletBlood + " blood / " + WalletBones + " bones / " + WalletBrains + " brains.");
    }

    public void SpawnWaypointMarker(Vector3 at)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "WaypointMarker";
        marker.transform.position = new Vector3(at.x, 0.15f, at.z);
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

    /// <summary>How much clear space stays between two units' bodies once
    /// separation stops pushing -- creator direction, 2026-07: "settled
    /// units are still too close together, at least 1 meter apart" (the
    /// original "settles exactly touching" design read as bodies stacked
    /// with zero gap, worst right after a group creeps in via TickSettle).</summary>
    private const float SeparationGap = 1f;

    /// <summary>Soft body separation so units never stand inside each other
    /// ("creatures should NOT walk through each other"), with at least
    /// SeparationGap of daylight between their bodies once it stops
    /// pushing. Each unit pushes HALF the overlap; the neighbor pushes
    /// its own half next frame, so a pair settles at exactly Radius +
    /// Radius + SeparationGap apart. Citizens are excluded on purpose --
    /// they're prey, and monsters must be able to reach them.</summary>
    public void ApplySeparation(UnitCombat self)
    {
        if (self == null) return;
        var p = self.transform.position;
        var moved = false;
        foreach (var c in _combatants)
        {
            if (c == null || c == self || !c.Alive) continue;
            var d = p - c.transform.position;
            d.y = 0f;
            var minDist = self.Radius + c.Radius + SeparationGap;
            var dist = d.magnitude;
            if (dist < minDist && dist > 1e-3f)
            {
                p += d / dist * ((minDist - dist) * 0.5f);
                moved = true;
            }
        }
        if (moved) self.transform.position = new Vector3(p.x, self.transform.position.y, p.z);
    }

    public void OnCombatantDied(UnitCombat c)
    {
        if (c != null) _combatants.Remove(c);
    }

    /// <summary>Local collision-avoidance steer: perturb a unit's desired
    /// direction to arc AROUND any unit sitting in front of it, so a
    /// faster creature overtakes a slower one instead of shoving into its
    /// back. Only things roughly ahead (within a forward cone) and close
    /// count; the unit with clear space ahead (the one in front) isn't
    /// deflected, so the asymmetry -- faster-from-behind goes around --
    /// falls out of the geometry. Returns a NORMALIZED direction; equals
    /// the input when nothing blocks.</summary>
    public Vector3 AvoidanceDir(UnitCombat self, Vector3 desiredDir)
    {
        if (self == null) return desiredDir;
        var fwd = new Vector3(desiredDir.x, 0f, desiredDir.z);
        if (fwd.sqrMagnitude < 1e-4f) return desiredDir;
        fwd = fwd.normalized;
        var right = new Vector3(fwd.z, 0f, -fwd.x);   // fwd rotated -90 about up
        var pos = self.transform.position;

        var avoid = Vector3.zero;
        foreach (var c in _combatants)
        {
            if (c == null || c == self || !c.Alive) continue;
            var to = c.transform.position - pos;
            to.y = 0f;
            var dist = to.magnitude;
            var reach = self.Radius + c.Radius + 4f;   // lookahead
            if (dist < 1e-3f || dist > reach) continue;
            var ahead = Vector3.Dot(to / dist, fwd);
            if (ahead < 0.35f) continue;                // only things ~in front

            // steer to the side away from the blocker (dead-ahead breaks to
            // the left deterministically)
            var onRight = Vector3.Dot(to, right);
            var side = onRight > 0f ? -1f : 1f;
            var strength = (reach - dist) / reach * ahead;
            avoid += right * (side * strength);
        }

        if (avoid.sqrMagnitude < 1e-6f) return desiredDir;
        return (fwd + avoid * 1.2f).normalized;
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
