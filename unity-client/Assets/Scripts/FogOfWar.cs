using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Docs/02's one-line spec, actually implemented for the first time
/// anywhere in this repo ("Fog of war: light -- monsters reveal a
/// radius; emitter status is always visible to both players"). Tracks
/// two hex sets: EXPLORED (seen at least once, stays revealed forever --
/// the classic RTS "remembered terrain") and VISIBLE NOW (within
/// <see cref="visionRadiusHexes"/> of a currently-alive player monster
/// right now). The Minimap reads both to draw the black/dimmed/lit fog
/// overlay and to gate enemy-unit blips.
///
/// Recomputed on a timer, not every frame, and only ever walks each
/// alive monster's own <c>HexCoord.Range(radius)</c> -- cheap regardless
/// of map size (a Big City's 250x250 hex field never gets touched in
/// full; only the handful of hexes near each monster do).
/// </summary>
public class FogOfWar : MonoBehaviour
{
    [Tooltip("Master switch. Off = everything counts as explored and visible (dev/testing).")]
    public bool enabledFog = true;

    [Tooltip("Hexes a player monster reveals around itself.")]
    public float visionRadiusHexes = 6f;

    private const float RecomputeInterval = 0.35f;

    private RuntimeCityBuilder _builder;
    private readonly HashSet<HexCoord> _explored = new HashSet<HexCoord>();
    private HashSet<HexCoord> _visibleNow = new HashSet<HexCoord>();
    private float _timer;

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        Recompute();   // don't wait a full interval to show the starting view
    }

    /// <summary>Ever been within a monster's vision -- stays true forever
    /// once set. Always true when the fog master switch is off.</summary>
    public bool IsExplored(HexCoord hex)
    {
        return !enabledFog || _explored.Contains(hex);
    }

    /// <summary>Within a currently-alive monster's vision RIGHT NOW --
    /// this is what should gate live things (enemy unit positions), as
    /// opposed to remembered terrain. Always true when the fog master
    /// switch is off.</summary>
    public bool IsVisibleNow(HexCoord hex)
    {
        return !enabledFog || _visibleNow.Contains(hex);
    }

    private void Update()
    {
        if (_builder == null || !enabledFog) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = RecomputeInterval;
        Recompute();
    }

    private void Recompute()
    {
        if (_builder == null) return;
        var radius = Mathf.Max(1, Mathf.RoundToInt(visionRadiusHexes));
        var visible = new HashSet<HexCoord>();
        foreach (var m in _builder.Monsters)
        {
            if (m == null || m.Fighter == null || !m.Fighter.Alive) continue;
            var center = _builder.HexAt(m.transform.position);
            foreach (var h in center.Range(radius))
            {
                if (!_builder.City.Contains(h)) continue;
                visible.Add(h);
                _explored.Add(h);
            }
        }
        _visibleNow = visible;
    }
}
