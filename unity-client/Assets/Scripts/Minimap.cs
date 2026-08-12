using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Quick-navigation minimap overlay (creator direction, 2026-07): the
/// generated city (roads/water/ridges/buildings/landmarks) baked once
/// into a small texture, plus every live unit plotted as a blip, a
/// fog-of-war dimming layer (FogOfWar), a camera-frustum indicator, and
/// click-to-navigate / right-click-to-order input -- all through IMGUI
/// (OnGUI), this project's only UI layer (see HudStatus's header for why:
/// fine alongside the New Input System, which only replaces the legacy
/// Input class, not OnGUI).
///
/// Default bottom-left, but every placement number below is a public
/// Inspector field so a developer can put it anywhere on screen (creator
/// direction: "bottom left of the screen but movable anywhere on the
/// screen by the developer") -- `useCustomPosition` breaks fully free of
/// the corner presets for pixel-exact placement.
///
/// Rotation: `rotateWithCamera` spins the whole minimap (texture + blips)
/// so the camera's forward always points "up," Civ/Total-War style,
/// instead of the default fixed north-up. Zoom: mouse wheel over the map
/// narrows the displayed texture region around the camera's current
/// position, from the whole map (zoom 1) down to a close-in view.
///
/// Fog of war: reads FogOfWar's explored/visible-now hex sets and paints
/// unexplored hexes solid black, explored-but-not-currently-visible
/// hexes dimmed, and visible hexes at full color -- `showFogOfWar` is
/// the developer's own on/off tuning knob for this layer specifically
/// (independent of FogOfWar.enabledFog, which governs the underlying
/// system; this one just controls whether the MINIMAP respects it).
/// Enemy-unit blips (tanks) are gated by fog (visible-now only); a
/// player's own monsters always show, the standard RTS convention.
///
/// Legibility pass (2026-08, creator direction: "colour coding the
/// streets. Icons for buildings and make sure non of the text is
/// cropped"), designed after consulting three period-map art-direction
/// research passes (1950s military situation maps, 1950s Rand McNally/
/// AAA road-map conventions, this project's own gothic-palette/journal
/// doctrine) that independently converged on the same two fixes -- see
/// `BakeTerrain`'s own doc comment for the full reasoning: arterial
/// roads now get their own bolder/warmer color and wider stamp than
/// ordinary streets, and the two landmark kinds get a distinct pixel
/// SILHOUETTE (a starburst vs. a hollow ring) instead of same-shape
/// blobs differing only by color. `DrawCompass`/`DrawLegend` add the new
/// "N" chip and a swatch-key panel, both sized off their own actual text
/// via `CalcSize` and clamped to the reference canvas so neither can
/// ever crop off-screen regardless of which corner the map itself sits
/// in -- see those methods' own doc comments for why they're
/// deliberately drawn screen-locked (never rotated with the map, even
/// when `rotateWithCamera` is on).
/// </summary>
public class Minimap : MonoBehaviour
{
    public enum ScreenCorner { BottomLeft, BottomRight, TopLeft, TopRight }

    [Header("Placement (default bottom-left; developer-tunable anywhere)")]
    public ScreenCorner corner = ScreenCorner.BottomLeft;
    // 2026-08 (creator report: "the bottom left map is too close to the
    // corner"): bumped from 16 -- a real, flagged v0.1 tuning number
    // like every other placement default in this file, not a structural
    // fix (the corner-anchoring MATH itself is unchanged; this just
    // gives it more breathing room from the true edge).
    public Vector2 marginPixels = new Vector2(28f, 28f);
    public float sizePixels = 220f;
    [Tooltip("Bypasses the corner presets entirely for pixel-exact placement anywhere on screen.")]
    public bool useCustomPosition = false;
    public Vector2 customTopLeftPixels = new Vector2(16f, 16f);

    [Header("Rotation & Zoom")]
    [Tooltip("Off = fixed north-up. On = the map spins so the camera's forward always points up.")]
    public bool rotateWithCamera = false;
    [Range(1f, 8f)] public float zoom = 1f;
    public float zoomMin = 1f;
    public float zoomMax = 8f;
    public float scrollZoomSpeed = 0.2f;

