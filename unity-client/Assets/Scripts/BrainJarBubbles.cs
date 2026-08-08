using UnityEngine;

/// <summary>
/// Big Brain jar -- creator direction (2026-08, "Major Improvement"):
/// "Add small bubbles suspended within the green liquid. Bubbles should
/// slowly and randomly rise toward the top. Vary their size, speed, and
/// trajectory. Keep them sparse enough to feel physical. Avoid a uniform
/// particle grid or repetitive looping pattern."
///
/// No ParticleSystem -- same "primitive-kit dressing pipeline, no
/// Inspector-configured module graph this environment has no Editor to
/// verify" convention DamageFx's own smoke/fire/dust effects already
/// established for every other effect in this codebase (see DamageFx.cs's
/// own class header: "No ParticleSystem -- period-appropriate for the
/// primitive-kit dressing pipeline and keeps everything on the project's
/// existing Update-driven animation idiom"). A small pool of plain
/// sphere primitives, each driven INDEPENDENTLY -- its own rise speed,
/// size, lateral drift target, and (while waiting to reappear) its own
/// random respawn delay -- rather than one shared emitter cadence, so
/// the pool never reads as "the same N bubbles on a loop" even though it
/// technically does recycle a fixed set of GameObjects.
/// </summary>
public class BrainJarBubbles : MonoBehaviour
{
    private class Bubble
    {
        public Transform T;
        public Renderer R;
        public float RiseSpeed;      // fraction of liquid height per second
        public float Size;
        public float WobbleAmp;
        public float WobbleFreq;
        public float WobblePhase;
        public float StartLateral;   // local-X/Z offset at the bottom
        public float TopLateral;     // local-X/Z offset near the top -- real bubbles don't rise dead straight
        public float Progress;       // 0 (bottom) .. 1 (top)
        public float RespawnDelay;   // seconds still waiting before reappearing
        public bool Visible;
    }

    private Bubble[] _bubbles;
    private float _lateralRange;
    private float _liquidHeight;
    private float _liquidBottomLocalY;
    private System.Random _rng;   // display-only jitter -- never touches match state/determinism

    /// <summary>Caller owns this component's own transform (position it
    /// at the liquid's center and parent it under the jar BEFORE calling
    /// Init, same convention EerieChamberGlow.Init already follows) --
    /// every bubble position below is LOCAL to that transform, so
    /// `liquidBottomLocalY`/`liquidHeight` describe the vertical span
    /// bubbles travel relative to wherever this transform's own origin
    /// was placed.</summary>
    public void Init(Material bubbleMat, float jarRadius,
        float liquidBottomLocalY, float liquidHeight, int count, int seed)
    {
        _lateralRange = jarRadius * 0.7f;   // stay clear of the glass wall
        _liquidHeight = liquidHeight;
        _liquidBottomLocalY = liquidBottomLocalY;
        _rng = new System.Random(seed);

        _bubbles = new Bubble[count];
        for (var i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bubble";
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = bubbleMat;
            var b = new Bubble { T = go.transform, R = renderer };
            RandomizeMotion(b);
            // Staggered initial progress so the jar doesn't look freshly
            // switched on the moment it's built -- a real jar that's been
            // sitting there has bubbles at every stage of their rise, not
            // all queued at the very bottom.
            b.Progress = (float)_rng.NextDouble();
            b.Visible = true;
            Place(b);
            _bubbles[i] = b;
        }
    }

    private float NextFloat(float min, float max)
    {
        return (float)_rng.NextDouble() * (max - min) + min;
    }

    private void RandomizeMotion(Bubble b)
    {
        b.RiseSpeed = NextFloat(0.05f, 0.16f);
        b.Size = NextFloat(0.03f, 0.09f);
        b.WobbleAmp = NextFloat(0.15f, 0.5f) * (_lateralRange * 0.12f);
        b.WobbleFreq = NextFloat(0.5f, 1.3f);
        b.WobblePhase = NextFloat(0f, Mathf.PI * 2f);
        b.StartLateral = NextFloat(-1f, 1f) * _lateralRange;
        b.TopLateral = NextFloat(-1f, 1f) * _lateralRange;
    }

    private void Update()
    {
        if (_bubbles == null) return;
        var dt = Time.deltaTime;
        for (var i = 0; i < _bubbles.Length; i++)
        {
            var b = _bubbles[i];
            if (!b.Visible)
            {
                b.RespawnDelay -= dt;
                if (b.RespawnDelay > 0f) continue;
                b.Visible = true;
            }

            b.Progress += b.RiseSpeed * dt;
            if (b.Progress >= 1f)
            {
                // Pops at the surface, then waits a genuinely random
                // interval before the next one appears at the bottom --
                // an instant recycle is exactly the "repetitive looping
                // pattern" the brief asks to avoid; a real gap (plus a
                // fully re-randomized motion profile below) keeps the
                // pool from reading as a fixed, predictable cast.
                RandomizeMotion(b);
                b.Progress = 0f;
                b.RespawnDelay = NextFloat(0.3f, 2.6f);
                b.Visible = false;
                if (b.R != null) b.R.enabled = false;
                continue;
            }

            Place(b);
        }
    }

    private void Place(Bubble b)
    {
        var lateralX = Mathf.Lerp(b.StartLateral, b.TopLateral, b.Progress);
        var wobble = Mathf.Sin((b.Progress * 5f + b.WobblePhase) * b.WobbleFreq * Mathf.PI * 2f) * b.WobbleAmp;
        var localY = _liquidBottomLocalY + b.Progress * _liquidHeight;
        b.T.localPosition = new Vector3(lateralX + wobble, localY, wobble * 0.6f);
        b.T.localScale = Vector3.one * b.Size;
        if (b.R != null) b.R.enabled = true;
    }
}
