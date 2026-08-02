using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/25 Phase B/C: the steering entry point `MonsterAgent.FollowPath`
/// calls, replacing the old two-call-site fight between `AvoidanceDir` (a
/// heading deflection, inside FollowPath) and `ApplySeparation` (a hard
/// position edit, once more every frame from `MonsterAgent.Update()`) --
/// docs/25 section 2's root cause #1: "Seek and Separation are applied in
/// sequence, not blended." `Combine()` folds seek, a softened
/// separation-as-force, and (Phase C) time-to-collision predictive
/// avoidance into ONE heading for a path-following unit, so all three
/// settle on the same answer this frame instead of separation yanking the
/// position back out of whatever heading avoidance just chose. It also now
/// returns a speed scale, so a unit fighting a strong deflection eases off
/// its own throttle instead of ramming full speed into whatever's blocking
/// it (docs/25 Phase C's "speed modulation" requirement).
///
/// `RuntimeCityBuilder.ApplySeparation` is UNTOUCHED as a public entry point
/// and UNCONDITIONAL behaviourally -- `Tank.cs` calls it too (docs/25
/// explicitly keeps tanks out of scope), and `MonsterAgent.Update()` still
/// calls it every frame regardless of order state, same as before this
/// plan. It remains the hard "never actually overlap" guarantee; `Combine`
/// below is an earlier-reacting heading NUDGE layered on top while
/// path-following, not a replacement for it (a Phase B harness confirmed a
/// soft blend alone isn't enough to prevent interpenetration on its own).
/// `ApplySeparation` now calls `SeparationForce` below for its per-pair
/// math instead of duplicating it -- a pure extract, same numbers, same
/// cumulative-push-per-neighbour order.
///
/// Stateless and dependency-free (matches SpatialGrid's style) so it
/// compiles and runs in the standalone console harness used to verify
/// docs/25 phases -- no MonoBehaviour, no engine calls beyond UnityEngine's
/// math types.
///
/// 2026-08 (creator report: "monsters will spin around each other trying
/// to pass one another"): two real fixes, `PredictiveAvoidance`'s
/// `TieBreakDeadband` and `Alignment`/`Cohesion`'s `OpposingHeadingCutoff`
/// (both below, see their own comments). CORRECTION to this pass's own
/// first verification: the original `steerverify` harness never
/// published a moved unit's `LastVelocity` between frames, so every
/// neighbour was silently tested as if PERMANENTLY STATIONARY --
/// `PredictiveAvoidance`'s closing-speed math still degraded gracefully
/// enough to look plausible, but `Alignment` (which skips near-zero
/// velocity) never contributed AT ALL, so the harness could never have
/// caught what turned out to be the DOMINANT cause for the simple,
/// most-common case. Fixed and re-verified: with real velocities
/// published, a lone pair walking straight at each other was NOT already
/// clean (it spun violently -- worst unit reversed side ~20 TIMES over
/// one approach) and `OpposingHeadingCutoff` alone fixes it completely
/// (0 reversals); `TieBreakDeadband` alone does nothing for a lone pair
/// (same-`Faction` Alignment, pulling a unit's own heading toward an
/// oncoming neighbour's OPPOSING velocity, was the real mechanism, not
/// the avoidance side-tie-break). For a tight multi-squad scrum both
/// fixes together still beat either alone, but even combined it's a
/// real, measured improvement (20 -&gt; 19 total side-reversals in the
/// harness's 3v3 scenario), not a full cure -- dense multi-unit combat
/// likely needs real hysteresis/state this file's own "Stateless"
/// design deliberately doesn't have yet, flagged rather than silently
/// claimed solved.
///
/// 2026-08 follow-up (creator report: "monsters are still circling each
/// other... if they are the same speed and they can't get around, what
/// if... the nav system picks one to give way to the other, until they
/// are body size + X distance apart, then they resume normal speed"):
/// implemented close to verbatim as `GiveWaySpeedScale`/`IsYieldingTo`
/// below -- a deterministic per-pair "who yields" (same `GetInstanceID`
/// tie-break `PredictiveAvoidance` already uses) with NO stored grant;
/// the release condition is real distance re-checked every frame, not a
/// timer. Measured in `steerverify`: the toughest 3v3 scenario improves
/// (19 -&gt; 18 total flips) with zero regression anywhere else. Two
/// design choices were only settled by testing, not guessed up front:
/// (1) the trigger REQUIRES the neighbour's heading to actually oppose
/// `fwd` (`OpposingHeadingCutoff`, same threshold `Alignment`/`Cohesion`
/// use) -- an earlier cut gated on proximity+moving alone and made the
/// SAME scenario measurably WORSE (19 -&gt; 30 flips), because packed
/// squadmates marching the same direction are well within the trigger
/// radius of EACH OTHER too, and got randomly throttled against their
/// own packmates for no reason; (2) yielding only throttles SPEED, never
/// excludes the neighbour from the DIRECTION blend -- that was tried
/// too, and also regressed a previously-clean scenario (a yielding unit
/// walking dead straight with zero avoidance input turned out to make
/// the geometry WORSE for the other unit routing around it, not
/// better). Still not a full cure for the hardest multi-squad case, same
/// honest limit as the paragraph above -- this is a real, measured
/// improvement on top of it, not a claim of "fixed."
///
/// 2026-08 ROOT-CAUSE PASS (creator: "circling is still happening").
/// Every fix above was verified against a throwaway harness that only
/// ever tested a LONE PAIR -- and a lone pair really was already clean,
/// which is why each pass looked like it worked. `Tools~/SteerVerify`
/// (committed this pass, so this can't silently happen again) tests
/// three-or-more units in contest, and every such scenario orbited
/// FOREVER: 13-16x more distance walked than the straight line, 15692
/// side-reversals, 3 of 6 scenarios never resolving inside 60 seconds.
///
/// The circling was not a tuning problem. It was geometry:
/// `PredictiveAvoidance` returns a purely LATERAL (perpendicular to
/// `fwd`) vector, summed over neighbours with NO bound. One neighbour at
/// full urgency already deflects the blended heading ~50 degrees; three
/// deflect it past 90, at which point the unit is steering AWAY from its
/// goal while still moving at full speed. A unit that persistently moves
/// perpendicular to the direction of its goal is, by definition,
/// travelling a circle around it -- so orbit was never an emergent
/// oscillation to be damped out, it was the blend's stable fixed point,
/// and no amount of side tie-breaking could have removed it. The three
/// fixes this pass adds, in order of how much they matter:
///
///   1. `ClampToCone` -- steering may deflect at most `MaxDeflection`
///      from the seek direction. Progress toward the goal is therefore
///      never less than `MaxDeflectionCos` of speed, which makes a closed
///      orbit arithmetically impossible rather than merely unlikely.
///      This is the actual cure.
///   2. `SteeringMemory` -- a committed left/right side, held for
///      `SideCommitSeconds` against the SAME dominant threat. Removes the
///      frame-to-frame flip-flopping ("commitment"/"hysteresis").
///   3. `ShouldAdvanceWaypoint` -- see its own comment block. A SECOND,
///      independent cause, and the one that explains "it's worse with
///      larger monsters": a path node standing inside another unit's
///      separation envelope could never be consumed, so the unit orbited
///      a waypoint it was physically unable to reach. Measured as the
///      single biggest contributor of the three.
///
/// See `Tools~/SteerVerify/README.md` for the before/after numbers.
/// </summary>
public static class MonsterSteeringController
{
    /// <summary>2026-08 root-cause pass: the widest angle steering may
    /// deviate from the seek direction, in degrees. THE fix for circling
    /// -- see this class's header for why an unbounded lateral avoidance
    /// term makes orbit a stable fixed point rather than an oscillation.
    ///
    /// 60 degrees is chosen for a concrete guarantee, not by feel: a unit
    /// always retains at least cos(60) = HALF its speed as progress
    /// toward its goal, however crowded it is. Two units contesting the
    /// same ground therefore always close on their goals while dodging,
    /// so the encounter resolves in bounded time instead of settling into
    /// a circle. It is deliberately generous enough to still read as real
    /// avoidance (a 60-degree swerve is a big, legible dodge), just never
    /// as an orbit.
    ///
    /// This is a hard clamp on the OUTPUT, not a re-weighting of the
    /// inputs: every existing term keeps its already-tuned weight and its
    /// full influence right up to the cone edge, so an uncrowded unit's
    /// steering is byte-for-byte what it was before this pass. Only the
    /// crowded case -- the only case that was ever broken -- changes.
    ///
    /// Safe because this layer steers around UNITS only: static geometry
    /// (buildings, water) is the hex pathfinder's job, so a capped
    /// deflection can never be the difference between routing around a
    /// wall and walking into it.</summary>
    public const float MaxDeflection = 60f;
    public static readonly float MaxDeflectionCos = Mathf.Cos(MaxDeflection * Mathf.Deg2Rad);
    public static readonly float MaxDeflectionSin = Mathf.Sin(MaxDeflection * Mathf.Deg2Rad);