    [Header("Fog of War")]
    [Tooltip("Whether the MINIMAP itself respects fog of war (the underlying FogOfWar system has its own master switch too).")]
    public bool showFogOfWar = true;

    [Header("Blips")]
    public float unitBlipPixels = 4f;
    public float crowdBlipPixels = 2f;

    /// <summary>True while the pointer sits over the minimap this frame
    /// -- WaypointCommander checks this before its own world-space
    /// select/order handling, so a minimap click doesn't ALSO fire a
    /// 3D-raycast order underneath it (OnGUI's event queue and the New
    /// Input System's Mouse.current are two separate, non-communicating
    /// input paths).</summary>
    public static bool PointerOver { get; private set; }

    /// <summary>Current on-screen rect (corner/margin/custom-position all
    /// resolved) -- lets other bottom-left HUD elements (SelectionHud,
    /// RecallHud, BattalionHud, LabBattalionHud) dock against the
    /// minimap's actual live position instead of duplicating its
    /// placement math.
    ///
    /// 2026-08 (creator direction: "the ui is not scaling properly to
    /// screen sizes"): in `UiScale.Width`/`Height` (reference-resolution)
    /// coordinates, NOT real `Screen.width`/`Screen.height` -- every
    /// caller above reads this from inside its OWN `UiScale`-wrapped
    /// `OnGUI()`, so this needs to already be in that same reference
    /// space for their Rect math (and IMGUI's own matrix-aware mouse hit-
    /// testing) to line up. See `UiScale.cs`'s own header for the full
    /// "why one shared reference canvas" reasoning.</summary>
    public Rect ScreenRect { get { return GetScreenRect(); } }

    private const int TerrainTexRes = 256;
    private const int FogTexRes = 128;
    private const float FogRepaintInterval = 0.4f;

    // 2026-08 (art-direction consultation, "make sure none of the text is
    // cropped" pass): warm sepia-ink instead of flat black -- the same
    // "translucent dark box so text/frame reads against any city color
    // behind it" trick this file already used, just tinted to sit in the
    // journal-page family instead of a neutral HUD gray/black. Used for
    // the map's own frame AND the new compass/legend chips below, so
    // every backing box in this component reads as one consistent
    // material.
    private static readonly Color BackingColor = new Color(0.15f, 0.10f, 0.06f, 0.78f);

    private RuntimeCityBuilder _builder;
    private WaypointCommander _commander;
    private FogOfWar _fog;

    private Texture2D _terrainTex;
    private Texture2D _fogTex;
    private Color32[] _fogPixels;
    private float _minX, _maxX, _minZ, _maxZ;
    private float _fogTimer;

    public void Init(RuntimeCityBuilder builder, WaypointCommander commander, FogOfWar fog)
    {
        _builder = builder;
        _commander = commander;
        _fog = fog;
        BakeTerrain();
    }

    private void Update()
    {
        if (_builder == null || _fog == null || _fogTex == null) return;
        _fogTimer -= Time.deltaTime;
        if (_fogTimer > 0f) return;
        _fogTimer = FogRepaintInterval;
        RepaintFog();
    }

    // ---- one-time terrain bake -----------------------------------------------

