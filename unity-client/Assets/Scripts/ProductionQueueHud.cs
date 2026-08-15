using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "factories, like in StarCraft, make x number
/// of units... there is a cued icons with numbers in the lower right hand
/// corner that specify the number of units to make of each type that
/// includes battalions and individual units... a small icon will float
/// over top [of the factory] with a number showing where we are in the
/// build process... click on the factory and can abort all builds"):
///
/// a row of IMGUI tiles anchored to the screen's bottom-right corner --
/// every screen corner but this one is already claimed (minimap +
/// RecallHud/BattalionHud bottom-left, BuildMenuHud/BuildingNavHud
/// top-left/bottom-center) -- one tile per <see
/// cref="GrabCursor.ProductionQueue"/> entry, in build order, each showing
/// its remaining count as a corner badge. The FRONT tile (index 0, the one
/// actually being timed by <see cref="GrabCursor.TickProduction"/>) gets a
/// fill bar underneath showing live progress toward its next spawn.
///
/// Two requested pieces are deliberately folded into this single panel
/// rather than built as separate UI, since there is currently no click-to-
/// select machinery for RTS/SimBuilding buildings at all (<see
/// cref="RuntimeCityBuilder"/>'s own `_buildingByCollider` registry only
/// tracks procedural CityGen buildings, never SimBuilding roster entries --
/// confirmed by reading that registration code, not assumed): (1) "click
/// the factory, queue pops up next to the minimap" becomes this same
/// always-visible panel (there is normally only ever one queue to show, so
/// a second copy gated behind a building click would just be a redundant
/// path to the identical list); (2) "click the factory to abort all
/// builds" becomes this panel's own "Cancel All" button rather than a 3D
/// raycast against a building that isn't selectable yet. Both are scope
/// simplifications, not literal readings of the request -- flagged here so
/// a future pass building real building-selection can revisit them.
///
/// The floating per-Factory progress badge IS built as literally described:
/// a small billboard (same world-to-screen idiom as <see
/// cref="HarvesterMarkerHud"/>/<see cref="HealthBars"/>) hovering over
/// <see cref="GrabCursor.FindAnyOwnCompleteFactory"/>'s position whenever
/// the queue is non-empty, showing the front item's remaining count.
///
/// 2026-08 (creator direction: "When Building a battalion it should show
/// a image of the monster being built, and the battalion name
/// underneath... use the portrait created in the lab. Export that with
/// the monster"): each tile now fills with the item's real portrait
/// (<see cref="GrabCursor.ProductionQueue"/>'s own new `PortraitPng`
/// field -- the Lab's actual WebGL-rendered thumbnail, decoded here into
/// a <see cref="Texture2D"/> and cached per data-URL string so the SAME
/// base64 blob is never decoded twice) instead of the flat tinted square
/// the old text-abbreviation tile used, with the item's `Label`
/// (genome id for a single unit, the battalion's own saved name for a
/// Battalion/LabBattalion) drawn as a real text strip BELOW the tile --
/// "the battalion name underneath," read literally, not squeezed inside
/// the tile itself where a real name would overflow a 44px square. Falls
/// back to the old flat-tile-plus-abbreviation look whenever a portrait
/// is unavailable (an older genome saved before portraits existed, or a
/// failed client-side bake) -- never a hard error, never a blank
/// tile.</summary>
public class ProductionQueueHud : MonoBehaviour
{
    public GrabCursor grabCursor;

    [Header("Placement (docked to the screen's bottom-right corner)")]
    public float tileSize = 44f;
    public float tileGap = 6f;
    public float marginPixels = 12f;
    public float cancelButtonHeight = 20f;
    public int maxVisibleTiles = 8;
    [Tooltip("Extra strip below each tile for the item's name (\"the battalion name underneath\") -- 0 would silently omit it, so this stays a real, non-zero default rather than an opt-in.")]
    public float labelStripHeight = 14f;

    private static Texture2D _tex;

