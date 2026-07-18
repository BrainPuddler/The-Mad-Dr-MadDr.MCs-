using MadDr.CityGen;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A 1950s human-faction tank -- the test dummy that fights the monsters.
/// Drives toward the nearest monster and opens up once it's in range;
/// half of them carry a flamethrower (because it's cool), the rest a
/// 75mm cannon. Has a UnitCombat like everything else, so monsters can
/// target and kill it and it shows a health bar.
///
/// Treaded-vehicle behavior (creator direction, 2026-07): the TURRET
/// tracks the target independently and continuously, in world space, no
/// matter what the hull is doing (moving, turning, standing still and
/// firing) -- a real tank's traverse. The HULL only turns toward its own
/// travel direction, at a limited rate, and only ever drives along its
/// OWN current forward vector -- it can't strafe sideways into a steer
/// correction the way the old single-`look`-for-both code did. Steering
/// (<see cref="SteerTank"/>) is a lightweight look-ahead deflection (no
/// A* -- these are combat targets, not navigators): it excludes
/// buildings and water too deep to ford, and prefers hexes on the road
/// network, so a tank sticks to streets when one's headed roughly the
/// right way instead of always beelining cross-country.
/// </summary>
public class Tank : MonoBehaviour
{
    // 1950s tank proportions, scaled up from the original numbers so a
    // tank reads clearly larger than either car body (creator direction:
    // "make them larger than the cars" -- the sedan is 2.2x0.8x5.2, the
    // delivery truck 2.4x1.7x4.4).
    private const float Scale = 1.55f;

    // The height the water-crossing rule keys off (creator direction:
    // "only cross water < their 0.3 of their height") -- an overall
    // silhouette height, not any one mesh dimension, since the fordable
    // fraction covers the whole vehicle, not just the hull box.
    private const float TankHeight = 3.0f;
    private const float FordableDepthFraction = 0.3f;

    private RuntimeCityBuilder _builder;
    private UnitCombat _combat;
    private Transform _turret;
    private Transform _muzzle;
    private float _speed = 9f;
    private float _aggro = 150f;
    private const float HullTurnRate = 2.0f;
    private const float TurretTurnRate = 3.5f;

    public UnitCombat Combat { get { return _combat; } }

    public void Init(RuntimeCityBuilder builder, bool flame)
    {
        _builder = builder;
        BuildModel(flame);
        _combat = gameObject.AddComponent<UnitCombat>();
        var weapon = flame ? WeaponProfile.TankFlamethrower() : WeaponProfile.TankCannon();
        _combat.Configure("human", flame ? 150f : 210f, 2.6f * Scale, 1.4f * Scale, weapon, OnDied);
    }

    private void Update()
    {
        if (_combat == null || !_combat.Alive || _builder == null) return;
        var dt = Time.deltaTime;

        var target = _builder.NearestEnemyOf(_combat, _aggro);
        if (target != null)
        {
            var to = target.AimPoint - transform.position;
            to.y = 0f;
            var dist = to.magnitude;
            var aimDir = dist > 0.01f ? to / dist : transform.forward;

            // turret traverse: always tracking, decoupled from the hull
            var aimLook = Quaternion.LookRotation(aimDir, Vector3.up);
            if (_turret != null) _turret.rotation = Quaternion.Slerp(_turret.rotation, aimLook, dt * TurretTurnRate);

            var range = (float)_combat.Weapon.Range;
            if (dist > range * 0.85f)
            {
                var steer = SteerTank(aimDir);
                if (steer.sqrMagnitude > 0.0001f)
                {
                    // hull slews toward the travel direction, then drives
                    // along wherever it's ACTUALLY currently facing --
                    // never a straight strafe toward `steer`. Alignment
                    // gates forward speed too: a sharp turn slows the
                    // hull to a near-pivot instead of gliding sideways.
                    var hullLook = Quaternion.LookRotation(steer, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, hullLook, dt * HullTurnRate);
                    var align = Mathf.Clamp01(Vector3.Dot(transform.forward, steer));
                    transform.position += transform.forward * (_speed * align * dt);
                }
                // boxed in on every probed direction: hold position (hull
                // keeps its current heading) and keep range-checking next
                // frame rather than ramming the one obstacle in the way
            }
            else if (_combat.TryFire(target, _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.4f))
            {
                DamageFx.MuzzleSmoke(_muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.4f);
            }
        }

        _builder.ApplySeparation(_combat);

        // terrain-follow the sculpted ground (docs/21): treads on the
        // hill (or the shallow ford-bed), not floating over the valley
        var p = transform.position;
        var gy = _builder.GroundHeightAt(p);
        if (!Mathf.Approximately(p.y, gy)) transform.position = new Vector3(p.x, gy, p.z);
    }

