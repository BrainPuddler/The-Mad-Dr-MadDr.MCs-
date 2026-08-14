using UnityEngine;

/// <summary>
/// 2026-08 (creator brief: "Character System Overhaul"): the shared
/// procedural animation library every rig built by <see
/// cref="HumanCharacterKit"/> uses -- one set of Tick* methods, not one
/// per unit type, matching the brief's own "reuse a common lightweight
/// animation set across all compatible characters" ask.
///
/// Pure transform math, no skinning, no bones, no IK solver, no
/// ragdoll/physics -- every method below computes an ABSOLUTE pose each
/// call from the rig's own rest pose (<see
/// cref="HumanCharacterRig.TorsoRestLocalPos"/>/<see
/// cref="HumanCharacterRig.TorsoRestPitchDeg"/>) plus a phase/timer
/// carried on <see cref="HumanCharacterAnimState"/>, the same "no
/// skinned mesh, procedural transform animation" idiom MonsterBody
/// already established in this codebase (see this file's own header on
/// why -- no Editor/DCC pipeline exists here to author a real rig).
///
/// The one rule every locomotion method below is built around
/// (maddr-aesthetic-preferences skill §7, verbatim): "No skating, ever
/// -- a walk cycle's stride length must match actual distance
/// traveled." <see cref="TickLocomotion"/> and <see cref="TickCarry"/>
/// take `distanceMoved` (the exact displacement the CALLER already
/// computed for this tick, e.g. `speed * dt`) and advance the gait
/// phase by that distance, never by raw elapsed time -- a stopped
/// character's legs hold mid-stride exactly like a real stopped
/// walker's would, instead of continuing to swing in place.
///
/// UNVERIFIED IN A REAL RENDER, same standing caveat as
/// HumanCharacterKit's own header.
/// </summary>
public static class HumanCharacterAnimator
{
    private const float TwoPi = Mathf.PI * 2f;

    // Radians of gait phase per world unit moved -- walk vs. run aren't
    // just "the same cycle faster," a run is also a LONGER stride (the
    // brief's own "longer stride" bullet), so run advances phase MORE
    // SLOWLY per unit distance despite covering more distance per second
    // overall (the caller's own MoveSpeed difference already provides
    // the "per second" part; this constant only shapes stride length).
    private const float WalkStrideRadPerUnit = 4.6f;
    private const float RunStrideRadPerUnit = 2.9f;

    private const float WalkArmSwingDeg = 24f;
    private const float RunArmSwingDeg = 46f;
    private const float WalkLegSwingDeg = 26f;
    private const float RunLegSwingDeg = 40f;
    private const float WalkBendDeg = 18f;
    private const float RunBendDeg = 32f;
    private const float WalkBobAmount = 0.025f;
    private const float RunBobAmount = 0.055f;
    private const float RunLeanDeg = 12f;

    /// <summary>Walk/run -- the default ground locomotion for every
    /// legged profile. `running` just widens the same formulas (see the
    /// Run* constants above); there's no separate run-only code path.</summary>
    public static void TickLocomotion(HumanCharacterRig rig, HumanCharacterAnimState state,
        float distanceMoved, bool running, float dt)
    {
        if (rig == null || state == null || !rig.HasLegs) return;

        var strideRate = running ? RunStrideRadPerUnit : WalkStrideRadPerUnit;
        state.GaitPhase = Mathf.Repeat(state.GaitPhase + distanceMoved * strideRate, TwoPi);

        var armSwing = running ? RunArmSwingDeg : WalkArmSwingDeg;
        var legSwing = running ? RunLegSwingDeg : WalkLegSwingDeg;
        var bend = running ? RunBendDeg : WalkBendDeg;
        var bob = running ? RunBobAmount : WalkBobAmount;

        var legPhaseL = state.GaitPhase;
        var legPhaseR = state.GaitPhase + Mathf.PI;
        // arms cross with the OPPOSITE leg (natural human gait: left arm
        // forward exactly when right leg is forward).
        var armPhaseL = legPhaseR;
        var armPhaseR = legPhaseL;

        SetHip(rig.LeftHip, legSwing * Mathf.Sin(legPhaseL));
        SetHip(rig.RightHip, legSwing * Mathf.Sin(legPhaseR));
        SetKneeBend(rig.LeftKnee, bend * Mathf.Clamp01(Mathf.Sin(legPhaseL + 0.6f)));
        SetKneeBend(rig.RightKnee, bend * Mathf.Clamp01(Mathf.Sin(legPhaseR + 0.6f)));

        SetShoulder(rig.LeftShoulder, armSwing * Mathf.Sin(armPhaseL));
        SetShoulder(rig.RightShoulder, armSwing * Mathf.Sin(armPhaseR));
        SetElbowBend(rig.LeftElbow, bend * 0.6f * Mathf.Clamp01(Mathf.Sin(armPhaseL + 0.6f)));
        SetElbowBend(rig.RightElbow, bend * 0.6f * Mathf.Clamp01(Mathf.Sin(armPhaseR + 0.6f)));

        // torso bounce: both feet contribute one bounce per half-stride,
        // hence the doubled frequency from Abs() -- "slight torso
        // bounce" (walk) reads as a real bounce at run without a second
        // set of formulas.
        var bounceY = bob * Mathf.Abs(Mathf.Sin(state.GaitPhase));
        var lean = running ? RunLeanDeg : 0f;
        SetTorso(rig, bounceY, lean, 0f, 0f);
    }

