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
    private static Texture2D _roofShingle;
    private static Texture2D _asphaltWet;
    private static Texture2D _chrome;
    private static Texture2D _paintedMetal;
    private static Texture2D _glass;
    private static Texture2D _brass;

    public static Texture2D Brick { get { return _brick != null ? _brick : (_brick = BuildBrick()); } }
    public static Texture2D Limestone { get { return _limestone != null ? _limestone : (_limestone = BuildLimestone()); } }

    /// <summary>2026-08 ("AAA upgrades" pass, creator direction: "Create
    /// texture maps. Brick and lime stone, roof shingles that match the
    /// b-movie style we have established"): asphalt roofing shingles --
    /// staggered overlapping courses (same brick-coursing IDEA as <see
    /// cref="BuildBrick"/>, but shingles overlap DOWNWARD instead of
    /// mortar-separating, and each course's own row reads as a stack of
    /// slightly-uneven tabs rather than uniform bricks) plus weathered
    /// tone variation per shingle, same jitter technique as every other
    /// texture here.</summary>
    public static Texture2D RoofShingle { get { return _roofShingle != null ? _roofShingle : (_roofShingle = BuildRoofShingle()); } }

    /// <summary>2026-08 (faction gauntlet, docs/31 §3/§7 Phase 3: "large
    /// stone blocks, not decorative brick" -- `DoctorDarkBrick`'s own
    /// brick-coursing texture was flagged as the WRONG texture for the
    /// gothic-castle transform, needing a genuinely larger-scale block).
    /// Same staggered-course technique <see cref="BuildBrick"/> already
    /// uses, but with far fewer, far larger blocks (2 per row instead of
    /// 4, twice the row height) and a thicker, darker joint line -- reads
    /// as massive dressed masonry rather than a small fired brick, at
    /// the same 64x64 atlas size as everything else here. Salt 41 --
    /// past every salt already in use elsewhere in this file (1-6, 11-14,
    /// 20-34, 40).</summary>
    private static Texture2D _dressedStone;
    public static Texture2D DressedStone { get { return _dressedStone != null ? _dressedStone : (_dressedStone = BuildDressedStone()); } }
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

    // AlienCrystal/AlienMembrane (the purple organic crystal/"living
    // tissue" textures) removed 2026-08 (faction gauntlet, "Replace the
    // existing biological components with shiny silver steel... preserve
    // the Alien visual identity through shapes/proportions/detailing --
    // not through biological-looking building components"). BaseDresser's
    // Alien geometry now uses AlienSilverSteel (built on the existing
    // Chrome texture, already clean/premium by construction) for every
    // site that used to call AlienCrystalMat/AlienMembraneMat -- deleted
    // outright rather than left unused, same "no dead code" discipline
    // this project applies everywhere else. Grepped the whole unity-client
    // tree first to confirm nothing outside BaseDresser.cs ever referenced
    // either texture.

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

    private static Texture2D BuildDressedStone()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        const int rowHeight = 16;
        const int jointPx = 2;
        var stoneBase = new Color(0.42f, 0.41f, 0.4f);
        var joint = new Color(0.14f, 0.13f, 0.13f);
        // 2026-08 (Mad Doctor weathering pass): a dark, cool moss/damp-
        // staining tone -- distinct from CastIron's warm rust and
        // OxidizedCopper's brighter verdigris, so a Doctor building's
        // three main weathered surfaces (iron/copper/stone) don't all
        // converge on the same green-brown wash.
        var mossCol = new Color(0.18f, 0.22f, 0.16f);
        for (var y = 0; y < Size; y++)
        {
            var row = y / rowHeight;
            var inRow = y % rowHeight;
            // staggered vertical joints, half a block offset per row --
            // same "real mason staggers seams" idea BuildBrick uses, just
            // at 2 blocks per row instead of 4
            var offset = (row % 2 == 0) ? 0 : Size / 4;
            for (var x = 0; x < Size; x++)
            {
                var shifted = (x + offset) % Size;
                var blockX = shifted % (Size / 2);
                var isJoint = inRow < jointPx || blockX < jointPx;
                Color c;
                if (isJoint)
                {
                    c = joint;
                }
                else
                {
                    // coarser per-block mottling than BuildBrick's own
                    // per-pixel jitter -- a whole block reads one uneven
                    // weathered tone, not a speckled surface
                    var jitter = 0.8f + Jitter(row, shifted / (Size / 2), 41) * 0.4f;
                    c = stoneBase * jitter;

                    // 2026-08 (Mad Doctor weathering pass, "heaviest...
                    // grime, staining... should collect around seams,
                    // recesses, drainage areas"): a per-BLOCK (not
                    // per-pixel) moss/grime patch field, biased to land
                    // more often on blocks nearer the joint (`blockX`/
                    // `inRow` small) than block centers -- real damp-
                    // staining spreads outward FROM a seam, it doesn't
                    // appear in the middle of an open face at random.
                    var nearJoint = blockX < jointPx * 6 || inRow < jointPx * 6;
                    var mossField = Jitter(row, shifted / (Size / 2), 45);
                    var mossThreshold = nearJoint ? 0.45f : 0.8f;
                    if (mossField > mossThreshold)
                        c = Color.Lerp(c, mossCol, (mossField - mossThreshold) / (1f - mossThreshold) * 0.6f);
                }
                pixels[y * Size + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Neutral mid-gray base -- deliberately NOT baked warm or
    /// cool (unlike <see cref="BuildBrick"/>'s own reddish base tone) so
    /// the same texture serves both `BuildingDresser`'s warm (rust-red)
    /// and cool (slate-blue) roof-color picks via the calling Material's
    /// own tint, same "one neutral texture, many material-color variants"
    /// precedent <see cref="BuildLimestone"/> already establishes (reused
    /// tinted for concrete, faction stone, the Big Brain pedestal plaque).
    /// Salt 40 -- picked past every salt already in use elsewhere in this
    /// file (1-6, 11-14, 20-34).</summary>
    private static Texture2D BuildRoofShingle()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        const int courseHeight = 6;   // a shingle course's own row height
        const int shadowPx = 1;       // the overlap-shadow line at each course's bottom edge
        var baseCol = new Color(0.5f, 0.5f, 0.5f);
        for (var y = 0; y < Size; y++)
        {
            var course = y / courseHeight;
            var inCourse = y % courseHeight;
            // staggered tab joints: odd courses offset half a tab -- the
            // same "a real roofer staggers seams so they never stack"
            // idea BuildBrick already uses for its own vertical joints
            var offset = (course % 2 == 0) ? 0 : Size / 10;
            // shadow at the BOTTOM of each course -- shingles overlap
            // downward, so the course below reads a shadow line under
            // the one lapping over it, not a mortar gap
            var isShadow = inCourse >= courseHeight - shadowPx;
            for (var x = 0; x < Size; x++)
            {
                var shifted = (x + offset) % Size;
                var tabX = shifted % (Size / 5);   // ~5 tabs per course width
                var isTabGap = tabX < 1;           // thin vertical seam between adjacent tabs
                Color c;
                if (isShadow || isTabGap)
                {
                    c = baseCol * 0.55f;
                }
                else
                {
                    var jitter = 0.82f + Jitter(x, y, 40) * 0.35f;   // per-shingle weathering
                    c = baseCol * jitter;
                }
                pixels[y * Size + x] = c;
            }
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
        // 2026-08 (creator report on a rendered window pane: "looks like
        // a solid line not glass"): BuildingWindowGrid gives every window
        // its own private, un-tiled 0..1 UV square (one pane = one full
        // sample of this texture, never repeated -- consistent with this
        // file's own "no per-object UV tiling" doc comment above, so the
        // fix belongs here, not in added tiling). The old `(x + y) % Size`
        // band was written assuming wraparound tiling: within a single
        // untiled 0..1 sample it only crosses threshold near TWO opposite
        // corners of the square, not along one continuous line, so a
        // single small on-screen pane showed two disconnected corner
        // slivers that blurred into a flat diagonal gradient instead of a
        // recognizable glint. `diag` here is unwrapped (no modulo) so one
        // sample shows exactly one soft diagonal sheen streak, with a
        // smooth falloff (not a hard on/off band) so it reads as a
        // highlight rather than a stripe.
        var diagMax = (Size - 1) * 2f;
        var bandCenter = diagMax * 0.38f;
        const float bandWidth = 9f;
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var diag = x + y;
            var dist = Mathf.Abs(diag - bandCenter);
            var sheen = 1f + 0.8f * Mathf.Clamp01(1f - dist / bandWidth);
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

    /// <summary>Mad Doctor faction, EXCLUSIVELY, as of the 2026-08
    /// per-faction weathering pass ("the heaviest weathering and aging of
    /// all factions... deep, layered weathering... grime, staining,
    /// corrosion, discoloration"). Used to be shared with Alien's own
    /// gunmetal (`AlienGunmetal` -- see that method's own comment history)
    /// -- that faction moved to a clean Chrome-based steel instead
    /// (`AlienSilverSteel`/rewired `AlienGunmetal`), specifically so this
    /// texture could be pushed harder without also making the "avoid
    /// excessive grime" faction look dirty. Rust coverage/intensity both
    /// raised from the original shared-with-Alien version, plus a NEW,
    /// coarser soot/grime field layered on top (a separate noise field,
    /// not blended into the rust one, so the two read as distinct
    /// accumulation types the way Brass's own patina-vs-scratches split
    /// already does) -- "layered weathering," not just "more of one
    /// effect."</summary>
    private static Texture2D BuildCastIron()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.14f, 0.14f, 0.15f);
        var rustCol = new Color(0.34f, 0.19f, 0.12f);
        var grimeCol = new Color(0.07f, 0.07f, 0.07f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 5, y / 5, 20);
            var fine = Jitter(x, y, 21);
            var tone = 0.85f + (coarse * 0.6f + fine * 0.4f - 0.5f) * 0.35f;
            var c = baseCol * tone;

            var rustField = Jitter(x / 7, y / 7, 22);
            if (rustField > 0.55f) c = Color.Lerp(c, rustCol, (rustField - 0.55f) / 0.45f * 0.75f);

            var grimeField = Jitter(x / 11, y / 11, 42);
            if (grimeField > 0.6f) c = Color.Lerp(c, grimeCol, (grimeField - 0.6f) / 0.4f * 0.55f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Mad Doctor faction. 2026-08 weathering pass: patina
    /// coverage/intensity both raised, plus a second, darker "verdigris
    /// streak" field layered on top of the original patina -- two
    /// distinct green-oxide tones rather than one uniform wash, closer to
    /// how real weathered copper actually accumulates unevenly.</summary>
    private static Texture2D BuildOxidizedCopper()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.62f, 0.35f, 0.2f);
        var patinaCol = new Color(0.32f, 0.58f, 0.46f);
        var deepPatinaCol = new Color(0.22f, 0.42f, 0.34f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var coarse = Jitter(x / 6, y / 6, 23);
            var fine = Jitter(x, y, 24);
            var tone = 0.85f + (coarse * 0.6f + fine * 0.4f - 0.5f) * 0.3f;
            var c = baseCol * tone;

            var patinaField = Jitter(x / 8, y / 8, 25);
            if (patinaField > 0.4f) c = Color.Lerp(c, patinaCol, (patinaField - 0.4f) / 0.6f * 0.8f);

            var streakField = Jitter(x / 13, y / 13, 43);
            if (streakField > 0.72f) c = Color.Lerp(c, deepPatinaCol, (streakField - 0.72f) / 0.28f * 0.7f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Human Army faction. 2026-08 weathering pass ("basic dirt,
    /// dust, grime, and moderate wear rather than extreme deterioration"):
    /// a SUBTLE dirt-speckle field layered onto the original clean
    /// brushed banding -- deliberately much lower coverage/opacity than
    /// Doctor's own rust/grime fields above (this is "practical and
    /// military," not "old and neglected"), and no rust/patina at all
    /// (Human hardware stays maintained, per the same brief).</summary>
    private static Texture2D BuildBrushedAluminum()
    {
        var tex = NewTexture();
        var pixels = new Color32[Size * Size];
        var baseCol = new Color(0.78f, 0.79f, 0.81f);
        var dirtCol = new Color(0.52f, 0.5f, 0.46f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            // fine HORIZONTAL brushed-metal streaks -- perpendicular to
            // Chrome's own vertical banding (BuildChrome, above), so the
            // two read as genuinely different materials side by side,
            // not the same chrome re-tinted.
            var band = 0.88f + 0.24f * Mathf.Abs(((y % 5) / 5f) - 0.5f) * 2f;
            var speck = Jitter(x, y, 26) > 0.97f ? 1.15f : 1f;
            var c = baseCol * band * speck;

            var dirtField = Jitter(x / 10, y / 10, 44);
            if (dirtField > 0.82f) c = Color.Lerp(c, dirtCol, (dirtField - 0.82f) / 0.18f * 0.3f);

            pixels[y * Size + x] = c;
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

}
