using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "the ui is not scaling properly to screen
/// sizes, everything should support dynamic scaling and resolutions").
/// Investigated first: every one of this project's 19 `OnGUI()` HUDs
/// draws with raw pixel constants (`new Rect(12f, 140f, ...)`, a 900px-
/// wide status line, a 56px build-menu tile...) -- zero resolution-
/// scaling infrastructure exists anywhere (confirmed via grep: no prior
/// `GUI.matrix`/`ScaleAroundPivot`/reference-resolution code except two
/// LOCAL, unrelated uses in `Minimap`/`AnalogClockHud` for rotating/
/// clipping specific elements, not screen scaling). On any resolution
/// other than whatever each panel happened to be eyeballed at, it reads
/// too small, too large, or clipped off-screen.
///
/// This is the single shared fix, not 19 separate ones: a uniform
/// `GUI.matrix` scale applied once per HUD's `OnGUI()`, the same
/// "author everything against one reference canvas, scale the whole
/// canvas to fit the real screen" idea Unity's own UGUI `CanvasScaler`
/// (Scale With Screen Size mode) uses for the newer UI system -- IMGUI
/// has no built-in equivalent, so this is that pattern hand-rolled for
/// it. Every existing HUD's pixel constants were authored against
/// something in the neighborhood of 1920x1080 (<see cref="ReferenceHeight"/>
/// below); they keep working completely unchanged -- this scales the
/// OUTPUT, not the input, so no HUD's own layout math needs to be
/// rewritten.
///
/// 2026-08 follow-up (creator report: "mini map, clock, and buildings
/// icons, are still off the screen" -- AFTER the fix above already
/// shipped): the first version of this class matched CanvasScaler's
/// "Scale With Screen Size" at `Mathf.Min` of both axis ratios --
/// fit-inside, LETTERBOXED. That's exactly the bug: fitting a fixed
/// 1920x1080 canvas inside a screen with a DIFFERENT aspect ratio (any
/// 16:10 laptop, an ultrawide monitor, a resized/non-16:9 Editor Game
/// view -- none of which are exotic) leaves an unused gap on ONE axis,
/// and since the scale transform anchors at the top-left origin, that
/// gap always falls on the BOTTOM and RIGHT. An element anchored to the
/// bottom or right edge of the REFERENCE canvas therefore lands short of
/// the TRUE bottom/right edge of the real screen -- it doesn't touch the
/// corner it's supposed to, which reads exactly as "off the screen" even
/// though, technically, it's still within `[0, Screen.width] x [0,
/// Screen.height]`. `Minimap` (bottom-left), `AnalogClockHud` (top-right
/// default), and `BuildingNavHud` (bottom-center) are the three worst
/// hit because ALL of them anchor to a bottom or right edge; `HudStatus`/
/// `BuildMenuHud` (top-left) were never affected, since the origin
/// (0,0) doesn't move regardless of aspect ratio -- which is exactly why
/// only the first three were the ones reported.
///
/// **Fix: match HEIGHT, let the reference WIDTH follow the real aspect
/// ratio instead of staying fixed.** <see cref="Height"/> is still a
/// fixed <see cref="ReferenceHeight"/>, but <see cref="Width"/> is now
/// `ReferenceHeight * (Screen.width / Screen.height)` -- the reference
/// canvas's own aspect ratio always exactly equals the real screen's, so
/// scaling it by <see cref="Factor"/> (now simply `Screen.height /
/// ReferenceHeight`, one axis, not a min of two) maps the FULL reference
/// canvas onto the FULL real screen with zero leftover gap on either
/// axis, for any aspect ratio -- the same "Match: Height" option
/// CanvasScaler itself already offers as an alternative to its own
/// default fit-inside mode, chosen here specifically because it's the
/// one that can't produce an edge gap. A corner-anchored HUD now always
/// reaches the true corner; a very wide screen simply gets more
/// reference-space width to lay out in (more room), a very narrow one
/// gets less -- never a mismatch.
///
/// **Usage (screen-space panels -- the vast majority of HUDs):**
/// <code>
/// private void OnGUI()
/// {
///     var prevMatrix = UiScale.Begin();
///     // ... existing drawing code, completely unchanged ...
///     UiScale.End(prevMatrix);
/// }
/// </code>
/// Any layout code that reads `Screen.width`/`Screen.height` to position
/// itself (e.g. right-align, center) must read <see cref="Width"/>/
/// <see cref="Height"/> instead once wrapped -- those return the
/// REFERENCE resolution, which is the coordinate space every Rect passed
/// to GUI calls is now interpreted in (the matrix maps it to the real
/// screen). Using the real `Screen.width` there would double-apply the
/// scale and place things off-screen. This contract is UNCHANGED by the
/// letterbox-gap fix above -- every HUD already wrapped in `Begin()`/
/// `End()` and already reading `Width`/`Height` needed zero further
/// edits; only this class's own internal math changed.
///
/// **NOT for world-anchored UI.** A few HUDs (health bars, the harvest
/// status badge, LumenHud's capture-progress markers) project a live 3D
/// world position via `Camera.WorldToScreenPoint` and draw AT that exact
/// real screen pixel -- that position must stay in true screen space
/// (matrix identity); wrapping it in this scale would double-transform
/// it and detach the marker from the object it's supposed to float over.
/// Those call sites are deliberately left alone -- see each file's own
/// notes at its `WorldToScreenPoint` call site.
/// </summary>
public static class UiScale
{
    /// <summary>The height every existing HUD's own pixel constants were
    /// authored against -- a real, flagged v0.1 estimate (CLAUDE.md's
    /// standing policy for invented tuning numbers), not measured from
    /// any design doc. Chosen because 1080p is the most common desktop
    /// resolution and every existing panel (BuildMenuHud's default
    /// (12,140) placement, HudStatus's ~900px-wide status line, LumenHud's
    /// dial) reads as comfortably-sized, not cramped or lost, at roughly
    /// this size. Unlike width (see <see cref="Width"/>), this stays a
    /// fixed constant -- it's the one axis <see cref="Factor"/> matches
    /// exactly, by design (see this class's own header).</summary>
    public const float ReferenceHeight = 1080f;

