using UnityEngine;

/// <summary>
/// 2026-07 (creator direction): "emphasize that the geometry stays
/// simple while the materials do the heavy lifting... Drive the visual
/// detail with a high-resolution normal map containing thousands of
/// small, irregular folds and branching ridges. Add a height (parallax/
/// pseudo-displacement) map... Use ambient occlusion... The roughness
/// map should vary across the surface... Overlay thin, branching red
/// blood vessels using the albedo and roughness maps rather than
/// geometry."
///
/// Same standing constraint as PbrTextureAtlas (this environment has no
/// DCC/Editor pipeline to paint a real normal/height/AO/roughness set),
/// extended from that file's flat-color-only technique to a genuine
/// multi-map PBR set: every map here is derived from ONE shared,
/// multi-octave procedural heightfield (broad rolling folds + medium
/// branching ridges + fine wrinkles, three frequency bands summed --
/// the "multiple scales of detail... irregular and non-repeating" the
/// brief asks for), so the normal map's bumps, the AO map's shadowed
/// valleys, and the smoothness map's peak/valley split all agree with
/// each other and with the albedo's own subtle height-driven shading --
/// not four independently-authored maps that could visually disagree.
///
/// Honest limit, stated plainly rather than silently attempted: true
/// subsurface scattering has no slot in URP's standard Lit shader (that
/// exists in HDRP, not here) and a custom Shader Graph approximation
/// isn't attempted -- authoring HLSL/Shader Graph blind, with no Editor
/// to compile or render it against, risks shipping something broken in
/// a way nothing here could catch. BaseDresser leans on Lit's own
/// existing levers instead (a translucent-reading base color, moderate
/// smoothness, a faint warm low-level emission) as the closest
/// achievable approximation of "soft, organic, not plastic" -- see
/// BaseDresser.BuildBigBrainShape's own comment for the material wiring.
/// </summary>
public static class BrainTextureKit
{
    private const int Size = 256;

    // frequency/amplitude of each octave, in noise-grid cells across the
    // whole [0,1] UV range -- broad folds dominate, medium ridges add
    // branching structure, fine wrinkles add the last bit of irregular
    // micro-detail. Amplitudes sum to 1 so Height() returns a clean 0..1.
    private const float BroadFreq = 3.5f, BroadAmp = 0.55f;
    private const float MedFreq = 10f, MedAmp = 0.3f;
    private const float FineFreq = 26f, FineAmp = 0.15f;

    private static Texture2D _albedo, _normal, _occlusion, _metallicGloss;

    public static Texture2D Albedo { get { return _albedo != null ? _albedo : (_albedo = BuildAlbedo()); } }
    public static Texture2D Normal { get { return _normal != null ? _normal : (_normal = BuildNormal()); } }
    public static Texture2D Occlusion { get { return _occlusion != null ? _occlusion : (_occlusion = BuildOcclusion()); } }
    public static Texture2D MetallicGloss { get { return _metallicGloss != null ? _metallicGloss : (_metallicGloss = BuildMetallicGloss()); } }
    /// <summary>Parallax/height source -- URP Lit samples this from the
    /// SAME channel a normal map's own height input would use; reusing
    /// the identical heightfield the normal map was derived from (not a
    /// separate pass) is what keeps the fake displacement and the bump
    /// shading in agreement with each other.</summary>
    public static Texture2D Height { get { return _height != null ? _height : (_height = BuildHeight()); } }
    private static Texture2D _height;