    /// <summary>How long a unit commits to a chosen left/right side
    /// before it may reconsider -- the "movement commitment"/hysteresis
    /// this layer never had (its own header above flagged the absence
    /// honestly: "dense multi-unit combat likely needs real
    /// hysteresis/state this file's own 'Stateless' design deliberately
    /// doesn't have yet").
    ///
    /// 0.9s is long enough to actually carry a unit PAST a neighbour at
    /// walking speed rather than merely damping the twitch, and short
    /// enough that a held side never reads as a unit ignoring a genuinely
    /// new situation. The commitment is keyed to the DOMINANT THREAT'S
    /// identity as well as the clock (see `SteeringMemory`): a unit that
    /// finishes with one neighbour and meets a different one re-decides
    /// immediately, so this is per-ENCOUNTER commitment, not a blanket
    /// refusal to re-steer for 0.9 seconds.</summary>
    public const float SideCommitSeconds = 0.9f;

    /// <summary>The per-unit steering memory that commitment needs. A
    /// plain mutable struct living on `UnitCombat` (beside `LastVelocity`/
    /// `YieldTarget`, which this layer already reads through the same
    /// handle) rather than a dictionary in here: no allocation, no
    /// per-frame lookup, nothing to clean up when a unit dies, and this
    /// class keeps its no-stored-collections shape so
    /// `Tools~/SteerVerify` can still drive it directly.</summary>
    public struct SteeringMemory
    {
        /// <summary>-1 (left) / +1 (right) / 0 (nothing committed).</summary>
        public sbyte Side;

        /// <summary>`GetInstanceID` of the threat the side was chosen
        /// against. A different dominant threat releases the commitment
        /// at once -- commitment is to an ENCOUNTER, not to a clock.</summary>
        public int SideThreatId;

        /// <summary>Clock time the commitment lapses once the encounter
        /// ends; refreshed every frame the same threat still dominates.</summary>
        public float HoldUntil;
    }
    /// <summary>docs/25 Phase C: how far ahead (seconds) predictive
    /// avoidance looks for a converging neighbour. Long enough to react to
    /// a head-on pair closing at a brisk run well before contact; short
    /// enough that a neighbour merely passing nearby on a diverging course
    /// never registers.</summary>
    public const float Horizon = 2.5f;

    /// <summary>Extra clearance (meters) predictive avoidance reacts
    /// within, on top of the two bodies' own combined radii -- a personal-
    /// space buffer distinct from the Inspector `groupSpacing` knob (which
    /// governs REST spacing once a group has stopped, not in-transit
    /// urgency).</summary>
    public const float AvoidancePadding = 1.5f;

    /// <summary>Floor on Combine's speed-scale output -- a heavily blocked
    /// unit eases off, but steering alone never fully stops it (that
    /// escalation is DeadlockManager's job, docs/25 Phase D, not this
    /// layer).</summary>
    public const float MinSpeedScale = 0.35f;