    /// <summary>Bakes the whole generated city into a single texture once
    /// at Init -- the city layout never changes after generation (only
    /// building damage state does, which doesn't move anything on a
    /// minimap), so redrawing every hex every OnGUI frame would be pure
    /// waste. Palette matches CityGizmo's Scene-view gizmo (water/ridge/
    /// bridge/building-tier/landmark colors), except roads: the gizmo's
    /// near-black reads fine against a lit 3D scene but disappears at
    /// minimap scale, so roads get a lighter tone here specifically.
    ///
    /// 2026-08 creator direction ("colour coding the streets... icons for
    /// buildings"), designed after consulting three period-map art-
    /// direction passes (1950s military situation maps, 1950s Rand
    /// McNally/AAA road-map conventions, and this project's own gothic-
    /// palette/journal doctrine) -- all three independently converged on
    /// the same two fixes, which is the actual justification for them:
    /// (1) arterial roads (the generator's own Main Street tag) get a
    /// distinct, bolder color from ordinary streets, echoing the "red =
    /// the road that matters" convention every one of those real map
    /// traditions shares; (2) landmark sites get a distinct pixel
    /// SILHOUETTE per kind (a starburst vs. a ring) instead of same-shape
    /// blobs differing only by color -- the old approach had color doing
    /// shape's job, which this project's own "shape = kind, color =
    /// state" doctrine calls out as a real mistake class, not a style
    /// nitpick. Ordinary tiered buildings are left as flat color fill --
    /// they already communicate kind via footprint SIZE (a Large
    /// building's multi-hex footprint is visibly bigger than a Small
    /// one's single hex) as well as color, so they weren't actually
    /// suffering from the same "color doing shape's job" problem
    /// landmarks were.</summary>
    private void BakeTerrain()
    {
        var city = _builder.City;

        _minX = float.MaxValue; _maxX = float.MinValue;
        _minZ = float.MaxValue; _maxZ = float.MinValue;
        void Expand(HexCoord h)
        {
            var w = _builder.WorldOf(h);
            if (w.x < _minX) _minX = w.x;
            if (w.x > _maxX) _maxX = w.x;
            if (w.z < _minZ) _minZ = w.z;
            if (w.z > _maxZ) _maxZ = w.z;
        }
        foreach (var h in city.Roads) Expand(h);
        foreach (var h in city.Water) Expand(h);
        foreach (var h in city.Ridges) Expand(h);
        foreach (var b in city.Buildings) foreach (var h in b.Footprint) Expand(h);
        if (_minX > _maxX) { _minX = 0f; _maxX = 1f; _minZ = 0f; _maxZ = 1f; }   // degenerate empty-map guard

        var pad = (float)HexCoord.HexMeters;
        _minX -= pad; _maxX += pad; _minZ -= pad; _maxZ += pad;

        var pixels = new Color32[TerrainTexRes * TerrainTexRes];
        var ground = (Color32)new Color(0.09f, 0.12f, 0.09f);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = ground;

        // how many texels one hex-width covers, so adjacent hexes' stamps
        // touch without gaps but don't smear into distant neighbors
        var texelsPerHex = TerrainTexRes / Mathf.Max(1f, (_maxX - _minX) / (float)HexCoord.HexMeters);
        var stampRadius = Mathf.Clamp(Mathf.RoundToInt(texelsPerHex * 0.6f), 1, 6);

        void Plot(HexCoord h, Color32 c, int radius = -1)
        {
            if (radius < 0) radius = stampRadius;
            var (px, py) = WorldToTexel(_builder.WorldOf(h), TerrainTexRes);
            for (var dy = -radius; dy <= radius; dy++)
            {
                var y = py + dy;
                if (y < 0 || y >= TerrainTexRes) continue;
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var x = px + dx;
                    if (x < 0 || x >= TerrainTexRes) continue;
                    pixels[y * TerrainTexRes + x] = c;
                }
            }
        }

        // A distinct silhouette per landmark kind, not just a differently-
        // colored blob -- both stamp at ONE texel beyond the base
        // stampRadius (clamped so the hollow center of the ring can't
        // degenerate to a solid square at the smallest map scales) so
        // landmarks read as bigger, more important marks than an ordinary
        // building, on top of their own distinct shape.
        var iconRadius = Mathf.Clamp(stampRadius + 1, 2, 6);

