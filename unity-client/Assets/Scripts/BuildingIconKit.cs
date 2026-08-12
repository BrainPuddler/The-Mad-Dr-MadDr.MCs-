using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-08 creator direction: "Update the ui to reflect each theme,
/// faction style established by the maps and period style. Including
/// All user Building must have thumbnail icons." This project has no
/// real icon sprite art anywhere (the pre-existing flat-color-plus-
/// abbreviation "icons" <see cref="BuildingNavHud"/>/<see
/// cref="BuildMenuHud"/> used before this pass both said so directly in
/// their own doc comments) -- same constraint the Minimap legibility
/// pass (docs/12) worked within, and the same fix: procedurally bake a
/// small white-silhouette-on-
/// transparent texture ONCE per building kind (cached forever after
/// first use, matching AnalogClockHud's own "bake a texture once, stretch
/// to any Rect at draw time" precedent), then let the CALLER tint it live
/// via <c>GUI.color</c> before <c>GUI.DrawTexture</c> -- the same
/// texture works for every faction's own accent color (<see
/// cref="BuildingFactionSkin.AccentColorFor"/>) without needing 9 kinds
/// x 3 factions = 27 separate bakes.
///
/// Every icon is a simple, bold, high-contrast PICTOGRAM built from
/// analytic shape tests (circle/ring/rect/taper), not hand-authored art
/// -- the same "period pictograph, not a detailed illustration" idiom
/// the Minimap's landmark star/ring icons already established, chosen
/// specifically because it reads at true small on-screen sizes (a 44-56px
/// HUD tile) without needing an art pipeline this project doesn't have.
/// </summary>
public static class BuildingIconKit
{
    private const int Res = 28;

    private static readonly Dictionary<BuildingKind, Texture2D> Cache = new Dictionary<BuildingKind, Texture2D>();

    /// <summary>The baked silhouette for one kind -- white where the
    /// pictogram is "on," fully transparent elsewhere, so a caller's own
    /// <c>GUI.color</c> tint shows through as the icon's actual color.
    /// Lazily baked on first request and cached forever (the shape never
    /// changes at runtime) -- same lifecycle as Minimap's own terrain
    /// bake, just per-kind instead of per-city.</summary>
    public static Texture2D IconFor(BuildingKind kind)
    {
        if (Cache.TryGetValue(kind, out var tex) && tex != null) return tex;
        tex = Bake(kind);
        Cache[kind] = tex;
        return tex;
    }

    private static Texture2D Bake(BuildingKind kind)
    {
        var pixels = new Color32[Res * Res];
        var clear = new Color32(255, 255, 255, 0);
        var solid = new Color32(255, 255, 255, 255);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

        var c = (Res - 1) * 0.5f;
        for (var y = 0; y < Res; y++)
        {
            var dy = y - c;
            for (var x = 0; x < Res; x++)
            {
                var dx = x - c;
                if (PixelOn(kind, dx, dy, c)) pixels[y * Res + x] = solid;
            }
        }

        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;   // smooth edges at small on-screen sizes, unlike Minimap's deliberately crisp Point filtering
        tex.SetPixels32(pixels);
        tex.Apply(false);
        return tex;
    }

