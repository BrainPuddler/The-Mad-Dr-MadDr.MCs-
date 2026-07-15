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

    public CityModel City { get { return _city; } }
    public int CityVersion { get { return _cityVersion; } }
    public int WalletBlood { get; private set; }
    public int WalletBones { get; private set; }
    public int WalletBrains { get; private set; }
    public int CitizensEaten { get; private set; }

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
            float height;
            Material mat;
            switch (building.Tier)
            {
                case BuildingTier.Medium: height = 12f; mat = medium; break;
                case BuildingTier.Large: height = 30f; mat = large; break;
                case BuildingTier.Landmark: height = 40f; mat = landmark; break;
                default: height = 6f; mat = small; break;
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
        }
    }

    private void HandleRosterFailed(string reason)
    {
        Debug.LogWarning("RuntimeCityBuilder: could not load a roster (" + reason + "). "
            + "Spawn a creature in the Lab, click Save to stable, and paste your Account ID into this component.");
    }
}
