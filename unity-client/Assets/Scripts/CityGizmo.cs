using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Scene-view visualization of a generated city (docs/18 SS2) -- drop on
/// an empty GameObject and a full procedural city appears as gizmos:
/// roads as dark slabs, buildings as tier-colored boxes (taller = bigger
/// tier), landmarks as wire spheres at their mechanic's radius (cyan
/// emitter aura, 3 hexes; red Collection Station, 5 hexes -- docs/03,
/// docs/18/20). Same seed always draws the same city; that IS the
/// docs/18 determinism contract, visible.
///
/// Gizmos only, no runtime cost. Defaults to the village preset --
/// big_city draws ~19k buildings, which the gizmo pipeline survives but
/// noticeably chugs on; switch up briefly, not permanently.
/// </summary>
public class CityGizmo : MonoBehaviour
{
    public enum PresetChoice { Village, SmallTown, BigCity }

    [Tooltip("City seed: same seed + preset = identical city, every time, on every machine.")]
    public int seed = 42;

    public PresetChoice preset = PresetChoice.Village;

    [Tooltip("Draw the landmark radius spheres (emitter auras, Collection Stations).")]
    public bool drawLandmarkRadii = true;

    private CityModel _model;
    private int _builtSeed = int.MinValue;
    private PresetChoice _builtPreset = (PresetChoice)(-1);

    private CityPreset ResolvePreset()
    {
        switch (preset)
        {
            case PresetChoice.SmallTown: return CityPreset.SmallTown();
            case PresetChoice.BigCity: return CityPreset.BigCity();
            default: return CityPreset.Village();
        }
    }

    private void OnDrawGizmos()
    {
        if (_model == null || _builtSeed != seed || _builtPreset != preset)
        {
            _model = CityGenerator.Generate(unchecked((uint)seed), ResolvePreset());
            _builtSeed = seed;
            _builtPreset = preset;
        }

        var origin = transform.position;
        var hexSize = (float)HexCoord.HexMeters;

        Gizmos.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        foreach (var road in _model.Roads)
        {
            Gizmos.DrawCube(WorldOf(road, origin, 0.1f), new Vector3(hexSize * 0.9f, 0.2f, hexSize * 0.9f));
        }

        foreach (var building in _model.Buildings)
        {
            float height;
            Color color;
            switch (building.Tier)
            {
                case BuildingTier.Medium: height = 12f; color = new Color(0.55f, 0.55f, 0.8f); break;
                case BuildingTier.Large: height = 30f; color = new Color(0.35f, 0.35f, 0.7f); break;
                case BuildingTier.Landmark: height = 40f; color = new Color(0.9f, 0.75f, 0.2f); break;
                default: height = 6f; color = new Color(0.75f, 0.75f, 0.75f); break;
            }
            Gizmos.color = color;
            foreach (var hex in building.Footprint)
            {
                Gizmos.DrawCube(WorldOf(hex, origin, height / 2f), new Vector3(hexSize * 0.8f, height, hexSize * 0.8f));
            }
        }

        if (drawLandmarkRadii)
        {
            foreach (var landmark in _model.Landmarks)
            {
                Gizmos.color = landmark.Kind == LandmarkKind.Emitter
                    ? new Color(0.2f, 0.9f, 0.9f, 0.8f)   // emitter aura
                    : new Color(0.9f, 0.2f, 0.2f, 0.8f);  // Collection Station
                Gizmos.DrawWireSphere(
                    WorldOf(landmark.Site, origin, 0f),
                    landmark.RadiusHexes * hexSize);
            }
        }
    }

    private static Vector3 WorldOf(HexCoord hex, Vector3 origin, float y)
    {
        var (x, z) = hex.ToWorld();
        return origin + new Vector3((float)x, y, (float)z);
    }
}
