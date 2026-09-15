using UnityEngine;

/// <summary>
/// docs/39 §6/§11 item 6: "the gait/idle/breath ticks run every frame in
/// Close/Normal, every second frame in Overview, and not at all in Map."
/// One shared, self-memoizing per-frame read of the camera's live height
/// (docs/39 §1.2's own band boundaries) so every animated unit's Update
/// can ask a cheap question instead of each doing its own `Camera.main`
/// lookup and threshold math -- same "one shared static answer, many
/// scattered callers" shape as <see cref="GlowPointRegistry"/> and <see
/// cref="DynamicLightBudget"/>, just for a much cheaper question.
///
/// Deliberately reads `Camera.main` directly rather than depending on
/// `RuntimeCityBuilder`'s own Update() to have already run this frame
/// (Unity doesn't guarantee MonoBehaviour Update order across
/// components) -- cached by `Time.frameCount`, so repeated calls within
/// the same frame (dozens of monsters) cost one comparison each after
/// the first.
///
/// `SimpleCameraRig.maxHeight` is 300 m as of 2026-09-16 (docs/39 §1.2 --
/// briefly tightened to 150 m mid-session as a stopgap for the §11 item 4
/// SRP-batching break, raised back once that fix landed), so all three
/// bands below are reachable in play.
/// </summary>
public static class AnimationLodBudget
{
    public enum Band { CloseOrNormal, Overview, Map }

    public const float OverviewHeight = 110f;
    public const float MapHeight = 250f;

    private static int _cachedFrame = -1;
    private static Band _cachedBand = Band.CloseOrNormal;

    public static Band CurrentBand
    {
        get
        {
            var frame = Time.frameCount;
            if (frame != _cachedFrame)
            {
                _cachedFrame = frame;
                var cam = Camera.main;
                var height = cam != null ? cam.transform.position.y : 0f;
                _cachedBand = height >= MapHeight ? Band.Map
                    : height >= OverviewHeight ? Band.Overview
                    : Band.CloseOrNormal;
            }
            return _cachedBand;
        }
    }

    /// <summary>Whether an animation tick should actually run THIS frame
    /// for a unit currently in `band`. Map never ticks; Overview ticks on
    /// even frames only (a global parity, not staggered per-unit -- the
    /// doc's own ask is "every second frame," not smoothed popcorn).
    /// Close/Normal always ticks.</summary>
    public static bool ShouldTick(Band band)
    {
        switch (band)
        {
            case Band.Map: return false;
            case Band.Overview: return (Time.frameCount & 1) == 0;
            default: return true;
        }
    }

    /// <summary>For a caller with SEVERAL animation-tick call sites per
    /// Update (docs/39 §11 item 6's humanoid half -- `HumanoidCombatant`/
    /// `RosterInfantryView`/`Worker` each branch into one of several
    /// `HumanCharacterAnimator.TickXxx` calls depending on state, unlike
    /// `MonsterBody`'s single `UpdateLocomotion` choke point). Call once
    /// per Update, before deciding which `TickXxx` applies this frame:
    /// if it returns true, use `effectiveDt` (already folded with any
    /// accumulated skipped time) for whichever call actually fires; if
    /// false, skip EVERY `TickXxx` call this frame entirely (the caller's
    /// own state-machine/movement/gameplay logic around those calls still
    /// runs as normal -- only the animator call itself is gated).
    /// `skippedDt` is the caller's own per-instance accumulator field,
    /// passed by ref so this stays a pure function with no static
    /// per-unit state of its own.</summary>
    public static bool TryGetAnimDt(ref float skippedDt, float dt, out float effectiveDt)
    {
        if (!ShouldTick(CurrentBand))
        {
            skippedDt += dt;
            effectiveDt = 0f;
            return false;
        }
        effectiveDt = dt + skippedDt;
        skippedDt = 0f;
        return true;
    }
}