    /// <summary>Carrying a resource visibly -- same leg cycle as a plain
    /// walk (still ground locomotion, still distance-synced), but arms
    /// hold a raised "carrying something in front of the body" pose with
    /// their swing amplitude cut down rather than swinging freely --
    /// "carry resources visibly" (Human Worker) / a real object held
    /// snug doesn't swing.</summary>
    public static void TickCarry(HumanCharacterRig rig, HumanCharacterAnimState state, float distanceMoved, float dt)
    {
        if (rig == null || state == null || !rig.HasLegs) return;

        state.GaitPhase = Mathf.Repeat(state.GaitPhase + distanceMoved * WalkStrideRadPerUnit, TwoPi);
        var legPhaseL = state.GaitPhase;
        var legPhaseR = state.GaitPhase + Mathf.PI;

        SetHip(rig.LeftHip, WalkLegSwingDeg * Mathf.Sin(legPhaseL));
        SetHip(rig.RightHip, WalkLegSwingDeg * Mathf.Sin(legPhaseR));
        SetKneeBend(rig.LeftKnee, WalkBendDeg * Mathf.Clamp01(Mathf.Sin(legPhaseL + 0.6f)));
        SetKneeBend(rig.RightKnee, WalkBendDeg * Mathf.Clamp01(Mathf.Sin(legPhaseR + 0.6f)));

        const float carrySwing = WalkArmSwingDeg * 0.3f;
        const float carryRaiseDeg = -55f;   // negative pitch = arms forward/up, see SetShoulder's own sign convention
        SetShoulder(rig.LeftShoulder, carryRaiseDeg + carrySwing * Mathf.Sin(legPhaseR));
        SetShoulder(rig.RightShoulder, carryRaiseDeg + carrySwing * Mathf.Sin(legPhaseL));
        SetElbowBend(rig.LeftElbow, 70f);
        SetElbowBend(rig.RightElbow, 70f);

        var bounceY = WalkBobAmount * Mathf.Abs(Mathf.Sin(state.GaitPhase));
        SetTorso(rig, bounceY, 0f, 0f, 0f);
    }

