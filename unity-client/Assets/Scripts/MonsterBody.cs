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
/// smoothly lifts the whole body -- torso, every leg's articulated
/// segments, AND every leg's hip HARDWARE (a separate static transform
/// from the LegKit brace/joint mesh -- easy to miss, and missing it is
/// exactly what "feet are still stuck to the ground" looked like: a
/// piece of the leg staying planted at ground height while the rest of
/// the creature flew off) -- to a flight altitude together, tucking the
/// legs into a folded mid-air pose instead of trying to plant a ground
/// step. SetFlying(false) eases back down and replants properly
/// (SnapFeetToGround) the instant it touches down. This is purely a
/// visual/height concern -- MonsterAgent's flight decision and pathing
/// live entirely on its side; this class just makes the airborne state
/// LOOK like flight -- including a nose-down lean proportional to speed
/// and a bank into turns proportional to yaw rate, both applied to the
/// TORSO's local rotation only (never the root transform, which stays
/// pure-yaw for MonsterAgent's steering and the leg rig's own math), so
/// it reads as actual flight rather than a sled hanging in the air.
/// DESCENT is gated by SetDescentFloor: the altitude ease never drops
/// below whatever building is directly beneath the creature right now,
/// so a takeoff off a tall roof (or a drop to a lower cruise tier over a
/// shorter building) holds height until horizontally clear instead of
/// sinking THROUGH the structure. And takeoff resets the standing
/// surface to street level, so a creature that leaves a 40m roof stops
/// treating 40m as "the ground" the instant it climbs off.
///
/// WING FLAP (creator direction, 2026-07): a winged creature's wings are
/// no longer baked into the static body mesh -- packages/creature-mesh
/// returns them as a separate, root-relative WingSocketInfo (the same
/// treatment as legs, for the same reason), which BuildWings mounts as
/// two hinge Transforms Unity can actually rotate. UpdateWingFlap beats
/// them: fast while still climbing or descending, a slower cruise beat
/// once holding altitude, folded to rest once grounded.
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
        public Transform Hip;     // LegKit's brass joint hardware, if any (null for primitive-cylinder legs)
        public float DressScale;  // >0: LegKit mesh segments (x/z keep this scale); 0: primitive cylinders
    }

    private readonly List<Leg> _legs = new List<Leg>();
    private readonly List<Transform> _tailSegments = new List<Transform>(); // serpentine
    private readonly List<Vector3> _tailTrail = new List<Vector3>();
    private Transform _torso;

    /// <summary>2026-07 (GrabCursor grab feature, "for arm actions just
    /// rotate the shoulder joints up and down"): the pivot the whole
    /// hand/weapon assembly is built under, at the SAME mount point
    /// <see cref="BuildWeapon"/> always used -- not an anatomically real
    /// shoulder socket (this rig has no such data for the hand/weapon the
    /// way legs have real hip sockets), just an armature to rotate the
    /// existing static geometry around. Null for a creature with nothing
    /// mounted there (BuildWeapon still creates it unconditionally for
    /// simplicity -- an empty pivot is harmless).</summary>
    private Transform _shoulder;
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
    private bool _flyingHigh;      // which cruise tier MonsterAgent picked -- see LowFlightAltitude/HighFlightAltitude
    private float _flightLift;     // actual animated lift, MoveTowards-eased toward the current target
    private float _flightLowLift;  // clears short buildings (small/medium tier) -- the old single-tier altitude
    private float _flightHighLift; // clears EVERYTHING (large/landmark tier too), for "climb over it" legs
    private float _groundY;        // the surface under this creature: 0 at street level, a roof height when perched on a building
    private float _descentFloor;   // minimum altitude the body may ease DOWN to right now (a building directly below) -- MonsterAgent pushes it every frame from SurfaceHeightAt

    /// <summary>World terrain height at an XZ position (docs/21 sculpted
    /// ground) -- set by MonsterAgent before Build so feet plant on the
    /// slope under each foot instead of a flat plane. Null (tests, plans
    /// without an agent) means flat ground, the old behavior. The
    /// creature's OWN transform.y already carries the terrain under its
    /// center (agent terrain-follow); this sampler is only for points
    /// AWAY from the center: individual foot plants.</summary>
    public System.Func<Vector3, float> GroundSampler;

    private float TerrainAt(Vector3 p)
    {
        return GroundSampler != null ? GroundSampler(p) : 0f;
    }
    private const float FlightLiftAirborneThreshold = 0.5f;   // this far above _groundY, legs are considered "in the air"

    /// <summary>2026-07 (GrabCursor's grab/roof-display feature): forces
    /// <see cref="Airborne"/> true regardless of <see cref="_flightLift"/>
    /// -- a non-flying creature grabbed by the claw (or resting on a
    /// Factory roof after a clone-drop) has no ground under it either,
    /// the exact same "nothing to plant a foot on" situation flight
    /// already solves for winged creatures, just reached a different way.
    /// Defaults false, so every creature that never sets it renders
    /// byte-for-byte unchanged.</summary>
    public bool ForceTuckLegs;

    /// <summary>2026-07 (GrabCursor's grab feature: "arm and legs
    /// struggling"): set externally (MonsterAgent.TickHeld) every frame
    /// while <see cref="ForceTuckLegs"/> is active -- each leg kicks
    /// against its own tucked fold with a phase offset derived from this
    /// value, so the legs read as actively struggling against the grip
    /// rather than passively folded the way flight-tuck is. Ignored
    /// entirely while ForceTuckLegs is false (a flying creature's normal
    /// flight-tuck stays exactly as it was).</summary>
    public float StrugglePhase;

    /// <summary>True while the body is meaningfully off its standing
    /// surface -- the single definition every airborne check shares, so
    /// "perched on a 6m roof" (lift == groundY, feet planted) never gets
    /// confused with "flying 6m up" (lift far above groundY, legs
    /// tucked).</summary>
    private bool Airborne { get { return ForceTuckLegs || _flightLift > _groundY + FlightLiftAirborneThreshold; } }

    // wing flap (winged plan only): a hinge transform per side, posed
    // ROOT-RELATIVE geometry parented under it (packages/creature-mesh's
    // WingSocketInfo) rather than baked into the static body mesh, so it
    // can actually rotate. Faster beats while climbing/descending, a
    // slower cruise beat once holding altitude -- creator direction,
    // 2026-07: "faster to get off the ground and land, slower once
    // they're thoroughly airborne."
    private Transform _wingPivotL;
    private Transform _wingPivotR;
    private float _wingFlapPhase;
    private const float WingFlapHzTakeoff = 2.3f;
    private const float WingFlapHzCruise = 0.9f;
    private const float WingFlapAmpTakeoff = 42f;   // degrees
    private const float WingFlapAmpCruise = 22f;    // degrees, a lighter cruise beat

    // flight attitude (winged plan only, airborne only): nose-down lean
    // proportional to forward speed, bank proportional to turn rate --
    // creator direction, 2026-07: "should lean a little bit forward in
    // the direction of travel, bank while making turns, more rooted in
    // actual flight." Applied to _torso's LOCAL rotation only, never the
    // root transform -- the root stays pure-yaw (MonsterAgent's
    // FollowPath already owns it, and legs/hip math are computed in its
    // frame), so pitch/bank is purely cosmetic on the visible body+wings
    // and never fights the steering or the leg rig.
    private float _flightPitch;
    private float _flightBank;
    private const float FlightMaxPitch = 22f;    // degrees, nose-down at speed
    private const float FlightMaxBank = 30f;     // degrees, into a hard turn
    private const float FlightPitchPerSpeed = 2.4f;   // degrees per m/s
    private const float FlightBankPerYawRate = 0.4f;  // degrees per deg/s of turn

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

    /// <summary>2026-08 (creator report: "the indicator on the monsters
    /// should be attached to body not to ground indicator... especially
    /// important for flying units"): the SAME animated altitude offset
    /// `UpdateLocomotion` already applies to `_torso.localPosition` and
    /// `_selectionCollider.center` (both above, `BodyHeight + _flightLift`)
    /// -- 0 for a grounded/non-flying creature, eased up toward its cruise
    /// altitude while airborne. Exposed so `MonsterAgent`'s own body-
    /// attached UI (the selection ring) can track the SAME offset instead
    /// of staying pinned to the ground-locked root transform.</summary>
    public float FlightLift { get { return _flightLift; } }

    /// <summary>Cruise altitude that clears short buildings (small/medium
    /// tier, per RuntimeCityBuilder's height table) but NOT tall ones --
    /// MonsterAgent reads these two so it can weigh "detour around a tall
    /// building at this altitude" against "climb to HighFlightAltitude
    /// and fly the direct line over it" by actual energy cost, rather
    /// than the game guessing which is prettier.</summary>
    public float LowFlightAltitude { get { return _flightLowLift; } }

    /// <summary>Cruise altitude that clears every building tier, including
    /// landmarks -- nothing left to route around, so a path flown at this
    /// altitude is always the direct line.</summary>
    public float HighFlightAltitude { get { return _flightHighLift; } }

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
        // two cruise tiers (BuildingTier heights, RuntimeCityBuilder:
        // small 6 / medium 12 / large 30 / landmark 40): Low clears
        // small/medium with margin -- MonsterAgent's pathing lets flight
        // at this altitude ignore those buildings' footprints entirely,
        // only weaving around large/landmark. High clears everything, so
        // a path flown there never needs to detour at all. Which tier a
        // given leg actually uses is MonsterAgent's call (SetFlying's
        // `high` argument), weighed by energy: a short detour beats a
        // big climb, a long detour loses to it.
        _flightLowLift = Mathf.Max(14f, BodyHeight * 2.5f);
        _flightHighLift = Mathf.Max(46f, _flightLowLift + 30f);

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
            if (lab.Wing != null) BuildWings(lab.Wing, s);
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
            // hips sit above the STANDING SURFACE (street or rooftop
            // perch), same offset the airborne leg loop applies
            var hipW = transform.TransformPoint(leg.HipLocal) + Vector3.up * _groundY;
            // small per-group stagger so the first steps alternate
            // naturally instead of every leg triggering at once
            var stagger = (leg.Group == 0 ? 1f : -1f) * _stride * 0.15f;
            leg.FootWorld = new Vector3(hipW.x, 0f, hipW.z) + transform.forward * stagger;
            // the foot plants on the terrain under ITS OWN xz (a slope's
            // downhill foot sits lower than the uphill one) plus any
            // perch offset -- building plots are terrain-flat, so the
            // two never both apply (docs/21 flat-lock rule)
            leg.FootWorld = new Vector3(leg.FootWorld.x, TerrainAt(leg.FootWorld) + _groundY, leg.FootWorld.z);
            leg.Swinging = false;
            leg.SwingT = 0f;
            if (leg.Hip != null) leg.Hip.localPosition = leg.HipLocal + Vector3.up * _groundY;
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

    /// <summary>MonsterAgent's flight intent (SetFlying(true, ...) to take
    /// off, SetFlying(false, ...) to land). `high` picks which cruise
    /// tier to climb to while flying -- see LowFlightAltitude /
    /// HighFlightAltitude -- ignored while landing. No-op on a plan that
    /// can't fly. The actual lift animates smoothly in UpdateLocomotion
    /// rather than snapping -- see _flightLift.</summary>
    public void SetFlying(bool flying, bool high)
    {
        if (!_canFly) return;
        // TAKING OFF (false->true): we've left whatever we were standing
        // on, so the standing surface is "unknown until we land again" --
        // reset it to street level. Without this, a creature that took
        // off from a 40m roof kept _groundY=40 the whole flight, and the
        // instant its altitude eased below 40 (down to a 14m cruise, say)
        // Airborne flipped false and the legs tried to plant on a 40m
        // plane while the torso hung at 14m -- stretched, distorted legs.
        // Landing sets the real new surface (SetGroundHeight, before
        // SetFlying(false)), so only reset on the takeoff edge.
        if (flying && !_flying) _groundY = 0f;
        _flying = flying;
        _flyingHigh = high;
    }

    /// <summary>2026-08 (creator direction, verbatim: "When grab is
    /// grabbing a flying units must snap elevation to the ground. This
    /// will avoid the floating way above the factory"): an immediate,
    /// non-eased reset of flight state, unlike `SetFlying(false, ...)`'s
    /// normal ~1.4s glide-down (see `_flightLift`'s own comment). A
    /// flying creature grabbed mid-cruise kept its `_flightLift` cruise-
    /// altitude offset on the TORSO (a child transform -- see
    /// `UpdateLocomotion`'s `_torso.localPosition` line) even though
    /// `MonsterAgent.BeginHeld`/`TickHeld` immediately takes over the
    /// ROOT transform and repositions it right at the cursor's own
    /// ground/roof height every frame from then on -- `_flying` was
    /// never told to turn off, so the torso just kept rendering tens of
    /// meters above a root that had already snapped down, and that same
    /// stale offset was still there later when the specimen landed on a
    /// Factory roof. Called once from `BeginHeld`, not every frame --
    /// this is a one-time snap, not an ongoing constraint.
    ///
    /// 2026-08 bugfix (creator report: "it sort of works now but is
    /// unreliable" -- after the first pass above only reset
    /// `_flightLift`/`_groundY`): `_descentFloor` is a THIRD altitude
    /// field, pushed every frame by `MonsterAgent.Update()` from
    /// `SurfaceHeightAt(transform.position)` -- exactly the kind of
    /// per-frame push that stops the instant `Update()` early-returns
    /// for `_held`. Left un-reset, it stayed pinned at whatever it was
    /// the instant BEFORE the grab -- a real, nonzero number anytime a
    /// flyer got grabbed while cruising over/near a building (the
    /// factory itself, most often, since that's exactly where a player
    /// reaches for one). The very next `UpdateLocomotion` tick then read
    /// `Mathf.Max(_groundY=0, _descentFloor=stale)` and started easing
    /// `_flightLift` right back UP toward that stale floor -- the snap
    /// "worked" (briefly touched 0) only to immediately climb again,
    /// which is exactly "sort of works... unreliable": fine when grabbed
    /// clear of any building, broken again next to the one building this
    /// whole feature is about.</summary>
    public void SnapToGrabbed()
    {
        if (!_canFly) return;
        _flying = false;
        _flyingHigh = false;
        _flightLift = 0f;
        _groundY = 0f;
        _descentFloor = 0f;
        SnapFeetToGround();
    }

    /// <summary>The surface this creature stands (or lands) on: 0 at
    /// street level, a roof height when perching on a building. Landing
    /// eases the lift down to THIS height instead of always to the
    /// street, and planted feet live on this plane. MonsterAgent sets it
    /// right before landing (from RuntimeCityBuilder.SurfaceHeightAt) and
    /// re-syncs it when destruction changes the surface under a perched
    /// unit -- a collapsed roof lowers this to 0 and the body eases down
    /// to the rubble on its own.</summary>
    public void SetGroundHeight(float y)
    {
        if (!_canFly) return;   // only flyers can ever stand anywhere but the street
        _groundY = Mathf.Max(0f, y);
    }

    /// <summary>Hard floor on how low the body may ease its altitude
    /// RIGHT NOW: the height of any building directly beneath the
    /// creature, so it can never descend THROUGH a roof/wall on the way
    /// down (creator direction, 2026-07: "see if they have a clear path
    /// before they descend"). Only gates DESCENT -- climbing over things
    /// is always allowed, and the floor never forces the creature up.
    /// MonsterAgent recomputes it every frame from
    /// RuntimeCityBuilder.SurfaceHeightAt(current position).</summary>
    public void SetDescentFloor(float minAltitude)
    {
        if (!_canFly) return;
        _descentFloor = Mathf.Max(0f, minAltitude);
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

        // 2026-07: every hand/weapon part now hangs off ONE shoulder pivot
        // at the mount point instead of straight off _torso -- an
        // armature purely to rotate the whole assembly around (see
        // UpdateLocomotion's own new shoulder-swing block), not a change
        // to how anything looks at rest. Every part's position below is
        // therefore relative to `mount` now (the pivot's own origin), not
        // an absolute `mount + offset` anymore.
        _shoulder = new GameObject("Shoulder").transform;
        _shoulder.SetParent(_torso, false);
        _shoulder.localPosition = mount;

        switch (handFamily)
        {
            case "rifle_arm":
            case "chain_blade":
                Part(PrimitiveType.Cube, _shoulder, Vector3.zero, new Vector3(0.25f, 0.3f, 0.9f) * _bulkScale, metal);
                Part(PrimitiveType.Cylinder, _shoulder, new Vector3(0f, 0.05f * _bulkScale, 0.9f * _bulkScale),
                    new Vector3(0.1f, 0.55f, 0.1f) * _bulkScale, Shade(metal, 0.7f))
                    .localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case "plasma_lance":
                Part(PrimitiveType.Cylinder, _shoulder, new Vector3(0f, 0.3f * _bulkScale, 0.2f * _bulkScale),
                    new Vector3(0.12f, 0.7f, 0.12f) * _bulkScale, ichor)
                    .localRotation = Quaternion.Euler(70f, 0f, 0f);
                Part(PrimitiveType.Sphere, _shoulder, new Vector3(0f, 0.9f * _bulkScale, 0.5f * _bulkScale),
                    Vector3.one * 0.25f * _bulkScale, new Color(1f, 0.8f, 0.2f));
                break;
            case "laser_array":
                for (var i = -1; i <= 1; i++)
                {
                    Part(PrimitiveType.Cylinder, _shoulder,
                        new Vector3(i * 0.15f * _bulkScale, 0.35f * _bulkScale, 0.1f * i * _bulkScale),
                        new Vector3(0.05f, 0.55f, 0.05f) * _bulkScale, new Color(0.51f, 0.86f, 1f))
                        .localRotation = Quaternion.Euler(75f, i * 8f, 0f);
                }
                break;
            case "photon_blaster":
                Part(PrimitiveType.Sphere, _shoulder, new Vector3(0f, 0.2f * _bulkScale, 0.3f * _bulkScale),
                    Vector3.one * 0.55f * _bulkScale, Shade(skin, 0.9f));
                Part(PrimitiveType.Sphere, _shoulder, new Vector3(0f, 0.2f * _bulkScale, 0.62f * _bulkScale),
                    Vector3.one * 0.3f * _bulkScale, new Color(1f, 0.92f, 0.69f));
                break;
            case "spore_launcher":
                Part(PrimitiveType.Sphere, _shoulder, new Vector3(0f, 0.25f * _bulkScale, 0.2f * _bulkScale),
                    Vector3.one * 0.5f * _bulkScale, Shade(ichor, 0.75f));
                break;
            case "electric_arc":
                // 2026-08: a pair of divergent electrode prongs -- this
                // primitive fallback path is effectively dead code today
                // (CreatureBuilder.Build never returns null for a
                // well-formed genome, see MonsterBody's own Init), kept
                // in step with every other hand family for defensive
                // consistency rather than left to fall through to the
                // organic-arm default below.
                Part(PrimitiveType.Sphere, _shoulder, Vector3.zero, Vector3.one * 0.3f * _bulkScale, Shade(ichor, 0.85f));
                Part(PrimitiveType.Cylinder, _shoulder, new Vector3(-0.15f * _bulkScale, 0.35f * _bulkScale, 0.15f * _bulkScale),
                    new Vector3(0.05f, 0.3f, 0.05f) * _bulkScale, new Color(0.55f, 0.69f, 1f))
                    .localRotation = Quaternion.Euler(60f, 0f, -15f);
                Part(PrimitiveType.Cylinder, _shoulder, new Vector3(0.15f * _bulkScale, 0.35f * _bulkScale, 0.15f * _bulkScale),
                    new Vector3(0.05f, 0.3f, 0.05f) * _bulkScale, new Color(0.55f, 0.69f, 1f))
                    .localRotation = Quaternion.Euler(60f, 0f, 15f);
                break;
            case "hand_stump":
                break; // healed over -- nothing to mount (the pivot stays, just empty)
            default: // claw_hand, pincer, tentacle: an organic arm nub + claw wedge
                Part(PrimitiveType.Sphere, _shoulder, Vector3.zero, Vector3.one * 0.4f * _bulkScale, Shade(skin, 0.9f));
                Part(PrimitiveType.Cube, _shoulder, new Vector3(0.1f * _bulkScale, -0.25f * _bulkScale, 0.3f * _bulkScale),
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
                // hip hardware (brass brace, joint ball): fixed RELATIVE to
                // the socket on the ground, but still has to ride the same
                // flight lift as everything else once airborne -- kept on
                // the Leg so UpdateLocomotion can reposition it, not just
                // set once here and forgotten
                leg.Hip = LabMeshBuilder.AttachChunks(kit.Hip, transform, "LegHip", s);
                leg.Hip.localPosition = leg.HipLocal;
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

    /// <summary>Mounts each wing's root-relative geometry (packages/
    /// creature-mesh's WingSocketInfo) under its own hinge Transform, at
    /// the shoulder joint's actual position on the body -- the SAME
    /// lab-to-world transform the main body mesh holder uses (scale by
    /// `s`, then the shared -BodyHeight offset), so the pivot lines up
    /// with where the wing root actually sits on the rendered body.
    /// UpdateLocomotion rotates these pivots to flap; nothing else here
    /// needs to touch them again after Build.</summary>
    private void BuildWings(WingSocketInfo wing, float s)
    {
        var offset = new Vector3(0f, -BodyHeight, 0f);

        _wingPivotL = new GameObject("WingPivotL").transform;
        _wingPivotL.SetParent(_torso, false);
        _wingPivotL.localPosition = new Vector3((float)wing.RootL.X, (float)wing.RootL.Y, (float)wing.RootL.Z) * s + offset;
        LabMeshBuilder.AttachChunks(wing.Left, _wingPivotL, "WingMeshL", s);

        _wingPivotR = new GameObject("WingPivotR").transform;
        _wingPivotR.SetParent(_torso, false);
        _wingPivotR.localPosition = new Vector3((float)wing.RootR.X, (float)wing.RootR.Y, (float)wing.RootR.Z) * s + offset;
        LabMeshBuilder.AttachChunks(wing.Right, _wingPivotR, "WingMeshR", s);
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
            var wasAirborne = Airborne;
            var tierLift = _flyingHigh ? _flightHighLift : _flightLowLift;
            // landing eases down to the STANDING SURFACE, not always to
            // the street -- that's what perching on a roof is
            var targetLift = _flying ? tierLift : _groundY;
            // ...but never below whatever building is directly below RIGHT
            // NOW: Max(target, floor) holds altitude over a roof (takeoff
            // off a tall building, a descent passing over a shorter one)
            // until the creature has moved horizontally clear of it, then
            // the floor drops and the descent completes. Climbing is
            // unaffected -- if target is already above the floor, Max is a
            // no-op. This is the whole "clear path before you descend."
            targetLift = Mathf.Max(targetLift, _descentFloor);
            // ~1.4s for a full lift-off/landing/tier change, whatever the
            // relevant altitude is (a low->high climb mid-flight takes
            // longer than a landing from low, proportionally)
            _flightLift = Mathf.MoveTowards(_flightLift, targetLift, (tierLift / 1.4f) * dt);
            if (wasAirborne && !_flying && !Airborne)
            {
                _flightLift = _groundY;
                SnapFeetToGround();   // just touched down -- replant properly, not a stale mid-air tuck
            }
            // click-to-select must track what's actually on screen
            if (_selectionCollider != null)
                _selectionCollider.center = new Vector3(0f, BodyHeight + _flightLift, 0f);

            UpdateWingFlap(dt);
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
            if (Airborne)
            {
                _flightBobPhase += dt * 3.2f;
                flightBob = Mathf.Sin(_flightBobPhase) * 0.3f;
            }
            _torso.localPosition = new Vector3(0f,
                BodyHeight + Mathf.Sin(bobPhase) * _legLen * 0.045f + _flightLift + flightBob, 0f);

            // flight attitude: nose down with speed, banked into turns --
            // eased toward its target so a sudden speed/turn spike doesn't
            // snap the body into a pose. Settles back level on the ground
            // (or on a rooftop perch -- Airborne is false there too).
            var pitchTarget = Airborne ? Mathf.Clamp(speed * FlightPitchPerSpeed, 0f, FlightMaxPitch) : 0f;
            var bankTarget = Airborne
                ? Mathf.Clamp(-yawRate * Mathf.Rad2Deg * FlightBankPerYawRate, -FlightMaxBank, FlightMaxBank)
                : 0f;
            _flightPitch = Mathf.Lerp(_flightPitch, pitchTarget, dt * 3f);
            _flightBank = Mathf.Lerp(_flightBank, bankTarget, dt * 4f);
            if (_canFly) _torso.localRotation = Quaternion.Euler(_flightPitch, 0f, _flightBank);
        }

        var flatVel = new Vector3(velocity.x, 0f, velocity.z);
        var bodyPos = transform.position;

        foreach (var leg in _legs)
        {
            var hipW = transform.TransformPoint(leg.HipLocal) + Vector3.up * _flightLift;
            // the hip HARDWARE (brass brace/joint ball) is a separate
            // static transform from the articulated Upper/Lower/Foot
            // segments -- it was being left behind at ground height
            // during flight while the rest of the leg lifted with the
            // body, which is exactly the "feet are still stuck to the
            // ground" report (a piece of the leg literally staying
            // planted while everything else flew off)
            if (leg.Hip != null) leg.Hip.localPosition = leg.HipLocal + Vector3.up * _flightLift;

            if (Airborne)
            {
                // airborne: fold the leg up rather than reaching for a
                // ground contact that isn't there. FootWorld is scratch
                // here -- SnapFeetToGround() re-derives it from scratch
                // the moment this creature actually lands.
                var tucked = hipW - Vector3.up * (_legLen * 0.55f) + transform.forward * (_legLen * 0.35f);

                // 2026-07 (GrabCursor grab feature: "arm and legs
                // struggling"): ForceTuckLegs (never true for ordinary
                // flight) layers a per-leg kicking offset on top of the
                // static flight-tuck fold, phase-shifted by this leg's
                // own index so the legs kick out of sync with each
                // other -- a struggle, not a synchronized wiggle. _legs
                // is small (a handful of legs at most) so the IndexOf
                // scan here is cheap; not worth caching an index per Leg
                // for a list this size.
                if (ForceTuckLegs)
                {
                    var kickPhase = StrugglePhase + _legs.IndexOf(leg) * 1.7f;
                    tucked += transform.right * (Mathf.Sin(kickPhase) * _legLen * 0.25f)
                            + Vector3.up * (Mathf.Cos(kickPhase * 1.3f) * _legLen * 0.12f);
                }

                leg.FootWorld = tucked;
                leg.Swinging = false;
                RenderLeg(leg, hipW);
                continue;
            }

            // feet plant on the STANDING SURFACE -- the sculpted terrain
            // under this hip (docs/21), plus the rooftop-perch offset
            var restW = new Vector3(hipW.x, 0f, hipW.z);
            restW.y = TerrainAt(restW) + _groundY;

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
                    // arc between the two plants' own surface heights --
                    // a downhill step starts high and lands low
                    pos.y = Mathf.Lerp(leg.SwingFrom.y, leg.SwingTo.y, eased)
                        + Mathf.Sin(t * Mathf.PI) * strideEff * 0.22f;
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

        UpdateShoulderSwing();
    }

    // 2026-07 (GrabCursor grab feature, creator direction verbatim: "for
    // arm actions just rotate the shoulder joints up and down a
    // naturalist way do not go over 30 degrees of motion. NOT IN THE Y
    // Axis"): 30 degrees is the swing's own AMPLITUDE, not a peak-to-
    // peak span -- Sin ranges [-1,1], so the shoulder never deviates more
    // than this many degrees from its rest pose in either direction.
    private const float ShoulderSwingMaxDeg = 30f;

    /// <summary>Swings the whole hand/weapon assembly (see BuildWeapon's
    /// own <see cref="_shoulder"/> pivot) up and down around its LOCAL X
    /// axis ONLY -- pitch, never Y (yaw) or Z (roll), matching "NOT IN
    /// THE Y Axis" precisely -- driven by the SAME <see
    /// cref="StrugglePhase"/> the legs already kick to, so the whole
    /// creature reads as one struggling gesture instead of two
    /// independently-timed animations. Rests at identity the instant
    /// <see cref="ForceTuckLegs"/> goes false (dropped, or never grabbed
    /// at all) -- StrugglePhase itself is reset to 0 on drop (see
    /// MonsterAgent.EndHeld/BeginRoofDisplay), so this naturally settles
    /// there too without a separate flag.</summary>
    private void UpdateShoulderSwing()
    {
        if (_shoulder == null) return;
        var swingDeg = ForceTuckLegs ? Mathf.Sin(StrugglePhase * 0.9f) * ShoulderSwingMaxDeg : 0f;
        _shoulder.localRotation = Quaternion.Euler(swingDeg, 0f, 0f);
    }

    /// <summary>Bat-style flap: fast, powerful beats while still climbing
    /// or descending (_flightLift hasn't reached its cruise target yet),
    /// a slower, lighter beat once actually holding altitude. Rest pose
    /// (identity) once grounded -- the wing's own authored droop already
    /// reads as folded, no extra "fold" rotation needed. Left/right rotate
    /// with OPPOSITE signs around their own local Z: the geometry itself
    /// is mirrored (built with side=+-1 baked in, packages/creature-mesh),
    /// so the same-sign local rotation on both sides would swing them in
    /// opposite senses -- the sign flip is what makes them beat together.</summary>
    private void UpdateWingFlap(float dt)
    {
        if (_wingPivotL == null || _wingPivotR == null) return;

        var flapping = _flying || Airborne;   // folded while standing, street OR rooftop perch
        if (!flapping)
        {
            _wingPivotL.localRotation = Quaternion.identity;
            _wingPivotR.localRotation = Quaternion.identity;
            return;
        }

        var tierLift = _flyingHigh ? _flightHighLift : _flightLowLift;
        var liftFrac = tierLift > 0.01f ? _flightLift / tierLift : 0f;
        var atCruise = _flying && liftFrac > 0.97f;   // holding altitude, not still climbing/descending
        var hz = atCruise ? WingFlapHzCruise : WingFlapHzTakeoff;
        var amp = atCruise ? WingFlapAmpCruise : WingFlapAmpTakeoff;

        _wingFlapPhase += dt * hz * Mathf.PI * 2f;
        var angle = Mathf.Sin(_wingFlapPhase) * amp;
        _wingPivotL.localRotation = Quaternion.Euler(0f, 0f, angle);
        _wingPivotR.localRotation = Quaternion.Euler(0f, 0f, -angle);
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

    /// <summary>Per-plan color only, no per-individual hash -- for HUD
    /// panels (SelectionHud) that group creatures by TYPE (<see
    /// cref="MonsterAgent.BodyPlan"/>, this codebase's own established
    /// "type" concept -- see its doc comment) rather than by individual
    /// identity, where <see cref="SkinColor"/>'s per-creature hash would
    /// give every individual of an unlisted plan a different color instead
    /// of one stable color for the whole group. Passing `plan` itself as
    /// the hash seed (instead of a creature id) means every plan --
    /// listed or not -- gets one fixed, stable color.</summary>
    internal static Color PlanColor(string plan)
    {
        return SkinColor(plan, plan);
    }

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
