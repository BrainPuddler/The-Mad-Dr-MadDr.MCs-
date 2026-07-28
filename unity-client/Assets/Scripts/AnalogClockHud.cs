using UnityEngine;

/// <summary>
/// Analog time-of-day clock overlay (creator direction, 2026-07): "Give
/// me an analog clock in the top right corner of the screen for the
/// time of day, the arms should sweep, and there should be a ticking
/// grandfather clock swinging arm. but it should only take up 1/10th of
/// the screen" -- then "the clock should be editor resizable and
/// repositionable." IMGUI, same as every other HUD element in this
/// project (see HudStatus's header for why: fine alongside the New
/// Input System, which only replaces the legacy Input class, not
/// OnGUI) -- built with the exact same "bake a texture once, draw
/// stretched rects rotated with GUIUtility.RotateAroundPivot" technique
/// Minimap already uses for its own rotate-with-camera mode.
///
/// Time source is <see cref="DayNightState.CycleProgress"/> (published
/// every frame by LumenCycleController) -- one full Lumen day/night
/// cycle (Dawn->Day->Dusk->Night->Dawn) is treated as ONE 12-hour dial
/// revolution for the hour hand, with the minute hand doing the usual
/// 12 revolutions per hour-hand revolution (the standard analog-clock
/// gear ratio). This is a stylized "how far through today's cycle are
/// we" reading, not meant to line up with any particular real
/// hour-of-day -- there's no requirement that it does.
///
/// Placement/size mirror Minimap's own established pattern exactly
/// (corner + margin presets, `useCustomPosition` for pixel-exact
/// override) so both are tuned the same way. Size is a FRACTION of the
/// shorter screen dimension rather than a fixed pixel count -- that's
/// what actually keeps "only 1/10th of the screen" true across
/// different resolutions/window sizes, where a fixed pixel size
/// wouldn't.
///
/// The pendulum is deliberately a SEPARATE motion from the hands: the
/// hands sweep continuously (smooth rotation straight from
/// CycleProgress, matching "the arms should sweep"); the pendulum
/// instead snaps between two fixed angles once per `tickIntervalSeconds`
/// -- a real ticking cadence, not a smooth swing, matching "a ticking
/// grandfather clock."
///
/// Honesty note (2026-07, no Unity Editor in this environment): the
/// hand/pendulum ANGLE MATH below is pure and was verified via
/// reflection (see docs/12) -- but IMGUI drawing itself (does the baked
/// face texture actually look like a clock face, do the rotated rects
/// actually read as hands, does the pendulum look like it's hanging
/// below the face) has NOT been seen in a real render. This is the
/// first HUD/visual feature built this session with literally zero
/// rendering feedback of any kind (the lighting fixes at least had
/// creator screenshots to reason from) -- expect this to need visual
/// tuning once it's actually on screen.
/// </summary>
public class AnalogClockHud : MonoBehaviour
{
    [Header("Placement (default top-right; developer-tunable anywhere)")]
    public Minimap.ScreenCorner corner = Minimap.ScreenCorner.TopRight;
    public Vector2 marginPixels = new Vector2(16f, 16f);
    [Tooltip("Clock diameter as a fraction of the SHORTER screen dimension -- 0.1 default is 'only take up 1/10th of the screen,' and stays that fraction across resolutions/window sizes unlike a fixed pixel size would.")]
    [Range(0.02f, 0.5f)]
    public float sizeFraction = 0.1f;
    [Tooltip("Bypasses the corner presets entirely for pixel-exact placement anywhere on screen.")]
    public bool useCustomPosition = false;
    public Vector2 customTopLeftPixels = new Vector2(16f, 16f);

    [Header("Pendulum (ticking, deliberately separate from the sweeping hands)")]
    [Tooltip("How far the pendulum swings from center, in degrees, each direction.")]
    [Range(2f, 30f)]
    public float pendulumSwingDeg = 12f;
    [Tooltip("Real seconds between each tick/tock -- 1 = a classic once-a-second cadence.")]
    [Range(0.2f, 3f)]
    public float tickIntervalSeconds = 1f;

