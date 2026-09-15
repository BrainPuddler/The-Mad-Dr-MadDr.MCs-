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
/// 2026-09-16: `SimpleCameraRig.maxHeight` sits at 150 m (docs/39 §1.2,
/// an interim stopgap for the §11 item 4 SRP-batching break), well below
/// `MapHeight` below -- so `Band.Map` is currently unreachable in play,
/// same known-and-flagged situation as item 5's zone-conditional rules.
/// The thresholds themselves are left at their real docs/39 §1.2 values
/// rather than temporarily lowered to match, so this keeps working
/// unmodified once `maxHeight` is raised back.
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
}
