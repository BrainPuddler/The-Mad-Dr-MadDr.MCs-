using UnityEngine;

/// <summary>
/// Gentle vertical sine-wave bob around a fixed local base position --
/// the Alien faction's "subtle levitation effects" / "hovering
/// components" / "floating crystal growths" (docs/12 2026-08 per-faction
/// Factory/Control Centre pass). Reads the STARTING localPosition at
/// Awake as the center of the bob rather than requiring a caller-
/// supplied one, so it drops onto any already-placed prop with zero
/// extra wiring beyond AddComponent.
/// </summary>
public class Bob : MonoBehaviour
{
    public float amplitude = 0.15f;
    public float period = 3.2f;

    private Vector3 _basePos;
    private float _phaseSeed;

    private void Awake()
    {
        _basePos = transform.localPosition;
        // Per-instance phase so multiple bobbing props on the same
        // building don't all bob in lockstep -- same "hash the instance
        // ID into a phase offset" idiom this codebase already uses
        // (DamageFx.SmokePuff.Init, EerieChamberGlow.Init).
        _phaseSeed = (GetInstanceID() & 1023) / 1023f * period;
    }

    private void Update()
    {
        var t = Time.time + _phaseSeed;
        var offset = Mathf.Sin(t * (Mathf.PI * 2f / period)) * amplitude;
        transform.localPosition = _basePos + new Vector3(0f, offset, 0f);
    }
}