    [Header("Colors")]
    public Color faceColor = new Color(0.08f, 0.07f, 0.05f, 0.92f);
    public Color rimColor = new Color(0.78f, 0.67f, 0.43f, 1f);
    public Color hourHandColor = new Color(0.85f, 0.8f, 0.7f, 0.95f);
    public Color minuteHandColor = new Color(0.95f, 0.9f, 0.8f, 0.95f);
    public Color pendulumColor = new Color(0.75f, 0.7f, 0.6f, 0.9f);

    private const int FaceTexRes = 128;
    private Texture2D _faceTex;

    private void Awake()
    {
        BakeFace();
    }

    // ---- pure angle math (kept free of GUI/Texture calls so it can be
    // verified by reflection without a real Unity render -- see the
    // class header's honesty note) ------------------------------------

    /// <summary>Hour hand: one full 360-degree sweep per whole Lumen
    /// cycle, 0 = straight up (dial "12"), increasing clockwise.</summary>
    public static float HourHandDeg(float cycleProgress)
    {
        return Frac01(cycleProgress) * 360f;
    }

    /// <summary>Minute hand: 12 sweeps per whole Lumen cycle -- the same
    /// 12:1 gear ratio a real analog clock's hour and minute hands
    /// have.</summary>
    public static float MinuteHandDeg(float cycleProgress)
    {
        return Frac01(cycleProgress * 12f) * 360f;
    }

    /// <summary>Discrete tick-tock: alternates between +swingDeg and
    /// -swingDeg every `intervalSeconds`, snapping rather than
    /// smoothly swinging -- the "ticking," as distinct from the hands'
    /// continuous sweep.</summary>
    public static float PendulumDeg(float timeSeconds, float intervalSeconds, float swingDeg)
    {
        if (intervalSeconds <= 0f) return swingDeg;
        var step = Mathf.FloorToInt(timeSeconds / intervalSeconds);
        return (step % 2 == 0) ? swingDeg : -swingDeg;
    }

    private static float Frac01(float v)
    {
        var f = v - Mathf.Floor(v);
        return f < 0f ? f + 1f : f;   // Mathf.Floor already handles negative v correctly, but keep this defensive
    }

    // ---- one-time face bake (same "bake once, stretch to any Rect at
    // draw time" technique Minimap uses for its own terrain texture)
    // ----------------------------------------------------------------

    private void BakeFace()
    {
        var pixels = new Color32[FaceTexRes * FaceTexRes];
        var center = (FaceTexRes - 1) * 0.5f;
        var outerR = center - 1f;
        var rimR = outerR - 3f;
        var faceC = (Color32)faceColor;
        var rimC = (Color32)rimColor;
        var clear = new Color32(0, 0, 0, 0);

        for (var y = 0; y < FaceTexRes; y++)
        {
            for (var x = 0; x < FaceTexRes; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * FaceTexRes + x] = d > outerR ? clear : d > rimR ? rimC : faceC;
            }
        }

        // 12 dial ticks, thicker every 3rd (12/3/6/9) -- baked straight
        // into the texture rather than drawn as separate GUI rects each
        // frame, same reasoning Minimap's terrain bake already uses.
        for (var i = 0; i < 12; i++)
        {
            var ang = i * 30f * Mathf.Deg2Rad;
            var thick = i % 3 == 0;
            var len = thick ? 12f : 7f;
            var w = thick ? 1.6f : 0.9f;
            for (var t = 0f; t < len; t += 0.5f)
            {
                var r = rimR - 2f - t;
                var px = center + Mathf.Sin(ang) * r;
                var py = center - Mathf.Cos(ang) * r;
                StampDisc(pixels, px, py, w, rimC);
            }
        }

