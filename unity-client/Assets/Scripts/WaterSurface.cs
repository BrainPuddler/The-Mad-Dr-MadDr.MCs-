using UnityEngine;

/// <summary>
/// Makes the water sheet (RuntimeCityBuilder.BuildWater) actually flow
/// instead of sitting as a dead-flat blue sheet: it holds the mesh's
/// rest positions and, every frame, displaces each vertex vertically by a
/// couple of travelling sine waves, then recomputes normals so the light
/// glints and rolls across the surface. Purely cosmetic -- no collider, no
/// gameplay data (the water HEX set in citygen-core is the truth; this only
/// dresses it), same as every other dresser in the docs/21 miniature-set
/// arc.
///
/// The <see cref="_flow"/> direction is the river's long axis, so the
/// primary wave marches downstream; a softer secondary wave runs across
/// it so ponds sharing the sheet read as chop rather than a conveyor.
/// The wavelength MUST stay several times the sheet's grid spacing
/// (BuildWaterSheet quads, >=4m) or the sampled wave aliases into
/// shimmer. Time-driven like NightMode / TrafficCar / the gait rig --
/// never UnityEngine.Random, and the rest geometry it animates around is
/// itself deterministic.
/// </summary>
public sealed class WaterSurface : MonoBehaviour
{
    private Mesh _mesh;
    private Vector3[] _rest;
    private Vector3[] _work;
    private Vector2 _flow;      // downstream direction
    private float _phase;       // offset so a second sheet wouldn't pulse in lockstep

    // Gentle: a miniature-set pond/river, not an ocean. Amplitude stays
    // well under the bank crest height so waves never poke over the shore.
    private const float Amplitude = 0.08f;
    private const float WaveLength = 17f;   // metres between crests -- ~4x the sheet's finest grid spacing
    private const float Speed = 1.8f;       // metres/sec crest travel
    private const float CrossChop = 0.5f;   // secondary wave across the flow

    public void Init(Mesh mesh, Vector3[] restVerts, Vector2 flow, float phase)
    {
        _mesh = mesh;
        _rest = restVerts;
        _work = new Vector3[restVerts.Length];
        _flow = flow.sqrMagnitude > 1e-4f ? flow.normalized : Vector2.zero;
        _phase = phase;
    }

    private void Update()
    {
        if (_mesh == null || _rest == null) return;

        var k = 2f * Mathf.PI / WaveLength;
        // scale by k so a crest genuinely travels at Speed metres/sec
        var t = (Time.time * Speed + _phase) * k;

        // Primary wave marches along the flow (or, for a still pond with no
        // flow, along a fixed diagonal so the chop still has direction).
        var dir = _flow == Vector2.zero ? new Vector2(0.707f, 0.707f) : _flow;
        var cross = new Vector2(-dir.y, dir.x);
        var chopScale = _flow == Vector2.zero ? 1f : CrossChop;

        for (var i = 0; i < _rest.Length; i++)
        {
            var v = _rest[i];
            var along = v.x * dir.x + v.z * dir.y;
            var across = v.x * cross.x + v.z * cross.y;
            var y = Amplitude * Mathf.Sin(k * along - t)
                  + Amplitude * chopScale * Mathf.Sin(k * 0.6f * across + t * 0.7f + _phase);
            _work[i] = new Vector3(v.x, v.y + y, v.z);
        }

        _mesh.vertices = _work;
        _mesh.RecalculateNormals();
    }
}