    // deflection angles tried in order: straight first (the common case,
    // nothing in the way), then widening zig-zags each side -- a cheap
    // stand-in for real pathfinding
    private static readonly float[] DeflectAngles =
        { 0f, 20f, -20f, 40f, -40f, 65f, -65f, 90f, -90f, 120f, -120f };
    private const float ProbeDistance = 6f;
    private const float RoadPreferencePenalty = 35f;   // degrees-equivalent cost added to a non-road candidate

    /// <summary>Scores every deflection candidate by how far it turns
    /// from the desired direction, PLUS a penalty if its hex isn't on
    /// the road network -- so a tank takes a mild detour onto a nearby
    /// street rather than always beelining straight cross-country, but
    /// won't detour wildly just to touch a road. Buildings and water
    /// deeper than <see cref="FordableDepthFraction"/> of the tank's
    /// height are hard exclusions, not penalties.</summary>
    private Vector3 SteerTank(Vector3 desiredDir)
    {
        if (_builder == null || _builder.City == null) return desiredDir;
        var roads = _builder.RoadNetworkHexes();
        var best = Vector3.zero;
        var bestCost = float.PositiveInfinity;
        foreach (var angle in DeflectAngles)
        {
            var candidate = Quaternion.Euler(0f, angle, 0f) * desiredDir;
            var probe = transform.position + candidate * ProbeDistance;
            var hex = _builder.HexAt(probe);
            if (BlockedForTank(hex)) continue;
            var cost = Mathf.Abs(angle) + (roads.Contains(hex) ? 0f : RoadPreferencePenalty);
            if (cost < bestCost) { bestCost = cost; best = candidate.normalized; }
        }
        return best;   // Vector3.zero if hemmed in on every probed direction
    }

    /// <summary>Buildings always block. Water only blocks if it's deeper
    /// than the fordable fraction of the tank's height -- shallow banks
    /// and puddly ponds are crossable, the main channel isn't (creator
    /// direction: "only cross water < their 0.3 of their height").</summary>
    private bool BlockedForTank(HexCoord hex)
    {
        if (!_builder.City.Contains(hex)) return true;
        if (_builder.BlockedFor(true).Contains(hex)) return true;   // buildings + downed bridges, never water
        return _builder.WaterDepthAt(hex) > TankHeight * FordableDepthFraction;
    }

    private void OnDied()
    {
        _builder.OnCombatantDied(_combat);
        SpawnWreck();
        Object.Destroy(gameObject);
    }

