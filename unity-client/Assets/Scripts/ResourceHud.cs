using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// The live component economy (docs/05 + docs/23 §3: Blood/Fuel/Ichor/
/// Bones/Parts/Brains) plus supply, entirely invisible in Unity until
/// now despite being real, tested sim state since Phase 1 (supply) and
/// Phase 3 (wallet caps) -- the exact same "sim ready, display missing"
/// gap every other HUD element built this session closed for its own
/// system (LumenHud for mana/emitters, BuildMenuHud for costs). The
/// build menu already previews individual costs against
/// <see cref="SimBridge.PlayerWallet"/>; this is the always-visible
/// "where do I actually stand right now" readout docs/05's own six
/// currencies never had one of.
///
/// Deliberately a SEPARATE panel from <see cref="LumenHud"/>, not folded
/// into it -- docs/03's own framing is explicit that mana (energy) and
/// components (material) are never interchangeable, and every other
/// system in this project already keeps them as genuinely separate
/// concerns (`PlayerState.Wallet` vs `PlayerState.Mana`, different
/// grant/spend methods, different caps). Showing them in one glance-
/// panel would blur a distinction the sim itself is careful to keep.
///
/// IMGUI, same as every other HUD element in this project (see
/// HudStatus's header for why). Default corner is top-right, same as
/// <see cref="AnalogClockHud"/> -- `topOffsetPixels` exists specifically
/// to clear the clock's own default footprint in that corner; like
/// every other HUD element here, the exact final layout in a busy scene
/// is a developer tuning knob, not something this environment (no Unity
/// Editor) can verify by eye.
/// </summary>
public class ResourceHud : MonoBehaviour
{
    public enum ScreenCorner { BottomLeft, BottomRight, TopLeft, TopRight }

    [Header("Data source")]
    public SimBridge bridge;
    public int localPlayerIndex = 0;

    [Header("Placement (default top-right, below AnalogClockHud)")]
    public ScreenCorner corner = ScreenCorner.TopRight;
    public Vector2 marginPixels = new Vector2(16f, 16f);
    [Tooltip("Extra vertical offset from the corner margin -- default clears AnalogClockHud's own default footprint (screen-shorter-dimension * 0.1 sizeFraction, plus its own margin) in the same corner. Nudge live in the Inspector if a scene's actual clock size/margin differs.")]
    public float topOffsetPixels = 210f;
    public float panelWidth = 220f;
    [Tooltip("Bypasses the corner presets entirely for pixel-exact placement anywhere on screen.")]
    public bool useCustomPosition = false;
    public Vector2 customTopLeftPixels = new Vector2(16f, 226f);

    [Header("Colors")]
    public Color backingColor = new Color(0.02f, 0.02f, 0.02f, 0.65f);
    public Color labelColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color cappedColor = new Color(1f, 0.75f, 0.4f, 1f);   // wallet sitting AT its cap

    // docs/05's own resource order: energy currencies first, then the
    // shared construction/RPG currencies -- matches ResourceKind's own
    // declared order, not alphabetical or usage-frequency.
    private static readonly ResourceKind[] Kinds =
    {
        ResourceKind.Blood, ResourceKind.Fuel, ResourceKind.Ichor,
        ResourceKind.Bones, ResourceKind.Parts, ResourceKind.Brains,
    };
    private static readonly string[] Icons = { "🩸", "⛽", "🧪", "🦴", "🔧", "🧠" };

    private const float Padding = 10f;
    private const float LineHeight = 18f;

    public void Init(SimBridge simBridge, int playerIndex)
    {
        bridge = simBridge;
        localPlayerIndex = playerIndex;
    }

