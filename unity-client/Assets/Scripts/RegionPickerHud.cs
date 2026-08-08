using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// docs/23 Phase 8's own still-open "region picker" item (see docs/00-
/// index's own Phase 8 status note: "Unity-side dressing... and a region
/// picker... explicitly deferred"). citygen-core has had real New York/
/// Paris/Montreal presets since Phase 8; nothing in Unity ever let a
/// player choose one -- <see cref="RuntimeCityBuilder.preset"/> was an
/// Inspector-only field a developer set before hitting Play, never a
/// runtime choice.
///
/// Only exists in a scene when <see cref="RuntimeCityBuilder.
/// showRegionPicker"/> opts in (off by default, so every existing
/// workflow -- the Inspector preset field, CityGizmo sync -- keeps
/// working byte-for-byte unchanged): <see cref="RuntimeCityBuilder.
/// Start"/> then adds this component and returns BEFORE doing any city
/// generation, camera setup, or roster fetch, all of which now live in
/// the extracted <see cref="RuntimeCityBuilder.BeginMatch"/>. Picking a
/// region here sets <see cref="RuntimeCityBuilder.preset"/> and calls
/// that same method -- the exact generation path every non-picker scene
/// already uses, not a second one.
///
/// 2026-07 follow-up: a live thumbnail per option, not just a labeled
/// button. Each preset gets a REAL <see cref="CityModel"/> generated at
/// <see cref="Init"/> time (a fixed preview seed -- docs/18's own
/// determinism contract, "same seed + preset = identical city, every
/// time," means the same six thumbnails every time this screen shows,
/// not a random reroll) and baked into a small top-down texture using
/// the SAME palette/stamp technique <see cref="Minimap"/>'s own
/// BakeTerrain already established (water/ridge/road/building-tier/
/// landmark colors) -- reimplemented against a standalone CityModel
/// (via <see cref="HexCoord.ToWorld"/> directly) rather than reusing
/// Minimap's own private method, since this runs before any
/// RuntimeCityBuilder/city/`_origin` exists to hang a world-space
/// projection off of. Hovering a button swaps the preview panel to that
/// option's thumbnail; nothing hovered defaults to the first option
/// (Village) so a thumbnail is always visible immediately.
///
/// IMGUI, same as every other HUD element in this project (see
/// HudStatus's header for why). Centered on screen since (unlike every
/// other HUD element here) there is no city/camera/gameplay yet for a
/// corner-anchored overlay to avoid overlapping.
///
/// Honesty note (2026-07, no Unity Editor in this environment): the
/// bake math was run for real against all six actual presets (a
/// standalone console check, real citygen-core DLL, no stubs) -- no
/// preset throws, bounds never collapse, and the same seed bakes
/// identical output twice (docs/18's own determinism contract). One
/// real finding from that check: BigCity and NewYork (both dense-by-
/// design presets) come back with ~100% of texels touched by SOME
/// color at this resolution -- <c>stampRadius</c> deliberately matches
/// <c>Minimap.BakeTerrain</c>'s own proven clamp exactly rather than
/// inventing a different one to chase a lower number, since whether a
/// near-fully-covered thumbnail reads as a legible colored mosaic (the
/// likely case -- roads/buildings/landmarks are still distinct colors)
/// or a flat blob is a real visual question this environment has no way
/// to answer without a render. Recorded here rather than silently
/// assumed either way.
/// </summary>
public class RegionPickerHud : MonoBehaviour
{
    private struct Option
    {
        public RuntimeCityBuilder.PresetChoice Choice;
        public string Label;
        public string Blurb;
        public Option(RuntimeCityBuilder.PresetChoice choice, string label, string blurb)
        {
            Choice = choice;
            Label = label;
            Blurb = blurb;
        }
    }

