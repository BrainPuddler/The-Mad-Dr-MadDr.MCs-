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
/// </summary>
public class Minimap : MonoBehaviour
{
    public enum ScreenCorner { BottomLeft, BottomRight, TopLeft, TopRight }

    [Header("Placement (default bottom-left; developer-tunable anywhere)")]
    public ScreenCorner corner = ScreenCorner.BottomLeft;
    public Vector2 marginPixels = new Vector2(16f, 16f);
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

    private const int TerrainTexRes = 256;
    private const int FogTexRes = 128;
    private const float FogRepaintInterval = 0.4f;

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
    /// minimap scale, so roads get a lighter tone here specifically.</summary>
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

        void Plot(HexCoord h, Color32 c)
        {
            var (px, py) = WorldToTexel(_builder.WorldOf(h), TerrainTexRes);
            for (var dy = -stampRadius; dy <= stampRadius; dy++)
            {
                var y = py + dy;
                if (y < 0 || y >= TerrainTexRes) continue;
                for (var dx = -stampRadius; dx <= stampRadius; dx++)
                {
                    var x = px + dx;
                    if (x < 0 || x >= TerrainTexRes) continue;
                    pixels[y * TerrainTexRes + x] = c;
                }
            }
        }

        foreach (var h in city.Water) Plot(h, new Color(0.15f, 0.30f, 0.85f));
        foreach (var h in city.Ridges) Plot(h, new Color(0.35f, 0.55f, 0.25f));
        foreach (var h in city.Roads) Plot(h, new Color(0.55f, 0.53f, 0.47f));   // lighter than the gizmo's near-black -- reads at minimap scale
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
            var c = lm.Kind == LandmarkKind.Emitter ? new Color(0.2f, 0.9f, 0.9f) : new Color(0.9f, 0.2f, 0.2f);
            Plot(lm.Site, c);
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
            case ScreenCorner.BottomLeft: x = marginPixels.x; y = Screen.height - marginPixels.y - sizePixels; break;
            case ScreenCorner.BottomRight: x = Screen.width - marginPixels.x - sizePixels; y = Screen.height - marginPixels.y - sizePixels; break;
            case ScreenCorner.TopLeft: x = marginPixels.x; y = marginPixels.y; break;
            default: x = Screen.width - marginPixels.x - sizePixels; y = marginPixels.y; break;
        }
        return new Rect(x, y, sizePixels, sizePixels);
    }

    // ---- draw + input ----------------------------------------------------------

    private void OnGUI()
    {
        if (_builder == null || _terrainTex == null) return;
        var rect = GetScreenRect();
        UpdatePointerOverFlag(rect);

        var cam = Camera.main;
        var rig = cam != null ? cam.GetComponent<SimpleCameraRig>() : null;

        var oldMatrix = GUI.matrix;
        var pivot = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        if (rotateWithCamera && cam != null)
            GUIUtility.RotateAroundPivot(-cam.transform.eulerAngles.y, pivot);

        // frame
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
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

        GUI.matrix = oldMatrix;
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