        // Emitter: a solid core (one texel smaller than the full icon
        // radius) plus four single-texel spikes at N/S/E/W beyond it --
        // "broadcasts something," the lighthouse/compass-rose read every
        // one of the three art-direction passes converged on.
        void PlotStar(HexCoord h, Color32 c)
        {
            Plot(h, c, iconRadius - 1);
            var (px, py) = WorldToTexel(_builder.WorldOf(h), TerrainTexRes);
            void Tick(int tx, int ty) { if (tx >= 0 && tx < TerrainTexRes && ty >= 0 && ty < TerrainTexRes) pixels[ty * TerrainTexRes + tx] = c; }
            Tick(px, py - iconRadius); Tick(px, py + iconRadius);
            Tick(px - iconRadius, py); Tick(px + iconRadius, py);
        }

        // CommunityHub: a hollow ring (only the outermost Chebyshev-
        // distance texels of the icon radius) -- "a place people gather
        // AROUND," visually the opposite of the Emitter's solid-core-plus-
        // spikes. Leaves whatever's already painted at the center (ground/
        // road) showing through rather than filling it.
        void PlotRing(HexCoord h, Color32 c)
        {
            var (px, py) = WorldToTexel(_builder.WorldOf(h), TerrainTexRes);
            for (var dy = -iconRadius; dy <= iconRadius; dy++)
            {
                var y = py + dy;
                if (y < 0 || y >= TerrainTexRes) continue;
                for (var dx = -iconRadius; dx <= iconRadius; dx++)
                {
                    var x = px + dx;
                    if (x < 0 || x >= TerrainTexRes) continue;
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) < iconRadius) continue;   // hollow center
                    pixels[y * TerrainTexRes + x] = c;
                }
            }
        }

        foreach (var h in city.Water) Plot(h, new Color(0.15f, 0.30f, 0.85f));
        foreach (var h in city.Ridges) Plot(h, new Color(0.35f, 0.55f, 0.25f));

        // Arterial (the generator's own Main Street tag, docs/12) gets a
        // bolder, warmer color AND a wider stamp than an ordinary
        // residential street -- "red = the road that matters," the one
        // convention every period map this file's own header describes
        // consulting (1950s military situation maps, 1950s Rand McNally/
        // AAA road atlases) shares. Plotted in its own pass, AFTER
        // residential, so an arterial hex is never overdrawn back to the
        // duller residential tone.
        var arterial = new HashSet<HexCoord>(city.ArterialRoads);
        var arterialRadius = Mathf.Min(6, stampRadius + 1);
        foreach (var h in city.Roads)
        {
            if (arterial.Contains(h)) continue;
            Plot(h, new Color(0.50f, 0.48f, 0.43f));   // residential -- lighter than the gizmo's near-black, but duller than arterial so the through-route reads as THE route
        }
        foreach (var h in arterial) Plot(h, new Color(0.62f, 0.20f, 0.15f), arterialRadius);   // oxblood -- USGS/Rand-McNally "highway red," muted for the gothic LUT

        foreach (var br in city.Bridges) foreach (var h in br.Footprint) Plot(h, new Color(0.5f, 0.33f, 0.15f));
        foreach (var b in city.Buildings)
        {
            Color32 c;
            switch (b.Tier)
            {
                case BuildingTier.Medium: c = new Color(0.55f, 0.55f, 0.8f); break;
                case BuildingTier.Large: c = new Color(0.35f, 0.35f, 0.7f); break;
                case BuildingTier.Landmark: c = new Color(0.9f, 0.75f, 0.2f); break;
                default: c = new Color(0.75f, 0.75f, 0.75f); break;
            }
            foreach (var h in b.Footprint) Plot(h, c);
        }
        foreach (var lm in city.Landmarks)
        {
            if (lm.Kind == LandmarkKind.Emitter)
                PlotStar(lm.Site, new Color(0.2f, 0.9f, 0.9f));
            else
                // 2026-08: moved off red (was `new Color(0.9f, 0.2f, 0.2f)`)
                // -- red now means "arterial road" above, and this file's
                // OWN existing color grammar already uses red for a
                // hostile unit blip (see DrawBlips) -- a red CommunityHub
                // marker would collide with both. Warm amber instead.
                PlotRing(lm.Site, new Color(0.85f, 0.55f, 0.15f));
        }

        _terrainTex = new Texture2D(TerrainTexRes, TerrainTexRes, TextureFormat.RGB24, false);
        _terrainTex.filterMode = FilterMode.Point;
        _terrainTex.SetPixels32(pixels);
        _terrainTex.Apply(false);

        _fogTex = new Texture2D(FogTexRes, FogTexRes, TextureFormat.RGBA32, false);
        _fogTex.filterMode = FilterMode.Bilinear;
        _fogPixels = new Color32[FogTexRes * FogTexRes];
        RepaintFog();
    }

    /// <summary>Repaints the WHOLE fog overlay from FogOfWar's current
    /// explored/visible sets -- simple full repaint rather than an
    /// incremental diff, and comfortably cheap at FogTexRes^2 = 16384
    /// hex lookups every 0.4s regardless of map size (a Big City's own
    /// terrain is never re-walked, only this small fixed-resolution
    /// overlay is).</summary>
    private void RepaintFog()
    {
        if (!showFogOfWar || _fog == null)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (var i = 0; i < _fogPixels.Length; i++) _fogPixels[i] = clear;
        }
        else
        {
            for (var y = 0; y < FogTexRes; y++)
            {
                var v = (y + 0.5f) / FogTexRes;
                for (var x = 0; x < FogTexRes; x++)
                {
                    var u = (x + 0.5f) / FogTexRes;
                    var hex = _builder.HexAt(UVToWorld(u, v));
                    Color32 c;
                    if (_fog.IsVisibleNow(hex)) c = new Color32(0, 0, 0, 0);
                    else if (_fog.IsExplored(hex)) c = new Color32(5, 8, 7, 150);
                    else c = new Color32(3, 4, 4, 235);
                    _fogPixels[y * FogTexRes + x] = c;
                }
            }
        }
        _fogTex.SetPixels32(_fogPixels);
        _fogTex.Apply(false);
    }

    // ---- coordinate mapping ---------------------------------------------------

    private (int x, int y) WorldToTexel(Vector3 world, int res)
    {
        var (u, v) = WorldToUV(world);
        return (Mathf.Clamp(Mathf.RoundToInt(u * (res - 1)), 0, res - 1),
                Mathf.Clamp(Mathf.RoundToInt(v * (res - 1)), 0, res - 1));
    }

    private (float u, float v) WorldToUV(Vector3 world)
    {
        return (Mathf.InverseLerp(_minX, _maxX, world.x), Mathf.InverseLerp(_minZ, _maxZ, world.z));
    }

    private Vector3 UVToWorld(float u, float v)
    {
        return new Vector3(Mathf.Lerp(_minX, _maxX, u), 0f, Mathf.Lerp(_minZ, _maxZ, v));
    }

    /// <summary>Screen-space point within `rect` for a world position,
    /// given the current zoomed texCoords sub-rect -- null if the point
    /// falls outside the zoomed-in view.</summary>
    private Vector2? WorldToMinimapPoint(Vector3 world, Rect rect, Rect texCoords)
    {
        var (u, v) = WorldToUV(world);
        var lu = (u - texCoords.x) / texCoords.width;
        var lv = (v - texCoords.y) / texCoords.height;
        if (lu < 0f || lu > 1f || lv < 0f || lv > 1f) return null;
        return new Vector2(rect.x + lu * rect.width, rect.y + lv * rect.height);
    }

    private Rect GetScreenRect()
    {
        if (useCustomPosition) return new Rect(customTopLeftPixels.x, customTopLeftPixels.y, sizePixels, sizePixels);
        float x, y;
        switch (corner)
        {
            case ScreenCorner.BottomLeft: x = marginPixels.x; y = UiScale.Height - marginPixels.y - sizePixels; break;
            case ScreenCorner.BottomRight: x = UiScale.Width - marginPixels.x - sizePixels; y = UiScale.Height - marginPixels.y - sizePixels; break;
            case ScreenCorner.TopLeft: x = marginPixels.x; y = marginPixels.y; break;
            default: x = UiScale.Width - marginPixels.x - sizePixels; y = marginPixels.y; break;
        }
        return new Rect(x, y, sizePixels, sizePixels);
    }

    // ---- draw + input ----------------------------------------------------------

    private void OnGUI()
    {
        if (_builder == null || _terrainTex == null) return;
        var prevMatrix = UiScale.Begin();
        var rect = GetScreenRect();
        UpdatePointerOverFlag(rect);

        var cam = Camera.main;
        var rig = cam != null ? cam.GetComponent<SimpleCameraRig>() : null;

        var oldMatrix = GUI.matrix;
        var pivot = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        // 2026-08: was plain `GUIUtility.RotateAroundPivot` with a
        // reference-space pivot -- same latent bug AnalogClockHud's
        // hands/pendulum had (see UiScale.RotateAroundReferencePivot's
        // doc comment), just never reported here since this mode
        // defaults off. Fixed alongside that report rather than left for
        // whenever `rotateWithCamera` first gets toggled on.
        if (rotateWithCamera && cam != null)
            UiScale.RotateAroundReferencePivot(-cam.transform.eulerAngles.y, pivot);

        // frame
        GUI.color = BackingColor;
        GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var focusWorld = cam != null ? new Vector3(cam.transform.position.x, 0f, cam.transform.position.z) : UVToWorld(0.5f, 0.5f);
        var uvSize = 1f / Mathf.Max(1f, zoom);
        var (fu, fv) = WorldToUV(focusWorld);
        var u0 = Mathf.Clamp(fu - uvSize * 0.5f, 0f, 1f - uvSize);
        var v0 = Mathf.Clamp(fv - uvSize * 0.5f, 0f, 1f - uvSize);
        var texCoords = new Rect(u0, v0, uvSize, uvSize);

        GUI.DrawTextureWithTexCoords(rect, _terrainTex, texCoords);
        if (showFogOfWar) GUI.DrawTextureWithTexCoords(rect, _fogTex, texCoords);

        DrawBlips(rect, texCoords);
        DrawCameraFrustum(rect, texCoords, cam);
        HandleInput(rect, texCoords, rig);

        // 2026-08 ("make sure non of the text is cropped"): compass +
        // legend are drawn AFTER restoring `oldMatrix` -- deliberately
        // NOT inside the `rotateWithCamera` rotation block above. Rotated
        // IMGUI text reads poorly (font rendering isn't built for
        // arbitrary-angle text) and IS exactly the kind of thing that
        // ends up visually cropped/garbled, so both stay screen-locked
        // and always upright regardless of map rotation -- a known,
        // deliberate simplification: with `rotateWithCamera` on, the "N"
        // compass chip labels the panel as a map rather than tracking
        // true north on the rotated display. `rotateWithCamera` defaults
        // off, so this trades a minor accuracy gap in a non-default mode
        // for guaranteed-legible text in the default one.
        GUI.matrix = oldMatrix;
        DrawCompass(rect);
        DrawLegend(rect);
        UiScale.End(prevMatrix);
    }

    /// <summary>Small "N" chip pinned to the top-center of the map's own
    /// frame -- sized off the actual glyph via CalcSize (not a guessed
    /// fixed width) and clamped to the reference canvas so it can never
    /// clip off-screen even when the map itself sits flush against a
    /// screen edge (TopLeft/TopRight corner, or a tight custom
    /// position).</summary>
    private void DrawCompass(Rect rect)
    {
        var content = new GUIContent("N");
        var textSize = GUI.skin.label.CalcSize(content);
        var chipSize = Mathf.Max(textSize.x, textSize.y) + 8f;

        var chipRect = new Rect(rect.x + rect.width * 0.5f - chipSize * 0.5f, rect.y - chipSize - 4f, chipSize, chipSize);
        if (chipRect.y < 0f) chipRect.y = rect.yMax + 4f;   // no room above -- sit below the map instead
        chipRect.x = Mathf.Clamp(chipRect.x, 0f, Mathf.Max(0f, UiScale.Width - chipRect.width));
        chipRect.y = Mathf.Clamp(chipRect.y, 0f, Mathf.Max(0f, UiScale.Height - chipRect.height));

        GUI.color = BackingColor;
        GUI.DrawTexture(chipRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(chipRect.x + (chipRect.width - textSize.x) * 0.5f,
            chipRect.y + (chipRect.height - textSize.y) * 0.5f, textSize.x + 2f, textSize.y + 2f), content);
    }

    /// <summary>Key for the three markers this pass added/recolored
    /// (arterial roads, the two landmark icon kinds) -- docked BESIDE the
    /// 256px bake rather than crammed onto it (there's no room and no
    /// custom font to draw small legible text into a handful of texels),
    /// same "dock against the minimap's actual live position" idiom
    /// SelectionHud/RecallHud/BattalionHud already use. Every row's Rect
    /// is sized from `GUI.skin.label.CalcSize` on its OWN text, and the
    /// whole panel picks whichever side of the map has open screen space
    /// and clamps against both screen edges -- the direct fix for "make
    /// sure non of the text is cropped," not just a cosmetic pass.</summary>
    private void DrawLegend(Rect rect)
    {
        // (Color32, string) isn't unmanaged (string is a reference type),
        // so this is a plain array, not stackalloc -- three tiny, one-
        // time-per-frame allocations, not a hot path.
        var rows = new (Color32 swatch, string label)[]
        {
            (new Color(0.62f, 0.20f, 0.15f), "Arterial road"),
            (new Color(0.2f, 0.9f, 0.9f), "Beacon (Emitter)"),
            (new Color(0.85f, 0.55f, 0.15f), "Hub (Community)"),
        };

        const float swatchSize = 10f;
        const float swatchGap = 6f;
        const float rowGap = 4f;
        const float pad = 6f;

        var sizes = new Vector2[rows.Length];
        var maxRowWidth = 0f;
        var totalHeight = pad * 2f;
        for (var i = 0; i < rows.Length; i++)
        {
            sizes[i] = GUI.skin.label.CalcSize(new GUIContent(rows[i].label));
            var rowWidth = swatchSize + swatchGap + sizes[i].x;
            if (rowWidth > maxRowWidth) maxRowWidth = rowWidth;
            totalHeight += Mathf.Max(swatchSize, sizes[i].y) + (i > 0 ? rowGap : 0f);
        }
        var panelWidth = maxRowWidth + pad * 2f;

        // dock to whichever half of the screen the map ISN'T hugging, so
        // the panel can't run off whichever edge the map's own corner
        // preset already sits against.
        var mapCenterX = rect.x + rect.width * 0.5f;
        var dockRight = mapCenterX < UiScale.Width * 0.5f;
        var panelX = dockRight ? rect.xMax + 8f : rect.x - 8f - panelWidth;
        panelX = Mathf.Clamp(panelX, 0f, Mathf.Max(0f, UiScale.Width - panelWidth));
        var panelY = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, UiScale.Height - totalHeight));

        GUI.color = BackingColor;
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, totalHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = panelY + pad;
        for (var i = 0; i < rows.Length; i++)
        {
            var rowH = Mathf.Max(swatchSize, sizes[i].y);
            GUI.color = rows[i].swatch;
            GUI.DrawTexture(new Rect(panelX + pad, y + (rowH - swatchSize) * 0.5f, swatchSize, swatchSize), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panelX + pad + swatchSize + swatchGap, y, sizes[i].x + 4f, rowH), rows[i].label);
            y += rowH + rowGap;
        }
    }

    private void UpdatePointerOverFlag(Rect rect)
    {
        var e = Event.current;
        PointerOver = e != null && rect.Contains(e.mousePosition);
    }

    private void DrawBlips(Rect rect, Rect texCoords)
    {
        foreach (var c in _builder.Combatants)
        {
            if (c == null || !c.Alive) continue;
            var isPlayerUnit = c.Faction == "monster";
            if (!isPlayerUnit && showFogOfWar && !_fog.IsVisibleNow(_builder.HexAt(c.transform.position))) continue;
            var p = WorldToMinimapPoint(c.transform.position, rect, texCoords);
            if (!p.HasValue) continue;
            GUI.color = isPlayerUnit ? new Color(0.35f, 0.95f, 0.4f) : new Color(0.9f, 0.25f, 0.2f);
            var s = unitBlipPixels;
            GUI.DrawTexture(new Rect(p.Value.x - s * 0.5f, p.Value.y - s * 0.5f, s, s), Texture2D.whiteTexture);
        }

        foreach (var z in _builder.Citizens)
        {
            if (z == null) continue;
            if (showFogOfWar && !_fog.IsVisibleNow(_builder.HexAt(z.transform.position))) continue;
            var p = WorldToMinimapPoint(z.transform.position, rect, texCoords);
            if (!p.HasValue) continue;
            GUI.color = new Color(0.9f, 0.85f, 0.5f, 0.85f);
            var s = crowdBlipPixels;
            GUI.DrawTexture(new Rect(p.Value.x - s * 0.5f, p.Value.y - s * 0.5f, s, s), Texture2D.whiteTexture);
        }

        foreach (var t in _builder.TrafficCars)
        {
            if (t == null) continue;
            if (showFogOfWar && !_fog.IsVisibleNow(_builder.HexAt(t.transform.position))) continue;
            var p = WorldToMinimapPoint(t.transform.position, rect, texCoords);
            if (!p.HasValue) continue;
            GUI.color = new Color(0.6f, 0.6f, 0.65f, 0.85f);
            var s = crowdBlipPixels;
            GUI.DrawTexture(new Rect(p.Value.x - s * 0.5f, p.Value.y - s * 0.5f, s, s), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DrawCameraFrustum(Rect rect, Rect texCoords, Camera cam)
    {
        if (cam == null) return;
        var p = WorldToMinimapPoint(new Vector3(cam.transform.position.x, 0f, cam.transform.position.z), rect, texCoords);
        if (!p.HasValue) return;

        // camera height is the zoom proxy (SimpleCameraRig clamps it
        // 8..400) -- a bigger footprint box when zoomed out, smaller when
        // zoomed in, scaled into minimap pixels by the current view span
        var worldSpan = (_maxX - _minX) * texCoords.width;
        var footprintWorld = Mathf.Clamp(cam.transform.position.y * 0.9f, 6f, 90f);
        var footprintPx = footprintWorld / Mathf.Max(1f, worldSpan) * rect.width;

        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        var r = new Rect(p.Value.x - footprintPx, p.Value.y - footprintPx, footprintPx * 2f, footprintPx * 2f);
        const float t = 1.4f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void HandleInput(Rect rect, Rect texCoords, SimpleCameraRig rig)
    {
        var e = Event.current;
        if (e == null || !rect.Contains(e.mousePosition)) return;

        if (e.type == EventType.ScrollWheel)
        {
            zoom = Mathf.Clamp(zoom - e.delta.y * scrollZoomSpeed, zoomMin, zoomMax);
            e.Use();
            return;
        }

        var isClickOrDrag = e.type == EventType.MouseDown
            || (e.type == EventType.MouseDrag && (e.button == 0 || e.button == 1));
        if (!isClickOrDrag) return;

        var lu = (e.mousePosition.x - rect.x) / rect.width;
        var lv = (e.mousePosition.y - rect.y) / rect.height;
        var u = texCoords.x + lu * texCoords.width;
        var v = texCoords.y + lv * texCoords.height;
        var world = UVToWorld(u, v);

        if (e.button == 0 && rig != null)
        {
            rig.FocusOn(world);   // left click/drag: quick-navigate the camera
            e.Use();
        }
        else if (e.button == 1 && _commander != null)
        {
            _commander.OrderSelectionTo(world, false);   // right click: order the current selection
            e.Use();
        }
    }
}