    /// <summary>Repetitive construction motion -- "repetitive hammering
    /// or repair motion, lean into work, alternate arm movement" (Human/
    /// Mad-Doctor Workers), or on a legless (Alien) rig, "telekinetic
    /// building motions" instead: no hammering swing at all, a slow
    /// graceful two-arm gesture, since nothing about a floating alien
    /// should read as physically pounding on something.</summary>
    public static void TickBuild(HumanCharacterRig rig, HumanCharacterAnimState state, float dt)
    {
        if (rig == null || state == null) return;
        if (!rig.HasLegs) { TickTelekineticGesture(rig, state, dt); return; }

        const float hammerHz = 1.3f;
        state.WorkPhase = Mathf.Repeat(state.WorkPhase + dt * hammerHz * TwoPi, TwoPi);

        const float leanDeg = 16f;
        const float hammerSwingDeg = 55f;
        const float braceSwingDeg = 12f;

        SetShoulder(rig.RightShoulder, -20f + hammerSwingDeg * Mathf.Sin(state.WorkPhase));
        SetElbowBend(rig.RightElbow, 30f + 40f * Mathf.Clamp01(Mathf.Sin(state.WorkPhase + 1.2f)));
        // "alternate arm movement" -- the off-hand braces the work at a
        // much smaller amplitude, half a cycle out of phase.
        SetShoulder(rig.LeftShoulder, -10f + braceSwingDeg * Mathf.Sin(state.WorkPhase + Mathf.PI));
        SetElbowBend(rig.LeftElbow, 45f);

        SetHip(rig.LeftHip, 8f);
        SetHip(rig.RightHip, -8f);
        SetKneeBend(rig.LeftKnee, 12f);
        SetKneeBend(rig.RightKnee, 12f);

        SetTorso(rig, 0f, leanDeg, 0f, 0f);
    }

    /// <summary>Scavenge/harvest -- both arms move TOGETHER (a scooping/
    /// gathering motion, unlike Build's alternating strike), deeper
    /// forward bend. Legless rigs get the same telekinetic gesture Build
    /// falls back to -- gathering and building read the same way on
    /// something that manipulates the world without hands.</summary>
    public static void TickHarvest(HumanCharacterRig rig, HumanCharacterAnimState state, float dt)
    {
        if (rig == null || state == null) return;
        if (!rig.HasLegs) { TickTelekineticGesture(rig, state, dt); return; }

        const float gatherHz = 0.9f;
        state.WorkPhase = Mathf.Repeat(state.WorkPhase + dt * gatherHz * TwoPi, TwoPi);

        const float leanDeg = 26f;
        const float swingDeg = 32f;

        var sway = swingDeg * Mathf.Sin(state.WorkPhase);
        SetShoulder(rig.LeftShoulder, -30f + sway);
        SetShoulder(rig.RightShoulder, -30f + sway);
        SetElbowBend(rig.LeftElbow, 40f + 25f * Mathf.Clamp01(Mathf.Sin(state.WorkPhase + 1f)));
        SetElbowBend(rig.RightElbow, 40f + 25f * Mathf.Clamp01(Mathf.Sin(state.WorkPhase + 1f)));

        SetHip(rig.LeftHip, 4f);
        SetHip(rig.RightHip, -4f);
        SetKneeBend(rig.LeftKnee, 22f);
        SetKneeBend(rig.RightKnee, 22f);

        SetTorso(rig, 0f, leanDeg, 0f, 0f);
    }

    /// <summary>Standing still. Plain profiles get a barely-there
    /// breathing bob (alive, not frozen, without drawing attention);
    /// <see cref="HumanCharacterProfile.Twitchy"/> profiles (Mad Doctor
    /// Worker) get actual "twitching idle... occasional head tilt...
    /// shoulder twitches" instead -- an irregular wobble built from two
    /// mismatched sine frequencies (same layered-wave idiom
    /// EmissiveAnimator's Buzz flutter and PbrTextureAtlas's Jitter
    /// noise already use elsewhere in this codebase for "looks
    /// irregular, costs nothing, needs no persisted random state"), so
    /// different characters twitch out of phase via their own <see
    /// cref="HumanCharacterAnimState.Seed"/> rather than in lockstep.</summary>
    public static void TickIdle(HumanCharacterRig rig, HumanCharacterAnimState state, bool twitchy, float dt)
    {
        if (rig == null || state == null) return;
        state.AmbientTime += dt;
        var t = state.AmbientTime;
        var seed = state.Seed;

        if (rig.HasLegs)
        {
            SetHip(rig.LeftHip, 0f);
            SetHip(rig.RightHip, 0f);
            SetKneeBend(rig.LeftKnee, 4f);
            SetKneeBend(rig.RightKnee, 4f);
        }

        if (!twitchy)
        {
            var breathe = 0.012f * Mathf.Sin(t * 1.1f + seed);
            SetShoulder(rig.LeftShoulder, 3f);
            SetShoulder(rig.RightShoulder, 3f);
            SetElbowBend(rig.LeftElbow, 8f);
            SetElbowBend(rig.RightElbow, 8f);
            if (rig.Head != null) rig.Head.localRotation = Quaternion.identity;
            SetTorso(rig, breathe, 0f, 0f, 0f);
            return;
        }

        var headTiltX = 10f * Mathf.Sin(t * 0.9f + seed) * (0.5f + 0.5f * Mathf.Sin(t * 2.3f + seed * 1.7f));
        var headTiltZ = 8f * Mathf.Sin(t * 1.6f + seed * 2.1f);
        if (rig.Head != null) rig.Head.localRotation = Quaternion.Euler(headTiltX, 0f, headTiltZ);

        var shoulderTwitchL = 6f * Mathf.Sin(t * 3.1f + seed) * (0.5f + 0.5f * Mathf.Sin(t * 0.7f + seed));
        var shoulderTwitchR = 6f * Mathf.Sin(t * 2.6f + seed * 1.3f) * (0.5f + 0.5f * Mathf.Sin(t * 0.5f + seed));
        SetShoulder(rig.LeftShoulder, shoulderTwitchL);
        SetShoulder(rig.RightShoulder, shoulderTwitchR);
        SetElbowBend(rig.LeftElbow, 10f);
        SetElbowBend(rig.RightElbow, 10f);

        SetTorso(rig, 0.01f * Mathf.Sin(t * 1.7f + seed), 0f, 2.5f * Mathf.Sin(t * 0.6f + seed), 0f);
    }

