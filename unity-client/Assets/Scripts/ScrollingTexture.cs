using UnityEngine;

/// <summary>
/// Shifts a Renderer's material mainTextureOffset over time -- the
/// classic zero-geometry "moving surface" trick, used for the Human
/// Alliance Factory's conveyor-belt strip (docs/12 2026-08 per-faction
/// Factory/Control Centre pass: "conveyor systems... lighting should be
/// clean and functional"). Mutates the Renderer's OWN material instance
/// (`.material`, not `.sharedMaterial`) since the whole point is a
/// per-instance-scrolling offset -- this deliberately breaks SRP
/// batching for the one small strip it's applied to, an acceptable
/// trade at "a handful of Factory buildings per match," the exact scale
/// EmissiveAnimator's own MaterialPropertyBlock technique exists to
/// avoid needing at hundreds-of-city-props scale instead.
/// </summary>
public class ScrollingTexture : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.4f, 0f);

    private Renderer _renderer;
    private Vector2 _offset;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void Update()
    {
        if (_renderer == null) return;
        _offset += speed * Time.deltaTime;
        _renderer.material.mainTextureOffset = _offset;
    }
}