    /// <summary>Docs/20 corpse salvage (creator direction, 2026-07: "can
    /// be destroyed and then breakdown into salvageable parts"): replaces
    /// the intact model with a permanent broken-apart wreck at the same
    /// spot -- a scorched, slumped hull, the turret knocked off at an
    /// angle beside it, and a couple of loose plate/track chunks nearby
    /// -- reading as salvageable debris instead of vanishing.
    /// Colliderless, same convention as RubbleDresser's building rubble
    /// (already open to pathing, purely cosmetic). Parented under the
    /// same "Tanks" host the live tank was, so the scene hierarchy stays
    /// tidy. NOTE: this is the visual breakdown only -- wiring an actual
    /// harvest/resource pickup for these wrecks (docs/20's faction
    /// corpse-salvage materials) is a separate economy feature, not
    /// attempted here; logged as a follow-up, not hidden (docs/12).</summary>
    private void SpawnWreck()
    {
        var scorched = new Color(0.12f, 0.11f, 0.10f);
        var metal = new Color(0.20f, 0.22f, 0.22f);

        var host = new GameObject("TankWreck").transform;
        host.SetParent(transform.parent, false);
        host.position = transform.position;
        host.rotation = transform.rotation;

        var hull = Prim(PrimitiveType.Cube, host, new Vector3(0f, 0.9f * Scale, 0f),
            new Vector3(2.6f, 1.0f, 4.0f) * Scale, scorched);
        hull.localRotation = Quaternion.Euler(3f, 8f, -4f);   // slumped, not sitting upright

        // turret knocked off and askew beside the hull -- the single
        // most recognizable "this thing is dead" read
        var id = GetInstanceID();
        var turretOff = new Vector3((1.0f + (id & 3) * 0.2f) * Scale, 0.4f * Scale, (-1.0f + ((id >> 2) & 3) * 0.3f) * Scale);
        var turret = Prim(PrimitiveType.Sphere, host, turretOff, new Vector3(2.0f, 1.1f, 2.2f) * Scale, scorched);
        turret.localRotation = Quaternion.Euler((id % 17) - 8f, (id * 53) % 360, (id % 13) - 6f);

        // a few loose plate/track chunks scattered around the hull
        for (var i = 0; i < 3; i++)
        {
            var hi = id + i * 977;
            var off = new Vector3((((hi % 7) - 3f) * 0.8f) * Scale, 0.25f * Scale, (((hi / 7 % 7) - 3f) * 0.8f) * Scale);
            var size = (0.8f + (hi % 3) * 0.3f) * Scale;
            var chunk = Prim(PrimitiveType.Cube, host, off, new Vector3(size, 0.5f * Scale, size * 1.8f), metal);
            chunk.localRotation = Quaternion.Euler((hi % 23) - 11f, (hi * 37) % 360, (hi % 19) - 9f);
        }

        var groundY = _builder.GroundHeightAt(host.position);
        host.position = new Vector3(host.position.x, groundY, host.position.z);

        DamageFx.DustBurst(host.position + Vector3.up * 1.2f, host);
    }

    // ---- model ---------------------------------------------------------------

    private void BuildModel(bool flame)
    {
        var hullCol = flame ? new Color(0.42f, 0.28f, 0.20f) : new Color(0.30f, 0.36f, 0.24f);
        var metal = new Color(0.20f, 0.22f, 0.22f);

        // hull KEEPS its collider so left/right-click raycasts can pick the
        // tank (GetComponentInParent<Tank> climbs from it to this root).
        // Every position/size below is the original 1950s-tank proportion
        // times Scale, so the whole assembly grows uniformly (creator
        // direction: "make them larger than the cars").
        Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.9f, 0f) * Scale, new Vector3(2.6f, 1.0f, 4.0f) * Scale, hullCol, true);
        Prim(PrimitiveType.Cube, transform, new Vector3(-1.5f, 0.5f, 0f) * Scale, new Vector3(0.6f, 0.7f, 4.3f) * Scale, metal);
        Prim(PrimitiveType.Cube, transform, new Vector3(1.5f, 0.5f, 0f) * Scale, new Vector3(0.6f, 0.7f, 4.3f) * Scale, metal);

        _turret = new GameObject("Turret").transform;
        _turret.SetParent(transform, false);
        _turret.localPosition = new Vector3(0f, 1.5f, -0.2f) * Scale;
        Prim(PrimitiveType.Sphere, _turret, Vector3.zero, new Vector3(2.0f, 1.1f, 2.2f) * Scale, hullCol);

        if (flame)
        {
            // stubby wide nozzle + tanks on the back
            var noz = Prim(PrimitiveType.Cylinder, _turret, new Vector3(0f, 0.1f, 1.7f) * Scale, new Vector3(0.4f, 0.7f, 0.4f) * Scale, metal);
            noz.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 1.5f, -1.7f) * Scale, new Vector3(0.5f, 0.6f, 0.5f) * Scale, new Color(0.5f, 0.35f, 0.1f));
        }
        else
        {
            var barrel = Prim(PrimitiveType.Cylinder, _turret, new Vector3(0f, 0.1f, 1.6f) * Scale, new Vector3(0.24f, 1.5f, 0.24f) * Scale, metal);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        _muzzle = new GameObject("Muzzle").transform;
        _muzzle.SetParent(_turret, false);
        _muzzle.localPosition = new Vector3(0f, 0.1f, flame ? 2.4f : 3.1f) * Scale;
    }

    private static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale,
        Color color, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (!keepCollider)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var m = new Material(ShaderUtil.FindRenderableShader());
            m.color = color;
            r.sharedMaterial = m;
        }
        return go.transform;
    }
}
