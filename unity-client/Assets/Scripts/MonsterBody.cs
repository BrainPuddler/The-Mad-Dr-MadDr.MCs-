using System.Collections.Generic;
using MadDr.CreatureMesh;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A genome-driven articulated body with distance-driven stepping.
/// ALL nine body plans regenerate the Lab renderer's actual stitched
/// b-movie body from DNA (packages/creature-mesh, the docs/08 port):
/// torso lathes, bolted brass belts, franken faces, tier-ladder heads
/// including the mastermind's brain under glass, wings, cobra hoods,
/// see-through blob organs, and every hand/sensor/eye part family.
/// Legs stay owned by the gait rig below, but dressed in the family's
/// real geometry via LegKit (tapered flesh/chitin/piston segments,
/// hoofs, talon fans, brass hip hardware) -- no more stick cylinders.
/// The primitive-based builders remain only as a fallback should the
/// mesh build ever fail.
///
/// THE GAIT RULE (creator direction, 2026-07): no skating, ever.
/// Planted feet are stored as WORLD positions and never move while
/// planted -- the body travels over them, and a leg only swings when the
/// body's actual displacement has pulled its hip far enough from the
/// planted foot. Step length and cadence derive from the creature's
/// LocomotionProfile (physiology), and the body bob is phased by
/// DISTANCE TRAVELED, not wall-clock time, so motion always matches
/// ground covered by construction rather than by tuning.
///
/// FLIGHT (winged plan only, creator direction 2026-07): SetFlying(true)
/// smoothly lifts the whole body -- torso AND every leg hip together, so
/// nothing floats free of its legs -- to a flight altitude, tucking the
/// legs into a folded mid-air pose instead of trying to plant a ground
/// step. SetFlying(false) eases back down and replants properly
/// (SnapFeetToGround) the instant it touches down. This is purely a
/// visual/height concern -- MonsterAgent's flight decision and pathing
/// live entirely on its side; this class just makes the airborne state
/// LOOK like flight. No wing-flap animation (the mesh's wings are baked
/// into the same static chunk as the rest of the body, not a separately
/// posable transform, docs/08 port scope) -- deferred, logged in docs/12.
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
        public float DressScale;  // >0: LegKit mesh segments (x/z keep this scale); 0: primitive cylinders
    }

    private readonly List<Leg> _legs = new List<Leg>();
    private readonly List<Transform> _tailSegments = new List<Transform>(); // serpentine
    private readonly List<Vector3> _tailTrail = new List<Vector3>();
    private Transform _torso;
    private string _plan = "tetrapod";
    private float _legLen = 1.2f;
    private float _stride = 1.8f;
    private float _bulkScale = 1f;
    private float _standHeight = 1.2f;
    private float _distTraveled;
    private float _gaitDist;       // linear + rotational displacement -- what feet actually have to cover
    private float _lastYaw;
    private bool _yawInitialized;
    private float _avgHipRadius = 1f;
    private float _hoverPhase;
    private float _flightBobPhase;
    private BoxCollider _selectionCollider;

    // ---- flight (winged plan only) ------------------------------------------
    private bool _canFly;
    private bool _flying;          // MonsterAgent's current intent
    private float _flightLift;     // actual animated lift, MoveTowards-eased toward _flying's target
    private float _flightTargetLift;
    private const float FlightLiftAirborneThreshold = 0.5f;   // above this, legs are considered "in the air"

    /// <summary>Fixed at Build time from the plan's leg count -- NOT
    /// derived from _legs.Count on the fly: the first version did that,
    /// and every read that happened before BuildLegs() ran (the torso's
    /// own height, the first leg's hip) silently got the legless
    /// fallback value. Order-dependent getters bite exactly once,
    /// at init, where they're hardest to see.</summary>
    public float BodyHeight { get { return _standHeight; } }

    /// <summary>True only for the winged plan -- SetFlying is a no-op
    /// for everything else.</summary>
    public bool CanFly { get { return _canFly; } }

    public void Build(StoredGenomeDto creature)
    {
        var g = creature.Genome;
        _plan = g.Body.Plan;
        var bulk = g.Body.Params.Length > 1 ? (float)g.Body.Params[1] : 0.5f;
        _bulkScale = Mathf.Lerp(0.8f, 2.4f, bulk);
        var legGene = g.Slots.Leg.Params.Length > 0 ? (float)g.Slots.Leg.Params[0] : 0.5f;
        _legLen = Mathf.Lerp(0.9f, 2.2f, legGene) * Mathf.Lerp(0.8f, 1.3f, bulk);
        _stride = _legLen * 1.5f;
        _standHeight = Locomotion.LegsFor(_plan) > 0 ? _legLen : _bulkScale * 0.9f;
        _canFly = _plan == "winged";
        // clears most building roofs (BuildingTier: small 6 / medium 12
        // / large 30 / landmark 40) without needing to track real
        // per-building height -- altitude is cosmetic only, since the
        // agent's pathing already keeps flight out of building footprints
        _flightTargetLift = Mathf.Max(14f, BodyHeight * 2.5f);

        var skin = SkinColor(g.Body.Plan, creature.Id);

        _torso = new GameObject("Torso").transform;
        _torso.SetParent(transform, false);
        _torso.localPosition = new Vector3(0f, BodyHeight, 0f);

        // the real Lab body, regenerated from DNA -- every plan now.
        // Legged plans scale so the mesh's own leg length lands exactly
        // on the rig's; legless plans (blob/serpentine/treant/floater
        // ignore the leg slot) scale by overall height instead. The mesh
        // is authored ground-up at y=0, so it hangs at -BodyHeight under
        // the bobbing torso node and rides the same gait bob (and blob
        // squash, and floater hover) as everything else.
        var lab = CreatureBuilder.Build(g);
        if (lab != null)
        {
            var s = lab.Leg != null
                ? _legLen / (float)lab.Leg.Len
                : Mathf.Lerp(2.4f, 4.6f, bulk) / Mathf.Max(0.1f, (float)lab.TopY);
            LabMeshBuilder.Attach(lab, _torso, new Vector3(0f, -BodyHeight, 0f), s);
            if (lab.Leg != null) BuildLegsFromSocket(lab, s);
        }
        else
        {
            BuildTorso(skin, bulk);
            BuildHead(g, skin);
            BuildWeapon(g.Slots.Hand.Family, skin);
            BuildLegs(g, skin);
        }
        SnapFeetToGround();

        // one collider on the root for selection raycasts; the parts
        // themselves stay collider-free (StripCollider on every primitive).
        // Kept as a field so UpdateLocomotion can slide it up with the
        // body while flying -- otherwise a flying unit's clickable box
        // stays pinned at ground height while the model floats up above
        // it, and clicking what you SEE stops working.
        _selectionCollider = gameObject.AddComponent<BoxCollider>();
        _selectionCollider.center = new Vector3(0f, BodyHeight, 0f);
        _selectionCollider.size = new Vector3(_bulkScale * 2.2f, BodyHeight * 2f, _bulkScale * 2.6f);
    }

    /// <summary>Re-plants every foot (and the serpentine tail trail)
    /// directly under the body at its CURRENT world position. Must be
    /// called after any teleport -- planted feet are world-locked by
    /// design (the no-skate rule), which means a teleport leaves them
    /// behind at the old location and every leg renders as a huge
    /// stretched line back to it. That's exactly what the first live
    /// test showed: Build() ran while the monster still stood at the
    /// world origin, THEN the agent teleported it to its home hex,
    /// and all feet stayed obediently planted at (0,0,0).</summary>
    public void SnapFeetToGround()
    {
        foreach (var leg in _legs)
        {
            var hipW = transform.TransformPoint(leg.HipLocal);
            // small per-group stagger so the first steps alternate
            // naturally instead of every leg triggering at once
            var stagger = (leg.Group == 0 ? 1f : -1f) * _stride * 0.15f;
            leg.FootWorld = new Vector3(hipW.x, 0f, hipW.z) + transform.forward * stagger;
            leg.FootWorld = new Vector3(leg.FootWorld.x, 0f, leg.FootWorld.z);
            leg.Swinging = false;
            leg.SwingT = 0f;
            RenderLeg(leg, hipW);
        }
        for (var i = 0; i < _tailTrail.Count; i++)
        {
            var p = transform.TransformPoint(new Vector3(0f, BodyHeight * 0.7f, -(i + 1) * _bulkScale * 0.8f));
            _tailTrail[i] = p;
            _tailSegments[i].position = p;
        }
        WarnIfFeetImplausible();
    }

    /// <summary>Loud failure beats silent spaghetti: if any planted foot
    /// ends up implausibly far from its hip, say so in the Console with
    /// numbers, instead of rendering kilometer-long legs and leaving the
    /// creator to diagnose it from a screenshot.</summary>
    private void WarnIfFeetImplausible()
    {
        foreach (var leg in _legs)
        {
            var hipW = transform.TransformPoint(leg.HipLocal);
            var d = leg.FootWorld - new Vector3(hipW.x, 0f, hipW.z);
            d.y = 0f;
            if (d.magnitude > _stride * 2f)
            {
                Debug.LogWarning("MonsterBody gait sanity: a foot is " + d.magnitude
                    + "m from its hip (stride " + _stride + "m) on " + name
                    + " -- was the body teleported without SnapFeetToGround()?");
            }
        }
    }

    /// <summary>MonsterAgent's flight intent (SetFlying(true) to take
    /// off, SetFlying(false) to land). No-op on a plan that can't fly.
    /// The actual lift animates smoothly in UpdateLocomotion rather than
    /// snapping -- see _flightLift.</summary>
    public void SetFlying(bool flying)
    {
        if (!_canFly) return;
        _flying = flying;
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

        ComputeAvgHipRadius();
    }

    /// <summary>Legs for a Lab-mesh body: mounted at the socket frame the
    /// creature builder returned, scaled into world units, and DRESSED in
    /// the leg family's real geometry (LegKit) instead of stick
    /// cylinders. Pair count follows the family the way the Lab does:
    /// insect struts come 2-3 pairs, piston spider mode 2, everything
    /// else one mirrored pair. The mesh bakes everything above the hips;
    /// the rig owns everything below so the no-skate contract holds.</summary>
    private void BuildLegsFromSocket(CreatureMeshResult lab, float s)
    {
        var sock = lab.Leg;
        var p = sock.P;
        var count = sock.Params.Length > 4 ? (float)sock.Params[4] : 0.5f;

        float[] zOffsets;
        if (sock.Family == "insect_leg")
        {
            var pairs = count >= 0.5f ? 3 : 2;
            zOffsets = new float[pairs];
            for (var i = 0; i < pairs; i++)
                zOffsets[i] = (i / (pairs - 1f) - 0.5f) * 2.6f;   // front reaches, rear rakes
        }
        else if (sock.Family == "piston_leg")
        {
            zOffsets = new[] { -0.99f, 0.99f };                   // spider-strut quad
        }
        else
        {
            zOffsets = new[] { 0f };
        }

        for (var pi = 0; pi < zOffsets.Length; pi++)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var kit = LegKit.Build(sock.Family, sock.Params, lab.Skin, side);
                var leg = new Leg
                {
                    HipLocal = new Vector3(side * (float)p.X * s, (float)p.Y * s,
                        ((float)p.Z + zOffsets[pi]) * s),
                    Group = (pi + (side > 0 ? 0 : 1)) % 2,   // diagonal/tripod alternation
                    Upper = LabMeshBuilder.AttachChunks(kit.Upper, transform, "LegUpper", s),
                    Lower = LabMeshBuilder.AttachChunks(kit.Lower, transform, "LegLower", s),
                    Foot = LabMeshBuilder.AttachChunks(kit.Foot, transform, "LegFoot", s),
                    DressScale = s,
                };
                // hip hardware (brass brace, joint ball) sits fixed at the
                // socket; the segments below it articulate
                var hip = LabMeshBuilder.AttachChunks(kit.Hip, transform, "LegHip", s);
                hip.localPosition = leg.HipLocal;
                leg.FootWorld = transform.TransformPoint(new Vector3(leg.HipLocal.x, 0f, leg.HipLocal.z));
                leg.FootWorld = new Vector3(leg.FootWorld.x, 0f, leg.FootWorld.z);
                _legs.Add(leg);
            }
        }
        ComputeAvgHipRadius();
    }

    /// <summary>Average horizontal hip distance from the body axis --
    /// converts a yaw rate into the meters/sec the feet actually must
    /// cover to keep up with a turn (beetle turning: rotation is
    /// footwork too).</summary>
    private void ComputeAvgHipRadius()
    {
        var sum = 0f;
        foreach (var leg in _legs)
            sum += new Vector3(leg.HipLocal.x, 0f, leg.HipLocal.z).magnitude;
        _avgHipRadius = _legs.Count > 0 ? Mathf.Max(0.3f, sum / _legs.Count) : 1f;
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

        if (_canFly)
        {
            var wasAirborne = _flightLift > FlightLiftAirborneThreshold;
            var targetLift = _flying ? _flightTargetLift : 0f;
            // ~1.4s for a full lift-off or landing, whatever _flightTargetLift is
            _flightLift = Mathf.MoveTowards(_flightLift, targetLift, (_flightTargetLift / 1.4f) * dt);
            if (wasAirborne && !_flying && _flightLift <= FlightLiftAirborneThreshold)
            {
                _flightLift = 0f;
                SnapFeetToGround();   // just touched down -- replant properly, not a stale mid-air tuck
            }
            // click-to-select must track what's actually on screen
            if (_selectionCollider != null)
                _selectionCollider.center = new Vector3(0f, BodyHeight + _flightLift, 0f);
        }

        if (_legs.Count == 0)
        {
            UpdateLeglessLocomotion(velocity, speed, dt);
            return;
        }

        // longer strides at higher speed (animals do this too) --
        // reduces cadence pressure so alternation can keep up at a run
        var strideEff = _stride * Mathf.Clamp(speed / 3f, 1f, 1.6f);

        // BEETLE TURNING (gait v4, verified in a standalone sim at 6
        // speed/turn combos including rotate-in-place before porting).
        // v3's phase clock advanced only with LINEAR distance -- but a
        // turn sweeps the hips sideways, generating foot strain while
        // the clock (and therefore the other group's step window) barely
        // moved: legs stretched until the fail-safe fired, exactly the
        // "turning is problematic, legs get all stretchy" report. Insects
        // treat rotation as footwork: (a) the gait clock now advances
        // with linear PLUS rotational displacement (|yaw rate| x average
        // hip radius = the meters/sec the feet actually must cover), and
        // (b) each leg leads its step along its OWN rest-point velocity
        // (body velocity + rotational velocity at that hip) -- outside
        // legs automatically take long arcs, inside legs short ones,
        // which IS how beetles corner.
        var yaw = Mathf.Atan2(transform.forward.x, transform.forward.z);
        if (!_yawInitialized) { _lastYaw = yaw; _yawInitialized = true; }
        var dYaw = yaw - _lastYaw;
        while (dYaw > Mathf.PI) dYaw -= 2f * Mathf.PI;
        while (dYaw < -Mathf.PI) dYaw += 2f * Mathf.PI;
        _lastYaw = yaw;
        var yawRate = dYaw / Mathf.Max(dt, 0.0001f);

        var effSpeed = speed + Mathf.Abs(yawRate) * _avgHipRadius;
        _gaitDist += effSpeed * dt;
        var phase = (_gaitDist / Mathf.Max(strideEff, 0.01f)) % 1f;

        // gait-phased body bob: peaks line up with footfalls, including
        // the footfalls a turn-in-place produces
        if (_torso != null)
        {
            var bobPhase = _gaitDist / Mathf.Max(strideEff, 0.01f) * Mathf.PI * 2f;
            // airborne gets its OWN wall-clock bob layered on top -- like
            // the floater's hover, there's no footfall to sync a
            // distance-driven bob to once truly off the ground
            var flightBob = 0f;
            if (_flightLift > FlightLiftAirborneThreshold)
            {
                _flightBobPhase += dt * 3.2f;
                flightBob = Mathf.Sin(_flightBobPhase) * 0.3f;
            }
            _torso.localPosition = new Vector3(0f,
                BodyHeight + Mathf.Sin(bobPhase) * _legLen * 0.045f + _flightLift + flightBob, 0f);
        }

        var flatVel = new Vector3(velocity.x, 0f, velocity.z);
        var bodyPos = transform.position;

        foreach (var leg in _legs)
        {
            var hipW = transform.TransformPoint(leg.HipLocal) + Vector3.up * _flightLift;

            if (_flightLift > FlightLiftAirborneThreshold)
            {
                // airborne: fold the leg up rather than reaching for a
                // ground contact that isn't there. FootWorld is scratch
                // here -- SnapFeetToGround() re-derives it from scratch
                // the moment this creature actually lands.
                var tucked = hipW - Vector3.up * (_legLen * 0.55f) + transform.forward * (_legLen * 0.35f);
                leg.FootWorld = tucked;
                leg.Swinging = false;
                RenderLeg(leg, hipW);
                continue;
            }

            var restW = new Vector3(hipW.x, 0f, hipW.z);

            // this leg's rest-point velocity: translation + rotation's
            // contribution at this hip (v_rot = yawRate * (r.z, 0, -r.x))
            var rx = hipW.x - bodyPos.x;
            var rz = hipW.z - bodyPos.z;
            var restVel = flatVel + new Vector3(yawRate * rz, 0f, -yawRate * rx);
            var lead = restVel.sqrMagnitude > 0.0001f ? restVel.normalized
                : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

            if (leg.Swinging)
            {
                // swing progress advances with gait displacement too (a
                // swing occupies half a stride of travel), min-clamped so
                // a swing still settles if the body stops mid-step
                leg.SwingT += Mathf.Max(effSpeed, 1f) * dt / (strideEff * 0.5f);
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
                    pos.y = Mathf.Sin(t * Mathf.PI) * strideEff * 0.22f;
                    leg.FootWorld = pos;
                }
                RenderLeg(leg, hipW);
                continue;
            }

            if (effSpeed > 0.05f)
            {
                var strainVec = restW + lead * (strideEff * 0.2f) - leg.FootWorld;
                strainVec.y = 0f;
                var windowOpen = leg.Group == 0 ? phase < 0.5f : phase >= 0.5f;

                if (windowOpen && strainVec.magnitude > strideEff * 0.15f)
                {
                    leg.Swinging = true;
                    leg.SwingT = 0f;
                    leg.SwingFrom = leg.FootWorld;
                    leg.SwingTo = restW + lead * (strideEff * 0.5f);
                    RenderLeg(leg, hipW);
                    continue;
                }
            }

            // fail-safes, kept as the backstop that makes gum
            // GEOMETRICALLY impossible whatever the scheduler does: a
            // leg stretched past 1.5x stride force-launches out of
            // window; past 3x (a teleport, or a bug this code hasn't
            // met yet) it snap-replants instantly and warns -- visible
            // recovery beats invisible taffy.
            var lag = restW - leg.FootWorld;
            lag.y = 0f;
            if (lag.magnitude > strideEff * 3f)
            {
                leg.FootWorld = restW + lead * (strideEff * 0.3f);
                Debug.LogWarning("MonsterBody gait fail-safe: snap-replanted a foot " + lag.magnitude
                    + "m behind its hip on " + name + " (stride " + strideEff + "m).");
            }
            else if (lag.magnitude > strideEff * 1.5f)
            {
                leg.Swinging = true;
                leg.SwingT = 0f;
                leg.SwingFrom = leg.FootWorld;
                leg.SwingTo = restW + lead * (strideEff * 0.5f);
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

        if (leg.DressScale > 0f)
        {
            // LegKit segments: radius is baked into the mesh (lab units),
            // so x/z hold the lab->world scale and only y stretches to the
            // live joint distance
            SetBetweenDressed(leg.Upper, hipW, knee, leg.DressScale);
            SetBetweenDressed(leg.Lower, knee, foot, leg.DressScale);
            // feet are authored ground-origin, +z forward: plant exactly at
            // the world-locked foot point, yawed with the body
            leg.Foot.position = foot;
            var fwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (fwd.sqrMagnitude > 0.0001f)
                leg.Foot.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            return;
        }

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

    /// <summary>SetBetween for LegKit meshes: same midpoint/rotation
    /// convention (local -1 lands on `a`, +1 on `b` -- the kit authors
    /// its proximal radius at -1 to match), but x/z keep the fixed
    /// lab-to-world scale instead of becoming the radius.</summary>
    private static void SetBetweenDressed(Transform t, Vector3 a, Vector3 b, float xz)
    {
        var span = b - a;
        var len = span.magnitude;
        t.position = (a + b) * 0.5f;
        if (len > 0.001f) t.rotation = Quaternion.FromToRotation(Vector3.up, span);
        t.localScale = new Vector3(xz, len * 0.5f, xz);
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
