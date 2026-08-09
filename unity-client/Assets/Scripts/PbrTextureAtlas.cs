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
    private static Texture2D _brass;

    public static Texture2D Brick { get { return _brick != null ? _brick : (_brick = BuildBrick()); } }
    public static Texture2D Limestone { get { return _limestone != null ? _limestone : (_limestone = BuildLimestone()); } }
    public static Texture2D AsphaltWet { get { return _asphaltWet != null ? _asphaltWet : (_asphaltWet = BuildAsphaltWet()); } }
    public static Texture2D Chrome { get { return _chrome != null ? _chrome : (_chrome = BuildChrome()); } }
    public static Texture2D PaintedMetal { get { return _paintedMetal != null ? _paintedMetal : (_paintedMetal = BuildPaintedMetal()); } }
    public static Texture2D Glass { get { return _glass != null ? _glass : (_glass = BuildGlass()); } }
    /// <summary>2026-08 (Big Brain jar "Major Improvement": "Brass should
    /// have subtle age, patina, scratches, and surface variation"). Same
    /// small-atlas placeholder technique as every other entry here --
    /// warm brass base tone, low-frequency blotchy patina patches (a
    /// SEPARATE, coarser noise field than the tone mottling, so patina
    /// reads as distinct aged patches rather than blending into the
    /// general variation), sparse bright scratches. Deliberately does
    /// NOT bake a fake rivet-dot grid the way <see cref="PaintedMetal"/>
    /// does -- the brass ring's rivets are real embedded 3D geometry
    /// (BaseDresser.SpawnRivets), and a flat texture dot UNDER a real
    /// domed stud would either be invisible or, worse, misaligned with
    /// it.</summary>
    public static Texture2D Brass { get { return _brass != null ? _brass : (_brass = BuildBrass()); } }

    // 2026-08 (creator direction: "apply the same level of visual
    // refinement... to the Factory and Control Centre for every race"):
    // six new entries, two per faction, same small-atlas placeholder
    // technique as everything above. Salts 20-34 -- picked past every
    // salt already in use elsewhere in this file (1-6, 11-14) and in
    // BrainMesh.cs's own separate noise fields, purely so nobody has to
    // wonder whether two textures share a noise field by accident.
    private static Texture2D _castIron;
    private static Texture2D _oxidizedCopper;
    private static Texture2D _brushedAluminum;
    private static Texture2D _carbonFiberPanel;
    private static Texture2D _alienCrystal;
    private static Texture2D _alienMembrane;

    /// <summary>Mad Doctor faction: dark, heavy, matte cast iron for
    /// factory/control-centre framework, with sparse rust-colored
    /// patches (a SEPARATE, coarser field than the tone mottling, same
    /// "distinct aged patches, not blended-in variation" technique
    /// Brass's own patina already uses).</summary>
    public static Texture2D CastIron { get { return _castIron != null ? _castIron : (_castIron = BuildCastIron()); } }

    /// <summary>Mad Doctor faction: copper-pipe base tone with a
    /// green-patina field -- distinct from Brass's own warm-gold patina
    /// (a different metal, a different oxide color) despite using the
    /// same technique.</summary>
    public static Texture2D OxidizedCopper { get { return _oxidizedCopper != null ? _oxidizedCopper : (_oxidizedCopper = BuildOxidizedCopper()); } }

    /// <summary>Human Alliance faction: light brushed-metal banding,
    /// HORIZONTAL (perpendicular to Chrome's own vertical banding) so
    /// the two read as genuinely different materials side by side, not
    /// the same chrome re-tinted -- aerospace-panel brushed aluminum
    /// rather than polished chrome trim.</summary>
    public static Texture2D BrushedAluminum { get { return _brushedAluminum != null ? _brushedAluminum : (_brushedAluminum = BuildBrushedAluminum()); } }

    /// <summary>Human Alliance faction: a diagonal criss-cross weave
    /// (two offset diagonal stripe fields XORed against each other) for
    /// carbon-fiber panel accents.</summary>
    public static Texture2D CarbonFiberPanel { get { return _carbonFiberPanel != null ? _carbonFiberPanel : (_carbonFiberPanel = BuildCarbonFiberPanel()); } }

    /// <summary>Alien faction: purple crystal base tone plus thin bright
    /// facet lines -- the same difference-of-two-shifted-noise-fields
    /// vein technique BrainTextureKit.VeinMask uses for blood vessels,
    /// reading here as crystal facet edges instead of vessels.</summary>
    public static Texture2D AlienCrystal { get { return _alienCrystal != null ? _alienCrystal : (_alienCrystal = BuildAlienCrystal()); } }

    /// <summary>Alien faction: mottled organic "living tissue" purple-
    /// pink blotching for the Factory's bio-organic hull surfaces --
    /// deliberately softer/blobbier than AlienCrystal's own faceted
    /// read (a bigger low-frequency field, no thin vein lines).</summary>
    public static Texture2D AlienMembrane { get { return _alienMembrane != null ? _alienMembrane : (_alienMembrane = BuildAlienMembrane()); } }

    /// <summary>A tiny deterministic hash for per-pixel jitter -- NOT
    /// UnityEngine.Random/System.Random (this project's own determinism
    /// discipline, docs/23 §0), and unnecessary anyway: this texture is
    /// built once from fixed (x, y) inputs, so "deterministic" here just
    /// means the same code always produces the same pixel, not that it
    /// needs to agree with any match/session seed. Internal (2026-08):
    /// BaseDresser's rivet placement reuses this same hash for its own
    /// per-rivet angle/radius jitter rather than duplicating it.</summary>
    internal static float Jitter(int x, int y, int salt)
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

    /// <summary>Warm brass base tone + two INDEPENDENT noise fields, not
    /// one: a fine/coarse tone-mottling blend (same technique as
    /// <see cref="BuildLimestone"/>) for everyday surface variation, and
    /// a separate, lower-frequency "patina field" that only shows up
    /// where it crosses its own threshold -- real tarnish doesn't fade
    /// in smoothly everywhere, it forms distinct aged PATCHES against
    /// otherwise-cleaner metal. Sparse bright scratches on top, same
    /// idiom as <see cref="BuildPaintedMetal"/>.</summary>
    private static Texture2D BuildBrass()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.72f, 0.56f, 0.26f);
        var patinaCol = new Color(0.33f, 0.4f, 0.32f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 6, y / 6, 11);
            var fine = Jitter(x, y, 12);
            var tone = 0.85f + (coarse * 0.6f + fine * 0.4f - 0.5f) * 0.3f;
            var c = baseCol * tone;

            var patinaField = Jitter(x / 9, y / 9, 13);
            if (patinaField > 0.7f)
                c = Color.Lerp(c, patinaCol, (patinaField - 0.7f) / 0.3f * 0.65f);

            if (Jitter(x, y, 14) > 0.985f) c = Color.Lerp(c, Color.white, 0.5f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildCastIron()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.16f, 0.16f, 0.17f);
        var rustCol = new Color(0.32f, 0.18f, 0.12f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 5, y / 5, 20);
            var fine = Jitter(x, y, 21);
            var tone = 0.85f + (coarse * 0.6f + fine * 0.4f - 0.5f) * 0.35f;
            var c = baseCol * tone;

            var rustField = Jitter(x / 7, y / 7, 22);
            if (rustField > 0.75f) c = Color.Lerp(c, rustCol, (rustField - 0.75f) / 0.25f * 0.5f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildOxidizedCopper()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.62f, 0.35f, 0.2f);
        var patinaCol = new Color(0.32f, 0.58f, 0.46f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 6, y / 6, 23);
            var fine = Jitter(x, y, 24);
            var tone = 0.85f + (coarse * 0.6f + fine * 0.4f - 0.5f) * 0.3f;
            var c = baseCol * tone;

            var patinaField = Jitter(x / 8, y / 8, 25);
            if (patinaField > 0.55f) c = Color.Lerp(c, patinaCol, (patinaField - 0.55f) / 0.45f * 0.75f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildBrushedAluminum()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.78f, 0.79f, 0.81f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // fine HORIZONTAL brushed-metal streaks -- perpendicular to
            // Chrome's own vertical banding (BuildChrome, above), so the
            // two read as genuinely different materials side by side,
            // not the same chrome re-tinted.
            var band = 0.88f + 0.24f * Mathf.Abs(((y % 5) / 5f) - 0.5f) * 2f;
            var speck = Jitter(x, y, 26) > 0.97f ? 1.15f : 1f;
            pixels[y * Size + x] = baseCol * band * speck;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildCarbonFiberPanel()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.08f, 0.08f, 0.09f);
        var weaveCol = new Color(0.15f, 0.15f, 0.17f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // diagonal criss-cross weave: two offset diagonal stripe
            // fields XORed against each other -- the standard cheap way
            // to fake a woven pattern without tracing actual fiber paths.
            var d1 = (x + y) % 8;
            var d2 = ((x - y) % 8 + 8) % 8;
            var weave = (d1 < 3) != (d2 < 3);
            var c = weave ? weaveCol : baseCol;
            var speck = Jitter(x, y, 27) > 0.985f ? 1.4f : 1f;
            pixels[y * Size + x] = c * speck;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildAlienCrystal()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.55f, 0.32f, 0.72f);
        var veinCol = new Color(0.85f, 0.6f, 1f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 9, y / 9, 28);
            var fine = Jitter(x, y, 29);
            var tone = 0.8f + (coarse * 0.6f + fine * 0.4f - 0.5f) * 0.35f;
            var c = baseCol * tone;

            // thin bright facet lines -- the same difference-of-two-
            // shifted-noise-fields technique BrainTextureKit.VeinMask
            // uses for blood vessels, reading here as crystal facet
            // edges instead.
            var a = Jitter(x / 3, y / 3, 30);
            var b = Jitter(x / 3 + 5, y / 3 + 9, 31);
            var vein = 1f - Mathf.Clamp01(Mathf.Abs(a - b) / 0.12f);
            c = Color.Lerp(c, veinCol, vein * 0.6f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D BuildAlienMembrane()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.42f, 0.22f, 0.5f);
        var glowCol = new Color(0.72f, 0.4f, 0.85f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 10, y / 10, 32);
            var med = Jitter(x / 4, y / 4, 33);
            var tone = 0.8f + (coarse * 0.6f + med * 0.4f - 0.5f) * 0.4f;
            var c = baseCol * tone;

            var glowField = Jitter(x / 6, y / 6, 34);
            if (glowField > 0.68f) c = Color.Lerp(c, glowCol, (glowField - 0.68f) / 0.32f * 0.55f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }
}
