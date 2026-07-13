using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// The actual payoff: hit Play and see your bred monsters wandering a
/// real generated city, not just gizmos in the Scene view. Where
/// CityGizmo (Scene-view-only, editor-time) and RosterFetcher (fetch +
/// cache, no visuals) each do one piece, this ties them together into
/// something you watch happen.
///
/// Builds the city as real primitive GameObjects (colors/heights mirror
/// CityGizmo's scheme, so the two previews agree), then fetches the
/// player's roster and spawns one MonsterAvatar per creature.
///
/// Defaults to the village preset for the same reason CityGizmo does:
/// big_city is ~19k buildings, each a real instantiated primitive here
/// (not a gizmo draw call) -- fine to try once, not to leave running.
/// </summary>
public class RuntimeCityBuilder : MonoBehaviour
{
    public enum PresetChoice { Village, SmallTown, BigCity }

    [Header("City")]
    [Tooltip("City seed: same seed + preset = identical city, every time (docs/18 determinism contract).")]
    public int seed = 42;

    public PresetChoice preset = PresetChoice.Village;

    [Header("Roster")]
    [Tooltip("Where mutator-service is running.")]
    public string baseUrl = "http://localhost:8787";

    [Tooltip("Paste this from the Lab website's \"Account ID\" header button.")]
    public string accountId = "";

    private CityModel _city;
    private BattlefieldState _battlefield;
    private Vector3 _origin;
    private RosterFetcher _roster;

    private void Start()
    {
        _origin = transform.position;
        _city = CityGenerator.Generate(unchecked((uint)seed), ResolvePreset());
        _battlefield = BattlefieldState.FreshFrom(_city);

        BuildTerrainAndRoads();
        BuildBuildings();
        BuildBridges();

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

    private void BuildTerrainAndRoads()
    {
        var terrain = new GameObject("Terrain").transform;
        terrain.SetParent(transform, false);

        var waterMat = NewMaterial(new Color(0.15f, 0.3f, 0.9f));
        foreach (var hex in _city.Water) SpawnCube(hex, -0.4f, 0.8f, waterMat, terrain);

        var ridgeMat = NewMaterial(new Color(0.35f, 0.55f, 0.25f));
        foreach (var hex in _city.Ridges) SpawnCube(hex, 1.5f, 3f, ridgeMat, terrain);

        var roadMat = NewMaterial(new Color(0.15f, 0.15f, 0.15f));
        foreach (var hex in _city.Roads) SpawnCube(hex, 0.1f, 0.2f, roadMat, terrain);
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
            foreach (var hex in building.Footprint) SpawnCube(hex, height / 2f, height, mat, buildings);
        }
    }

    private void BuildBridges()
    {
        var bridges = new GameObject("Bridges").transform;
        bridges.SetParent(transform, false);
        var mat = NewMaterial(new Color(0.5f, 0.33f, 0.15f));
        foreach (var bridge in _city.Bridges)
            foreach (var hex in bridge.Footprint) SpawnCube(hex, 0.6f, 1.2f, mat, bridges);
    }

    private void SpawnCube(HexCoord hex, float y, float height, Material mat, Transform parent)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent, false);
        var (x, z) = hex.ToWorld();
        var hexSize = (float)HexCoord.HexMeters;
        cube.transform.position = _origin + new Vector3((float)x, y, (float)z);
        cube.transform.localScale = new Vector3(hexSize * 0.9f, height, hexSize * 0.9f);
        cube.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Material NewMaterial(Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }

    private void HandleRosterReady(RosterCache cache, bool wasFromCache)
    {
        Debug.Log("RuntimeCityBuilder: roster ready (" + cache.Creatures.Length + " creatures, "
            + (wasFromCache ? "from local cache, fetched " + cache.FetchedAtUtc : "live") + ")");

        var monsters = new GameObject("Monsters").transform;
        monsters.SetParent(transform, false);

        var center = new HexCoord(0, 0);
        var blockedToGround = _battlefield.BlockedToGround();
        var landingSpots = new List<HexCoord>();
        foreach (var hex in center.Range(6))
            if (!blockedToGround.Contains(hex)) landingSpots.Add(hex);

        for (var i = 0; i < cache.Creatures.Length; i++)
        {
            var creature = cache.Creatures[i];
            var home = landingSpots.Count > 0 ? landingSpots[i % landingSpots.Count] : center;

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Monster_" + creature.Id;
            capsule.transform.SetParent(monsters, false);
            var avatar = capsule.AddComponent<MonsterAvatar>();
            var (x, z) = home.ToWorld();
            avatar.Init(creature, _city, _battlefield, home, _origin + new Vector3((float)x, 0f, (float)z), seed + i);
        }
    }

    private void HandleRosterFailed(string reason)
    {
        Debug.LogWarning("RuntimeCityBuilder: could not load a roster (" + reason + "). "
            + "Spawn a creature in the Lab, set its Menagerie, and paste your Account ID into this component.");
    }
}