    /// <summary>2026-08 (portrait tiles): one decoded <see cref="Texture2D"/>
    /// per distinct base64 PNG data URL, decoded ONCE and reused for as
    /// long as this component lives -- `GrabCursor.ProductionQueue`
    /// hands back the SAME data-URL string every frame for the same
    /// queued item (it's a plain field read off `StoredGenomeDto`, not
    /// re-fetched), so keying the cache by that string is exactly as
    /// stable as keying by genome id would be, with no extra plumbing to
    /// carry a genome id down to this HUD-only cache. A failed decode
    /// (`LoadImage` returning false -- malformed/truncated data) caches
    /// `null` under that same key rather than retrying every frame,
    /// same "don't hard-fail, don't hot-loop on a known-bad value"
    /// posture the rest of this pipeline's optional-field handling
    /// already follows.</summary>
    private readonly Dictionary<string, Texture2D> _portraitCache = new Dictionary<string, Texture2D>();

    public static bool PointerOver { get; private set; }

    public void Init(GrabCursor grabCursorRef)
    {
        grabCursor = grabCursorRef;
    }

    /// <summary>Decodes a `data:image/png;base64,...` URL into a
    /// `Texture2D`, or returns null (and caches the null) if `png` is
    /// empty or fails to decode. `Texture2D.LoadImage` auto-resizes the
    /// texture to the PNG's own real dimensions -- the Lab's own
    /// `renderThumbnail` always bakes at a fixed square size, so every
    /// portrait this ever sees is already a clean square with nothing
    /// further to crop/letterbox here.</summary>
    private Texture2D PortraitTextureFor(string png)
    {
        if (string.IsNullOrEmpty(png)) return null;
        if (_portraitCache.TryGetValue(png, out var cached)) return cached;

        Texture2D tex = null;
        var commaIdx = png.IndexOf(',');
        if (commaIdx >= 0 && commaIdx + 1 < png.Length)
        {
            try
            {
                var bytes = Convert.FromBase64String(png.Substring(commaIdx + 1));
                var candidate = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (candidate.LoadImage(bytes)) tex = candidate;
                else Destroy(candidate);
            }
            catch (Exception e)
            {
                Debug.LogWarning("ProductionQueueHud: failed decoding a queued item's portrait: " + e.Message);
            }
        }
        _portraitCache[png] = tex;   // caches null too -- see this field's own doc comment
        return tex;
    }

    private const float ProgressBarHeight = 4f;

    private void OnGUI()
    {
        if (grabCursor == null || !grabCursor.HasQueuedProduction) { PointerOver = false; return; }
        if (_tex == null) _tex = Texture2D.whiteTexture;
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;

        var items = new List<(string Label, int Remaining, float Progress, string PortraitPng)>(grabCursor.ProductionQueue);
        var shown = Mathf.Min(items.Count, maxVisibleTiles);

        var panelWidth = shown * tileSize + Mathf.Max(0, shown - 1) * tileGap;
        // 2026-08 (portrait tiles, "the battalion name underneath"): the
        // label strip and its own gap now sit between the tile row and
        // the progress bar/Cancel button -- every row below the tiles
        // shifted down by exactly labelStripHeight + tileGap from the
        // old layout, nothing else about the stacking order changed.
        var panelHeight = tileSize + tileGap + labelStripHeight + ProgressBarHeight + tileGap + cancelButtonHeight;
        var panelX = screenW - marginPixels - panelWidth;
        var panelY = screenH - marginPixels - panelHeight;
        var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        var e = Event.current;
        PointerOver = e != null && panelRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(panelRect, _tex);
        GUI.color = Color.white;

        for (var i = 0; i < shown; i++)
        {
            var item = items[i];
            var tileRect = new Rect(panelX + i * (tileSize + tileGap), panelY, tileSize, tileSize);
            var portrait = PortraitTextureFor(item.PortraitPng);

            if (portrait != null)
            {
                // fills the tile edge-to-edge -- no tint (the portrait IS
                // the visual, unlike the old flat-color placeholder tile)
                GUI.DrawTexture(tileRect, portrait, ScaleMode.ScaleToFit);
            }
            else
            {
                // fallback: the original flat tinted tile + text
                // abbreviation, for an item whose genome has no portrait
                // (an older save, or a failed client-side bake).
                GUI.color = new Color(0.32f, 0.28f, 0.38f, 1f);
                GUI.DrawTexture(tileRect, _tex);
                GUI.color = Color.white;
                GUI.Label(tileRect, Abbrev(item.Label));
            }

            var badgeRect = new Rect(tileRect.xMax - 18f, tileRect.yMax - 16f, 18f, 16f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(badgeRect, _tex);
            GUI.color = Color.white;
            GUI.Label(badgeRect, item.Remaining.ToString());

            // "the battalion name underneath" -- a real text strip below
            // the tile, shadowed for legibility against whatever the
            // portrait's own background happens to be (a flat color tile
            // never needed this; a photographic-ish portrait does).
            var labelRect = new Rect(tileRect.x, tileRect.yMax + 1f, tileSize, labelStripHeight);
            DrawShadowedNameLabel(labelRect, item.Label);

            if (i == 0)
            {
                var barRect = new Rect(tileRect.x, labelRect.yMax + 1f, tileSize, ProgressBarHeight);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(barRect, _tex);
                GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * item.Progress, barRect.height), _tex);
                GUI.color = Color.white;
            }
        }

