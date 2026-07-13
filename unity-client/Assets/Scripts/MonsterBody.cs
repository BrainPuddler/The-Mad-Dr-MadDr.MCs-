using System.Collections.Generic;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A genome-driven articulated body with distance-driven stepping --
/// replaces the wandering capsule. NOT the Lab renderer's full stitched
/// B-movie look (that's ~3500 lines of custom WebGL; porting it is its
/// own project, docs/08): this is the same genome shaping a simplified
/// silhouette -- plan picks the body shape and leg count, bulk scales
/// everything, the hand family renders as a recognizable weapon, brain
/// tier sizes the head.
///
/// THE GAIT RULE (creator direction, 2026-07): no skating, ever.
/// Planted feet are stored as WORLD positions and never move while
/// planted -- the body travels over them, and a leg only swings when the
/// body's actual displacement has pulled its hip far enough from the
/// planted foot. Step length and cadence derive from the creature's
/// LocomotionProfile (physiology), and the body bob is phased by
/// DISTANCE TRAVELED, not wall-clock time, so motion always matches
/// ground covered by construction rather than by tuning.
/// </summary>
public class MonsterBody : MonoBehaviour
{
    private sealed class Leg
    {
        public Vector3 HipLocal;
        public Vector3 FootWorld;
        public bool Swinging;
        public float SwingT;
        public Vector3 SwingFrom;
        public Vector3 SwingTo;
        public int Group;         // trot/tripod alternation group (0/1)
        public Transform Upper;
        public Transform Lower;
        public Transform Foot;
    }

    private readonly List<Leg> _legs = new List<Leg>();
    private readonly List<Transform> _tailSegments = new List<Transform>(); // serpentine
    private readonly List<Vector3> _tailTrail = new List<Vector3>();
    private Transform _torso;
    private string _plan = "tetrapod";
    private float _legLen = 1.2f;
    private float _stride = 1.8f;
    private float _bulkScale = 1f;
    private float _distTraveled;
    private float _hoverPhase;

    public float BodyHeight { get { return _legs.Count > 0 ? _legLen : _bulkScale * 0.9f; } }

