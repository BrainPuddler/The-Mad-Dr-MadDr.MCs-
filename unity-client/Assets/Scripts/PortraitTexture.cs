using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>2026-08 (roof portrait hologram, creator direction verbatim:
/// "I need to see the image rotating on the roof"): the base64 PNG
/// decode + cache this project's queue tiles (<see
/// cref="ProductionQueueHud"/>) already had inline, pulled out to a
/// shared static so a SECOND consumer (<see
/// cref="RoofPortraitHologram"/>) decoding the exact same <see
/// cref="GrabCursor.ProductionQueue"/> item's `PortraitPng` every frame
/// doesn't duplicate the decode OR keep its own separate cache of the
/// same bytes. A failed decode caches `null` under that same key rather
/// than retrying every frame, same posture the original inline version
/// already had.
///
/// 2026-08 follow-up (creator direction: "Add a debug alert for
/// portrait load errors. because the battalion build portrait is still
/// not visible above the factory"): the ONLY failure this used to log
/// was `Convert.FromBase64String`/`LoadImage` actually THROWING -- two
/// other silent-null paths existed with zero diagnostic: a `dataUrl`
/// with no comma at all (malformed data-URL), and `LoadImage` returning
/// `false` (well-formed bytes, just not a real image) without throwing.
/// Both now log too. Deliberately NOT logging the plain "no dataUrl at
/// all" case (`string.IsNullOrEmpty`) -- that's the normal, expected
/// state for any genome saved before portraits existed or with a failed
/// Lab upload (this pipeline's own established "optional field, never a
/// hard error" contract), not a load ERROR, which is specifically what
/// was asked for.</summary>
public static class PortraitTexture
{
    private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

    public static Texture2D For(string dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return null;
        if (_cache.TryGetValue(dataUrl, out var cached)) return cached;

        Texture2D tex = null;
        var commaIdx = dataUrl.IndexOf(',');
        if (commaIdx >= 0 && commaIdx + 1 < dataUrl.Length)
        {
            try
            {
                var bytes = Convert.FromBase64String(dataUrl.Substring(commaIdx + 1));
                var candidate = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (candidate.LoadImage(bytes))
                {
                    tex = candidate;
                }
                else
                {
                    Debug.LogWarning("PortraitTexture: Texture2D.LoadImage rejected " + bytes.Length
                        + " decoded bytes as an invalid image (data URL length " + dataUrl.Length + ").");
                    UnityEngine.Object.Destroy(candidate);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("PortraitTexture: failed decoding a portrait: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("PortraitTexture: dataUrl has no ',' separator (length " + dataUrl.Length
                + ") -- not a well-formed \"data:image/png;base64,...\" string.");
        }
        _cache[dataUrl] = tex;
        return tex;
    }
}
