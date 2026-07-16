using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared registry of every emissive "neon" material the dressers mint
/// (signage, bulbs, ad art) so NightMode can dim/boost them all at once
/// without either dresser needing to know NightMode exists. Each entry's
/// base emission color is recorded once, at mint time, so repeated day/
/// night toggles stay stable instead of drifting from compounding
/// multiplies applied on top of a previous boost.
/// </summary>
public static class NeonRegistry
{
    private static readonly List<Material> Mats = new List<Material>();
    private static readonly List<Color> BaseEmission = new List<Color>();

    public static void Register(Material mat, Color baseEmission)
    {
        Mats.Add(mat);
        BaseEmission.Add(baseEmission);
    }

    public static void SetBoost(float boost)
    {
        for (var i = 0; i < Mats.Count; i++)
        {
            if (Mats[i] == null) continue;
            Mats[i].SetColor("_EmissionColor", BaseEmission[i] * boost);
        }
    }
}
