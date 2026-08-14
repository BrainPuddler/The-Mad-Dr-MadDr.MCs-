using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "Human Army is from army barracks --
/// part of the basic kit for Human army", confirmed to also close the
/// real gap this uncovered: "Build the real RosterUnitKind pipeline
/// too"). The first real bridge from match-core's own unit simulation
/// to a rendered GameObject, for the two INFANTRY roster kinds Barracks
/// trains (<see cref="RosterUnitKind.Rifleman"/>, <see
/// cref="RosterUnitKind.FlamethrowerTrooper"/>). Every other <see
/// cref="RosterUnitKind"/> (HalfTrack/Tank/ZeppelinGunship/Drone/
/// Spitter/FloaterQueen) has no humanoid visual kit to build from and
/// stays exactly as invisible as it always was -- a real, separate,
/// much larger undertaking (vehicle/alien meshes) this pass doesn't
/// attempt; see <see cref="SimBridge.SpawnRosterUnit"/>'s own doc
/// comment for the standing gap this class only partially closes.
///
/// Deliberately a PURE VIEW, not a second AI -- CLAUDE.md's own
/// standing direction for the unit-sim migration ("the unit sim is
/// being ported here out of the frame-driven Unity MonoBehaviours...
/// do not add gameplay decisions to MonsterAgent.Update(); it becomes a
/// pure interpolated view") applies here from day one instead of being
/// retrofitted later: this class reads <see cref="SimUnit.X"/>/<see
/// cref="SimUnit.Z"/>/<see cref="SimUnit.Order"/>/<see
/// cref="SimUnit.IsAlive"/> each frame and drives a
/// <see cref="HumanCharacterKit"/> rig accordingly. It never moves a
/// unit, never decides a target, never applies damage -- match-core
/// already resolved all of that server-side before this class ever
/// sees the result. No <see cref="UnitCombat"/>, no <see
/// cref="GroundPathFollower"/> -- those belong to the SEPARATE, non-
/// synced local-AI kit <see cref="HumanoidCombatant"/> uses for
/// hostile-civilian variants (Grandma/Police/etc, docs/35); a
/// sim-driven roster unit has no local decisions left to make.
///
/// Same lifecycle-sync SHAPE as <see cref="BaseDresser"/> (one manager
/// MonoBehaviour, walks <see cref="SimBridge.UnitCount"/>/<see
/// cref="SimBridge.UnitAt"/> every frame, a Dictionary&lt;entityId,...&gt;
/// for spawn/update/despawn) rather than one MonoBehaviour per unit --
/// consistent with this codebase's established "view syncs against the
/// sim's own list, which only grows" pattern (match-core never removes
/// a dead unit, see <see cref="SimUnit.IsSalvageable"/>'s own corpse
/// window), including the same "_deadHandled fires exactly once" guard
/// <see cref="BaseDresser"/>'s own `_destroyedHandled` set already
/// established.
///
/// Position is read directly from <see cref="SimUnit.X"/>/<see
/// cref="SimUnit.Z"/> every frame, NOT interpolated between ticks --
/// true tick-to-tick interpolation (the "interpolated" half of
/// CLAUDE.md's own phrase) is a real, honestly-deferred polish item,
/// not attempted here: match-core ticks at a fixed rate independent of
/// render framerate, so a direct read shows visible position snapping
/// on a slow frame or a fast tick-catch-up -- flagged, not hidden.
/// </summary>
public class RosterInfantryView : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;

    private const float DeathDestroyDelay = 0.5f;
    // Below this horizontal displacement in one frame, treat the unit as
    // stationary rather than re-deriving a facing direction from a
    // near-zero (float-jitter-dominated) delta vector.
    private const float StationaryEpsilon = 0.01f;

    private class UnitVisual
    {
        public GameObject Root;
        public HumanCharacterRig Rig;
        public HumanCharacterAnimState AnimState;
        public Vector3 LastPos;
        public bool Dying;
        public float DeathTimer;
    }

    private readonly Dictionary<uint, UnitVisual> _visuals = new Dictionary<uint, UnitVisual>();
    // Same "fires exactly once, list only grows" guard BaseDresser's own
    // _destroyedHandled set already established -- without this, a dead
    // unit whose visual was already destroyed/removed from _visuals
    // would spawn a FRESH visual next frame (SpawnVisual only checks
    // "not currently in _visuals", not "was this entity ever alive").
    private readonly HashSet<uint> _deadHandled = new HashSet<uint>();

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder)
    {
        bridge = simBridge;
        builder = cityBuilder;
    }

    private void Update()
    {
        if (bridge == null || !bridge.HasMatch || builder == null) return;
        var dt = Time.deltaTime;

        for (var i = 0; i < bridge.UnitCount; i++)
        {
            var u = bridge.UnitAt(i);
            if (!IsInfantry(u.SourceRosterKind)) continue;
            if (_deadHandled.Contains(u.EntityId)) continue;
            TickUnit(u, dt);
        }
    }

    private static bool IsInfantry(RosterUnitKind? kind)
        => kind == RosterUnitKind.Rifleman || kind == RosterUnitKind.FlamethrowerTrooper;

    private void TickUnit(SimUnit u, float dt)
    {
        if (!_visuals.TryGetValue(u.EntityId, out var v))
        {
            // Died before this view ever rendered it (e.g. killed the
            // same tick it spawned) -- nothing to show, nothing to
            // clean up later either.
            if (!u.IsAlive) { _deadHandled.Add(u.EntityId); return; }
            v = SpawnVisual(u);
            _visuals[u.EntityId] = v;
        }

        var pos = new Vector3((float)u.X, 0f, (float)u.Z);
        pos.y = builder.GroundHeightAt(pos);

        if (!u.IsAlive)
        {
            if (!v.Dying) { v.Dying = true; v.DeathTimer = DeathDestroyDelay; }
            v.Root.transform.position = pos;   // corpse settles where it fell -- match-core stops moving a dead unit too
            HumanCharacterAnimator.TickDeath(v.Rig, v.AnimState, dt);
            v.DeathTimer -= dt;
            if (v.DeathTimer <= 0f)
            {
                Object.Destroy(v.Root);
                _visuals.Remove(u.EntityId);
                _deadHandled.Add(u.EntityId);
            }
            return;
        }

        var delta = pos - v.LastPos;
        delta.y = 0f;
        var moveDist = delta.magnitude;
        if (moveDist > StationaryEpsilon)
            v.Root.transform.rotation = Quaternion.Slerp(v.Root.transform.rotation,
                Quaternion.LookRotation(delta.normalized, Vector3.up), dt * 8f);
        v.Root.transform.position = pos;
        v.LastPos = pos;

        // Attacking reads as aim+fire regardless of exact per-shot
        // timing -- SimUnit's own attack cooldown is a private sim-
        // internal detail (no "just fired" event exposed to Unity), so
        // this is a deliberate v0.1 simplification: a continuous aim/
        // recoil pose while channeling an attack, not a precisely
        // synced muzzle flash per resolved hit.
        var attacking = u.Order == UnitOrderKind.AttackUnit
            || u.Order == UnitOrderKind.AttackBuilding
            || u.Order == UnitOrderKind.AttackAnomaly;
        if (attacking)
            HumanCharacterAnimator.TickAim(v.Rig, v.AnimState, aiming: true, firing: true, dt);
        else if (moveDist > StationaryEpsilon)
            HumanCharacterAnimator.TickLocomotion(v.Rig, v.AnimState, moveDist, running: false, dt);
        else
            HumanCharacterAnimator.TickIdle(v.Rig, v.AnimState, twitchy: false, dt);
    }

    private UnitVisual SpawnVisual(SimUnit u)
    {
        var kind = u.SourceRosterKind!.Value;
        var profile = kind == RosterUnitKind.FlamethrowerTrooper
            ? HumanCharacterProfile.FlamethrowerTrooper()
            : HumanCharacterProfile.HumanSoldier();   // Rifleman reuses docs/34's existing "Human Soldier" silhouette

        var root = new GameObject("Roster_" + kind + "_" + u.EntityId);
        root.transform.SetParent(transform, false);
        var rig = HumanCharacterKit.Build(root.transform, profile);
        var seed = (u.EntityId % 1000) / 1000f;
        var animState = new HumanCharacterAnimState { Seed = seed * 6.283f };
        BuildWeaponProp(rig, kind);

        return new UnitVisual
        {
            Root = root,
            Rig = rig,
            AnimState = animState,
            LastPos = new Vector3((float)u.X, 0f, (float)u.Z),
        };
    }

    /// <summary>Same cosmetic-cube-prop idiom <see
    /// cref="HumanoidCombatant.BuildWeaponProp"/> already established --
    /// a rifle silhouette on the right elbow for Rifleman (identical
    /// numbers, same "reads as a rifle at RTS camera distance" result);
    /// FlamethrowerTrooper instead gets twin fuel tanks on the torso's
    /// own back (in place of the generic backpack this profile
    /// deliberately turns off, see <see
    /// cref="HumanCharacterProfile.FlamethrowerTrooper"/>'s own doc
    /// comment) plus a short wide nozzle -- a genuinely different
    /// silhouette from every other infantry kind's rifle, not a recolor
    /// of the same prop.</summary>
    private static void BuildWeaponProp(HumanCharacterRig rig, RosterUnitKind kind)
    {
        var mat = HumanCharacterKit.SharedMaterial();
        if (kind == RosterUnitKind.FlamethrowerTrooper)
        {
            var tankColor = new Color(0.35f, 0.12f, 0.08f);   // dull firebrick red -- "cool 1950s hardware," not a faction color
            SpawnCosmeticCube(rig.Torso, new Vector3(-0.14f, -0.05f, -0.22f), new Vector3(0.16f, 0.5f, 0.16f), tankColor, mat);
            SpawnCosmeticCube(rig.Torso, new Vector3(0.14f, -0.05f, -0.22f), new Vector3(0.16f, 0.5f, 0.16f), tankColor, mat);
            SpawnCosmeticCube(rig.RightElbow, new Vector3(0.05f, -0.3f, 0.18f), new Vector3(0.07f, 0.07f, 0.42f), new Color(0.12f, 0.1f, 0.09f), mat);
            return;
        }
        // Rifleman -- same numbers as HumanoidCombatant.BuildWeaponProp's
        // non-shotgun rifle silhouette.
        SpawnCosmeticCube(rig.RightElbow, new Vector3(0.05f, -0.32f, 0.16f), new Vector3(0.05f, 0.05f, 0.55f), new Color(0.15f, 0.13f, 0.11f), mat);
    }

    private static void SpawnCosmeticCube(Transform parent, Vector3 localPos, Vector3 scale, Color color, Material mat)
    {
        if (parent == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(mpb);
    }
}