    /// <summary>Hover idle/move -- the entire Alien Worker locomotion
    /// story: "constant hovering, slow floating motion, gentle vertical
    /// bobbing, slight side-to-side drift, small body rotations, no
    /// visible footsteps." No legs exist on this rig at all (a geometry
    /// fact, see HumanCharacterKit), so there is nothing here to
    /// "not animate" -- only the torso (and everything parented under
    /// it: head, arms) moves. `horizontalSpeed` (already known by the
    /// caller, same as `distanceMoved`/dt elsewhere) banks the whole
    /// body forward like a craft in motion instead of driving a gait
    /// cycle -- an alien in motion still never takes a "step."</summary>
    public static void TickHover(HumanCharacterRig rig, HumanCharacterAnimState state, float horizontalSpeed, float dt)
    {
        if (rig == null || state == null || rig.HasLegs) return;
        state.AmbientTime += dt;
        var t = state.AmbientTime;
        var seed = state.Seed;

        const float bobAmp = 0.06f;
        const float bobHz = 0.55f;
        const float driftAmp = 0.03f;
        const float driftHz = 0.35f;

        var bobY = bobAmp * Mathf.Sin(t * bobHz * TwoPi + seed);
        var driftX = driftAmp * Mathf.Sin(t * driftHz * TwoPi + seed * 1.9f);
        var driftZ = driftAmp * Mathf.Cos(t * driftHz * TwoPi * 0.8f + seed * 1.3f);
        rig.Torso.localPosition = rig.TorsoRestLocalPos + new Vector3(driftX, bobY, driftZ);

        var wobbleYaw = 6f * Mathf.Sin(t * 0.4f + seed);
        var forwardLean = Mathf.Clamp(horizontalSpeed * 4f, 0f, 18f);
        rig.Torso.localRotation = Quaternion.Euler(rig.TorsoRestPitchDeg + forwardLean, wobbleYaw, 0f);

        // arms sway gently rather than staying rigid -- "graceful,
        // intelligent and effortless," never fully still.
        var sway = 8f * Mathf.Sin(t * 0.7f + seed);
        SetShoulder(rig.LeftShoulder, sway);
        SetShoulder(rig.RightShoulder, -sway);
        SetElbowBend(rig.LeftElbow, 15f);
        SetElbowBend(rig.RightElbow, 15f);
    }