    /// <summary>2026-08 (creator report: monsters spinning around each
    /// other instead of passing): meters of lateral offset, in
    /// `PredictiveAvoidance`'s own `onRight` measure, within which a
    /// near-head-on pair uses the deterministic per-pair tie-break
    /// instead of the raw (flip-prone) geometric sign -- see that
    /// method's own comment for why. Sized relative to a typical body
    /// (`Radius` 1.5m default) and to `AvoidancePadding` (1.5m): wide
    /// enough to actually swallow ordinary frame-to-frame positional
    /// noise near the boundary, narrow enough that any approach with a
    /// real, clearly-lopsided lateral offset still steers off the
    /// geometry, not the tie-break.
    ///
    /// 2026-08 CORRECTION (creator follow-up: "monsters are still
    /// circling each other... seems to happen with larger monster"):
    /// this used to be a flat 0.3m regardless of the pair's actual size
    /// -- fine for the default 1.5m-radius test units this was tuned
    /// against (a ~4.5m `combined` envelope, so 0.3m is a real ~7%
    /// slice of it), but a fixed METERS threshold doesn't scale with a
    /// bigger body's own proportionally bigger `combined`. A large pair
    /// (say 5m radius each, ~11.5m `combined`) got the SAME 0.3m
    /// window -- under 3% of their own envelope -- so a much WIDER
    /// range of "almost but not quite head-on" approaches fell OUTSIDE
    /// the deadband and back onto the noisy raw geometric sign this
    /// whole mechanism exists to avoid. `TieBreakDeadbandFor` below
    /// replaces the flat constant with one scaled to the ACTUAL pair's
    /// `combined`, floored at this same original 0.3m so the
    /// already-verified small-pair behavior (0 reversals in
    /// `steerverify`) doesn't regress.</summary>
    public const float TieBreakDeadband = 0.3f;

    /// <summary>Fraction of a pair's own `combined` collision envelope
    /// the tie-break deadband should cover -- see `TieBreakDeadband`'s
    /// own CORRECTION for why a flat meters value doesn't scale. 1/15
    /// chosen so it's the EXACT crossover point for the default 1.5m-
    /// radius case (`combined` = 4.5m, and 4.5 * 1/15 = 0.3 = the
    /// original flat constant) -- the default/small-body case is thus
    /// byte-for-byte unregressed (still exactly 0.3m, verified against
    /// `steerverify`'s own small-radius scenarios), while anything
    /// LARGER than default gets a proportionally wider deadband instead
    /// of staying pinned at a small body's scale.</summary>
    public const float TieBreakDeadbandFraction = 1f / 15f;

    /// <summary>The actual deadband to use for one pair, given their
    /// `combined` collision envelope (`PredictiveAvoidance`'s own
    /// `bodyRadius + AvoidancePadding`) -- `Max` against the original
    /// flat `TieBreakDeadband` so a small pair is never worse off than
    /// before this fix, only larger pairs get a wider window.</summary>
    public static float TieBreakDeadbandFor(float combined)
    {
        return Mathf.Max(TieBreakDeadband, combined * TieBreakDeadbandFraction);
    }

    /// <summary>2026-08 (creator follow-up: "monsters are still circling
    /// each other... if they are the same speed and they can't get
    /// around, what if when you detect another monster near, the nav
    /// system picks one to give way to the other, until they are body
    /// size + X distance apart, then they can resume their normal
    /// speed"): the floor `GiveWaySpeedScale` can push a yielding unit's
    /// speed down to -- deliberately BELOW `MinSpeedScale` (0.35), since
    /// this is a much stronger "just stop and let them by" signal than
    /// ordinary avoidance easing off. Not zero: a full stop is
    /// DeadlockManager's own escalation path (docs/25 Phase D), never
    /// this layer's job.</summary>
    public const float GiveWayMinSpeedScale = 0.15f;


    /// <summary>The actual per-neighbour speed multiplier for
    /// `GiveWaySpeedScale` -- 1 at the trigger boundary (`combined +
    /// AvoidancePadding`, "body size + X" in the creator's own words,
    /// reusing avoidance's existing personal-space buffer as X rather
    /// than inventing a second one), linearly down to
    /// `GiveWayMinSpeedScale` as the two bodies get closer.</summary>
    public static float GiveWayScaleFor(float dist, float combined)
    {
        var proximity = 1f - Mathf.Clamp01(dist / combined);
        return Mathf.Lerp(1f, GiveWayMinSpeedScale, proximity);
    }

    /// <summary>The creator's own proposed fix: rather than only steering
    /// AROUND a close neighbour (`PredictiveAvoidance`'s job -- WHICH way
    /// to go), this decides WHETHER to keep pushing forward into the
    /// contest at all right now. Two units of similar priority both
    /// fighting an equal-and-opposite avoidance nudge is exactly the
    /// symmetric standoff that produces a spin no amount of
    /// steering-direction tie-breaking fully cures (this file's own
    /// header already says so) -- breaking the SYMMETRY by making
    /// exactly one of the pair yield removes the standoff instead of
    /// just trying to out-guess it.
    ///
    /// Gated on `fwd` vs the neighbour's own `LastVelocity` opposing each
    /// other (`OpposingHeadingCutoff`, the SAME threshold `Alignment`/
    /// `Cohesion` already use to tell "an oncoming unit" from "a
    /// squadmate going my way"), not just proximity+moving -- a FIRST cut
    /// of this fix skipped that gate (proximity + "is the neighbour
    /// moving at all" only) and made `steerverify`'s 3v3 corridor
    /// scenario measurably WORSE (19 -&gt; 30 total flips): squadmates
    /// marching shoulder-to-shoulder toward the SAME destination are well
    /// within `combined`'s own reach (2m squad spacing versus a 4.5m
    /// default `combined`), so without the heading gate this was
    /// randomly throttling half of every squad's own members against
    /// their own packmates by `InstanceID` alone, for no reason -- pure
    /// self-inflicted chaos, not a fix. With the gate, only a neighbour
    /// actually heading opposite (or crossing) `fwd` counts, which is
    /// the genuine "in each other's way" case this exists for.
    ///
    /// `fwd` (not relative velocity) is deliberately what's tested: once
    /// a pair is ALREADY mid-spin their relative velocity swings mostly
    /// tangential (orbiting), which a closing-velocity gate (the way
    /// `PredictiveAvoidance` itself is gated) could miss entirely right
    /// when it matters most. `fwd` is each unit's own INTENDED heading,
    /// undistorted by whatever avoidance is doing to it this frame, so
    /// it stays a stable, correct signal for "are we actually contesting
    /// the same ground" throughout an active spin, not just on the
    /// initial approach.
    ///
    /// WHO yields uses the exact same pairwise-stable identity
    /// comparison `PredictiveAvoidance`'s own tie-break already relies
    /// on (`GetInstanceID`, so both units of a pair agree without
    /// talking to each other): the lower-ID unit always proceeds at full
    /// speed, only the higher-ID one's scale drops here. No stored
    /// grant/timer anywhere -- the release condition ("body size + X
    /// apart") is just real distance re-checked fresh every single
    /// frame, so this needs no persistent state at all, matching this
    /// whole file's "Stateless" design (unlike `DeadlockManager`'s
    /// separate, much rarer stall-recovery path, which DOES carry state
    /// but only ever engages after real, sustained lack of progress --
    /// this is the everyday, first-line version of "someone gives
    /// ground," checked every frame for every close, opposing pair, not
    /// a rare fallback).</summary>
    public static float GiveWaySpeedScale(UnitCombat self, Vector3 fwd, List<UnitCombat> neighbours)
    {
        var scale = 1f;
        foreach (var c in neighbours)
        {
            if (!IsYieldingTo(self, fwd, c)) continue;
            var dist = Distance2D(self, c);
            var combined = self.Radius + c.Radius + AvoidancePadding;
            var thisScale = GiveWayScaleFor(dist, combined);
            if (thisScale < scale) scale = thisScale;
        }
        return scale;
    }