        _faceTex = new Texture2D(FaceTexRes, FaceTexRes, TextureFormat.RGBA32, false);
        _faceTex.filterMode = FilterMode.Bilinear;
        _faceTex.SetPixels32(pixels);
        _faceTex.Apply(false);
    }

    private static void StampDisc(Color32[] pixels, float cx, float cy, float r, Color32 c)
    {
        var x0 = Mathf.Clamp(Mathf.FloorToInt(cx - r), 0, FaceTexRes - 1);
        var x1 = Mathf.Clamp(Mathf.CeilToInt(cx + r), 0, FaceTexRes - 1);
        var y0 = Mathf.Clamp(Mathf.FloorToInt(cy - r), 0, FaceTexRes - 1);
        var y1 = Mathf.Clamp(Mathf.CeilToInt(cy + r), 0, FaceTexRes - 1);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r * r) pixels[y * FaceTexRes + x] = c;
            }
        }
    }

    // ---- placement (mirrors Minimap.GetScreenRect exactly, size is a
    // fraction of the shorter screen dimension instead of a fixed pixel
    // count) ------------------------------------------------------------

    private Rect GetScreenRect()
    {
        var size = Mathf.Min(Screen.width, Screen.height) * sizeFraction;
        if (useCustomPosition) return new Rect(customTopLeftPixels.x, customTopLeftPixels.y, size, size);
        float x, y;
        switch (corner)
        {
            case Minimap.ScreenCorner.BottomLeft: x = marginPixels.x; y = Screen.height - marginPixels.y - size; break;
            case Minimap.ScreenCorner.BottomRight: x = Screen.width - marginPixels.x - size; y = Screen.height - marginPixels.y - size; break;
            case Minimap.ScreenCorner.TopLeft: x = marginPixels.x; y = marginPixels.y; break;
            default: x = Screen.width - marginPixels.x - size; y = marginPixels.y; break;   // TopRight
        }
        return new Rect(x, y, size, size);
    }

    // ---- draw --------------------------------------------------------

    private void OnGUI()
    {
        if (_faceTex == null) BakeFace();
        var rect = GetScreenRect();
        if (rect.width <= 0f) return;
        var center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);

        GUI.color = Color.white;
        GUI.DrawTexture(rect, _faceTex);

        DrawPendulum(rect);   // behind the hands, same as a real clock case

        DrawHand(center, rect.width, HourHandDeg(DayNightState.CycleProgress), 0.5f, Mathf.Max(1f, rect.width * 0.045f), hourHandColor);
        DrawHand(center, rect.width, MinuteHandDeg(DayNightState.CycleProgress), 0.74f, Mathf.Max(1f, rect.width * 0.03f), minuteHandColor);
    }

    private void DrawHand(Vector2 center, float faceSize, float angleDeg, float lengthFraction, float widthPx, Color color)
    {
        var length = faceSize * 0.5f * lengthFraction;
        var oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angleDeg, center);
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - widthPx * 0.5f, center.y - length, widthPx, length), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.matrix = oldMatrix;
    }

    /// <summary>Pivots just inside the bottom edge of the face and hangs
    /// a rod + bob below it, ticking (not sweeping) left/right --
    /// deliberately a small addition to the face's own footprint, not a
    /// second same-size element, so the whole widget still reads as
    /// "about 1/10th of the screen," not double that.</summary>
    private void DrawPendulum(Rect faceRect)
    {
        var pivot = new Vector2(faceRect.x + faceRect.width * 0.5f, faceRect.yMax - faceRect.height * 0.08f);
        var rodLength = faceRect.height * 0.35f;
        var angle = PendulumDeg(Time.time, tickIntervalSeconds, pendulumSwingDeg);

        var oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, pivot);
        GUI.color = pendulumColor;
        var rodWidth = Mathf.Max(1f, faceRect.width * 0.018f);
        GUI.DrawTexture(new Rect(pivot.x - rodWidth * 0.5f, pivot.y, rodWidth, rodLength), Texture2D.whiteTexture);
        var bobSize = faceRect.width * 0.08f;
        GUI.DrawTexture(new Rect(pivot.x - bobSize * 0.5f, pivot.y + rodLength - bobSize * 0.5f, bobSize, bobSize), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.matrix = oldMatrix;
    }
}
