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
/// size, orbit path, and (while waiting to reappear) its own random
/// respawn delay -- rather than one shared emitter cadence, so the pool
/// never reads as "the same N bubbles on a loop" even though it
/// technically does recycle a fixed set of GameObjects.
///
/// 2026-08 follow-up (creator direction: "round the brain not below the
/// brain"): the ORIGINAL motion model picked an independent random
/// lateral (X/Z) offset for each bubble's start/end with no relationship
/// to where the brain actually sits -- a bubble with a small offset
/// would rise on a path that passed close to (or straight through) the
/// brain mesh, getting hidden by it, while the ones that stayed clearly
/// visible mostly ended up reading as pooling in the narrow gap below
/// the brain rather than travelling past its sides. Bubbles now orbit
/// at a RADIUS constrained to the actual annular gap between the
/// brain's surface and the glass wall (`brainWorldRadius` and
/// `fluidWorldRadius`, both passed in from the real geometry, not
/// guessed) with a slowly drifting angle -- every bubble's rise
/// necessarily sweeps around the brain's own circumference instead of
/// through its footprint.
/// </summary>
public class BrainJarBubbles : MonoBehaviour
{
    private class Bubble
    {
        public Transform T;
        public Renderer R;
        public float RiseSpeed;         // fraction of liquid height per second
        public float Size;
        public float WobbleAmp;         // small radial wobble on top of the orbit path
        public float WobbleFreq;
        public float WobblePhase;
        public float OrbitRadiusStart;  // distance from the jar's central axis at the bottom
        public float OrbitRadiusEnd;    // distance from the jar's central axis near the top -- lets the orbit drift in/out slightly
        public float Angle;             // starting angle around the jar's axis, radians
        public float AngularDrift;      // total radians swept over one full rise -- the "sweeps around the brain" swirl
        public float Progress;          // 0 (bottom) .. 1 (top)
        public float RespawnDelay;      // seconds still waiting before reappearing
        public bool Visible;
    }

    private Bubble[] _bubbles;
    private float _orbitMin;
    private float _orbitMax;
    private float _liquidHeight;
    private float _liquidBottomLocalY;
    private float _bubbleDiameterMin;
    private float _bubbleDiameterMax;
    private System.Random _rng;   // display-only jitter -- never touches match state/determinism

    /// <summary>Caller owns this component's own transform (position it
    /// at the liquid's center and parent it under the jar BEFORE calling
    /// Init, same convention EerieChamberGlow.Init already follows) --
    /// every bubble position below is LOCAL to that transform.
    /// `fluidWorldRadius`/`brainWorldRadius` are the REAL computed world
    /// radii of the fluid cylinder and brain sphere (not scale/diameter
    /// values -- see the call site's own comment on that distinction),
    /// so the orbit band this class derives from them actually sits in
    /// the real gap between the two, at any building tier.
    /// `liquidBottomLocalY`/`liquidHeight` describe the vertical span
    /// bubbles travel relative to wherever this transform's own origin
    /// was placed.
    ///
    /// 2026-08 (creator report: "I can't see them"): the ORIGINAL version
    /// sized bubbles from fixed absolute world-unit constants (0.03-0.09)
    /// completely independent of the jar's own scale -- for a typical
    /// Big Brain jar (radius on the order of 3-4 world units) that's
    /// under 3% of the jar's own radius, functionally invisible from RTS
    /// camera height. The exact same mistake DamageFx's smoke plume made
    /// once already in this codebase ("a single fixed-size puff...
    /// disappears against a bigger silhouette" -- see DamageFx.cs's own
    /// history). Bubble diameter is a FRACTION of the fluid radius, so
    /// bubbles scale with whatever building tier the jar itself is at,
    /// same as every other prop in this file.
    ///
    /// 2026-08 follow-up (creator direction: "more bubble smaller by
    /// 35%"): that fraction is itself now 35% smaller than the size that
    /// shipped for the visibility fix above (0.05-0.14 -> 0.0325-0.091 of
    /// the fluid radius) -- a deliberate, explicit reversal of part of
    /// that earlier fix, not a re-guess.</summary>
    public void Init(Material bubbleMat, float fluidWorldRadius, float brainWorldRadius,
        float liquidBottomLocalY, float liquidHeight, int count, int seed)
    {
        _orbitMin = brainWorldRadius * 1.15f;   // clear of the brain's own surface
        // 0.85, not a tighter-looking 0.9 -- has to leave room for the
        // wobble amplitude AND the bubble's own half-size on top of the
        // orbit radius itself; checked numerically against the worst
        // case (max wobble + max bubble size) before picking this
        // constant, not just eyeballed -- 0.9 left under 3% clearance
        // from the glass wall at the extreme, 0.85 leaves a comfortable
        // ~8%.
        _orbitMax = fluidWorldRadius * 0.85f;   // clear of the glass wall
        if (_orbitMax < _orbitMin) _orbitMax = _orbitMin * 1.2f;   // defensive only -- not expected to trigger at any real tier, see call site
        _liquidHeight = liquidHeight;
        _liquidBottomLocalY = liquidBottomLocalY;
        const float sizeShrink = 0.65f;   // "more bubble smaller by 35%"
        _bubbleDiameterMin = fluidWorldRadius * 0.05f * sizeShrink;
        _bubbleDiameterMax = fluidWorldRadius * 0.14f * sizeShrink;
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
        b.Size = NextFloat(_bubbleDiameterMin, _bubbleDiameterMax);
        b.WobbleAmp = NextFloat(0.15f, 0.5f) * ((_orbitMax - _orbitMin) * 0.15f);
        b.WobbleFreq = NextFloat(0.5f, 1.3f);
        b.WobblePhase = NextFloat(0f, Mathf.PI * 2f);
        b.OrbitRadiusStart = NextFloat(_orbitMin, _orbitMax);
        b.OrbitRadiusEnd = NextFloat(_orbitMin, _orbitMax);
        b.Angle = NextFloat(0f, Mathf.PI * 2f);
        // Swirls up to ~2/3 of a full turn around the brain over one
        // rise, either direction -- some bubbles barely drift, some
        // sweep most of the way around, matching "vary... trajectory."
        b.AngularDrift = NextFloat(-4f, 4f);
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
        var orbitRadius = Mathf.Lerp(b.OrbitRadiusStart, b.OrbitRadiusEnd, b.Progress)
            + Mathf.Sin((b.Progress * 5f + b.WobblePhase) * b.WobbleFreq * Mathf.PI * 2f) * b.WobbleAmp;
        var angle = b.Angle + b.AngularDrift * b.Progress;
        var localY = _liquidBottomLocalY + b.Progress * _liquidHeight;
        b.T.localPosition = new Vector3(Mathf.Sin(angle) * orbitRadius, localY, Mathf.Cos(angle) * orbitRadius);
        b.T.localScale = Vector3.one * b.Size;
        if (b.R != null) b.R.enabled = true;
    }
}