    private void OnGUI()
    {
        if (bridge == null || !bridge.HasMatch) return;
        var prevMatrix = UiScale.Begin();

        var supplyUsed = bridge.PlayerSupplyUsed(localPlayerIndex);
        var supplyCap = bridge.PlayerSupplyCap(localPlayerIndex);
        var supplyText = "Supply " + supplyUsed + "/" + supplyCap;

        // 2026-08 (creator direction: "Steel is Bone"): the local
        // player's own faction-themed name per kind (ResourceFactionSkin,
        // same skin-layer split BuildingFactionSkin already established
        // for building names) -- a Human Army player reads "Steel" where
        // a Mad Doctor reads "Bones" for the identical underlying
        // ResourceKind.Bones wallet.
        var faction = bridge.PlayerFaction(localPlayerIndex);
        var lines = new string[Kinds.Length];
        for (var i = 0; i < Kinds.Length; i++)
        {
            var amount = bridge.PlayerWallet(localPlayerIndex, Kinds[i]);
            var cap = bridge.PlayerWalletCap(localPlayerIndex, Kinds[i]);
            var name = ResourceFactionSkin.NameFor(Kinds[i], faction);
            lines[i] = Icons[i] + " " + name + " " + amount + (cap < int.MaxValue ? "/" + cap : "");
        }

        // 2026-08 (creator direction: "make sure the resource text is
        // never clipped"): `panelWidth` used to be a fixed Inspector
        // guess (220px) regardless of how long the actual wallet numbers
        // got -- fine at small values, but nothing stopped a cap/amount
        // from growing past whatever 220px could fit. Measured against
        // the ACTUAL text every line will draw this frame (CalcSize, same
        // fix pattern the build-menu info line and the Minimap legend
        // both already use) and widened to fit -- `panelWidth` is now a
        // FLOOR, not a fixed value, so short numbers still get the
        // original compact panel and nothing is ever silently clipped.
        var style = GUI.skin.label;
        var neededWidth = style.CalcSize(new GUIContent(supplyText)).x;
        for (var i = 0; i < lines.Length; i++)
        {
            var w = style.CalcSize(new GUIContent(lines[i])).x;
            if (w > neededWidth) neededWidth = w;
        }
        var actualPanelWidth = Mathf.Max(panelWidth, neededWidth + Padding * 2f);

        var height = Padding * 2f + LineHeight * (Kinds.Length + 1);   // +1 for the supply row
        var rect = GetPanelRect(height, actualPanelWidth);
        if (rect.width <= 0f) { UiScale.End(prevMatrix); return; }

        GUI.color = backingColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = rect.x + Padding;
        var y = rect.y + Padding;
        var innerWidth = rect.width - Padding * 2f;

        var supplyColor = supplyUsed >= supplyCap && supplyCap > 0 ? cappedColor : labelColor;
        DrawShadowedLabel(new Rect(x, y, innerWidth, LineHeight), supplyText, supplyColor);
        y += LineHeight;

        for (var i = 0; i < Kinds.Length; i++)
        {
            var amount = bridge.PlayerWallet(localPlayerIndex, Kinds[i]);
            var cap = bridge.PlayerWalletCap(localPlayerIndex, Kinds[i]);
            var atCap = cap < int.MaxValue && amount >= cap;
            DrawShadowedLabel(new Rect(x, y, innerWidth, LineHeight), lines[i], atCap ? cappedColor : labelColor);
            y += LineHeight;
        }

        UiScale.End(prevMatrix);
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }

    private Rect GetPanelRect(float height, float width)
    {
        // 2026-08 (creator direction: "the ui is not scaling properly to
        // screen sizes"): reads UiScale.Width/Height (the fixed reference
        // canvas -- see UiScale.cs's own header), not the real Screen.
        // width/height -- OnGUI wraps this whole panel in UiScale.Begin(),
        // so every Rect it returns must be in that SAME reference space
        // or the matrix would double-scale it off-screen. UiScale's own
        // constants are never 0, so the old zero-Screen-size fallback/
        // warning this replaced is no longer a real code path.
        //
        // `width` (2026-08, "make sure the resource text is never
        // clipped"): the caller passes the ACTUAL needed width (measured
        // against this frame's real text, floored at the Inspector
        // `panelWidth`), not the raw `panelWidth` field directly -- so a
        // long wallet number widens the panel instead of clipping.
        var screenW = UiScale.Width;
        var screenH = UiScale.Height;
        if (useCustomPosition) return new Rect(customTopLeftPixels.x, customTopLeftPixels.y, width, height);
        float x, y;
        switch (corner)
        {
            case ScreenCorner.BottomLeft: x = marginPixels.x; y = screenH - marginPixels.y - height; break;
            case ScreenCorner.BottomRight: x = screenW - marginPixels.x - width; y = screenH - marginPixels.y - height; break;
            case ScreenCorner.TopLeft: x = marginPixels.x; y = marginPixels.y + topOffsetPixels; break;
            default: x = screenW - marginPixels.x - width; y = marginPixels.y + topOffsetPixels; break;   // TopRight
        }
        return new Rect(x, y, width, height);
    }
}