    /// <summary>True if the pixel at (dx,dy) from icon center belongs to
    /// this kind's own pictogram. `c` is the icon's own half-extent (used
    /// to scale every shape proportionally to `Res`, so changing `Res`
    /// later doesn't require re-tuning every kind's magic numbers).</summary>
    private static bool PixelOn(BuildingKind kind, float dx, float dy, float c)
    {
        switch (kind)
        {
            case BuildingKind.Hq:
                // Command roundel: a ring plus a cross through the middle
                // -- 1950s military-map "command post" pictogram (same
                // convention researched for the Minimap pass, docs/12).
                return InRing(dx, dy, c * 0.92f, c * 0.75f) || InCross(dx, dy, c * 0.85f, c * 0.16f);

            case BuildingKind.BloodStorage:
                // Droplet: circular bulb below center, linear taper to a
                // point above it.
                return dy >= 0f ? InCircle(dx, dy, c * 0.62f) : InTaperUp(dx, -dy, c * 1.05f, c * 0.62f);

            case BuildingKind.FuelPump:
                // Tank body (with a transparent gauge-dot CUTOUT -- a
                // solid circle drawn INSIDE an already-solid rect would be
                // invisible, so the gauge has to be a punched hole, same
                // trick FuelStorage's hoop gaps use below) + a nozzle
                // sticking out at top-right (texture Y grows DOWNWARD, so
                // "top" is the NEGATIVE dy side).
                if (InRect(dx - c * 0.55f, dy + c * 0.55f, c * 0.28f, c * 0.12f)) return true;   // nozzle, top-right
                if (!InRect(dx, dy, c * 0.38f, c * 0.85f)) return false;
                return !InCircle(dx, dy, c * 0.16f);   // gauge cutout

            case BuildingKind.FuelStorage:
                // Barrel: a solid body with two thin transparent "hoop"
                // gaps -- the swatch color behind shows through the gaps,
                // reading as ridge lines without a second bake color.
                if (!InRect(dx, dy, c * 0.7f, c * 0.85f)) return false;
                return Mathf.Abs(dy - c * 0.3f) > c * 0.08f && Mathf.Abs(dy + c * 0.3f) > c * 0.08f;

            case BuildingKind.PartsStorage:
                // Two offset stacked crates.
                return InRect(dx + c * 0.28f, dy + c * 0.28f, c * 0.5f, c * 0.5f)
                    || InRect(dx - c * 0.28f, dy - c * 0.28f, c * 0.5f, c * 0.5f);

            case BuildingKind.HarvestPost:
                // Upward chevron (gathering/collecting) over a small base
                // -- base sits at the BOTTOM (positive dy).
                if (InRect(dx, dy - c * 0.62f, c * 0.5f, c * 0.22f)) return true;
                return InChevronUp(dx, dy, c * 0.85f, c * 0.75f, c * 0.28f);

            case BuildingKind.Factory:
                // Base block with a sawtooth roofline plus one chimney
                // rising from it. SawtoothTopAt returns roughly
                // [c*0.02, c*0.38] (center c*0.2, amplitude c*0.18) -- the
                // chimney's own bottom edge (dy = c*0.4) is deliberately
                // PAST that maximum so it always overlaps the roofline
                // regardless of the sawtooth's phase at this x, instead of
                // floating above a gap.
                if (InRect(dx - c * 0.4f, dy + c * 0.25f, c * 0.14f, c * 0.65f)) return true;
                if (Mathf.Abs(dx) > c * 0.85f) return false;
                return dy > SawtoothTopAt(dx, c) && dy <= c * 0.75f;

            case BuildingKind.Defense:
                // Shield: flat top, tapering to a point at the bottom.
                return dy <= 0f ? InRect(dx, dy, c * 0.72f, c * 0.72f) : InTaperDown(dx, dy, c * 1.05f, c * 0.72f);

            default: // BigBrain
                // Three overlapping lobes -- a cell-cluster/brain read.
                return InCircle(dx + c * 0.32f, dy - c * 0.2f, c * 0.48f)
                    || InCircle(dx - c * 0.32f, dy - c * 0.2f, c * 0.48f)
                    || InCircle(dx, dy + c * 0.32f, c * 0.5f);
        }
    }

    private static bool InRect(float dx, float dy, float halfW, float halfH)
        => Mathf.Abs(dx) <= halfW && Mathf.Abs(dy) <= halfH;

    private static bool InCircle(float dx, float dy, float r) => dx * dx + dy * dy <= r * r;

    private static bool InRing(float dx, float dy, float rOuter, float rInner)
    {
        var d2 = dx * dx + dy * dy;
        return d2 <= rOuter * rOuter && d2 >= rInner * rInner;
    }

    private static bool InCross(float dx, float dy, float armLength, float thickness)
        => (Mathf.Abs(dx) <= thickness && Mathf.Abs(dy) <= armLength)
        || (Mathf.Abs(dy) <= thickness && Mathf.Abs(dx) <= armLength);

    /// <summary>Triangle tapering to a point at the TOP: `distFromBase` is
    /// how far below the shape's own base this pixel sits (so callers
    /// with different "which way is up" conventions can flip the sign
    /// before calling), `height`/`baseHalfWidth` set the taper's overall
    /// proportions.</summary>
    private static bool InTaperUp(float dx, float distFromBase, float height, float baseHalfWidth)
    {
        if (distFromBase < 0f || distFromBase > height) return false;
        var allowedHalfWidth = baseHalfWidth * (1f - distFromBase / height);
        return Mathf.Abs(dx) <= allowedHalfWidth;
    }

    private static bool InTaperDown(float dx, float dy, float height, float baseHalfWidth)
        => InTaperUp(dx, dy, height, baseHalfWidth);

    private static bool InChevronUp(float dx, float dy, float height, float halfWidth, float thickness)
    {
        // A "^" made of two diagonal bars meeting at the apex (dy most
        // negative = highest on screen, IMGUI/texture Y grows downward
        // same as the rest of this bake).
        var apexY = -height * 0.5f;
        var baseY = height * 0.5f;
        if (dy < apexY || dy > baseY) return false;
        var t = (dy - apexY) / (baseY - apexY);   // 0 at apex, 1 at base
        var expectedX = halfWidth * t;
        return Mathf.Abs(Mathf.Abs(dx) - expectedX) <= thickness;
    }

    private static float SawtoothTopAt(float dx, float c)
    {
        // A repeating triangular wave across the roofline -- period ~c*0.4,
        // amplitude ~c*0.18, centered at c*0.2 (just above the chimney's
        // own base) so the base block itself stays tall enough to read.
        const float period = 0.4f;
        const float amp = 0.18f;
        var period_px = period * c;
        var local = Mathf.Repeat(dx + c, period_px) / period_px;   // 0..1 sawtooth phase, offset so it's symmetric-ish across the icon
        var wave = Mathf.Abs(local - 0.5f) * 2f - 1f;   // -1..1 triangle wave
        return c * 0.2f + wave * amp * c;
    }
}