    /// <summary>True exactly when `self` is the (deterministically,
    /// pairwise-stable) yielding half of a genuine give-way encounter
    /// with `c` -- pulled out of `GiveWaySpeedScale` so the predicate has
    /// exactly one definition. (An earlier version of this fix also used
    /// this predicate to exclude a yielded-to neighbour from `Combine`'s
    /// direction terms entirely -- measurably WORSE in `steerverify`, a
    /// previously-clean scenario picked up more flips and took 40%
    /// longer to resolve, so that half was reverted: a yielding unit
    /// keeps steering normally, this predicate now only ever gates the
    /// speed throttle below.) See `GiveWaySpeedScale`'s own header for
    /// why each individual check here exists.</summary>
    private static bool IsYieldingTo(UnitCombat self, Vector3 fwd, UnitCombat c)
    {
        if (c == null || c == self || !c.Alive) return false;

        var v = c.LastVelocity;
        v.y = 0f;
        if (v.sqrMagnitude < 1e-4f) return false;   // stationary neighbour -- nothing to give way to
        // 2026-08: widening this cutoff to 0.35 (so give-way would also
        // catch the TANGENTIAL headings of a pair already mid-orbit, which
        // sit near dot 0 and so straddle this threshold) was tried and
        // MEASURABLY REJECTED -- SteerVerify got worse, 427 -> 466
        // side-reversals across the suite, with nothing resolving faster.
        // Recorded here because it is an obvious-looking idea that will
        // occur to the next reader too. The orbit it was meant to break is
        // gone for the real reasons documented in this class's header.
        if (Vector3.Dot(v.normalized, fwd) >= OpposingHeadingCutoff) return false;   // heading my own way -- a squadmate, not a contest

        if (self.GetInstanceID() < c.GetInstanceID()) return false;   // this pair's low-ID member always proceeds; only the high-ID one yields

        return Distance2D(self, c) < self.Radius + c.Radius + AvoidancePadding;
    }

