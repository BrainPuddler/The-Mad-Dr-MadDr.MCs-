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
    private const float MonsterAwareRadius = 28f; // a monster this close: steer a normal route away from it
    private const float ArriveRadius = 1.5f;
    private const float CurbOffset = 2.5f;     // same curb-lane distance RoadDresser parks its own cars at

    private const int MinTripHops = 5;
    private const int MaxTripHops = 14;

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

    public bool IsDriving { get { return _state == State.Driving; } }

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
        transform.position = _builder.WorldOf(start) + Vector3.up * 0.75f;
        _target = transform.position;
        BuildBody(body, Hash(start, 7) % 4 == 0);

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
        foreach (var n in _to.Neighbors())
            if (_network.Contains(n) && !n.Equals(_from)) candidates.Add(n);
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
        _target = _builder.WorldOf(_to) + Vector3.up * 0.75f;
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
            speed = FleeSpeed;
            if (!_fleeing) PickNext(threat.transform.position); // threat just appeared: redirect now
            _fleeing = true;
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < ArriveRadius) PickNext(threat.transform.position);
        }
        else
        {
            _fleeing = false;
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < ArriveRadius)
            {
                _hopsRemaining--;
                if (_hopsRemaining <= 0) { ParkHere(); return; }
                PickNext();
            }
        }

        MoveToward(_target, speed, dt);
    }

    private void MoveToward(Vector3 target, float speed, float dt)
    {
        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < 0.05f) return;
        var dir = to / dist;
        transform.position += dir * Mathf.Min(speed * dt, dist);
        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p) + 0.75f, p.z);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), dt * 4f);
    }
}
