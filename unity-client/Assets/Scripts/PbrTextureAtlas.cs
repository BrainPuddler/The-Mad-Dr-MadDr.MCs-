using UnityEngine;

/// <summary>
/// docs/23 Phase 10.3 (Materials): "a small PBR atlas set (brick,
/// limestone, asphalt-wet, chrome, painted metal, glass)... this is where
/// BuildingDresser's flat Brick()/Cream()/etc. materials gain real
/// coursing/weathering texture instead of a re-tint."
///
/// This environment has no Editor/DCC pipeline to author real texture
/// assets, so every texture here is a small, PROCEDURALLY GENERATED
/// placeholder (built once, cached, from pure code) -- a real technique
/// (this project's own LabMeshBuilder/creature-mesh already builds
/// geometry procedurally the same way), not an imported asset. Each
/// pattern is a coarse stand-in for the real material's defining visual
/// trait (brick coursing, stone mottling, wet-asphalt streaking, brushed
/// chrome banding, painted-metal scuffing, a glass sheen band) -- good
/// enough to read as "textured" instead of "flat color," not a finished
/// look. No per-object UV tiling-to-world-size: every material gets one
/// fixed tiling scale regardless of the prop/building face it lands on,
/// a deliberate v0.1 simplification (see docs/23-balance/graphics-3-notes.md).
/// </summary>
public static class PbrTextureAtlas
{
    private const int Size = 64;

    private static Texture2D _brick;
    private static Texture2D _limestone;
    private static Texture2D _asphaltWet;
    private static Texture2D _chrome;
    private static Texture2D _paintedMetal;
    private static Texture2D _glass;

    public static Texture2D Brick { get { return _brick != null ? _brick : (_brick = BuildBrick()); } }
    public static Texture2D Limestone { get { return _limestone != null ? _limestone : (_limestone = BuildLimestone()); } }
    public static Texture2D AsphaltWet { get { return _asphaltWet != null ? _asphaltWet : (_asphaltWet = BuildAsphaltWet()); } }
    public static Texture2D Chrome { get { return _chrome != null ? _chrome : (_chrome = BuildChrome()); } }
    public static Texture2D PaintedMetal { get { return _paintedMetal != null ? _paintedMetal : (_paintedMetal = BuildPaintedMetal()); } }
    public static Texture2D Glass { get { return _glass != null ? _glass : (_glass = BuildGlass()); } }

    /// <summary>A tiny deterministic hash for per-pixel jitter -- NOT
    /// UnityEngine.Random/System.Random (this project's own determinism
    /// discipline, docs/23 §0), and unnecessary anyway: this texture is
    /// built once from fixed (x, y) inputs, so "deterministic" here just
    /// means the same code always produces the same pixel, not that it
    /// needs to agree with any match/session seed.</summary>
    private static float Jitter(int x, int y, int salt)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + salt * 2246822519;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;   // [0, 1)
        }
    }

    private static Texture2D NewTexture()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private static Texture2D BuildBrick()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        const int rowHeight = 8;
        const int mortarPx = 1;
        var brickBase = new Color(0.62f, 0.32f, 0.24f);
        var mortar = new Color(0.74f, 0.71f, 0.65f);
        for (var y = 0; y < Size; y++)
        {
            var row = y / rowHeight;
            var inRow = y % rowHeight;
            // staggered vertical joints: odd rows offset half a brick
            var offset = (row % 2 == 0) ? 0 : Size / 8;
            for (var x = 0; x < Size; x++)
            {
                var shifted = (x + offset) % Size;
                var brickX = shifted % (Size / 4);
                var isMortar = inRow < mortarPx || brickX < mortarPx;
                Color c;
                if (isMortar)
                {
                    c = mortar;
                }
                else
                {
                    var jitter = 0.85f + Jitter(x, y, 1) * 0.3f;   // per-brick-ish weathering variation
                    c = brickBase * jitter;
                }
                pixels[y * Size + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildLimestone()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.72f, 0.7f, 0.63f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // low-frequency mottling: blend two jitter octaves for a
            // soft weathered-stone look instead of pure per-pixel noise
            var coarse = Jitter(x / 8, y / 8, 2);
            var fine = Jitter(x, y, 3);
            var v = 0.9f + (coarse * 0.7f + fine * 0.3f - 0.5f) * 0.18f;
            pixels[y * Size + x] = baseCol * v;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildAsphaltWet()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.1f, 0.1f, 0.11f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // fine grain plus a few horizontal "wet streak" highlight
            // bands standing in for a real reflective wet-asphalt shader
            var grain = 0.9f + Jitter(x, y, 4) * 0.2f;
            var streak = ((y + (int)(Jitter(0, y, 5) * 6)) % 11 == 0) ? 1.5f : 1f;
            pixels[y * Size + x] = baseCol * grain * streak;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildChrome()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.82f, 0.84f, 0.86f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // vertical brushed-metal bands -- a cheap stand-in for real
            // environment-reflection streaks with no reflection probe
            // setup to lean on
            var band = 0.75f + 0.5f * Mathf.Abs(((x % 6) / 6f) - 0.5f) * 2f;
            pixels[y * Size + x] = baseCol * band;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildPaintedMetal()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.42f, 0.45f, 0.48f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var v = 1f;
            // sparse scratches
            if (Jitter(x, y, 6) > 0.97f) v = 1.4f;
            // a faint rivet dot grid every 16px
            if (x % 16 == 8 && y % 16 == 8) v = 0.6f;
            pixels[y * Size + x] = baseCol * v;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildGlass()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.55f, 0.62f, 0.68f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // a diagonal sheen band standing in for a specular
            // highlight -- no real transparency (Opaque material, this
            // project has no transparent-material convention yet)
            var diag = (x + y) % Size;
            var sheen = diag > Size - 10 || diag < 10 ? 1.6f : 1f;
            pixels[y * Size + x] = baseCol * sheen;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }
}