    private static float Distance2D(UnitCombat a, UnitCombat b)
    {
        var d = b.transform.position - a.transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    /// <summary>docs/23 §5 v0.1 weights for the two NEW group forces this
    /// phase adds. Separation's own weight isn't listed here -- it keeps
    /// its existing, already-tuned Combine contribution (`sepBias * 0.8f`
    /// below, unchanged) rather than being re-weighted to docs/23's literal
    /// "sep 1.0," since that number describes match-core's own PARALLEL
    /// pure-math port (`Flocking.cs`), not a mandate to retune Unity's
    /// already-proven blend (see this class's own header: `ApplySeparation`
    /// and this whole file are explicitly NOT to be altered in their
    /// existing behaviour for units that aren't opted into something
    /// new).</summary>
    public const float AlignmentWeight = 0.35f;
    public const float CohesionWeight = 0.15f;

    /// <summary>docs/23 §5: "match average heading of groupmates within
    /// 12 m."</summary>
    public const float GroupmateRadius = 12f;

    /// <summary>2026-08 (creator report, "monsters spin around each other
    /// trying to pass"; see this file's own class header for the full
    /// writeup): dot-product threshold, against THIS unit's own intended
    /// heading (`fwd`), below which a same-Faction neighbour is excluded
    /// from `Alignment`/`Cohesion`'s "groupmate" pool. 0 means "more than
    /// 90 degrees off my own direction of travel" -- same-Faction ALONE
    /// was too loose a "groupmate" definition: two squads walking OPPOSITE
    /// directions through one corridor are both "monster" faction, so
    /// each squad was pulling every member of the OTHER toward its own
    /// average heading/position (Alignment/Cohesion) while simultaneously
    /// trying to avoid it (PredictiveAvoidance/SeparationForce) -- two
    /// systems fighting each other over the same pair, every frame. A
    /// neighbour with negligible velocity (idle, captured/possessed) is
    /// NEVER excluded by this check -- only a neighbour with a real,
    /// meaningfully-diverging-or-opposing heading is cut, so a squad
    /// gently curving around an obstacle (well within 90 degrees of each
    /// other) still flocks exactly as before.</summary>
    public const float OpposingHeadingCutoff = 0f;

    /// <summary>Combine's output: a steering direction plus how much of the
    /// caller's intended speed to actually use this frame.</summary>
    public struct SteeringResult
    {
        public Vector3 Direction;
        public float SpeedScale;

        /// <summary>2026-08: true when another unit actually influenced
        /// this frame's steering (avoidance fired, or bodies are inside
        /// separation range). Reported so the caller can tell "I'm not
        /// getting closer because someone is in my way" apart from "I'm
        /// not getting closer because my target is running away" -- see
        /// `ShouldAdvanceWaypoint`, which must escape the first and must
        /// NOT escape the second.</summary>
        public bool Contested;
    }

    // ---- waypoint progress (2026-08 root-cause pass) --------------------
    //
    // The SECOND independent cause of the reported circling, and the one
    // that explains "seems to happen with larger monsters."
    //
    // `MonsterAgent.FollowPath` consumed a path node once it came within a
    // FLAT 0.6 metre `GroundArriveDist`. But separation holds two bodies
    // `selfRadius + otherRadius + groupSpacing` apart -- 4 m for default
    // 1.5 m monsters, 11 m for big ones. So the moment another unit stands
    // on or near the node being sought, reaching within 0.6 m of it is
    // physically impossible: the unit is pulled in by seek and shoved out
    // by separation, forever, and `_pathIndex` never advances. It orbits a
    // waypoint it can never consume. The bigger the monster, the bigger
    // the exclusion zone and the more nodes become unreachable -- exactly
    // the size dependence the creator reported, and something no amount of
    // steering-side tuning could ever have fixed, because the steering was
    // being asked to satisfy an unsatisfiable arrival test.
    //
    // Three rules replace the single distance check, all pure functions
    // exercised directly by `Tools~/SteerVerify`:

    /// <summary>Floor on the arrive radius -- the original flat
    /// `MonsterAgent.GroundArriveDist`, kept so small units keep their
    /// existing precision exactly.</summary>
    public const float BaseArriveDist = 0.6f;

    /// <summary>A unit is "at" a node once its own BODY covers it, rather
    /// than once its origin is within 60 cm of it. Scales with the
    /// creature, which a flat constant never did.</summary>
    public static float ArriveDistFor(float radius)
    {
        return Mathf.Max(BaseArriveDist, radius);
    }

    /// <summary>How long a unit may fail to get closer to its current
    /// node, WHILE another unit is in its way, before it gives up on that
    /// node and takes the next one. Long enough that ordinary jostling
    /// never trips it, short enough that a jam never becomes the
    /// permanent stall the creator reported.</summary>
    public const float WaypointStallSeconds = 1f;

    /// <summary>Metres of improvement that counts as real progress toward
    /// the current node (below this is noise).</summary>
    public const float WaypointProgressEpsilon = 0.25f;

    /// <summary>Per-unit bookkeeping for the rule below. Same "plain
    /// struct on the owner, no dictionary in here" shape as
    /// `SteeringMemory`; `MonsterAgent` keeps one, reset whenever it
    /// adopts a new node.</summary>
    public struct WaypointProgress
    {
        public float BestDistance;   // closest this unit has EVER been to the current node
        public float StalledFor;
    }

    /// <summary>Should the caller consume the current path node and move
    /// on? True on any of three grounds:
    ///
    /// 1. **Arrived** -- within `arriveDist` (now body-scaled).
    /// 2. **Overshot** -- the node is now BEHIND the leg's own direction
    ///    of travel. A unit shoved sideways past a node used to turn
    ///    around and go back for it, fighting the very crowd that pushed
    ///    it there; there is nothing at a path node worth returning for.
    /// 3. **Contested and not closing** -- no progress for
    ///    `WaypointStallSeconds` while another unit is in the way. This is
    ///    the one that breaks the orbit: a node standing inside someone
    ///    else's body simply stops being sought.
    ///
    /// Rule 3 is gated on `contested` deliberately. Distance-to-goal
    /// refusing to fall ALSO describes a perfectly healthy chase of a
    /// target that is running away at the same speed -- and abandoning
    /// the node then would be the AI giving up on the player, the exact
    /// opposite of what's wanted. "Not closing" alone is not evidence of
    /// a jam; "not closing while someone is physically in the way" is.
    ///
    /// `progress` is mutated in place (it IS the per-unit memory) and
    /// reset on every advance, so the next node starts with a clean
    /// baseline.</summary>
    public static bool ShouldAdvanceWaypoint(ref WaypointProgress progress, Vector3 pos, Vector3 node,
        Vector3 legDir, float arriveDist, bool contested, float dt)
    {
        var to = node - pos;
        to.y = 0f;
        var dist = to.magnitude;

        if (dist < arriveDist) { ResetProgress(ref progress); return true; }

        // overshot: the node is behind the direction this leg travels in
        if (legDir.sqrMagnitude > 1e-6f && Vector3.Dot(to, legDir) < 0f) { ResetProgress(ref progress); return true; }

        if (dist < progress.BestDistance - WaypointProgressEpsilon)
        {
            progress.BestDistance = dist;
            progress.StalledFor = 0f;
            return false;
        }

        if (!contested) { progress.StalledFor = 0f; return false; }

        progress.StalledFor += dt;
        if (progress.StalledFor < WaypointStallSeconds) return false;
        ResetProgress(ref progress);
        return true;
    }

    /// <summary>Arms `progress` for a freshly adopted node -- "closest
    /// ever" starts at infinity so the very first frame counts as
    /// progress rather than as a stall.</summary>
    public static void ResetProgress(ref WaypointProgress progress)
    {
        progress.BestDistance = float.MaxValue;
        progress.StalledFor = 0f;
    }

    /// <summary>Ported verbatim from `RuntimeCityBuilder.ApplySeparation`'s
    /// per-pair math (unchanged formula: once inside Radius+Radius+
    /// groupSpacing, push half the overlap toward daylight) -- including its
    /// cumulative order: each neighbour after the first is checked against
    /// the position already nudged by earlier neighbours in the same call,
    /// not the original position, exactly as the old inline loop did.
    /// Returns the NET displacement (final position minus start) instead of
    /// writing to `self.transform` directly, so the same computation backs
    /// both the hard positional correction (`ApplySeparation`) and the
    /// softened blend used while path-following (`Combine`).</summary>
    public static Vector3 SeparationForce(UnitCombat self, List<UnitCombat> neighbours, float groupSpacing)
    {
        var start = self.transform.position;
        var p = start;
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            var d = p - c.transform.position;
            d.y = 0f;
            var minDist = self.Radius + c.Radius + groupSpacing;
            var dist = d.magnitude;
            if (dist < minDist && dist > 1e-3f)
                p += d / dist * ((minDist - dist) * 0.5f);
        }
        return p - start;
    }