    /// <summary>Slow, graceful two-arm gesture -- what Build/Harvest fall
    /// back to on a legless rig ("telekinetic building motions... resource
    /// gathering through subtle arm or tentacle gestures"). Keeps the
    /// hover bob running underneath (an alien doesn't stop floating to
    /// work) rather than freezing the body while the arms move.</summary>
    private static void TickTelekineticGesture(HumanCharacterRig rig, HumanCharacterAnimState state, float dt)
    {
        TickHover(rig, state, 0f, dt);
        var t = state.AmbientTime;
        var seed = state.Seed;
        var gesture = 22f * Mathf.Sin(t * 0.5f + seed);
        SetShoulder(rig.LeftShoulder, -20f + gesture);
        SetShoulder(rig.RightShoulder, -20f - gesture);
        SetElbowBend(rig.LeftElbow, 35f + 15f * Mathf.Sin(t * 0.6f + seed));
        SetElbowBend(rig.RightElbow, 35f + 15f * Mathf.Cos(t * 0.6f + seed));
    }

    /// <summary>Rifle-ready stance -- "standing guard idle, aim pose,
    /// fire pose" (Human Soldier). One method covers all three: `aiming`
    /// false is the relaxed guard idle (rifle held low), true is the
    /// raised aim pose; `firing` (only meaningful while `aiming`) adds a
    /// fast small recoil pulse on top using <see
    /// cref="HumanCharacterAnimState.WorkPhase"/> as a generic action-
    /// phase accumulator, the same field Build/Harvest already reuse for
    /// their own repetitive motion rather than adding a fire-specific
    /// field.</summary>
    public static void TickAim(HumanCharacterRig rig, HumanCharacterAnimState state, bool aiming, bool firing, float dt)
    {
        if (rig == null || state == null) return;

        if (!aiming)
        {
            SetShoulder(rig.RightShoulder, -25f);
            SetShoulder(rig.LeftShoulder, -15f);
            SetElbowBend(rig.RightElbow, 35f);
            SetElbowBend(rig.LeftElbow, 55f);
            SetTorso(rig, 0f, 0f, 0f, 0f);
            return;
        }

        var recoil = 0f;
        if (firing)
        {
            const float fireHz = 6f;
            state.WorkPhase = Mathf.Repeat(state.WorkPhase + dt * fireHz * TwoPi, TwoPi);
            recoil = 6f * Mathf.Max(0f, Mathf.Sin(state.WorkPhase));
        }

        SetShoulder(rig.RightShoulder, -70f - recoil);
        SetShoulder(rig.LeftShoulder, -60f);
        SetElbowBend(rig.RightElbow, 80f);
        SetElbowBend(rig.LeftElbow, 75f);
        SetTorso(rig, 0f, 4f, 0f, 0f);
    }

    /// <summary>"Quick collapse, no ragdolls" -- a short, one-way eased
    /// fall onto the torso's forward pitch plus a small sink and limb
    /// splay, driven by <see cref="HumanCharacterAnimState.DeathT"/>
    /// (0..1, advanced by real time here since there's no more
    /// locomotion to sync against). Callers keep calling this every
    /// frame after death is triggered; once `DeathT` reaches 1 the pose
    /// is stable and further calls are cheap no-ops in practice (the
    /// math just keeps recomputing the same clamped result).</summary>
    public static void TickDeath(HumanCharacterRig rig, HumanCharacterAnimState state, float dt)
    {
        if (rig == null || state == null) return;
        const float collapseDuration = 0.35f;
        state.DeathT = Mathf.Clamp01(state.DeathT + dt / collapseDuration);
        var eased = 1f - (1f - state.DeathT) * (1f - state.DeathT);   // ease-out: fast start, settles at the end

        var pitch = Mathf.Lerp(rig.TorsoRestPitchDeg, 82f, eased);
        var sink = Mathf.Lerp(0f, rig.TorsoRestLocalPos.y * 0.55f, eased);
        rig.Torso.localPosition = rig.TorsoRestLocalPos - new Vector3(0f, sink, 0f);
        rig.Torso.localRotation = Quaternion.Euler(pitch, 0f, 12f * eased);

        SetShoulder(rig.LeftShoulder, -30f * eased);
        SetShoulder(rig.RightShoulder, 20f * eased);
        SetElbowBend(rig.LeftElbow, 25f * eased);
        SetElbowBend(rig.RightElbow, 15f * eased);
        if (rig.HasLegs)
        {
            SetHip(rig.LeftHip, -15f * eased);
            SetHip(rig.RightHip, 25f * eased);
            SetKneeBend(rig.LeftKnee, 35f * eased);
            SetKneeBend(rig.RightKnee, 10f * eased);
        }
    }

