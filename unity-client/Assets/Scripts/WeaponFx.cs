using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// Turns a fired WeaponProfile into what you SEE and what it DOES:
///   Beam  (laser_array) -> instant cyan LineRenderer, damage now
///   Bolt  (photon/plasma phaser) -> a slow glowing Projectile
///   Bullet (rifle / tank cannon) -> a fast small Projectile
///   Spore -> a lobbed biotech Projectile
///   Flame (tank flamethrower) -> a short cone of fire, damage now
///   Melee (claws/blades) -> a quick reach streak, damage now
///   Arc   (electric_arc) -> instant jagged blue LineRenderer, damage now
/// Instant kinds apply damage immediately; projectile kinds apply it on
/// arrival (see Projectile). All FX are throwaway GameObjects that
/// Destroy themselves on a timer -- no pooling yet (a perf pass, docs/08).
/// </summary>
public static class WeaponFx
{
    public static void Fire(UnitCombat source, UnitCombat target, Vector3 muzzle)
    {
        var w = source.Weapon;
        var hit = target.AimPoint;
        switch (w.Kind)
        {
            case WeaponKind.Beam:
                Beam(muzzle, hit, Tint(w), 0.10f, 0.09f);
                target.TakeDamage((float)w.Damage, source);
                break;
            case WeaponKind.Melee:
                Beam(muzzle, hit, Tint(w), 0.07f, 0.14f);   // a fast slash streak
                target.TakeDamage((float)w.Damage, source);
                break;
            case WeaponKind.Arc:
                Arc(muzzle, hit, Tint(w), 0.09f, 0.12f);
                target.TakeDamage((float)w.Damage, source);
                break;
            case WeaponKind.Flame:
                Flame(muzzle, hit, Tint(w), (float)w.Range);
                target.TakeDamage((float)w.Damage, source);
                break;
            case WeaponKind.Bolt:
            case WeaponKind.Bullet:
            case WeaponKind.Spore:
                Shot(source, target, muzzle, w);
                break;
        }
    }

    public static void FireAtPoint(UnitCombat source, Vector3 point, Vector3 muzzle)
    {
        var w = source.Weapon;
        switch (w.Kind)
        {
            case WeaponKind.Beam:
            case WeaponKind.Melee:
                Beam(muzzle, point, Tint(w), 0.10f, 0.09f);
                break;
            case WeaponKind.Arc:
                Arc(muzzle, point, Tint(w), 0.09f, 0.12f);   // cosmetic: caller applies structural damage
                break;
            case WeaponKind.Flame:
                Flame(muzzle, point, Tint(w), (float)w.Range);
                break;
            default:
                ShotAtPoint(muzzle, point, w);   // cosmetic: caller applies structural damage
                break;
        }
    }

    // ---- primitives ----------------------------------------------------------

    private static Color Tint(WeaponProfile w) { return new Color(w.R / 255f, w.G / 255f, w.B / 255f); }

    private static Material Glow(Color c, float emis)
    {
        var m = new Material(ShaderUtil.FindRenderableShader());
        m.color = c;
        if (emis > 0f && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * emis);
        }
        return m;
    }

    private static void Beam(Vector3 a, Vector3 b, Color c, float width, float life)
    {
        var go = new GameObject("Beam");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sharedMaterial = Glow(c, 1.8f);
        lr.startColor = c;
        lr.endColor = c;
        Object.Destroy(go, life);
    }

    /// <summary>2026-08 (creator direction: "a direct Electric arc
    /// attack"): the electric_arc weapon's own visual -- mechanically
    /// identical to <see cref="Beam"/> (instant hit, same lifetime), but
    /// a jagged multi-segment line instead of Beam's perfectly straight
    /// 2-point one, so a laser_array beam and an electric_arc zap never
    /// read as the same weapon with a different tint. Perlin noise (not
    /// UnityEngine.Random) drives the jitter, same idiom TeslaArc.cs/
    /// AreaAttackEffect already use -- seeded off the shot's own start/
    /// end points plus Time.time so back-to-back shots from the same
    /// muzzle still crackle differently. Precomputed ONCE (not an
    /// Update()-driven jitter like TeslaArc's persistent building prop)
    /// since this GameObject is destroyed in a fraction of a second,
    /// same "throwaway GameObject on a timer" shape every other WeaponFx
    /// primitive already uses.</summary>
    private static void Arc(Vector3 a, Vector3 b, Color c, float width, float life)
    {
        const int segments = 5;
        var go = new GameObject("Arc");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = segments + 1;
        var len = (b - a).magnitude;
        var jitterMag = Mathf.Min(len * 0.12f, 0.6f);
        var seed = (a.x + b.z) * 13.7f + Time.time * 31f;
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < segments)
            {
                var s = seed + i * 4.1f;
                p += new Vector3(
                    Mathf.PerlinNoise(s, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, s) - 0.5f,
                    Mathf.PerlinNoise(s, s) - 0.5f) * jitterMag;
            }
            lr.SetPosition(i, p);
        }
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sharedMaterial = Glow(c, 2.2f);
        lr.startColor = c;
        lr.endColor = c;
        Object.Destroy(go, life);
    }

    private static void Flame(Vector3 muzzle, Vector3 target, Color c, float range)
    {
        var dir = target - muzzle;
        var len = dir.magnitude;
        var d = len > 0.01f ? dir / len : Vector3.forward;
        var reach = Mathf.Min(len, range);
        const int n = 5;
        for (var i = 0; i < n; i++)
        {
            var t = (i + 1) / (float)n;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            StripCollider(go);
            go.transform.position = muzzle + d * (reach * t);
            var s = 0.5f + t * 1.9f;                       // widening cone
            go.transform.localScale = new Vector3(s, s, s);
            var flame = Color.Lerp(c, new Color(1f, 0.85f, 0.4f), t * 0.5f);
            SetColor(go, Glow(flame, 1.9f));
            Object.Destroy(go, 0.13f + t * 0.06f);
        }
    }

    private static void Shot(UnitCombat source, UnitCombat target, Vector3 muzzle, WeaponProfile w)
    {
        var go = MakeShot(muzzle, w);
        var p = go.AddComponent<Projectile>();
        p.Init(source, target, target.AimPoint, (float)w.Damage, (float)w.ProjectileSpeed);
    }

    private static void ShotAtPoint(Vector3 muzzle, Vector3 point, WeaponProfile w)
    {
        var go = MakeShot(muzzle, w);
        var p = go.AddComponent<Projectile>();
        p.Init(null, null, point, 0f, (float)w.ProjectileSpeed);
    }

    private static GameObject MakeShot(Vector3 muzzle, WeaponProfile w)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Shot";
        StripCollider(go);
        go.transform.position = muzzle;
        var s = w.Kind == WeaponKind.Bullet ? 0.3f : 0.55f;
        go.transform.localScale = new Vector3(s, s, s);
        SetColor(go, Glow(Tint(w), 1.7f));
        Object.Destroy(go, 5f);   // hard backstop; Projectile destroys sooner on hit
        return go;
    }

    private static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
    }

    private static void SetColor(GameObject go, Material m)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = m;
    }
}