    private static readonly Option[] Options =
    {
        new Option(RuntimeCityBuilder.PresetChoice.Village, "Village", "1950s small-town grid, Main Street, a roundabout"),
        new Option(RuntimeCityBuilder.PresetChoice.SmallTown, "Small Town", "a modest downtown core"),
        new Option(RuntimeCityBuilder.PresetChoice.BigCity, "Big City", "dense high-rise downtown"),
        new Option(RuntimeCityBuilder.PresetChoice.NewYork, "New York", "1950s Manhattan-scale grid, named landmarks"),
        new Option(RuntimeCityBuilder.PresetChoice.Paris, "Paris", "boulevards, diagonal avenues, a grand roundabout"),
        new Option(RuntimeCityBuilder.PresetChoice.Montreal, "Montreal", "a distinct regional layout and landmark set"),
    };

    /// <summary>Arbitrary but fixed -- docs/18's determinism contract
    /// means this seed always bakes the same six preview cities, so the
    /// picker looks the same every time it's shown, not a fresh reroll
    /// per session. Deliberately NOT <see cref="RuntimeCityBuilder.
    /// seed"/> -- the actual match seed is a separate concern (still the
    /// Inspector field/CityGizmo value), previews are purely illustrative.</summary>
    private const uint PreviewSeed = 0xCAFE1950u;
    private const int PreviewRes = 160;

