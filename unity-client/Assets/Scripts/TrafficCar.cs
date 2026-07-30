using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Docs/19 traffic (docs/21 batch 2, item 9; extended per creator report --
/// see the fix note on <see cref="PickNext"/>): a car that drives the road
/// network toward a chosen DESTINATION hex (see <see cref="PickDestination"/>,
/// mirroring Citizen's own destination-walk pattern -- creator direction,
/// 2026-07: "they are driving erratically all over the road. They should
/// ... be picking destinations on the map to go to, be more realistic"),
/// then pulls to the curb and parks for a while before setting off again --
/// not an endless wander. Panics like a Citizen when a monster gets close
/// (peeling toward whichever reachable hex is farthest from the threat,
/// breaking off a trip OR a parked stay to do it), and steers away from
/// monsters even off-panic while picking a normal route. Not a combatant:
/// no collider, doesn't fight, doesn't block movement -- purely cosmetic
/// crowd dressing for the streets RoadDresser paints, same scoping
/// Citizen.cs already established for pedestrians.
/// </summary>
public class TrafficCar : MonoBehaviour
{
    private const float CruiseSpeed = 6.5f;
    private const float FleeSpeed = 11f;
    private const float FleeRadius = 16f;      // a monster this close: full panic, drop everything
    private const float SwerveRadius = 22f;    // between FleeRadius and MonsterAwareRadius: curve around it
    private const float SwerveMax = 5.5f;      // lateral metres at full strength -- a real lane's worth
    private const float MonsterAwareRadius = 28f; // a monster this close: steer a normal route away from it
    private const float ArriveRadius = 1.5f;
    private const float CurbOffset = 2.5f;     // same curb-lane distance RoadDresser parks its own cars at

    // lane discipline (creator direction, 2026-07: cars must drive in
    // straight lines, in their lane, with proper following gaps)
    private const float LaneOffset = 2.0f;     // metres to the right of the road centerline -- keeps opposing traffic apart and cars in a lane
    private const float FollowRange = 15f;     // start easing off the throttle when something's this close ahead
    private const float FollowGap = 5.5f;      // hard gap kept in front (~one car length + 0.2*size) -- speed hits 0 here
    private const float LaneHalfWidth = 2.4f;  // only things within this of the lane line count as "ahead of me"

    private const int MinTripHops = 5;
    private const int MaxTripHops = 14;   // also the destination-search radius -- see PickDestination

    // 2026-07 creator direction ("they are driving erratically all over
    // the road. They should ... be picking destinations on the map to go
    // to, be more realistic"): PickNext used to be a pure wander hash with
    // no concept of "going somewhere" -- fine for a first pass, but reads
    // as aimless. Mirrors Citizen.StepTowardDestination's own pattern: pick
    // a real hex to head for, then greedily favor whichever neighbor gets
    // closer to it every hop, over the road network instead of sidewalks.
    //
    // DestinationWeight, per hex-distance step, is sized to sit strictly
    // BETWEEN the wander hash's own 0-65535 spread and the monster-aware
    // penalty's own max of (MonsterAwareRadius * 4000 = 112000) -- big
    // enough that "closer to the destination" always beats the wander
    // noise (so the errand is never a coin flip), small enough that a
    // monster sitting genuinely close to a candidate hex can still
    // outweigh a 1-hex destination preference (safety still overrides the
    // errand -- verified by the driving-verify flightcheck's "destination
    // routing does not override real danger" check, not just asserted).
    private const float DestinationWeight = 90000f;
    private const float SafetyHopMultiplier = 3f;     // hop-count safety cap = chosen destination's hex distance * this, generous slack since the greedy walk is constrained to the road GRAPH (real detours around blocks), not a straight hex line -- exists only so an unreachable/looping destination still parks eventually

    // docs/28's two-tier lighting model, extended to traffic (creator
    // direction, 2026-07: "forward and slightly down facing lights and
    // brake lights... at night... only render[ing] the ones in regions
    // the user can see"). The headlight's Tier 2 (a real, budgeted
    // Light) competes in the SAME shared DynamicLightBudget pool every
    // streetlamp/window/neon already does -- nearest-to-camera wins, so
    // a far-away or off-screen car's headlight simply never costs a
    // real light, exactly the ask, with zero new perf-management code
    // of its own. Brake lights are Tier 1 (emissive material) only --
    // an indicator, not something that should ever cast light on its
    // surroundings, so there's no budget to compete for in the first
    // place.
    private const float HeadlightTiltDeg = 14f;          // "slightly down" from the car's own forward
    private const float BulbWorldDiameter = 0.22f;       // roughly a real headlight/taillight lens, meters
    private const float NightEligibleThreshold = 0.05f;  // DayNightState.NightAmount above this: headlight can compete for a real light
    private const float BrakeDecelEpsilon = 0.2f;        // speed must drop by at least this much frame-to-frame to read as braking

    // 2026-07 creator direction ("change where cars are driving to the
    // areas close to or near the player view area -- let's not waste
    // processing power"): biases WHICH destination PickDestination picks
    // (see that method), so a car's whole errand naturally tends to land
    // somewhere near the view instead of clear across a network nobody
    // will ever see, without fighting the per-hop destination-seeking
    // score every single hop the way it used to when this lived in
    // PickNext directly (a 2026-07 revision: an earlier version applied
    // this every hop alongside the destination term, but at that scale it
    // was either negligible next to DestinationWeight or, tuned larger,
    // fought the greedy walk hop to hop -- moving it to a ONE-TIME pick
    // at trip start gets the same "cars trend toward the view" outcome
    // without diluting "the car is actually going somewhere," caught by
    // the driving-verify flightcheck's route-bias test regressing when
    // this was still a per-hop term).
    private const float CameraBiasWeight = 250f;

    // 2026-07 creator direction ("verify naturalistic driving. Speed up
    // and some aggressive passing to slow cars"): real 1950s traffic
    // isn't a single uniform cruise speed -- some cars are naturally
    // faster/slower (PersonalSpeedMult), which is what actually creates
    // situations where a car catches up to one ahead of it in the first
    // place. When following gets sustained and tight, a car commits to
    // an overtake: swerves into the opposite side of the road (a real
    // lane doesn't exist per hex today, so "opposite side" is the
    // mirror of its own LaneOffset), speeds up aggressively, and merges
    // back once past -- re-checking the passing side stays clear every
    // frame it's committed, so it's aggressive, not suicidal.
    private const float PersonalSpeedMultMin = 0.8f;
    private const float PersonalSpeedMultMax = 1.35f;
    private const float PassBlockedTriggerTime = 1.0f;      // how long a car tolerates tight following before committing to pass
    private const float PassTriggerClearFraction = 0.55f;   // only bother passing a genuinely SLOW car, not a merely-close one
    private const float PassLaneOffset = LaneOffset * 2f;   // swerve fully across to the opposite side, clear of the own-lane check width
    private const float PassLookahead = 6f;                 // aim this far ahead of the CURRENT position while passing, not the far-off next-hex target -- a steep commit angle instead of a shallow drift
    private const float PassLaneCheckRange = FollowRange * 1.4f; // look further than a normal following check -- committing to a pass needs more runway
    private const float PassSpeedBoost = 1.45f;             // "aggressive" -- a real burst of acceleration to get the maneuver over with quickly
    private const float PassMaxDuration = 4f;               // safety cap so a pass can't get stuck open-ended if something keeps re-blocking the merge check

    private enum State { Driving, Parked }

    private RuntimeCityBuilder _builder;
    private HashSet<HexCoord> _network;
    private HexCoord _from;
    private HexCoord _to;
    private Vector3 _target;
    private bool _fleeing;

    private State _state;
    private int _hopsRemaining;
    private float _parkTimer;
    private float _parkDurationBase; // 0 when movingPercent is ~1 (never park)
    private int _tripSalt;
    private int _hopCounter; // rotates the wander hash every pick -- see PickNext
    private HexCoord _destination;
    private bool _hasDestination;

    // naturalistic-driving state (see the constants above for the "why")
    private float _personalSpeedMult;
    private float _blockedTimer;
    private bool _passing;
    private float _passTimer;
    private Vector3 _passStartPos; // where this car actually was the instant it committed to the pass -- see the ownLaneClear fix in Update()

    // roundabout circulation state (creator direction, 2026-07: "Cars
    // must follow the curve proper curves of the road")
    private bool _circling;        // currently arcing around a roundabout island
    private HexCoord _roundExit;   // hex chosen to leave the roundabout by
    private float _exitAngle;      // world angle of _roundExit from the island center
    private float _prevAngle;      // last frame's angle around the center (for sweep accumulation)
    private float _sweptDeg;       // total degrees circulated since entering

    // lights (see the constants above for the "why")
    private Transform _headlightAim;
    private Renderer[] _headlightBulbs;
    private Renderer[] _brakeLightBulbs;
    private float _lastSpeed;

    // defaults true: a car behaves exactly as it always did until
    // RuntimeCityBuilder's own periodic refresh has run at least once
    // (and for any harness/scene that never wires the refresh at all --
    // same "opt-in cost, unchanged default behavior" posture as every
    // other budget/gate this project has added).
    private bool _nearCamera = true;

    public bool IsDriving { get { return _state == State.Driving; } }

    /// <summary>Called by RuntimeCityBuilder's own throttled refresh, not
    /// every frame -- see RefreshTrafficActivity's own doc comment.</summary>
    public void SetNearCamera(bool near) { _nearCamera = near; }

    /// <summary>Force an immediate departure regardless of this car's own
    /// remaining park timer -- called by RuntimeCityBuilder's periodic
    /// traffic-band check when the fleet's live moving fraction has
    /// drifted too far below target (creator direction, 2026-07: "make
    /// sure the proper % of cars are in motion"). No-op if already
    /// driving.</summary>
    public void DepartNow()
    {
        if (_state == State.Driving) return;
        BeginTrip();
    }

    /// <summary>`movingPercent` is the docs/19 traffic-field target: the
    /// long-run fraction of the fleet that should be actively driving at
    /// any moment (the rest sit parked between trips). Derived once into
    /// an average park duration long enough, relative to an average
    /// trip's drive time, to hit that ratio -- see the derivation on
    /// <see cref="_parkDurationBase"/>'s computation below.</summary>
    public void Init(RuntimeCityBuilder builder, HashSet<HexCoord> network, HexCoord start, Color body,
        float movingPercent)
    {
        _builder = builder;
        _network = network;
        _from = start;
        _to = start;
        transform.position = RoadPoint(start, start);
        _target = transform.position;

        // 2026-07 hardening (creator report: "the cars are just parked...
        // I don't see any cars with light on either" -- both symptoms
        // together, confirmed by a real fault-injection flightcheck, are
        // the exact signature of THIS bug): BuildBody/BuildLights are
        // both purely cosmetic (chassis material/shape, headlight/brake-
        // light bulbs) and have no business being able to prevent the
        // state/route setup below -- what actually makes this car drive
        // at all -- from ever running. BuildBody in particular calls
        // `new Material(ShaderUtil.FindRenderableShader())` as its very
        // FIRST line; if that shader lookup ever comes back empty (this
        // environment has no real Unity Editor to confirm WHY it would,
        // but the flightcheck below proves the crash site and its
        // consequence precisely), the exception used to aborts Init()
        // before PickNext() -- or even the state assignment -- ever ran,
        // leaving `_state` at its C# default (Driving, enum value 0) with
        // `_target` still equal to the car's own spawn position and
        // `_hopsRemaining` still 0. The very first Update() would then
        // read that as "arrived, no hops left" and immediately call
        // ParkHere() -- and since BuildLights() (which sets up the bulb
        // arrays) never ran either, the car is left both permanently
        // parked-with-no-real-redeparture-context AND with no working
        // lights, in one single failure. Each cosmetic call is now
        // isolated so a failure in either one is a real degraded mode
        // (drives without a chassis material / without working lights),
        // never a silent total failure of movement. Logged so a real
        // fault here is visible, not swallowed invisibly.
        try
        {
            BuildBody(body, Hash(start, 7) % 4 == 0);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("TrafficCar.BuildBody faulted during Init (car will drive without a proper chassis): " + ex);
        }

        try
        {
            BuildLights();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("TrafficCar.BuildLights faulted during Init (car will drive without working lights): " + ex);
        }

        // naturalistic speed variance: real traffic isn't one uniform
        // cruise speed -- this is also what actually CREATES situations
        // where a car catches up to a slower one ahead of it, the
        // precondition the passing logic below reacts to.
        var speedRoll = (Hash(start, GetInstanceID() + 61) & 0xFFFF) / 65535f;
        _personalSpeedMult = Mathf.Lerp(PersonalSpeedMultMin, PersonalSpeedMultMax, speedRoll);

        _lastSpeed = CruiseSpeed * _personalSpeedMult;   // avoids a false "braking" flash on the very first driving frame

        var pct = Mathf.Clamp(movingPercent, 0.05f, 1f);
        var avgHops = (MinTripHops + MaxTripHops) / 2f;
        var avgDriveSeconds = avgHops * ((float)HexCoord.HexMeters / CruiseSpeed);
        _parkDurationBase = pct >= 0.999f ? 0f : avgDriveSeconds * (1f / pct - 1f);

        // Stagger the fleet's initial phase so every car doesn't drive (or
        // park) in lockstep: roll each car independently against the same
        // target fraction, and if parked, start partway through a park
        // stay instead of always at its full length.
        var startRoll = (Hash(start, GetInstanceID() + 3) & 0xFFFF) / 65535f;
        if (_parkDurationBase <= 0f || startRoll < pct)
        {
            _state = State.Driving;
            PickDestination();
            _hopsRemaining = RandomHopBudget();
            PickNext();
        }
        else
        {
            _state = State.Parked;
            transform.rotation = Quaternion.Euler(0f, Hash(start, GetInstanceID()) % 360, 0f);
            _parkTimer = ((Hash(start, GetInstanceID() + 41) & 0xFFFF) / 65535f) * Mathf.Max(ParkDuration(), 1f);
        }
    }

    private static int Hash(HexCoord hex, int salt)
    {
        unchecked
        {
            var h = hex.Q * 374761393 + hex.R * 668265263 + salt * 974711;
            h = (h ^ (h >> 13)) * 1274126177;
            return h & 0x7FFFFFFF;
        }
    }

    private void BuildBody(Color body, bool truck)
    {
        // this component sits on the chassis primitive itself
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = body;
        var renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        if (truck)
        {
            // a boxy 1950s delivery van -- one tall rectangular body plus
            // a dark windshield band up front, instead of the sedan's
            // sloped cabin/fins: period street variety without a second
            // multi-piece rig's part-count/positioning risk
            transform.localScale = new Vector3(2.4f, 1.7f, 4.4f);
            var windowMat = new Material(ShaderUtil.FindRenderableShader());
            windowMat.color = new Color(0.12f, 0.14f, 0.18f);
            var windshield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            windshield.name = "Windshield";
            windshield.transform.SetParent(transform, false);
            windshield.transform.localPosition = new Vector3(0f, -0.06f, 0.56f);
            windshield.transform.localScale = new Vector3(0.98f, 0.7f, 0.32f);
            var windshieldRenderer = windshield.GetComponent<Renderer>();
            if (windshieldRenderer != null) windshieldRenderer.sharedMaterial = windowMat;
            var windshieldCollider = windshield.GetComponent<Collider>();
            if (windshieldCollider != null) Object.Destroy(windshieldCollider);
            return;
        }

        // sedan: one extra cube for the cabin, no bumpers/fins (moving
        // fast enough that the extra parked-car detail wouldn't read
        // from RTS height)
        transform.localScale = new Vector3(2.2f, 0.8f, 5.2f);

        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        cabin.transform.SetParent(transform, false);
        cabin.transform.localPosition = new Vector3(0f, 0.5f, -0.25f);
        cabin.transform.localScale = new Vector3(0.85f, 0.85f, 0.42f);
        var cabinRenderer = cabin.GetComponent<Renderer>();
        if (cabinRenderer != null) cabinRenderer.sharedMaterial = mat;
        var cabinCollider = cabin.GetComponent<Collider>();
        if (cabinCollider != null) Object.Destroy(cabinCollider);
    }

    // ---- lights ---------------------------------------------------------

    // One shared Material PER KIND across the whole fleet (SRP-batcher
    // friendly, same caching idiom BuildingDresser/RoadDresser's M()
    // already uses) -- registered with NeonRegistry exactly once, at
    // first mint, so every car's bulbs track day/night brightness
    // through the SAME global boost pipeline every window/neon/marquee
    // already rides, with no per-car per-frame color work of its own.
    // Per-car ON/OFF (driving vs parked, braking vs not) is a SEPARATE
    // concern handled by toggling each car's own Renderer.enabled --
    // a shared material's brightness is necessarily the same for every
    // renderer using it, so "is this specific car's headlight showing
    // right now" can't live in the material at all.
    private static Material _headlightMat;
    private static Material _brakeLightMat;

    private static Material HeadlightMat()
    {
        if (_headlightMat != null) return _headlightMat;
        _headlightMat = new Material(ShaderUtil.FindRenderableShader());
        var baseColor = new Color(1f, 0.95f, 0.82f);
        var emission = baseColor * 3.2f;
        _headlightMat.color = baseColor;
        _headlightMat.EnableKeyword("_EMISSION");
        _headlightMat.SetColor("_EmissionColor", emission);
        NeonRegistry.Register(_headlightMat, emission);
        return _headlightMat;
    }

    private static Material BrakeLightMat()
    {
        if (_brakeLightMat != null) return _brakeLightMat;
        _brakeLightMat = new Material(ShaderUtil.FindRenderableShader());
        var baseColor = new Color(0.95f, 0.08f, 0.05f);
        var emission = baseColor * 3.5f;
        _brakeLightMat.color = baseColor;
        _brakeLightMat.EnableKeyword("_EMISSION");
        _brakeLightMat.SetColor("_EmissionColor", emission);
        NeonRegistry.Register(_brakeLightMat, emission);
        return _brakeLightMat;
    }

    /// <summary>Two small bulbs each for head/brake lights (front/rear
    /// corners) plus one otherwise-invisible "HeadlightAim" child that
    /// carries the Tier-2 real-light registration -- its LOCAL rotation
    /// is the fixed <see cref="HeadlightTiltDeg"/> down-tilt, so its
    /// WORLD rotation (read live by DynamicLightBudget every refresh,
    /// via <see cref="GlowPointRegistry.Register"/>'s `spotAimsWithTransform`)
    /// always combines that tilt with wherever THIS car is currently
    /// facing, not a fixed streetlamp-style straight-down aim. Bulb
    /// positions/the aim point are fractional offsets of the chassis's
    /// own 1x1x1 unit cube (same convention <see cref="BuildBody"/>'s
    /// cabin/windshield already use -- Unity multiplies a child's local
    /// position by the parent's scale automatically), so this one method
    /// places lights correctly on both the sedan and the truck body
    /// without needing to know which shape it's on. Bulb scale
    /// explicitly counters the chassis's own non-uniform scale so a
    /// bulb reads as a small round lens, not a squashed ellipsoid.</summary>
    private void BuildLights()
    {
        var parentScale = transform.localScale;
        var bulbScale = new Vector3(BulbWorldDiameter / parentScale.x, BulbWorldDiameter / parentScale.y, BulbWorldDiameter / parentScale.z);
        var headMat = HeadlightMat();
        var brakeMat = BrakeLightMat();

        _headlightBulbs = new[]
        {
            MakeBulb("HeadlightL", new Vector3(-0.32f, -0.05f, 0.49f), bulbScale, headMat),
            MakeBulb("HeadlightR", new Vector3(0.32f, -0.05f, 0.49f), bulbScale, headMat),
        };
        _brakeLightBulbs = new[]
        {
            MakeBulb("BrakeLightL", new Vector3(-0.32f, -0.05f, -0.49f), bulbScale, brakeMat),
            MakeBulb("BrakeLightR", new Vector3(0.32f, -0.05f, -0.49f), bulbScale, brakeMat),
        };

        var aimGo = new GameObject("HeadlightAim");
        aimGo.transform.SetParent(transform, false);
        aimGo.transform.localPosition = new Vector3(0f, -0.05f, 0.5f);
        aimGo.transform.localRotation = Quaternion.Euler(HeadlightTiltDeg, 0f, 0f);
        _headlightAim = aimGo.transform;

        // isEligible: only compete for a real Light while actually
        // driving at night -- see GlowPointRegistry.Register's own doc
        // comment for why this is the mechanism (not true register/
        // unregister) that keeps a parked or daylight car's headlight
        // from ever holding a budget slot.
        GlowPointRegistry.Register(_headlightAim, new Color(1f, 0.95f, 0.85f), LightType.Spot,
            spotAimsWithTransform: true,
            isEligible: () => IsDriving && DayNightState.NightAmount > NightEligibleThreshold);

        SetBulbsActive(_headlightBulbs, false);
        SetBulbsActive(_brakeLightBulbs, false);
    }

    private Renderer MakeBulb(string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        var t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = localPos;
        t.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        return renderer;
    }

    private static void SetBulbsActive(Renderer[] bulbs, bool active)
    {
        // 2026-07: null-safe on purpose, not just tidy defensiveness --
        // see UpdateLights' own doc comment for the real bug this closes
        // off (a lighting-subsystem failure during BuildLights() could
        // leave one of these arrays null forever, and an unguarded
        // bulbs.Length here would then throw EVERY subsequent frame).
        if (bulbs == null) return;
        for (var i = 0; i < bulbs.Length; i++) if (bulbs[i] != null) bulbs[i].enabled = active;
    }

    /// <summary>Driven from every real movement resolution
    /// (<see cref="MoveToward"/>, the one function every driving Update()
    /// path funnels through) plus explicitly from the Parked branch.
    /// Headlights: on while driving, once it's dark enough to matter.
    /// Brake lights: on while driving AND actively decelerating -- see
    /// <see cref="BrakeDecelEpsilon"/>'s own comment for what "actively
    /// decelerating" means here (the follow-traffic slowdown is the
    /// primary real-world trigger this covers; the abrupt stop-to-park
    /// transition has no gradual deceleration modeled today, so it isn't
    /// covered by this signal -- a real, deliberate scope limit, not an
    /// oversight).
    ///
    /// 2026-07 hardening (creator report: "the cars are just parked...
    /// I don't see any cars with light on either" -- both symptoms
    /// together are the exact signature of this exact bug class): this
    /// is called from the TOP of the Parked branch in Update(), BEFORE
    /// the park-timer/re-departure logic runs. If anything in here ever
    /// throws (a lighting-subsystem issue this environment has no way to
    /// reproduce/verify, since there's no real Unity Editor available
    /// here), the exception would propagate up and abort Update() before
    /// _parkTimer ever gets decremented or BeginTrip() ever gets called
    /// -- permanently stranding that car in Parked. Worse, since every
    /// Driving car eventually finishes its trip and calls ParkHere() too,
    /// the ENTIRE fleet would converge into this same stuck state over
    /// time even if only a FEW cars ever hit the underlying fault first
    /// -- exactly "the cars are just parked," not immediately, but
    /// eventually. This cosmetic method has no business being able to
    /// break movement/state logic at all, regardless of what specifically
    /// might go wrong inside it (now or in some future change) -- so it's
    /// wrapped defensively: a caller can trust this NEVER throws, full
    /// stop. Logged once (not every frame) if it ever actually fires, so
    /// a real fault here becomes visible in the Console instead of a
    /// silent, catastrophic Update() abort.</summary>
    private bool _loggedLightsFault;
    private void UpdateLights(bool driving, bool braking)
    {
        try
        {
            var on = driving && DayNightState.NightAmount > NightEligibleThreshold;
            SetBulbsActive(_headlightBulbs, on);
            SetBulbsActive(_brakeLightBulbs, on && braking);
        }
        catch (System.Exception ex)
        {
            if (!_loggedLightsFault)
            {
                _loggedLightsFault = true;
                Debug.LogWarning("TrafficCar.UpdateLights faulted (suppressing further logs for this car): " + ex);
            }
        }
    }

    /// <summary>Pick the next network hex from `_to`. ALWAYS excludes
    /// `_from` first -- the bug this fixes: the old wander pick scored
    /// every neighbor by a hash of its own coordinates only, with nothing
    /// keyed to where the car had just come from, so from a hex whose
    /// hash-best neighbor happened to be the one it just arrived from, the
    /// car would immediately reverse -- and the SAME deterministic hash
    /// would then send it right back, forever (the reported ping-pong).
    /// Backtracking is allowed again only as a fallback when `_from` is
    /// the sole neighbor (a true dead end).
    ///
    /// `awayFrom`, when given (fleeing a threat), scores by distance from
    /// it -- farthest wins. Otherwise (normal driving) it's a pseudo-
    /// random wander hash ROTATED by `_hopCounter` every call, MINUS a
    /// steep penalty for candidates near any monster within
    /// MonsterAwareRadius (steering off a threatened block before a car
    /// would ever need to panic-flee).
    ///
    /// The rotation matters: excluding `_from` alone fixed the raw 2-hex
    /// bounce, but on a 3-plus-way junction a FIXED per-coordinate hash
    /// still permanently ranks one branch above the others -- so a car
    /// bouncing off a dead-end arm and back to the junction would always
    /// re-pick the SAME highest-ranked arm, never the weaker one, settling
    /// into a bounded loop across just two of the three arms forever (a
    /// smaller-radius version of the same reported ping-pong, caught by
    /// simulating this exact junction shape). Folding `_hopCounter` into
    /// the hash input makes the ranking permute hop to hop instead of
    /// staying fixed, so every reachable branch eventually wins its turn.</summary>
    private void PickNext(Vector3? awayFrom = null)
    {
        var candidates = new List<HexCoord>();
        // Normal driving never immediately reverses (excludes _from);
        // FLEEING deliberately allows it, so a car can U-turn straight
        // back the way it came to escape a monster ahead (creator
        // direction, 2026-07: "If it is a monster they should make a
        // u-turn and run away").
        foreach (var n in _to.Neighbors())
            if (_network.Contains(n) && (awayFrom.HasValue || !n.Equals(_from))) candidates.Add(n);
        if (candidates.Count == 0)
            foreach (var n in _to.Neighbors())
                if (_network.Contains(n)) candidates.Add(n); // dead end: doubling back is the only option
        if (candidates.Count == 0) return; // isolated hex -- shouldn't happen on a generated road network

        _hopCounter++;
        var best = candidates[0];
        var bestScore = float.NegativeInfinity;
        foreach (var n in candidates)
        {
            float score;
            if (awayFrom.HasValue)
            {
                score = (_builder.WorldOf(n) - awayFrom.Value).sqrMagnitude;
            }
            else
            {
                // primary: real progress toward the chosen destination
                // (creator direction, 2026-07: "they are driving
                // erratically all over the road. They should ... be
                // picking destinations on the map to go to") -- mirrors
                // Citizen's own greedy walk-toward-destination scoring.
                // Negated hex distance so "closer" scores higher, matching
                // this loop's own max-wins convention; weighted to dwarf
                // every other term below except a genuinely close threat.
                var destScore = _hasDestination ? -(float)n.DistanceTo(_destination) * DestinationWeight : 0f;

                var baseScore = (float)(((long)n.Q * 928371 + (long)n.R * 128371
                    + GetInstanceID() + (long)_hopCounter * 40503) & 0xFFFF);
                var threat = _builder.NearestMonsterTo(_builder.WorldOf(n), MonsterAwareRadius);
                var penalty = 0f;
                if (threat != null)
                {
                    var d = (_builder.WorldOf(n) - threat.transform.position).magnitude;
                    // safety still overrides the errand: at its max (a
                    // threat right on top of this hex) this beats
                    // DestinationWeight's own single-hex-step value.
                    penalty = (MonsterAwareRadius - d) * 4000f;
                }
                // camera-bias lived here until 2026-07's destination-
                // routing pass -- it now only shapes WHICH destination
                // PickDestination picks (a one-time choice per trip), not
                // every hop's route score; see CameraBiasWeight's own
                // doc comment for why.
                score = destScore + baseScore - penalty;
            }
            if (score > bestScore) { bestScore = score; best = n; }
        }
        _from = _to;
        _to = best;
        _target = RoadPoint(_to, _from);
    }

    /// <summary>The world point a car aims at to sit ON the drawn road,
    /// in its own lane: the target hex's CARDINAL road centerline (the
    /// same corrected anchor RoadDresser renders the strip at -- driving
    /// to the RAW hex center instead is exactly why cars zig-zagged down
    /// a straightened street), nudged to the RIGHT of travel by
    /// LaneOffset so opposing traffic stays apart and each car holds a
    /// lane.</summary>
    private Vector3 RoadPoint(HexCoord hex, HexCoord from)
    {
        var vertical = RoadDresser.CardinalNeighbors(hex, _network).Vertical;
        var anchor = RoadDresser.CardinalAnchor(_builder, hex, vertical);

        var fromVertical = RoadDresser.CardinalNeighbors(from, _network).Vertical;
        var fromAnchor = RoadDresser.CardinalAnchor(_builder, from, fromVertical);
        var dir = anchor - fromAnchor;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            dir = dir.normalized;
            var right = new Vector3(dir.z, 0f, -dir.x);
            anchor += right * LaneOffset;
        }
        return new Vector3(anchor.x, 0.75f, anchor.z);
    }

    /// <summary>The hop-count SAFETY CAP for a trip -- no longer the
    /// primary trip-completion trigger (arrival at _destination is, see
    /// Update()'s arrival check), just a backstop so a car whose chosen
    /// destination turns out unreachable (a network gap, or a dead-end
    /// maze the greedy walk can't escape) still parks eventually instead
    /// of wandering forever. Scaled off the destination's own hex
    /// distance with slack, falling back to the old random range when
    /// there's no destination to measure against.</summary>
    private int RandomHopBudget()
    {
        if (_hasDestination)
        {
            var hexDist = Mathf.Max(1, _to.DistanceTo(_destination));
            return Mathf.Max(MinTripHops, Mathf.CeilToInt(hexDist * SafetyHopMultiplier));
        }
        _tripSalt++;
        var h = Hash(_to, unchecked(GetInstanceID() * 131 + _tripSalt * 977));
        return MinTripHops + h % (MaxTripHops - MinTripHops + 1);
    }

    /// <summary>Jittered +-40% around the average park stay derived at
    /// Init from the target moving fraction -- otherwise every car that
    /// starts a trip at the same time would also park for an identical
    /// span and re-depart in lockstep.</summary>
    private float ParkDuration()
    {
        if (_parkDurationBase <= 0f) return 0.01f; // movingPercent ~1: essentially no parking
        var jitter = 0.6f + ((Hash(_to, GetInstanceID() + 29) & 0xFFFF) / 65535f) * 0.8f;
        return _parkDurationBase * jitter;
    }

    /// <summary>Trip complete: pull off to the curb (the same lane offset
    /// RoadDresser parks its own set-dressing cars at) facing the way it
    /// arrived, and sit for a rolled park duration.</summary>
    private void ParkHere()
    {
        var dir = _builder.WorldOf(_to) - _builder.WorldOf(_from);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        dir = dir.normalized;
        var side = new Vector3(dir.z, 0f, -dir.x);
        var sign = (Hash(_to, GetInstanceID() + 13) % 2 == 0) ? 1f : -1f;
        var spot = _builder.WorldOf(_to) + side * (sign * CurbOffset);
        spot.y = _builder.GroundHeightAt(spot) + 0.75f;

        transform.position = spot;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        _target = spot;
        _state = State.Parked;
        _parkTimer = ParkDuration();
    }

    /// <summary>Sets off on a fresh trip -- either a normal re-departure
    /// (park timer elapsed) or breaking off a parked stay because a
    /// monster just got close (`awayFrom` set: flee immediately instead
    /// of rolling a calm wander pick).</summary>
    private void BeginTrip(Vector3? awayFrom = null)
    {
        _state = State.Driving;
        _fleeing = awayFrom.HasValue;
        if (!_fleeing) PickDestination(); // a flee doesn't need an errand -- PickNext(awayFrom) overrides route choice entirely below
        _hopsRemaining = RandomHopBudget();
        PickNext(awayFrom);
    }

    /// <summary>Choose a real hex on the road network to head for -- see
    /// the DestinationWeight doc comment for why. Mirrors Citizen's own
    /// RandomSidewalkNear pattern, scanning the road network this car was
    /// initialized with instead of the sidewalk set.</summary>
    private void PickDestination()
    {
        _tripSalt++;
        _destination = RandomRoadHexNear(_to, MaxTripHops, unchecked(GetInstanceID() * 131 + _tripSalt * 977));
        _hasDestination = !_destination.Equals(_to);
    }

    /// <summary>A random road hex within `radius` of `near`, for a car to
    /// head toward -- gently prefers hexes closer to the player's camera
    /// (see CameraBiasWeight) so a car's whole errand trends toward the
    /// view instead of clear across the network, without touching the
    /// per-hop destination-seeking score in PickNext at all. Falls back
    /// to `near` itself if nothing else is in range (a tiny or
    /// disconnected network).</summary>
    private HexCoord RandomRoadHexNear(HexCoord near, int radius, int salt)
    {
        var best = near;
        var bestScore = float.NegativeInfinity;
        foreach (var r in _network)
        {
            if (r.Equals(near) || r.DistanceTo(near) > radius) continue;
            var hash = (float)(unchecked((r.Q * 73856093) ^ (r.R * 19349663) ^ (salt * 83492791)) & 0xFFFF);
            var distToCamera = (_builder.WorldOf(r) - _builder.CameraGroundFocus).magnitude;
            var score = hash - distToCamera * CameraBiasWeight;
            if (score > bestScore) { bestScore = score; best = r; }
        }
        return best;
    }

    private void Update()
    {
        // 2026-07: zero cost for a car outside the player's view -- no
        // threat scan, no roundabout/follow-distance math, no movement at
        // all. It simply holds its exact current position/state until the
        // camera comes back near it, at which point this resumes exactly
        // where it left off (a frozen mid-trip car is imperceptible to a
        // player who wasn't looking at it in the first place).
        if (!_nearCamera) return;

        var dt = Time.deltaTime;

        if (_state == State.Parked)
        {
            UpdateLights(driving: false, braking: false);
            // avoid monsters even at the curb -- a parked car peels out
            // the instant a threat closes in, same panic radius as driving
            var parkThreat = _builder.NearestMonsterTo(transform.position, FleeRadius);
            if (parkThreat != null) { BeginTrip(parkThreat.transform.position); return; }
            _parkTimer -= dt;
            if (_parkTimer <= 0f) BeginTrip();
            return;
        }

        var threat = _builder.NearestMonsterTo(transform.position, FleeRadius);
        var speed = CruiseSpeed * _personalSpeedMult;

        if (threat != null)
        {
            // fleeing overrides everything, including roundabout etiquette
            // (creator: "unless fleeing from monster") and any in-progress
            // passing maneuver -- a clean slate to resume normal driving
            // from once the threat clears, rather than picking back up
            // mid-swerve.
            _circling = false;
            _passing = false;
            _blockedTimer = 0f;
            speed = FleeSpeed;
            if (!_fleeing) PickNext(threat.transform.position); // threat just appeared: redirect now
            _fleeing = true;
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < ArriveRadius) PickNext(threat.transform.position);
            MoveToward(_target, speed, dt);
            return;
        }

        _fleeing = false;

        // roundabout: arc around the circulating lane instead of driving
        // across the island (creator direction: cars follow the curve).
        if (_builder.IsRoundabout(_to))
        {
            var steer = CirculateRoundabout(dt);
            if (steer.HasValue) { MoveToward(steer.Value, speed, dt); return; }
            // steer==null means we've come around to our exit -- fall
            // through to a normal hop onto the chosen exit hex
        }

        var toTarget = _target - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < ArriveRadius)
        {
            _hopsRemaining--;
            // real trip completion: arrived at the chosen destination --
            // not just "used up N hops" (that's now only the safety cap
            // for an unreachable/looping pick, see RandomHopBudget).
            var arrived = _hasDestination && _to.Equals(_destination);
            if (arrived || _hopsRemaining <= 0) { ParkHere(); return; }
            PickNext();
        }

        // realistic on-road avoidance (creator direction, 2026-07:
        // "avoiding monsters on the road by swerving around monsters in
        // a realistic way"): the reroute above only changes which hex
        // gets picked NEXT (full panic at FleeRadius, aware-penalty at
        // MonsterAwareRadius) -- it doesn't touch the literal path
        // toward the CURRENT target. This nudges just this frame's
        // steering point sideways around a monster the car is about to
        // drive past. Purely cosmetic steering.
        var steerTarget = _target;
        var travelDir = _target - transform.position;
        travelDir.y = 0f;
        if (travelDir.sqrMagnitude > 0.01f)
        {
            var fwd = travelDir.normalized;
            var right = new Vector3(fwd.z, 0f, -fwd.x);

            if (_passing)
            {
                // committed to an overtake: swerve fully onto the
                // opposite side of the road and push the throttle, but
                // keep re-checking that side is still actually clear --
                // aggressive, not reckless. Ends when the maneuver times
                // out, the passing side stops being clear (abort, merge
                // back and resume normal following), or the ORIGINAL
                // lane has opened back up ahead (the whole point of a
                // successful pass: the slow car is no longer in the way).
                _passTimer -= dt;
                var passSideClear = _builder.DistanceAhead(transform.position - right * PassLaneOffset, fwd, PassLaneCheckRange, LaneHalfWidth, this);
                // 2026-07 fix (creator report: a car would drive for a bit,
                // lights on, then stop for good, and no other car on the
                // map was ever seen moving either): this used to query from
                // `transform.position` directly -- but WHILE passing, that
                // position is already offset sideways by (up to)
                // PassLaneOffset toward the opposite lane, so the blocker
                // sitting near the ORIGINAL lane centerline reads as
                // "outside LaneHalfWidth, therefore clear" within the very
                // first frame or two of the swerve, long before the car has
                // actually driven far enough ALONG the road to be past it.
                // That false-positive "clear" immediately aborts the pass
                // (see the branch below), which snaps the car back onto the
                // original lane line heading straight back at the still-
                // stationary blocker -- re-triggering `_blockedTimer` from
                // zero and re-attempting (and re-aborting) a pass every
                // ~1s, forever, with no net progress. A parked car sits
                // curbed only CurbOffset (2.5m) from the road centerline --
                // just outside LaneOffset (2.0m) -- so it is exactly the
                // kind of permanent, never-moving blocker this breaks on,
                // and every car whose route crosses one gets stuck behind
                // it the same way (the whole fleet reads as "stopped",
                // not just the one the creator happened to be watching).
                //
                // 2026-07 follow-up fix (found via the destination-routing
                // flightcheck pass: a route toward a different first hex
                // reproduced the SAME "stuck passing forever" symptom this
                // comment describes, just from a different direction --
                // this original fix wasn't wrong in intent, but assumed the
                // car had ALREADY swerved the full PassLaneOffset by the
                // time this check runs. On the very first frame(s) after
                // committing to a pass, the car is still essentially ON the
                // original lane -- adding a FULL assumed PassLaneOffset to
                // "undo the swerve" instead OVERSHOOTS past the centerline
                // to the far side, which reproduces the exact same false-
                // clear/immediate-abort bug the comment above already
                // diagnosed once, just via a different trigger. Projecting
                // back using the car's own ACTUAL lateral displacement
                // since it committed to the pass (`_passStartPos`), rather
                // than an assumed constant, fixes the reference point for
                // real: on frame one that displacement is genuinely ~0 (no
                // overshoot), and it grows to the real swerve amount as the
                // car actually moves -- verified by the driving-verify
                // flightcheck's passing test, which reproduced this exact
                // stuck-forever failure before this fix.
                var lateralOffset = Vector3.Dot(transform.position - _passStartPos, right);
                var ownLaneQuery = transform.position - right * lateralOffset;
                var ownLaneClear = _builder.DistanceAhead(ownLaneQuery, fwd, FollowRange, LaneHalfWidth, this);
                if (_passTimer <= 0f || passSideClear < FollowGap || ownLaneClear >= FollowRange)
                {
                    _passing = false;
                    _blockedTimer = 0f;
                }
                else
                {
                    // aim at a LOOKAHEAD point just ahead of the car's
                    // CURRENT position, not the far-off next-hex target
                    // -- offsetting the distant hex target sideways by a
                    // fixed PassLaneOffset produces only a shallow-angle
                    // drift over the whole remaining hex distance (a real
                    // v0.1 attempt at this measured just ~1-2m of actual
                    // lateral clearance before the maneuver concluded,
                    // nowhere near a real "crossed to the opposite side"
                    // look -- caught by the verification harness, not
                    // just assumed). A close lookahead point gives a much
                    // steeper commit angle, so the car visibly swings
                    // wide fast, then re-aims at the real target as it
                    // straightens out.
                    steerTarget = transform.position + fwd * PassLookahead - right * PassLaneOffset + SwerveOffset(fwd);
                    speed *= PassSpeedBoost;
                    MoveToward(steerTarget, speed, dt);
                    return;
                }
            }

            steerTarget += SwerveOffset(fwd);

            // follow the traffic ahead: slow (down to a full stop) when a
            // car, tank, or citizen sits in my lane just in front, so cars
            // keep a proper gap instead of piling through each other
            // (creator direction, 2026-07: "they need to slow down if
            // there is a human, car, tank something in front of them...
            // proper space between them").
            var clear = _builder.DistanceAhead(transform.position, fwd, FollowRange, LaneHalfWidth, this);
            if (clear < FollowRange)
            {
                _blockedTimer += dt;
                speed *= Mathf.Clamp01((clear - FollowGap) / (FollowRange - FollowGap));

                // naturalistic passing (creator direction, 2026-07: "some
                // aggressive passing to slow cars"): sustained close
                // following, of something genuinely slow (not just a
                // momentary gap), commits to an overtake IF the opposite
                // side of the road is clear far enough ahead to actually
                // complete one.
                if (!_passing && _blockedTimer > PassBlockedTriggerTime && clear < FollowRange * PassTriggerClearFraction)
                {
                    var passSideClear = _builder.DistanceAhead(transform.position - right * PassLaneOffset, fwd, PassLaneCheckRange, LaneHalfWidth, this);
                    if (passSideClear >= PassLaneCheckRange)
                    {
                        _passing = true;
                        _passTimer = PassMaxDuration;
                        _passStartPos = transform.position; // the reference point ownLaneClear measures actual swerve progress from
                    }
                }
            }
            else
            {
                _blockedTimer = 0f;
            }
        }

        MoveToward(steerTarget, speed, dt);
    }

    /// <summary>Steer the car around a roundabout's circulating lane
    /// (counter-clockwise, right-hand European traffic) rather than
    /// across the central island. Returns the steering point to aim at,
    /// or null once the car has circulated far enough AND lined up with
    /// its chosen exit -- at which point it hops onto that exit hex like
    /// a normal move. Chooses the exit the first frame it enters, so it
    /// knows which spoke to leave by.</summary>
    private Vector3? CirculateRoundabout(float dt)
    {
        var center = _builder.WorldOf(_to);
        var radial = transform.position - center;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.01f) radial = new Vector3(0f, 0f, 1f);
        var ang = Mathf.Atan2(radial.x, radial.z);   // world angle from +Z

        if (!_circling)
        {
            _circling = true;
            _sweptDeg = 0f;
            _prevAngle = ang;
            _roundExit = PickExit(_to);
            var exitRadial = _builder.WorldOf(_roundExit) - center;
            exitRadial.y = 0f;
            _exitAngle = Mathf.Atan2(exitRadial.x, exitRadial.z);
        }

        // accumulate how far we've swept (unsigned, wrap-safe)
        _sweptDeg += Mathf.Abs(Mathf.DeltaAngle(_prevAngle * Mathf.Rad2Deg, ang * Mathf.Rad2Deg));
        _prevAngle = ang;

        // exit once we've come around a bit AND are near the exit spoke
        var atExit = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, _exitAngle * Mathf.Rad2Deg)) < 22f;
        if (_sweptDeg > 40f && atExit)
        {
            _circling = false;
            _from = _to;
            _to = _roundExit;
            _target = RoadPoint(_to, _from);
            _hopsRemaining--;
            return null;
        }

        // aim a little further counter-clockwise along the ring
        const float lookahead = 0.55f;   // radians ahead around the circle
        var na = ang + lookahead;        // +ang = counter-clockwise circulation
        var ringPt = center + new Vector3(Mathf.Sin(na), 0f, Mathf.Cos(na)) * RuntimeCityBuilder.RoundaboutLaneRadius;
        return ringPt + Vector3.up * 0.75f;
    }

    /// <summary>Pick a spoke to leave a roundabout by -- a road neighbor
    /// of the hub other than the one we entered from (falling back to
    /// any neighbor at a dead-end hub). Same destination-first scoring as
    /// PickNext (with the rotating wander hash as a tie-breaker), so a
    /// car doesn't lose its errand's sense of direction just because it
    /// passed through a traffic circle.</summary>
    private HexCoord PickExit(HexCoord hub)
    {
        var candidates = new List<HexCoord>();
        foreach (var n in hub.Neighbors())
            if (_network.Contains(n) && !n.Equals(_from)) candidates.Add(n);
        if (candidates.Count == 0)
            foreach (var n in hub.Neighbors())
                if (_network.Contains(n)) candidates.Add(n);
        if (candidates.Count == 0) return hub;

        _hopCounter++;
        var best = candidates[0];
        var bestScore = float.NegativeInfinity;
        foreach (var n in candidates)
        {
            var destScore = _hasDestination ? -(float)n.DistanceTo(_destination) * DestinationWeight : 0f;
            var hashScore = (float)(((long)n.Q * 928371 + (long)n.R * 128371 + GetInstanceID() + (long)_hopCounter * 40503) & 0xFFFF);
            var score = destScore + hashScore;
            if (score > bestScore) { bestScore = score; best = n; }
        }
        return best;
    }

    /// <summary>Lateral offset that curves the car's immediate steering
    /// around a monster ahead of it within SwerveRadius, strongest when
    /// close and directly in the way, fading to zero once past or well
    /// off to the side -- see the Update() call site.</summary>
    private Vector3 SwerveOffset(Vector3 travelDir)
    {
        var m = _builder.NearestMonsterTo(transform.position, SwerveRadius);
        if (m == null) return Vector3.zero;
        var toMonster = m.transform.position - transform.position;
        toMonster.y = 0f;
        var dist = toMonster.magnitude;
        if (dist < 0.05f) return Vector3.zero;
        var ahead = Vector3.Dot(toMonster.normalized, travelDir);
        if (ahead < 0.15f) return Vector3.zero; // behind or well off to the side: nothing to swerve around

        var side = new Vector3(travelDir.z, 0f, -travelDir.x);
        var sign = Vector3.Dot(toMonster, side) > 0f ? -1f : 1f; // steer to whichever side it ISN'T on
        var strength = Mathf.Clamp01((SwerveRadius - dist) / SwerveRadius) * ahead;
        return side * (sign * strength * SwerveMax);
    }

    private void MoveToward(Vector3 target, float speed, float dt)
    {
        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < 0.05f) { UpdateLights(driving: true, braking: false); return; }
        var dir = to / dist;
        transform.position += dir * Mathf.Min(speed * dt, dist);
        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p) + 0.75f, p.z);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), dt * 4f);

        // this is the ONE point every driving Update() path funnels
        // through (fleeing, roundabout circulation, normal cruise), so
        // it's also the one place that needs to notice a frame-to-frame
        // drop in the `speed` the caller actually asked for -- see
        // BrakeDecelEpsilon's own comment for what this signal does and
        // doesn't cover.
        var braking = speed < _lastSpeed - BrakeDecelEpsilon;
        _lastSpeed = speed;
        UpdateLights(driving: true, braking: braking);
    }
}
