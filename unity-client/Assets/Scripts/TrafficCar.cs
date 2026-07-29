using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Docs/19 traffic (docs/21 batch 2, item 9; extended per creator report --
/// see the fix note on <see cref="PickNext"/>): a car that drives the road
/// network in bounded TRIPS, then pulls to the curb and parks for a while
/// before setting off again -- not an endless wander. Panics like a Citizen
/// when a monster gets close (peeling toward whichever reachable hex is
/// farthest from the threat, breaking off a trip OR a parked stay to do
/// it), and steers away from monsters even off-panic while picking a
/// normal route. Not a combatant: no collider, doesn't fight, doesn't
/// block movement -- purely cosmetic crowd dressing for the streets
/// RoadDresser paints, same scoping Citizen.cs already established for
/// pedestrians.
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
    private const int MaxTripHops = 14;

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

    public bool IsDriving { get { return _state == State.Driving; } }

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
        BuildBody(body, Hash(start, 7) % 4 == 0);
        BuildLights();
        _lastSpeed = CruiseSpeed;   // avoids a false "braking" flash on the very first driving frame

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
    /// oversight).</summary>
    private void UpdateLights(bool driving, bool braking)
    {
        var on = driving && DayNightState.NightAmount > NightEligibleThreshold;
        SetBulbsActive(_headlightBulbs, on);
        SetBulbsActive(_brakeLightBulbs, on && braking);
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
                var baseScore = (float)(((long)n.Q * 928371 + (long)n.R * 128371
                    + GetInstanceID() + (long)_hopCounter * 40503) & 0xFFFF);
                var threat = _builder.NearestMonsterTo(_builder.WorldOf(n), MonsterAwareRadius);
                var penalty = 0f;
                if (threat != null)
                {
                    var d = (_builder.WorldOf(n) - threat.transform.position).magnitude;
                    penalty = (MonsterAwareRadius - d) * 4000f; // dwarfs the 0..65535 wander hash near a monster
                }
                score = baseScore - penalty;
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

    private int RandomHopBudget()
    {
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
        _hopsRemaining = RandomHopBudget();
        _fleeing = awayFrom.HasValue;
        PickNext(awayFrom);
    }

    private void Update()
    {
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
        var speed = CruiseSpeed;

        if (threat != null)
        {
            // fleeing overrides everything, including roundabout etiquette
            // (creator: "unless fleeing from monster") -- drive straight out
            _circling = false;
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
            if (_hopsRemaining <= 0) { ParkHere(); return; }
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
            steerTarget += SwerveOffset(fwd);

            // follow the traffic ahead: slow (down to a full stop) when a
            // car, tank, or citizen sits in my lane just in front, so cars
            // keep a proper gap instead of piling through each other
            // (creator direction, 2026-07: "they need to slow down if
            // there is a human, car, tank something in front of them...
            // proper space between them").
            var clear = _builder.DistanceAhead(transform.position, fwd, FollowRange, LaneHalfWidth, this);
            if (clear < FollowRange)
                speed *= Mathf.Clamp01((clear - FollowGap) / (FollowRange - FollowGap));
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
    /// any neighbor at a dead-end hub), by the same rotating wander hash
    /// PickNext uses so exits vary trip to trip.</summary>
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
        var bestScore = long.MinValue;
        foreach (var n in candidates)
        {
            var score = ((long)n.Q * 928371 + (long)n.R * 128371 + GetInstanceID() + (long)_hopCounter * 40503) & 0xFFFF;
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