    public void Build(StoredGenomeDto creature)
    {
        var g = creature.Genome;
        _plan = g.Body.Plan;
        var bulk = g.Body.Params.Length > 1 ? (float)g.Body.Params[1] : 0.5f;
        _bulkScale = Mathf.Lerp(0.8f, 2.4f, bulk);
        var legGene = g.Slots.Leg.Params.Length > 0 ? (float)g.Slots.Leg.Params[0] : 0.5f;
        _legLen = Mathf.Lerp(0.9f, 2.2f, legGene) * Mathf.Lerp(0.8f, 1.3f, bulk);
        _stride = _legLen * 1.5f;

        var skin = SkinColor(g.Body.Plan, creature.Id);

        _torso = new GameObject("Torso").transform;
        _torso.SetParent(transform, false);
        _torso.localPosition = new Vector3(0f, BodyHeight, 0f);

        BuildTorso(skin, bulk);
        BuildHead(g, skin);
        BuildWeapon(g.Slots.Hand.Family, skin);
        BuildLegs(g, skin);

        // one collider on the root for selection raycasts; the parts
        // themselves stay collider-free (StripCollider on every primitive)
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, BodyHeight, 0f);
        box.size = new Vector3(_bulkScale * 2.2f, BodyHeight * 2f, _bulkScale * 2.6f);
    }

    // ---- construction ------------------------------------------------------

    private void BuildTorso(Color skin, float bulk)
    {
        switch (_plan)
        {
            case "serpentine":
            {
                // head-end sphere plus a trail of follower segments
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale, _bulkScale * 0.8f, _bulkScale), skin);
                var segs = 6;
                for (var i = 0; i < segs; i++)
                {
                    var t = Part(PrimitiveType.Sphere, transform,
                        new Vector3(0f, BodyHeight * 0.7f, -(i + 1) * _bulkScale * 0.8f),
                        Vector3.one * (_bulkScale * (0.9f - 0.09f * i)), skin);
                    _tailSegments.Add(t);
                    _tailTrail.Add(t.position);
                }
                break;
            }
            case "blob":
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 1.6f, _bulkScale * 1.2f, _bulkScale * 1.6f), skin);
                break;
            case "crab":
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 2.0f, _bulkScale * 0.7f, _bulkScale * 1.4f), skin);
                break;
            case "arachnid":
                Part(PrimitiveType.Sphere, _torso, new Vector3(0f, 0f, _bulkScale * 0.5f),
                    new Vector3(_bulkScale * 0.9f, _bulkScale * 0.8f, _bulkScale * 0.9f), skin);
                Part(PrimitiveType.Sphere, _torso, new Vector3(0f, 0.1f * _bulkScale, -_bulkScale * 0.8f),
                    new Vector3(_bulkScale * 1.3f, _bulkScale * 1.1f, _bulkScale * 1.5f), skin);
                break;
            case "avian":
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 0.9f, _bulkScale * 1.0f, _bulkScale * 1.4f), skin);
                break;
            case "treant":
                Part(PrimitiveType.Cylinder, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 0.9f, _bulkScale * 1.1f, _bulkScale * 0.9f), skin);
                break;
            case "winged":
            {
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 0.8f, _bulkScale * 0.9f, _bulkScale * 1.1f), skin);
                // two thin wing slabs
                for (var side = -1; side <= 1; side += 2)
                {
                    var wing = Part(PrimitiveType.Cube, _torso,
                        new Vector3(side * _bulkScale * 1.6f, _bulkScale * 0.3f, -0.2f),
                        new Vector3(_bulkScale * 2.4f, 0.08f, _bulkScale * 1.1f), Shade(skin, 0.8f));
                    wing.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
                }
                break;
            }
            case "floater":
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 0.9f, _bulkScale * 1.3f, _bulkScale * 0.9f), skin);
                Part(PrimitiveType.Cylinder, _torso, new Vector3(0f, -_bulkScale * 0.8f, 0f),
                    new Vector3(_bulkScale * 1.2f, 0.1f, _bulkScale * 1.2f), Shade(skin, 0.6f));
                break;
            default: // tetrapod
                Part(PrimitiveType.Sphere, _torso, Vector3.zero,
                    new Vector3(_bulkScale * 1.0f, _bulkScale * 0.9f, _bulkScale * 1.5f), skin);
                break;
        }
    }

    private void BuildHead(GenomeDto g, Color skin)
    {
        var headScale = g.Brain.Tier == "mastermind" ? 0.9f
            : g.Brain.Tier == "gifted" ? 0.7f
            : g.Brain.Tier == "dim" ? 0.45f : 0.55f;
        var headR = _bulkScale * headScale * 0.6f;
        var headPos = new Vector3(0f, _bulkScale * 0.5f, _bulkScale * 1.4f);
        if (_plan == "crab") headPos = new Vector3(0f, _bulkScale * 0.35f, _bulkScale * 1.2f);
        if (_plan == "serpentine") headPos = new Vector3(0f, _bulkScale * 0.3f, _bulkScale * 0.9f);

        var head = Part(PrimitiveType.Sphere, _torso, headPos, Vector3.one * headR * 2f, Shade(skin, 1.1f));

        // eyes: two small white spheres with dark pupils, front of the head
        for (var side = -1; side <= 1; side += 2)
        {
            var eyePos = headPos + new Vector3(side * headR * 0.45f, headR * 0.2f, headR * 0.8f);
            Part(PrimitiveType.Sphere, _torso, eyePos, Vector3.one * headR * 0.45f, Color.white);
            Part(PrimitiveType.Sphere, _torso, eyePos + new Vector3(0f, 0f, headR * 0.18f),
                Vector3.one * headR * 0.2f, new Color(0.06f, 0.04f, 0.09f));
        }

        // mastermind: exposed pink brain dome riding on top
        if (g.Brain.Tier == "mastermind")
        {
            Part(PrimitiveType.Sphere, _torso, headPos + new Vector3(0f, headR * 0.9f, 0f),
                Vector3.one * headR * 1.1f, new Color(0.84f, 0.59f, 0.63f));
        }
    }

    private void BuildWeapon(string handFamily, Color skin)
    {
        var mount = new Vector3(_bulkScale * 1.1f, _bulkScale * 0.35f, _bulkScale * 0.7f);
        var metal = new Color(0.45f, 0.51f, 0.56f);
        var ichor = new Color(0.59f, 0.33f, 0.9f);

        switch (handFamily)
        {
            case "rifle_arm":
            case "chain_blade":
                Part(PrimitiveType.Cube, _torso, mount, new Vector3(0.25f, 0.3f, 0.9f) * _bulkScale, metal);
                Part(PrimitiveType.Cylinder, _torso, mount + new Vector3(0f, 0.05f * _bulkScale, 0.9f * _bulkScale),
                    new Vector3(0.1f, 0.55f, 0.1f) * _bulkScale, Shade(metal, 0.7f))
                    .localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case "plasma_lance":
                Part(PrimitiveType.Cylinder, _torso, mount + new Vector3(0f, 0.3f * _bulkScale, 0.2f * _bulkScale),
                    new Vector3(0.12f, 0.7f, 0.12f) * _bulkScale, ichor)
                    .localRotation = Quaternion.Euler(70f, 0f, 0f);
                Part(PrimitiveType.Sphere, _torso, mount + new Vector3(0f, 0.9f * _bulkScale, 0.5f * _bulkScale),
                    Vector3.one * 0.25f * _bulkScale, new Color(1f, 0.8f, 0.2f));
                break;
            case "laser_array":
                for (var i = -1; i <= 1; i++)
                {
                    Part(PrimitiveType.Cylinder, _torso,
                        mount + new Vector3(i * 0.15f * _bulkScale, 0.35f * _bulkScale, 0.1f * i * _bulkScale),
                        new Vector3(0.05f, 0.55f, 0.05f) * _bulkScale, new Color(0.51f, 0.86f, 1f))
                        .localRotation = Quaternion.Euler(75f, i * 8f, 0f);
                }
                break;
            case "photon_blaster":
                Part(PrimitiveType.Sphere, _torso, mount + new Vector3(0f, 0.2f * _bulkScale, 0.3f * _bulkScale),
                    Vector3.one * 0.55f * _bulkScale, Shade(skin, 0.9f));
                Part(PrimitiveType.Sphere, _torso, mount + new Vector3(0f, 0.2f * _bulkScale, 0.62f * _bulkScale),
                    Vector3.one * 0.3f * _bulkScale, new Color(1f, 0.92f, 0.69f));
                break;
            case "spore_launcher":
                Part(PrimitiveType.Sphere, _torso, mount + new Vector3(0f, 0.25f * _bulkScale, 0.2f * _bulkScale),
                    Vector3.one * 0.5f * _bulkScale, Shade(ichor, 0.75f));
                break;
            case "hand_stump":
                break; // healed over -- nothing to mount
            default: // claw_hand, pincer, tentacle: an organic arm nub + claw wedge
                Part(PrimitiveType.Sphere, _torso, mount, Vector3.one * 0.4f * _bulkScale, Shade(skin, 0.9f));
                Part(PrimitiveType.Cube, _torso, mount + new Vector3(0.1f * _bulkScale, -0.25f * _bulkScale, 0.3f * _bulkScale),
                    new Vector3(0.15f, 0.4f, 0.5f) * _bulkScale, new Color(0.77f, 0.72f, 0.6f));
                break;
        }
    }

    private void BuildLegs(GenomeDto g, Color skin)
    {
        var count = Locomotion.LegsFor(_plan);
        if (count == 0) return;

        var legCol = Shade(skin, 0.85f);
        var pairs = count / 2;
        for (var p = 0; p < pairs; p++)
        {
            // hips spread along the body's length; single pair sits centered
            var zFrac = pairs == 1 ? 0f : Mathf.Lerp(0.8f, -0.8f, p / (float)(pairs - 1));
            for (var side = -1; side <= 1; side += 2)
            {
                var leg = new Leg
                {
                    HipLocal = new Vector3(side * _bulkScale * (_plan == "crab" ? 1.7f : 0.8f),
                        BodyHeight * 0.85f, zFrac * _bulkScale),
                    Group = (p + (side > 0 ? 0 : 1)) % 2,   // diagonal/tripod alternation
                    Upper = Segment(legCol),
                    Lower = Segment(Shade(legCol, 0.9f)),
                    Foot = Part(PrimitiveType.Sphere, transform, Vector3.zero,
                        Vector3.one * 0.28f * _bulkScale, Shade(legCol, 0.7f)),
                };
                leg.FootWorld = transform.TransformPoint(new Vector3(leg.HipLocal.x, 0f, leg.HipLocal.z));
                leg.FootWorld = new Vector3(leg.FootWorld.x, 0f, leg.FootWorld.z);
                _legs.Add(leg);
            }
        }
    }

    private Transform Segment(Color color)
    {
        var t = Part(PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, color);
        return t;
    }

    private Transform Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(ShaderUtil.FindRenderableShader());
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
        return go.transform;
    }

    // ---- gait ---------------------------------------------------------------

    /// <summary>Called by the agent every frame with the body's actual
    /// world velocity. All stepping is derived from it -- nothing here
    /// reads an animation clock.</summary>
    public void UpdateLocomotion(Vector3 velocity, float dt)
    {
        var speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        _distTraveled += speed * dt;

        if (_legs.Count == 0)
        {
            UpdateLeglessLocomotion(velocity, speed, dt);
            return;
        }

        // distance-phased body bob: peaks line up with footfalls
        if (_torso != null)
        {
            var bobPhase = _distTraveled / Mathf.Max(_stride, 0.01f) * Mathf.PI * 2f;
            _torso.localPosition = new Vector3(0f, BodyHeight + Mathf.Sin(bobPhase) * _legLen * 0.045f, 0f);
        }

        var dir = speed > 0.05f ? new Vector3(velocity.x, 0f, velocity.z) / speed : Vector3.zero;
        var swingDur = _stride / Mathf.Max(speed, 0.8f) * 0.55f;

        // whose turn: only one alternation group may be airborne at once
        var airborneGroup = -1;
        foreach (var leg in _legs)
            if (leg.Swinging) { airborneGroup = leg.Group; break; }

        foreach (var leg in _legs)
        {
            var hipW = transform.TransformPoint(leg.HipLocal);
            var restW = new Vector3(hipW.x, 0f, hipW.z);

            if (leg.Swinging)
            {
                leg.SwingT += dt / Mathf.Max(swingDur, 0.05f);
                if (leg.SwingT >= 1f)
                {
                    leg.Swinging = false;
                    leg.FootWorld = leg.SwingTo;   // plants EXACTLY at the target -- world-locked from here
                }
                else
                {
                    var t = leg.SwingT;
                    var eased = t * t * (3f - 2f * t);
                    var pos = Vector3.Lerp(leg.SwingFrom, leg.SwingTo, eased);
                    pos.y = Mathf.Sin(t * Mathf.PI) * _stride * 0.22f;
                    leg.FootWorld = pos;
                }
            }
            else if (speed > 0.05f)
            {
                // the no-skate trigger: only actual body displacement,
                // measured hip-to-planted-foot, can start a step
                var strain = restW + dir * (_stride * 0.2f) - leg.FootWorld;
                var mayStep = airborneGroup == -1 || airborneGroup == leg.Group;
                if (mayStep && strain.sqrMagnitude > _stride * 0.5f * (_stride * 0.5f))
                {
                    leg.Swinging = true;
                    leg.SwingT = 0f;
                    leg.SwingFrom = leg.FootWorld;
                    leg.SwingTo = restW + dir * (_stride * 0.55f);
                    airborneGroup = leg.Group;
                }
            }

            RenderLeg(leg, hipW);
        }
    }

    private void RenderLeg(Leg leg, Vector3 hipW)
    {
        var foot = leg.FootWorld;
        var seg = _legLen * 0.62f; // each of the two segments
        var span = foot - hipW;
        var d = span.magnitude;

        // 2-bone knee: bend sideways-and-forward, amount from the
        // law-of-cosines triangle the two segments make over the span
        var bendAmt = Mathf.Sqrt(Mathf.Max(0.01f, seg * seg - d * d * 0.25f));
        var sideDir = (transform.right * Mathf.Sign(leg.HipLocal.x) * 0.55f + transform.forward * 0.45f).normalized;
        var knee = (hipW + foot) * 0.5f + sideDir * bendAmt;

        SetBetween(leg.Upper, hipW, knee, 0.12f * _bulkScale);
        SetBetween(leg.Lower, knee, foot, 0.09f * _bulkScale);
        leg.Foot.position = foot + Vector3.up * 0.1f;
    }

    private static void SetBetween(Transform t, Vector3 a, Vector3 b, float radius)
    {
        var span = b - a;
        var len = span.magnitude;
        t.position = (a + b) * 0.5f;
        if (len > 0.001f) t.rotation = Quaternion.FromToRotation(Vector3.up, span);
        t.localScale = new Vector3(radius, len * 0.5f, radius); // unity cylinder is 2 units tall at scale 1
    }

    private void UpdateLeglessLocomotion(Vector3 velocity, float speed, float dt)
    {
        if (_plan == "floater")
        {
            // hover: the one body allowed a time-based bob -- it isn't
            // touching the ground, so there's no footfall to sync to
            _hoverPhase += dt * 2f;
            if (_torso != null)
                _torso.localPosition = new Vector3(0f, BodyHeight + 0.6f + Mathf.Sin(_hoverPhase) * 0.15f, 0f);
            return;
        }

        if (_plan == "blob")
        {
            // squash-and-stretch phased by distance: it rolls its mass
            // over the ground, so deformation tracks ground covered
            var phase = _distTraveled / Mathf.Max(_bulkScale * 1.2f, 0.01f) * Mathf.PI * 2f;
            var squash = 1f + Mathf.Sin(phase) * 0.12f * Mathf.Clamp01(speed);
            if (_torso != null)
                _torso.localScale = new Vector3(1f / squash, squash, 1f / squash);
            return;
        }

        // serpentine: tail segments FOLLOW the head's actual path (a
        // position trail), so the body traces the ground it covered --
        // the legless equivalent of planted feet
        for (var i = 0; i < _tailSegments.Count; i++)
        {
            var target = i == 0
                ? transform.TransformPoint(new Vector3(0f, BodyHeight * 0.7f, -_bulkScale * 0.8f))
                : _tailTrail[i - 1];
            var cur = _tailTrail[i];
            var spacing = _bulkScale * 0.8f;
            var to = target - cur;
            var dist = to.magnitude;
            if (dist > spacing)
                cur = target - to / dist * spacing;
            _tailTrail[i] = cur;
            _tailSegments[i].position = cur + Vector3.up * Mathf.Sin(_distTraveled * 1.5f + i * 0.9f) * 0.06f;
        }
    }

    // ---- colors -------------------------------------------------------------

    private static Color SkinColor(string plan, string creatureId)
    {
        switch (plan)
        {
            case "crab": return new Color(0.75f, 0.25f, 0.2f);
            case "serpentine": return new Color(0.2f, 0.55f, 0.25f);
            case "winged": return new Color(0.55f, 0.35f, 0.75f);
            case "avian": return new Color(0.8f, 0.65f, 0.2f);
            case "arachnid": return new Color(0.3f, 0.2f, 0.35f);
            case "treant": return new Color(0.35f, 0.5f, 0.25f);
            case "floater": return new Color(0.3f, 0.7f, 0.8f);
            case "blob": return new Color(0.6f, 0.3f, 0.55f);
            default:
            {
                var hash = 0;
                foreach (var c in creatureId) hash = hash * 31 + c;
                var hue = ((hash % 360) + 360) % 360 / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.85f);
            }
        }
    }

    private static Color Shade(Color c, float f)
    {
        return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);
    }
}