    /// <summary>Deterministic hash, same technique/rationale as
    /// PbrTextureAtlas.Jitter (not UnityEngine.Random -- this bakes once
    /// from fixed integer inputs, so "deterministic" just means
    /// reproducible, not that it needs to agree with any match seed).</summary>
    private static float Jitter(int x, int y, int salt)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + salt * 2246822519;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary>Smoothstep-interpolated value noise (bilinear between
    /// jittered grid corners, eased so the result doesn't read as
    /// grid-aligned) -- the standard cheap building block multi-octave
    /// procedural height/bump fields are layered from.</summary>
    private static float ValueNoise(float x, float y, int salt)
    {
        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var tx = x - x0;
        var ty = y - y0;
        var v00 = Jitter(x0, y0, salt);
        var v10 = Jitter(x0 + 1, y0, salt);
        var v01 = Jitter(x0, y0 + 1, salt);
        var v11 = Jitter(x0 + 1, y0 + 1, salt);
        var sx = tx * tx * (3f - 2f * tx);
        var sy = ty * ty * (3f - 2f * ty);
        var a = Mathf.Lerp(v00, v10, sx);
        var b = Mathf.Lerp(v01, v11, sx);
        return Mathf.Lerp(a, b, sy);
    }

    /// <summary>The one shared heightfield every map in this kit derives
    /// from -- three summed octaves (broad/medium/fine), normalized to
    /// 0..1 by construction (amplitudes sum to 1).</summary>
    private static float Height01(float u, float v)
    {
        var broad = ValueNoise(u * BroadFreq, v * BroadFreq, 10) * BroadAmp;
        var med = ValueNoise(u * MedFreq, v * MedFreq, 20) * MedAmp;
        var fine = ValueNoise(u * FineFreq, v * FineFreq, 30) * FineAmp;
        return broad + med + fine;
    }

    /// <summary>Difference-of-two-shifted-noise-fields: a well-known
    /// cheap technique for procedural crack/vein networks -- wherever the
    /// two fields nearly cancel out, the result crosses zero, and
    /// thresholding `abs(value)` against a small width reads as a thin,
    /// naturally BRANCHING line network (branches appear anywhere three
    /// or more near-zero regions meet) without needing a stateful
    /// line-tracing walk.</summary>
    private static float VeinMask(float u, float v)
    {
        var a = ValueNoise(u * 9f, v * 9f, 40);
        var b = ValueNoise(u * 9f + 37.1f, v * 9f + 91.7f, 41);
        var d = Mathf.Abs(a - b);
        return 1f - Mathf.Clamp01(d / 0.035f);   // 1 = on a vessel line, 0 = clear tissue
    }

    private static Texture2D NewTexture(bool linear)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, linear);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    /// <summary>Pale gray/pink/cream/faint-blue mottling (creator: "soft
    /// variations of pale gray, muted pink, cream, and faint blue
    /// undertones") plus a subtle height-driven shade (folds read very
    /// slightly warmer/lighter at their peaks) and the branching vessel
    /// overlay in a muted red.</summary>
    private static Texture2D BuildAlbedo()
    {
        var tex = NewTexture(false);
        var pixels = new Color32[Size * Size];
        var gray = new Color(0.72f, 0.7f, 0.7f);
        var pink = new Color(0.78f, 0.62f, 0.63f);
        var cream = new Color(0.82f, 0.76f, 0.66f);
        var blue = new Color(0.62f, 0.66f, 0.74f);
        var vesselColor = new Color(0.55f, 0.16f, 0.18f);

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var u = x / (float)Size;
            var v = y / (float)Size;
            var h = Height01(u, v);

            // large-scale mottling blend between the four base hues, each
            // driven by its OWN slow, independent noise field so the
            // color patchwork doesn't align 1:1 with the height bumps
            var wGray = ValueNoise(u * 2.2f, v * 2.2f, 50);
            var wPink = ValueNoise(u * 2.2f + 13f, v * 2.2f + 7f, 51);
            var wBlue = ValueNoise(u * 2.2f + 29f, v * 2.2f + 4f, 52);
            var total = wGray + wPink + wBlue + 0.4f;   // +0.4 reserves cream's own share
            var c = gray * (wGray / total) + pink * (wPink / total) + blue * (wBlue / total) + cream * (0.4f / total);

            // folds read a touch lighter at their peaks, darker in the
            // grooves -- ties the albedo's own shading to the same
            // heightfield the normal/AO maps use, so nothing disagrees
            var shade = 0.85f + h * 0.3f;
            c *= shade;

            var vein = VeinMask(u, v);
            c = Color.Lerp(c, vesselColor, vein * 0.7f);

            pixels[y * Size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Standard height-gradient-to-tangent-space-normal
    /// conversion (central differences on the shared heightfield,
    /// encoded as (nx*0.5+0.5, ny*0.5+0.5, nz*0.5+0.5) in RGB) -- this is
    /// the "thousands of small irregular folds and branching ridges"
    /// half of the brief: at 256x256 with three noise octaves layered
    /// in, the bump detail reads as continuous, non-repeating micro-
    /// terrain rather than a handful of discrete features.</summary>
    private static Texture2D BuildNormal()
    {
        var tex = NewTexture(true);
        var pixels = new Color32[Size * Size];
        const float step = 1f / Size;
        const float strength = 2.4f;

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var u = x / (float)Size;
            var v = y / (float)Size;
            var hL = Height01(u - step, v);
            var hR = Height01(u + step, v);
            var hD = Height01(u, v - step);
            var hU = Height01(u, v + step);
            var dx = (hR - hL) * strength;
            var dy = (hU - hD) * strength;
            var n = new Vector3(-dx, -dy, 1f).normalized;
            pixels[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Valleys darken (creator: "Use ambient occlusion to
    /// darken the valleys between folds") -- a pixel occludes when it
    /// sits BELOW a blurred/smoothed local average of its own
    /// neighborhood (i.e. it's in a groove relative to what surrounds
    /// it), which is exactly what a real fold's crevice does to nearby
    /// ambient light.</summary>
    private static Texture2D BuildOcclusion()
    {
        var tex = NewTexture(true);
        var pixels = new Color32[Size * Size];
        const float step = 1f / Size;
        // Wide enough to span roughly a fine-octave wavelength (period
        // ~1/FineFreq) so the 8-point compass ring actually samples
        // neighboring ridges/valleys instead of averaging back to nearly
        // the same point -- the original 3*step radius was well inside a
        // single fine-noise cell, so "blurred" barely differed from "h"
        // and the whole map came out almost flat (caught by the
        // brainverify harness's not-flat check, red channel only
        // 244..255 across the whole texture).
        const float blurRadius = 9f * step;
        const float gain = 4.5f;
        const float aoFloor = 0.28f;

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var u = x / (float)Size;
            var v = y / (float)Size;
            var h = Height01(u, v);
            var blurred = (
                Height01(u - blurRadius, v) + Height01(u + blurRadius, v) +
                Height01(u, v - blurRadius) + Height01(u, v + blurRadius) +
                Height01(u - blurRadius, v - blurRadius) + Height01(u + blurRadius, v - blurRadius) +
                Height01(u - blurRadius, v + blurRadius) + Height01(u + blurRadius, v + blurRadius)
            ) * 0.125f;
            var ao = 1f - Mathf.Clamp01((blurred - h) * gain) * (1f - aoFloor);
            var g = (byte)Mathf.RoundToInt(ao * 255f);
            pixels[y * Size + x] = new Color32(g, g, g, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>URP Lit's Metallic workflow samples smoothness from this
    /// texture's ALPHA channel by default (`_SmoothnessTextureChannel` ==
    /// Metallic Alpha) -- RGB stays black (this tissue has zero metallic
    /// response). Creator: "the peaks of the folds slightly smoother and
    /// more reflective while the grooves appear slightly rougher and
    /// darker" -- smoothness rides the SAME shared heightfield again, so
    /// a peak is simultaneously bump-raised, AO-bright, AND smoother, the
    /// three maps reading as one coherent surface instead of drifting
    /// independently.</summary>
    private static Texture2D BuildMetallicGloss()
    {
        var tex = NewTexture(true);
        var pixels = new Color32[Size * Size];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var u = x / (float)Size;
            var v = y / (float)Size;
            var h = Height01(u, v);
            var smoothness = 0.22f + h * 0.35f;   // valleys ~0.22 (rougher/darker), peaks ~0.57 (smoother/more reflective)
            var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(smoothness) * 255f);
            pixels[y * Size + x] = new Color32(0, 0, 0, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>URP Lit's own Parallax/Height Map input -- a plain
    /// grayscale bake of the SAME heightfield the normal map already
    /// uses, so the fake-displacement offset and the bump shading agree
    /// with each other pixel-for-pixel instead of two independently-
    /// authored maps that could visually disagree.</summary>
    private static Texture2D BuildHeight()
    {
        var tex = NewTexture(true);
        var pixels = new Color32[Size * Size];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var u = x / (float)Size;
            var v = y / (float)Size;
            var g = (byte)Mathf.RoundToInt(Height01(u, v) * 255f);
            pixels[y * Size + x] = new Color32(g, g, g, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }
}