    public static float Height { get { return ReferenceHeight; } }

    /// <summary>The reference-space width every wrapped HUD should lay
    /// itself out against -- NOT the real `Screen.width` (see this
    /// class's own header for why substituting the real value would
    /// double-scale). Deliberately NOT a fixed constant: it tracks the
    /// REAL screen's current aspect ratio (`ReferenceHeight * Screen.
    /// width / Screen.height`) so the reference canvas's own shape always
    /// matches the real screen's shape exactly -- the fix for the
    /// "off the screen" letterbox-gap bug this class's header describes.
    /// Falls back to a plain 16:9 assumption if queried outside Play mode
    /// (`Screen.width`/`height` are 0 there).</summary>
    public static float Width
    {
        get
        {
            var h = Screen.height > 0 ? Screen.height : ReferenceHeight;
            var w = Screen.width > 0 ? Screen.width : ReferenceHeight * 16f / 9f;
            return ReferenceHeight * (w / h);
        }
    }

    /// <summary>Uniform scale factor mapping the reference canvas onto
    /// the real screen -- simply `Screen.height / ReferenceHeight`, ONE
    /// axis, not a min of two. Safe to be one axis only because <see
    /// cref="Width"/> already tracks the real aspect ratio, so scaling
    /// height alone automatically scales width correctly too (the
    /// reference canvas's own aspect ratio equals the real screen's by
    /// construction) -- there is no second, mismatched ratio left to
    /// take a `Min` against anymore.</summary>
    public static float Factor
    {
        get
        {
            var h = Screen.height > 0 ? Screen.height : ReferenceHeight;
            return h / ReferenceHeight;
        }
    }

    /// <summary>Applies the scaled matrix and returns the PREVIOUS
    /// `GUI.matrix` -- pass it straight to <see cref="End"/> when this
    /// HUD is done drawing. `GUI.matrix` is IMGUI's own global, shared
    /// across every `MonoBehaviour`'s `OnGUI()` this frame (Unity does
    /// not reset it between them), so every caller MUST restore it
    /// before returning -- an unrestored matrix would silently scale
    /// (or worse, double-scale) whichever HUD's `OnGUI()` happens to run
    /// next.</summary>
    public static Matrix4x4 Begin()
    {
        var previous = GUI.matrix;
        var scale = Factor;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        return previous;
    }

    public static void End(Matrix4x4 previousMatrix)
    {
        GUI.matrix = previousMatrix;
    }

    /// <summary>
    /// 2026-08 (creator report, AnalogClockHud: "the hands are now fly
    /// off the face of the clock, the grandfather clock arm is also not
    /// attached"): rotates around a pivot given in REFERENCE space (the
    /// same space every Rect passed to GUI calls inside a Begin()/End()
    /// block is in) -- plain `GUIUtility.RotateAroundPivot(angle, pivot)`
    /// does NOT do this when `GUI.matrix` is already non-identity (as it
    /// always is inside Begin()/End(), since Begin() sets a uniform
    /// `Factor` scale): its pivot argument is implicitly in whatever
    /// space the CURRENT `GUI.matrix` maps FROM at that composition step,
    /// which for a scale-then-rotate stack is the real, POST-scale screen
    /// space, not reference space. Passing a reference-space point straight
    /// through pivots around `pivot` instead of the pivot's actual
    /// on-screen location `pivot * Factor` -- the further `Factor` is
    /// from 1 (i.e. the further the real screen height is from
    /// <see cref="ReferenceHeight"/>), the further the rotated element
    /// (a clock hand, a pendulum rod) swings from its intended anchor,
    /// exactly matching a hand/pendulum that reads as detached from the
    /// face it's supposed to pivot on. Every caller rotating something
    /// inside a Begin()/End() block should use THIS, not
    /// `GUIUtility.RotateAroundPivot` directly, unless it has first
    /// proven `Factor == 1` for its use case.
    ///
    /// Built as an explicit hand-rolled composition (not a call into
    /// `GUIUtility.RotateAroundPivot` with a pre-scaled pivot) specifically
    /// so its own multiply order is under our control and provably
    /// correct, rather than resting on an assumption about
    /// `RotateAroundPivot`'s undocumented internal composition order with
    /// a pre-existing `GUI.matrix` -- this project has no Unity Editor to
    /// verify that assumption against a real render.
    /// </summary>
    public static void RotateAroundReferencePivot(float angleDeg, Vector2 referencePivot)
    {
        var screenPivot = referencePivot * Factor;
        var rotation = Matrix4x4.TRS(screenPivot, Quaternion.Euler(0f, 0f, angleDeg), Vector3.one)
            * Matrix4x4.TRS(-screenPivot, Quaternion.identity, Vector3.one);
        GUI.matrix = rotation * GUI.matrix;
    }
}