    private static void SetTorso(HumanCharacterRig rig, float bobY, float extraPitchDeg, float rollDeg, float yawDeg)
    {
        rig.Torso.localPosition = rig.TorsoRestLocalPos + new Vector3(0f, bobY, 0f);
        rig.Torso.localRotation = Quaternion.Euler(rig.TorsoRestPitchDeg + extraPitchDeg, yawDeg, rollDeg);
    }

    // 2026-08 (Grandma-in-a-wheelchair, "Refactor Human Soldiers & Armed
    // Citizens" brief, "Drive a manual wheelchair" -- creator direction
    // verbatim): a SEATED legless rig (HumanCharacterProfile.SeatedHeight
    // > 0) rolls along the GROUND, unlike the Alien Worker's legless
    // HOVER rig -- TickHover's vertical bob/side-drift/forward-lean-when-
    // moving would read as levitating, wrong for something with wheels.
    private const float WheelchairStrideRadPerUnit = 3.2f;

    /// <summary>Distance-synced (same "no skating" rule as
    /// TickLocomotion), no vertical bob or drift at all -- a wheelchair
    /// stays on the ground plane. Arms cycle through a push-the-rim-then-
    /// recover motion instead of a natural walking swing, with a small
    /// forward-back torso rock from the effort, synced to the SAME phase
    /// so the rock and the push always agree with each other.</summary>
    public static void TickWheelchair(HumanCharacterRig rig, HumanCharacterAnimState state, float distanceMoved, float dt)
    {
        if (rig == null || state == null) return;
        state.GaitPhase = Mathf.Repeat(state.GaitPhase + distanceMoved * WheelchairStrideRadPerUnit, TwoPi);

        var push = 35f * Mathf.Clamp01(Mathf.Sin(state.GaitPhase));
        var recover = 20f * Mathf.Clamp01(-Mathf.Sin(state.GaitPhase));
        SetShoulder(rig.LeftShoulder, -10f - push + recover);
        SetShoulder(rig.RightShoulder, -10f - push + recover);
        SetElbowBend(rig.LeftElbow, 30f + 15f * Mathf.Clamp01(Mathf.Sin(state.GaitPhase + 0.8f)));
        SetElbowBend(rig.RightElbow, 30f + 15f * Mathf.Clamp01(Mathf.Sin(state.GaitPhase + 0.8f)));

        var rock = 3f * Mathf.Sin(state.GaitPhase * 2f);   // double frequency: one rock per push-recover half-cycle, same reasoning TickLocomotion's torso bounce uses
        SetTorso(rig, 0f, rock, 0f, 0f);
    }

    // Pivot rotation sign convention throughout this file: positive X
    // rotation swings a limb FORWARD (in the character's own facing
    // direction, since the root's own rotation already points local +Z
    // that way -- Worker/Citizen/HumanoidCombatant all set the root's
    // rotation via Quaternion.LookRotation exactly like before this
    // system existed).
    private static void SetShoulder(Transform t, float pitchDeg) { if (t != null) t.localRotation = Quaternion.Euler(pitchDeg, 0f, 0f); }
    private static void SetHip(Transform t, float pitchDeg) { if (t != null) t.localRotation = Quaternion.Euler(pitchDeg, 0f, 0f); }
    private static void SetElbowBend(Transform t, float bendDeg) { if (t != null) t.localRotation = Quaternion.Euler(-bendDeg, 0f, 0f); }
    private static void SetKneeBend(Transform t, float bendDeg) { if (t != null) t.localRotation = Quaternion.Euler(bendDeg, 0f, 0f); }
}

/// <summary>Per-instance animation state <see cref="HumanCharacterAnimator"/>
/// carries between ticks -- one of these per character, alongside its
/// <see cref="HumanCharacterRig"/>. `Seed` should differ per instance
/// (same convention as every other per-instance seed in this codebase --
/// EmissiveAnimator.Register, BuildingWindowGrid.AddWindow -- so a crowd
/// of idle Mad Doctor Workers twitches out of phase, not in unison).</summary>
public class HumanCharacterAnimState
{
    public float Seed;
    public float GaitPhase;
    public float WorkPhase;
    public float AmbientTime;
    public float DeathT;
}