    /// <summary>docs/25 Phase C: time-to-collision (RVO-lite) avoidance,
    /// replacing Phase B's ahead-cone `AvoidanceBias` (removed -- nothing
    /// else called it). For each neighbour, predicts the closest approach
    /// between self and that neighbour assuming BOTH keep their current
    /// velocity -- self's assumed velocity is `fwd * selfSpeed` (what it's
    /// about to do if nothing steers it away), the neighbour's is its own
    /// published `LastVelocity` (see that field's doc comment for the
    /// stationary-tank default). A neighbour already moving apart, or not
    /// projected to close within `Horizon` seconds, contributes nothing --
    /// that's what makes this predictive rather than reactive: something
    /// far away but closing fast steers around NOW, not only once it's
    /// within a fixed spatial ring the way the old ahead-cone worked.
    /// Already-overlapping neighbours are deliberately skipped here --
    /// that's `SeparationForce`'s job (a collision that already happened,
    /// not one being predicted); mixing the two signals for the same pair
    /// would double up the response for no gain.
    ///
    /// 2026-08 root-cause pass: now returns an `AvoidanceVote` (a SIGNED
    /// LATERAL SCALAR plus who the dominant threat is) instead of a raw
    /// Vector3. No arithmetic changed -- the old return value was always
    /// `right * (the scalar this now returns)`, since every term it
    /// accumulates lies along `right` by construction. Naming the scalar
    /// is what lets `Combine` apply left/right commitment to it (a sign
    /// is a thing you can hold; a vector isn't) and what lets it identify
    /// the ONE neighbour a held decision belongs to.</summary>
    public static AvoidanceVote PredictiveAvoidance(UnitCombat self, Vector3 selfVel, Vector3 fwd, List<UnitCombat> neighbours)
    {
        var right = new Vector3(fwd.z, 0f, -fwd.x);   // fwd rotated -90 about up
        var pos = self.transform.position;
        var vote = new AvoidanceVote();
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            var relPos = c.transform.position - pos;
            relPos.y = 0f;
            var bodyRadius = self.Radius + c.Radius;
            var combined = bodyRadius + AvoidancePadding;
            var dist = relPos.magnitude;
            // only skip once bodies are ACTUALLY overlapping -- that's
            // SeparationForce's job. Staying active all the way down to
            // bodyRadius (not the wider padded `combined`) matters: cutting
            // this off at `combined` left a dead zone where a closing
            // neighbour was too close for predictive avoidance to still
            // apply but not yet overlapping enough for separation to react
            // either -- a standalone harness caught this as a unit barely
            // slowing at all on approach to a blocker.
            if (dist < bodyRadius) continue;

            // relVel is the NEIGHBOUR's velocity relative to self, matching
            // relPos's same other-minus-self convention -- so relPos(t) =
            // relPos + relVel * t actually predicts the neighbour's future
            // position relative to self (getting the sign backwards here
            // makes every genuinely-closing pair compute a negative t and
            // get silently discarded as "already past," which is exactly
            // the bug a standalone harness caught: a unit walking straight
            // at a stationary blocker never triggered ANY predictive
            // response).
            var relVel = c.LastVelocity - selfVel;
            relVel.y = 0f;
            var relSpeedSq = relVel.sqrMagnitude;
            if (relSpeedSq < 1e-4f) continue;   // not closing -- nothing to predict

            var t = -Vector3.Dot(relPos, relVel) / relSpeedSq;
            if (t < 0f || t > Horizon) continue;   // closest approach already passed, or too far out to matter yet

            var closest = relPos + relVel * t;
            var closestDist = closest.magnitude;
            if (closestDist >= combined) continue;   // projected to clear -- no response needed

            // steer to the side away from the blocker's CURRENT position.
            // Near-exact head-on (onRight close to zero) is recomputed
            // from scratch every frame with no memory of which side was
            // picked last frame -- ordinary positional noise (separation,
            // a neighbour's own avoidance push, group alignment/cohesion
            // pulling headings around) can flip `onRight`'s sign
            // independently frame to frame right at that boundary, and
            // since it's evaluated fresh on BOTH units in the pair, they
            // can pick MISMATCHED sides. Real, but NOT the dominant
            // mechanism for the reported "spin" symptom in the common
            // case -- see this file's own class header's 2026-08
            // CORRECTION for the full story: for a lone pair,
            // `Alignment`'s `OpposingHeadingCutoff` (below) is what
            // actually fixes it, this deadband alone does nothing. This
            // tie-break still measurably helps once separation/avoidance
            // are the only forces at play (a tight multi-unit "scrum"
            // with flocking bypassed), and helps somewhat even with
            // flocking active. Within `TieBreakDeadbandFor(combined)`
            // (2026-08 CORRECTION: scaled to THIS pair's own size, not a
            // flat constant -- see that method's own doc for why a fixed
            // meters value under-covered larger monsters) of that
            // boundary, fall back to a per-PAIR-stable tie-break instead
            // of the noisy geometric sign -- built from `Mathf.Min` of
            // both units' InstanceIDs so it evaluates to the exact same
            // value regardless of which of the two is `self` this call
            // (unlike a naive `self &lt; c` compare, which would flip
            // between the pair's own two Combine calls and defeat the
            // whole point). Once a pair is this close to head-on, this
            // is the ENTIRE deciding vote: "one clockwise, one counter-
            // clockwise" (the creator's own suggested fix), same
            // structural idea TrafficCar.cs already uses for lane
            // assignment, just keyed off identity instead of a fixed
            // road lane. Clearly lopsided approaches (most real
            // encounters) are untouched -- the geometric signal still
            // decides those exactly as before.
            var deadband = TieBreakDeadbandFor(combined);
            var onRight = Vector3.Dot(relPos, right);
            if (Mathf.Abs(onRight) < deadband)
            {
                var pairLowId = Mathf.Min(self.GetInstanceID(), c.GetInstanceID());
                onRight = (pairLowId % 2 == 0) ? deadband : -deadband;
            }
            var side = onRight > 0f ? -1f : 1f;
            var urgency = (1f - t / Horizon) * (combined - closestDist) / combined;
            vote.Lateral += side * urgency;

            // remember WHICH neighbour is driving this frame's decision, so
            // Combine's side commitment can be tied to that specific
            // encounter rather than to a bare timer (see SteeringMemory).
            if (urgency > vote.DominantUrgency)
            {
                vote.DominantUrgency = urgency;
                vote.DominantThreatId = c.GetInstanceID();
            }
        }
        return vote;
    }

    /// <summary>What `PredictiveAvoidance` decided this frame. `Lateral`
    /// is signed in `right`'s own sense, so the caller reconstructs the
    /// old vector as `right * Lateral`; the dominant-threat fields exist
    /// only so `Combine` can decide whether a previously committed side
    /// still refers to the same encounter.</summary>
    public struct AvoidanceVote
    {
        public float Lateral;
        public int DominantThreatId;
        public float DominantUrgency;
    }

    /// <summary>Clamps `dir` to within `MaxDeflection` of `fwd`, keeping
    /// whichever side of `fwd` it was already on. THE anti-circling
    /// primitive -- see `MaxDeflection` and this class's header.
    ///
    /// Applied to the FINAL blended heading rather than to any one input
    /// term on purpose: it is the total deflection that decides whether a
    /// unit still makes progress, and capping terms individually would
    /// still let three of them stack past 90 degrees, which is exactly
    /// how the orbit arose. Because the result is rebuilt from `fwd` and
    /// `right` (not renormalized from a squashed vector), a unit pinned
    /// against the cone edge steers along it smoothly rather than
    /// chattering across it.
    ///
    /// A pure function of its arguments, no state, no engine calls --
    /// `Tools~/SteerVerify` exercises it directly.</summary>
    public static Vector3 ClampToCone(Vector3 dir, Vector3 fwd)
    {
        var along = Vector3.Dot(dir, fwd);
        if (along >= MaxDeflectionCos) return dir;   // inside the cone already: untouched
        var right = new Vector3(fwd.z, 0f, -fwd.x);
        var side = Vector3.Dot(dir, right) >= 0f ? 1f : -1f;
        return (fwd * MaxDeflectionCos + right * (side * MaxDeflectionSin)).normalized;
    }

    /// <summary>docs/23 §5: "match average heading of groupmates within
    /// 12 m" -- a groupmate is a same-`Faction` (docs/23's own words are
    /// "units sharing an order group," but this codebase has no formal
    /// order-group concept yet, the same thing docs/27 Phase B already
    /// deferred for queued group moves; same-Faction is the existing,
    /// already-used stand-in -- `UnitCombat.Faction` is how the codebase
    /// already tells "monster" from "human" everywhere else, e.g.
    /// `NearestEnemyOf`) neighbour within <see cref="GroupmateRadius"/> --
    /// 2026-08: AND not moving opposed to `fwd` (<see
    /// cref="OpposingHeadingCutoff"/>'s own comment explains why:
    /// same-Faction alone let two OPPOSING squads pull each other's
    /// headings together while fighting to avoid each other). Each
    /// groupmate's heading is its own published `LastVelocity` (the same
    /// field <see cref="PredictiveAvoidance"/> already reads, no new
    /// per-frame publish needed) -- a stationary groupmate contributes
    /// nothing (no heading to align to, and never excluded by the
    /// opposing-heading check either), matching `PredictiveAvoidance`'s
    /// own "not closing -- nothing to predict" idiom. Returns a
    /// normalized direction, or zero if there's nobody to align with or
    /// their headings cancel out.</summary>
    public static Vector3 Alignment(UnitCombat self, Vector3 fwd, List<UnitCombat> neighbours)
    {
        var sum = Vector3.zero;
        var counted = 0;
        var radiusSq = GroupmateRadius * GroupmateRadius;
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            if (c.Faction != self.Faction) continue;
            var d = c.transform.position - self.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > radiusSq) continue;
            var v = c.LastVelocity;
            v.y = 0f;
            if (v.sqrMagnitude < 1e-6f) continue;
            var vDir = v.normalized;
            if (Vector3.Dot(vDir, fwd) < OpposingHeadingCutoff) continue;
            sum += vDir;
            counted++;
        }
        if (counted == 0) return Vector3.zero;
        var avg = sum / counted;
        return avg.sqrMagnitude < 1e-9f ? Vector3.zero : avg.normalized;
    }

    /// <summary>docs/23 §5: "gentle pull toward group centroid, capped so
    /// it never fights the path." Same groupmate definition as
    /// <see cref="Alignment"/> (same-Faction, within
    /// <see cref="GroupmateRadius"/>, 2026-08: and not moving opposed to
    /// `fwd` -- see <see cref="OpposingHeadingCutoff"/>'s own comment;
    /// pulling toward an oncoming squad's centroid is exactly as wrong as
    /// aligning toward its heading was). A stationary neighbour is never
    /// excluded by the heading check (no heading to be opposed WITH), so
    /// an idle same-Faction unit still pulls the centroid same as before.
    /// Always returns a UNIT-length direction (or zero) regardless of how
    /// far away the centroid actually is -- the cap docs/23 asks for is
    /// <see cref="CohesionWeight"/>'s small blend weight in
    /// <see cref="Combine"/>, not a distance-dependent magnitude here (the
    /// same "return a bounded unit vector, let the weighted blend limit
    /// it" shape <see cref="PredictiveAvoidance"/>'s own `avoid` term and
    /// `Combine`'s `sepBias` already use).</summary>
    public static Vector3 Cohesion(UnitCombat self, Vector3 fwd, List<UnitCombat> neighbours)
    {
        var centroidSum = Vector3.zero;
        var counted = 0;
        var radiusSq = GroupmateRadius * GroupmateRadius;
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            if (c.Faction != self.Faction) continue;
            var d = c.transform.position - self.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > radiusSq) continue;
            var v = c.LastVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 1e-6f && Vector3.Dot(v.normalized, fwd) < OpposingHeadingCutoff) continue;
            centroidSum += c.transform.position;
            counted++;
        }
        if (counted == 0) return Vector3.zero;
        var centroid = centroidSum / counted;
        var toCentroid = centroid - self.transform.position;
        toCentroid.y = 0f;
        return toCentroid.sqrMagnitude < 1e-6f ? Vector3.zero : toCentroid.normalized;
    }

    /// <summary>The FollowPath entry point (docs/25 Phase B, extended by
    /// Phase C, and docs/23 §5 Phase 5): blends seek, separation-as-a-force,
    /// predictive avoidance, and (new) alignment/cohesion into one steering
    /// direction, plus a speed scale. A unit with a clear predicted path,
    /// no neighbour inside its separation envelope, and no same-Faction
    /// groupmates nearby steers at full speed straight toward `desiredDir`
    /// -- BYTE-FOR-BYTE the same as before this phase, since `Alignment`/
    /// `Cohesion` both return exactly `Vector3.zero` in that case; the new
    /// terms only ever contribute once a unit actually has groupmates
    /// within `GroupmateRadius`, and even then at docs/23 §5's own soft
    /// weights. `PredictiveAvoidance`/`SeparationForce`'s own headers still
    /// explain why NEITHER of the two ORIGINAL terms (nor these two new
    /// ones) replaces `RuntimeCityBuilder.ApplySeparation`'s hard
    /// positional correction, which keeps running unconditionally
    /// regardless of this call.</summary>
    public static SteeringResult Combine(UnitCombat self, Vector3 desiredDir, float selfSpeed, List<UnitCombat> neighbours, float groupSpacing, float now)
    {
        var fwd = new Vector3(desiredDir.x, 0f, desiredDir.z);
        if (fwd.sqrMagnitude < 1e-4f) return new SteeringResult { Direction = desiredDir, SpeedScale = 1f };
        fwd = fwd.normalized;

        // docs/25 Phase D escalation (2026-08): DeadlockManager has told
        // this unit to stop negotiating and push straight through for a
        // moment. Soft avoidance and give-way are exactly the terms that
        // keep a symmetric standoff symmetric, so they -- and ONLY they --
        // are suspended; separation still runs below, and
        // `RuntimeCityBuilder.ApplySeparation` still runs unconditionally
        // afterwards, so "ignores avoidance" never means "walks through
        // people." See UnitCombat.PushThroughUntil.
        var pushingThrough = now < self.PushThroughUntil;

        var selfVel = fwd * selfSpeed;
        var right = new Vector3(fwd.z, 0f, -fwd.x);
        var vote = pushingThrough
            ? new AvoidanceVote()
            : PredictiveAvoidance(self, selfVel, fwd, neighbours);

        // ---- left/right commitment (hysteresis) -----------------------
        // The raw geometric sign is recomputed from scratch every frame
        // and, in a crowd, flips freely as neighbours shuffle -- the
        // "frame-to-frame direction flipping" that reads as jitter and
        // stops either unit ever committing. Hold the sign for
        // SideCommitSeconds as long as the SAME neighbour is still the
        // dominant threat; take the magnitude live, so urgency still
        // responds instantly, only the DECISION is sticky.
        var lateral = vote.Lateral;
        if (vote.DominantThreatId != 0)
        {
            var committed = self.Steering.Side != 0
                && self.Steering.SideThreatId == vote.DominantThreatId
                && now < self.Steering.HoldUntil;
            if (committed)
                lateral = Mathf.Abs(lateral) * self.Steering.Side;
            else
                self.Steering.Side = (sbyte)(lateral >= 0f ? 1 : -1);
            self.Steering.SideThreatId = vote.DominantThreatId;
            self.Steering.HoldUntil = now + SideCommitSeconds;   // refresh while the encounter lasts
        }
        var avoid = right * lateral;

        var sepPush = SeparationForce(self, neighbours, groupSpacing);
        var sepBias = sepPush.sqrMagnitude > 1e-6f
            ? sepPush.normalized * Mathf.Min(1f, sepPush.magnitude / Mathf.Max(0.01f, self.Radius))
            : Vector3.zero;
        var alignBias = Alignment(self, fwd, neighbours);
        var cohesionBias = Cohesion(self, fwd, neighbours);

        var dir = avoid.sqrMagnitude < 1e-6f && sepBias.sqrMagnitude < 1e-6f
            && alignBias.sqrMagnitude < 1e-6f && cohesionBias.sqrMagnitude < 1e-6f
            ? fwd
            : (fwd + avoid * 1.2f + sepBias * 0.8f + alignBias * AlignmentWeight + cohesionBias * CohesionWeight).normalized;

        // 2026-08 root-cause pass -- THE anti-circling step. Everything
        // above keeps its original weights and its full influence; this
        // only refuses to let their SUM turn the unit so far off its goal
        // that it stops making progress at all. Without it, three
        // neighbours' worth of purely-lateral avoidance deflects the
        // heading past 90 degrees and the unit orbits its own goal
        // forever, which is precisely the reported bug. See ClampToCone /
        // MaxDeflection.
        dir = ClampToCone(dir, fwd);

        // speed modulation (docs/25 Phase C): alignment between the chosen
        // heading and the original seek direction is a cheap, principled
        // proxy for "how much is avoidance/separation dominating this
        // frame" -- a unit arcing gently around something eases off a
        // little, one fighting a near-reversal eases off a lot. Floored,
        // never fully stops here (DeadlockManager, Phase D, owns that).
        var alignment = Vector3.Dot(dir, fwd);
        var speedScale = Mathf.Clamp(alignment, MinSpeedScale, 1f);

        // 2026-08 (creator follow-up: "monsters are still circling each
        // other... pick one to give way to the other"): applied on top of
        // (not instead of) the alignment-based easing above -- a give-way
        // pair also gets the normal avoidance/separation steering nudge
        // via `dir`, this only additionally throttles the yielding one's
        // forward progress. See GiveWaySpeedScale's own header for why
        // this exists alongside, rather than replacing, the tie-break
        // already in PredictiveAvoidance.
        //
        // 2026-08: skipped entirely while pushing through -- a unit the
        // deadlock layer has explicitly told to force the issue must not
        // simultaneously be throttled to 15% speed by the very standoff
        // it was sent to break.
        if (!pushingThrough)
            speedScale = Mathf.Min(speedScale, GiveWaySpeedScale(self, fwd, neighbours));

        // "was anyone actually in my way this frame" -- read straight off
        // the two terms that already answered that question, so this costs
        // nothing extra. See SteeringResult.Contested.
        var contested = vote.DominantUrgency > 0f || sepPush.sqrMagnitude > 1e-6f;

        return new SteeringResult { Direction = dir, SpeedScale = speedScale, Contested = contested };
    }
}
