using UnityEngine;

/// <summary>
/// A jittering LineRenderer between two anchor transforms -- the Mad
/// Doctor faction's "Tesla coil structures... electrical arcs" (docs/12
/// 2026-08 per-faction Control Centre pass). LineRenderer is a real,
/// standard Unity component (not a ParticleSystem), so this stays
/// within the project's own "no Inspector-configured module graph,
/// hand-rolled Update-driven animation" convention (DamageFx.cs's own
/// class header) while still getting a genuinely dynamic, reactive-
/// looking effect for very little cost -- one line, a handful of
/// segments, refreshed every frame, not a burst of many particles.
/// Perlin noise (not UnityEngine.Random) drives the jitter/flicker so
/// it's smooth/continuous rather than popping randomly frame to frame.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TeslaArc : MonoBehaviour
{
    public Transform from;
    public Transform to;
    public float jitter = 0.15f;
    public int segments = 6;
    [Range(0f, 1f)] public float flickerMin = 0.4f;

    private LineRenderer _line;
    private Vector3[] _points;
    private float _phaseSeed;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = segments + 1;
        _line.useWorldSpace = true;
        _points = new Vector3[segments + 1];
        _phaseSeed = (GetInstanceID() & 1023) / 1023f * 10f;
    }

    private void Update()
    {
        if (from == null || to == null || _line == null) return;
        var a = from.position;
        var b = to.position;
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < segments)
            {
                var seed = Time.time * 11f + _phaseSeed + i * 3.7f;
                p += new Vector3(
                    Mathf.PerlinNoise(seed, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, seed) - 0.5f,
                    Mathf.PerlinNoise(seed, seed) - 0.5f) * jitter;
            }
            _points[i] = p;
        }
        _line.SetPositions(_points);

        var flicker = flickerMin + (1f - flickerMin) * Mathf.PerlinNoise(Time.time * 17f + _phaseSeed, 0f);
        var c = _line.startColor;
        c.a = flicker;
        _line.startColor = c;
        _line.endColor = c;
    }
}