    private RuntimeCityBuilder _builder;
    private bool _confirmed;
    private readonly Dictionary<RuntimeCityBuilder.PresetChoice, Texture2D> _thumbnails = new Dictionary<RuntimeCityBuilder.PresetChoice, Texture2D>();
    private int _hoveredIndex = 0;

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        _confirmed = false;
        _hoveredIndex = 0;
        BakeAllThumbnails();
    }

    private void BakeAllThumbnails()
    {
        foreach (var opt in Options)
        {
            if (_thumbnails.ContainsKey(opt.Choice)) continue;
            var preset = RuntimeCityBuilder.ResolvePreset(opt.Choice);
            var city = CityGenerator.Generate(PreviewSeed, preset);
            _thumbnails[opt.Choice] = BakePreviewTexture(city);
        }
    }

    /// <summary>Same palette and hex-stamp technique as <c>Minimap.
    /// BakeTerrain</c> (water/ridge/road/bridge/building-tier/landmark
    /// colors), reimplemented against a standalone <see cref="CityModel"/>
    /// via <see cref="HexCoord.ToWorld"/> directly -- there is no live
    /// <see cref="RuntimeCityBuilder"/>/`_origin` to project through yet
    /// at picker time, so this can't just call Minimap's own (private,
    /// instance-bound) method.</summary>
    private static Texture2D BakePreviewTexture(CityModel city)
    {
        var minX = float.MaxValue; var maxX = float.MinValue;
        var minZ = float.MaxValue; var maxZ = float.MinValue;

        void Expand(HexCoord h)
        {
            var (wx, wz) = h.ToWorld();
            var x = (float)wx; var z = (float)wz;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }
        foreach (var h in city.Roads) Expand(h);
        foreach (var h in city.Water) Expand(h);
        foreach (var h in city.Ridges) Expand(h);
        foreach (var b in city.Buildings) foreach (var h in b.Footprint) Expand(h);
        if (minX > maxX) { minX = 0f; maxX = 1f; minZ = 0f; maxZ = 1f; }   // degenerate empty-map guard

        var pad = (float)HexCoord.HexMeters;
        minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;

        var pixels = new Color32[PreviewRes * PreviewRes];
        var ground = (Color32)new Color(0.09f, 0.12f, 0.09f);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = ground;

        var texelsPerHex = PreviewRes / Mathf.Max(1f, (maxX - minX) / (float)HexCoord.HexMeters);
        var stampRadius = Mathf.Clamp(Mathf.RoundToInt(texelsPerHex * 0.6f), 1, 6);   // matches Minimap.BakeTerrain's own clamp exactly

        (int x, int y) ToTexel(HexCoord h)
        {
            var (wx, wz) = h.ToWorld();
            var u = Mathf.InverseLerp(minX, maxX, (float)wx);
            var v = Mathf.InverseLerp(minZ, maxZ, (float)wz);
            return (Mathf.Clamp(Mathf.RoundToInt(u * (PreviewRes - 1)), 0, PreviewRes - 1),
                    Mathf.Clamp(Mathf.RoundToInt(v * (PreviewRes - 1)), 0, PreviewRes - 1));
        }

        void Plot(HexCoord h, Color32 c)
        {
            var (px, py) = ToTexel(h);
            for (var dy = -stampRadius; dy <= stampRadius; dy++)
            {
                var y = py + dy;
                if (y < 0 || y >= PreviewRes) continue;
                for (var dx = -stampRadius; dx <= stampRadius; dx++)
                {
                    var x = px + dx;
                    if (x < 0 || x >= PreviewRes) continue;
                    pixels[y * PreviewRes + x] = c;
                }
            }
        }

        foreach (var h in city.Water) Plot(h, new Color(0.15f, 0.30f, 0.85f));
        foreach (var h in city.Ridges) Plot(h, new Color(0.35f, 0.55f, 0.25f));
        foreach (var h in city.Roads) Plot(h, new Color(0.55f, 0.53f, 0.47f));
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

        var tex = new Texture2D(PreviewRes, PreviewRes, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        return tex;
    }

    private const float ButtonWidth = 260f;
    private const float ButtonHeight = 46f;
    private const float Gap = 10f;
    private const float TitleHeight = 40f;
    private const float ThumbnailSize = 200f;
    private const float ThumbnailLabelHeight = 22f;

    private void OnGUI()
    {
        if (_builder == null || _confirmed) return;
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;

        var listHeight = Options.Length * (ButtonHeight + Gap);
        var previewHeight = ThumbnailLabelHeight + ThumbnailSize;
        var contentHeight = Mathf.Max(listHeight, previewHeight);
        var contentWidth = ButtonWidth + Gap + ThumbnailSize;

        var panelWidth = contentWidth + Gap * 2f;
        var panelHeight = TitleHeight + contentHeight + Gap * 2f;
        var panelRect = new Rect((screenW - panelWidth) * 0.5f, (screenH - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.85f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawShadowedLabel(new Rect(panelRect.x, panelRect.y + Gap * 0.5f, panelRect.width, TitleHeight), "Choose your city", new Color(0.95f, 0.9f, 0.7f, 1f));

        var listX = panelRect.x + Gap;
        var listY = panelRect.y + TitleHeight + Gap;
        var e = Event.current;

        for (var i = 0; i < Options.Length; i++)
        {
            var rect = new Rect(listX, listY, ButtonWidth, ButtonHeight);
            if (e != null && rect.Contains(e.mousePosition)) _hoveredIndex = i;

            var highlighted = i == _hoveredIndex;
            if (highlighted)
            {
                GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.2f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (GUI.Button(rect, Options[i].Label + "\n" + Options[i].Blurb)) Confirm(Options[i].Choice);
            listY += ButtonHeight + Gap;
        }

        DrawPreview(panelRect, listX + ButtonWidth + Gap, panelRect.y + TitleHeight + Gap);

        UiScale.End(prevMatrix);
    }

    private void DrawPreview(Rect panelRect, float x, float y)
    {
        var opt = Options[Mathf.Clamp(_hoveredIndex, 0, Options.Length - 1)];
        DrawShadowedLabel(new Rect(x, y, ThumbnailSize, ThumbnailLabelHeight), opt.Label, new Color(0.85f, 0.85f, 0.95f, 1f));

        var texRect = new Rect(x, y + ThumbnailLabelHeight, ThumbnailSize, ThumbnailSize);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(texRect.x - 2f, texRect.y - 2f, texRect.width + 4f, texRect.height + 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (_thumbnails.TryGetValue(opt.Choice, out var tex) && tex != null)
            GUI.DrawTexture(texRect, tex);
    }

    private void Confirm(RuntimeCityBuilder.PresetChoice choice)
    {
        if (_confirmed || _builder == null) return;
        _confirmed = true;
        _builder.preset = choice;
        _builder.BeginMatch();
        Object.Destroy(this);
    }

    private void OnDestroy()
    {
        foreach (var kv in _thumbnails) if (kv.Value != null) Object.Destroy(kv.Value);
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }
}