        if (items.Count > shown)
            GUI.Label(new Rect(panelX, panelY - 16f, panelWidth, 14f), "+" + (items.Count - shown) + " more");

        var cancelRect = new Rect(panelX, panelY + tileSize + tileGap + labelStripHeight + ProgressBarHeight + tileGap, panelWidth, cancelButtonHeight);
        if (GUI.Button(cancelRect, "Cancel All")) grabCursor.CancelAllProduction();

        // 2026-08 (creator direction: "the ui is not scaling properly to
        // screen sizes"): UiScale.End happens HERE, before the factory
        // badge -- that badge is projected from a real 3D world position
        // via Camera.WorldToScreenPoint, which already returns true
        // screen pixels regardless of any GUI.matrix. Drawing it INSIDE
        // the scaled block above would double-apply the scale and detach
        // it from the Factory it's meant to float over -- see this
        // method's own note just below.
        UiScale.End(prevMatrix);
        DrawFactoryBadge(items[0].Remaining);
    }

    /// <summary>Billboards the front item's remaining count over the
    /// Factory it's actually draining into -- a no-op if the player
    /// somehow has queued production with no live Factory (lost it mid-
    /// match) or the camera can't see it. Deliberately drawn in REAL
    /// screen-pixel space (called after UiScale.End, not before) -- its
    /// position comes from Camera.WorldToScreenPoint, already true
    /// screen pixels, not the reference-resolution canvas the rest of
    /// this panel is authored against.</summary>
    private void DrawFactoryBadge(int remaining)
    {
        var cam = Camera.main;
        if (cam == null || grabCursor.builder == null) return;

        var factory = grabCursor.FindAnyOwnCompleteFactory();
        if (factory == null) return;

        var world = grabCursor.builder.WorldOf(factory.Hex) + Vector3.up * 4f;
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return;

        const float badgeSize = 20f;
        var x = sp.x - badgeSize * 0.5f;
        var y = Screen.height - sp.y - badgeSize * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, badgeSize + 2f, badgeSize + 2f), _tex);
        GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(x, y, badgeSize, badgeSize), _tex);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + 1f, badgeSize, badgeSize), remaining.ToString());
    }

    private static string Abbrev(string label)
    {
        if (string.IsNullOrEmpty(label)) return "?";
        return label.Length <= 3 ? label : label.Substring(0, 3);
    }

    /// <summary>"The battalion name underneath" -- the FULL label (a
    /// genome id or a real saved battalion name), not the 3-letter
    /// `Abbrev` the fallback-tile text used, since this strip has real
    /// horizontal room a cramped 44px tile never did. IMGUI wraps/clips
    /// to the Rect automatically at small sizes, so an unusually long
    /// name degrades to a clipped tail rather than overflowing into a
    /// neighboring tile.</summary>
    private static void DrawShadowedNameLabel(Rect rect, string label)
    {
        var text = string.IsNullOrEmpty(label) ? "?" : label;
        var prevAlignment = GUI.skin.label.alignment;
        var prevFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.alignment = TextAnchor.UpperCenter;
        GUI.skin.label.fontSize = 10;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = new Color(0.92f, 0.9f, 0.85f, 1f);
        GUI.Label(rect, text);
        GUI.color = Color.white;
        GUI.skin.label.alignment = prevAlignment;
        GUI.skin.label.fontSize = prevFontSize;
    }
}
