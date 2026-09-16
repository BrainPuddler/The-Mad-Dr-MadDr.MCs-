using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/40 §3 item 1: a wet-response state gated to a real weather
/// toggle, not a constant material tweak -- docs/28 rows 14/15 already
/// tried an always-on smoothness bump on road materials and reverted it
/// for reading "too shiny" in the ordinary dry case (see that doc's
/// table). The fix here is conditional: dry play keeps the already-
/// tuned matte asphalt/sidewalk look completely untouched, and only a
/// real <see cref="IsRaining"/> toggle raises smoothness and darkens
/// albedo, eased over a real transition rather than an instant pop --
/// this is a physical/atmospheric change, not a human-motivated one,
/// unlike docs/28 row 37's window-light "always a hard switch, never a
/// dimmer" rule (that rule is specifically about a light someone
/// flips; weather rolling in/clearing is the opposite case).
///
/// `IsRaining` is deliberately NOT tied to the day/night cycle -- rain
/// and time of day are orthogonal, and nothing here forces them to
/// coincide, though the reference brief (docs/39 §2: "night, rain, and
/// wet-surface specular are the hero look") reads best when a player
/// happens to trigger both. See <see cref="RainToggleHud"/> for the one
/// current trigger (a visible toggle button, same idiom as
/// `WindowLightsHud`'s existing "is this environmental effect on"
/// button) -- a future match-event-driven trigger is a separate,
/// later decision, not attempted here.
/// </summary>
public static class WeatherController
{
    public static bool IsRaining;

    /// <summary>Current eased wetness, 0 = fully dry, 1 = fully wet.
    /// Read-only externally; <see cref="Tick"/> is the only writer.</summary>
    public static float Wetness { get; private set; }

    /// <summary>A full dry↔wet transition takes about this many seconds
    /// -- slow enough to read as weather actually rolling in or
    /// clearing, matching real rain's own onset/clearing pace far
    /// better than an instant toggle would.</summary>
    private const float TransitionSeconds = 6f;

    /// <summary>Called once per frame from <see
    /// cref="LumenCycleController.Update"/> -- the existing single
    /// per-frame driver for whole-scene visual state, same choke point
    /// that already ticks <see cref="NeonRegistry"/>'s boost. Rain is
    /// orthogonal to the day/night phase blend that method otherwise
    /// computes, so this call doesn't touch or depend on any of that
    /// math -- it would be equally correct called from anywhere else
    /// that reliably runs once per frame.</summary>
    public static void Tick(float dt)
    {
        var target = IsRaining ? 1f : 0f;
        Wetness = Mathf.MoveTowards(Wetness, target, dt / TransitionSeconds);
        WetSurfaceRegistry.SetWetness(Wetness);
    }
}

/// <summary>
/// Same "record each material's own base value once at mint time, then
/// apply a live blended override to every registered SHARED Material"
/// shape <see cref="NeonRegistry"/> already establishes for emissive
/// night boost -- here for smoothness/albedo darkening instead. Every
/// registered material is one of RoadDresser's own cached materials
/// (one instance shared by every road/sidewalk/plaza tile in the
/// city), so mutating it here touches the whole city at once with zero
/// per-instance cost and involves no `MaterialPropertyBlock` at all --
/// this never touches docs/39 §7's SRP-batching concern, unlike a
/// per-instance override would.
/// </summary>
public static class WetSurfaceRegistry
{
    private static readonly List<Material> Mats = new List<Material>();
    private static readonly List<Color> BaseColor = new List<Color>();
    private static readonly List<float> BaseSmoothness = new List<float>();
    private static readonly List<float> WetSmoothnessTarget = new List<float>();
    private static readonly List<float> DarkenFactor = new List<float>();
    private static readonly List<float> WetAlphaTarget = new List<float>();

    /// <summary>`wetSmoothness`/`darken` are per-material (not one global
    /// pair) so a caller can tune how strongly each surface responds --
    /// e.g. open asphalt pooling more than a curb -- without this
    /// registry needing to know why. `BaseSmoothness` is read from the
    /// material's OWN current value (whatever the shader's default is,
    /// or whatever the caller already set) rather than assumed, so this
    /// never silently overrides an unrelated tuning choice made before
    /// registration. `wetAlpha` is a SEPARATE, optional target (default
    /// -1, meaning "leave alpha exactly as it is" -- every road/sidewalk
    /// caller so far is opaque and stays opaque) for docs/40 §3 item 4's
    /// puddle decals, which need to fade IN with rain rather than just
    /// darken -- a dry puddle patch shouldn't be visible at all, unlike
    /// asphalt/sidewalk which are always fully opaque either way.</summary>
    public static void Register(Material mat, float wetSmoothness = 0.8f, float darken = 0.6f, float wetAlpha = -1f)
    {
        if (mat == null) return;
        Mats.Add(mat);
        BaseColor.Add(mat.color);
        BaseSmoothness.Add(mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f);
        WetSmoothnessTarget.Add(wetSmoothness);
        DarkenFactor.Add(darken);
        WetAlphaTarget.Add(wetAlpha);
    }

    /// <summary>Lets `RuntimeCityBuilder.ApplyWorldScaledTiling` propagate
    /// a registration onto a CLONED tiled variant of an already-
    /// registered base material -- without this, `Asphalt()` (the one
    /// registered material that carries a real `_BaseMap` texture,
    /// unlike Sidewalk/RoundaboutCurb/IslandStone/PuddleDecal, which are
    /// flat-color and never get cloned) would silently stop responding
    /// to `SetWetness` the moment a real road tile's world-scaled tiling
    /// clones it into a separate Material object this registry never
    /// sees again. A small linear scan is fine -- this list never holds
    /// more than a handful of entries (the road/sidewalk/plaza surfaces
    /// docs/40 §3 scopes items 1/4 to, not one entry per city object).</summary>
    public static bool TryGetParams(Material baseMat, out float wetSmoothness, out float darken, out float wetAlpha)
    {
        var i = Mats.IndexOf(baseMat);
        if (i < 0)
        {
            wetSmoothness = 0f; darken = 0f; wetAlpha = -1f;
            return false;
        }
        wetSmoothness = WetSmoothnessTarget[i];
        darken = DarkenFactor[i];
        wetAlpha = WetAlphaTarget[i];
        return true;
    }

    public static void SetWetness(float wetness01)
    {
        for (var i = 0; i < Mats.Count; i++)
        {
            var mat = Mats[i];
            if (mat == null) continue;
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", Mathf.Lerp(BaseSmoothness[i], WetSmoothnessTarget[i], wetness01));
            var rgb = Color.Lerp(BaseColor[i], BaseColor[i] * DarkenFactor[i], wetness01);
            // WetAlphaTarget < 0 is the "don't touch alpha" sentinel --
            // Lerp(BaseAlpha, BaseAlpha, t) is exactly BaseAlpha for any
            // t, so every pre-existing caller's opaque materials are
            // completely unaffected by this addition.
            var alphaTarget = WetAlphaTarget[i] >= 0f ? WetAlphaTarget[i] : BaseColor[i].a;
            var a = Mathf.Lerp(BaseColor[i].a, alphaTarget, wetness01);
            mat.color = new Color(rgb.r, rgb.g, rgb.b, a);
        }
    }
}
