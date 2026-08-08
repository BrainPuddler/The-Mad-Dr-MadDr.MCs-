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
/// something in the neighborhood of 1920x1080 (`ReferenceWidth`/
/// `ReferenceHeight` below); they keep working completely unchanged --
/// this scales the OUTPUT, not the input, so no HUD's own layout math
/// needs to be rewritten.
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
/// scale and place things off-screen.
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
    /// <summary>The resolution every existing HUD's own pixel constants
    /// were authored against -- a real, flagged v0.1 estimate (CLAUDE.md's
    /// standing policy for invented tuning numbers), not measured from
    /// any design doc. Chosen because it's the most common desktop
    /// resolution and every existing panel (BuildMenuHud's default
    /// (12,140) placement, HudStatus's ~900px-wide status line, LumenHud's
    /// dial) reads as comfortably-sized, not cramped or lost, at
    /// roughly this size.</summary>
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    /// <summary>The reference-space width every wrapped HUD should lay
    /// itself out against -- NOT the real `Screen.width` (see this
    /// class's own header for why substituting the real value would
    /// double-scale). Falls back to <see cref="ReferenceWidth"/> itself
    /// if queried outside Play mode (`Screen.width` is 0 there).</summary>
    public static float Width { get { return ReferenceWidth; } }

    public static float Height { get { return ReferenceHeight; } }

    /// <summary>Uniform scale factor mapping the reference canvas onto
    /// the real screen. `Mathf.Min` of the two axis ratios (never
    /// stretching non-uniformly, never overflowing either dimension) --
    /// the same "fit inside, letterbox the rest" contract UGUI's
    /// `CanvasScaler.matchWidthOrHeight` gives at its 0.5 default,
    /// simplified to the safer of the two extremes (never crops content
    /// off a very wide or very tall window) since no per-HUD "prefer
    /// width" dial exists here to tune.</summary>
    public static float Factor
    {
        get
        {
            var w = Screen.width > 0 ? Screen.width : ReferenceWidth;
            var h = Screen.height > 0 ? Screen.height : ReferenceHeight;
            return Mathf.Min(w / ReferenceWidth, h / ReferenceHeight);
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
}
