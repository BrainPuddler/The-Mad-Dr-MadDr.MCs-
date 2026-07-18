using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Bridge dressing (docs/21 batch 2, item 1): guardrails along the deck
/// edges, a through-truss arch over water spans, and piers dropping into
/// the riverbed -- replaces the old flat brown deck slab with something
/// that reads as a built crossing, not a plank. Colliderless, like the
/// deck it dresses (bridges were never click targets); the bridge
/// footprint hex set stays the sole pathing truth, untouched.
/// </summary>
public static class BridgeDresser
{
    private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

    private static Material M(float r, float g, float b)
    {
        var key = ((int)(r * 255) << 16) | ((int)(g * 255) << 8) | (int)(b * 255);
        Material mat;
        if (Cache.TryGetValue(key, out mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(r, g, b);
        Cache[key] = mat;
        return mat;
    }

    private static Material Deck() { return M(0.42f, 0.3f, 0.18f); }
    private static Material Truss() { return M(0.32f, 0.22f, 0.12f); }
    private static Material Pier() { return M(0.5f, 0.49f, 0.46f); }
    private static Material Rail() { return M(0.28f, 0.2f, 0.11f); }

    // Deck top sits at ~0.3, matching RoadDresser's "slightly proud of
    // the ground" asphalt height -- NOT an arbitrary choice: TerrainField
    // flat-locks bridge footprint hexes to exactly y=0 (same rule as
    // roads/buildings), and every ground unit's GroundHeightAt puts its
    // feet AT that flat-locked height. The deck used to span y=[0, 1.2]
    // (top at 1.2), which put a unit's feet exactly at the deck's BOTTOM
    // face -- units visually clipped through/under a meter-plus of solid
    // "deck" instead of standing on top of it. A thin plank at road
    // height reads as a surface units actually walk ON.
    private const float DeckY = 0.05f;
    private const float DeckHeight = 0.5f;
    private const float DeckHalfWidth = 9f; // matches the massing cube's hexSize*0.9 footprint

    private static int Hash(HexCoord hex, int salt)
    {
        unchecked
        {
            var h = hex.Q * 374761393 + hex.R * 668265263 + salt * 974711;
            h = (h ^ (h >> 13)) * 1274126177;
            return h & 0x7FFFFFFF;
        }
    }

    public static void Build(RuntimeCityBuilder builder, CityModel city, Transform parent)
    {
        var host = new GameObject("Bridges").transform;
        host.SetParent(parent, false);

        var water = new HashSet<HexCoord>(city.Water);

        // the SAME network RoadDresser straightens against -- bridge
        // hexes are already ordinary members of city.Roads, so this is
        // one shared set, not a bridge-local one, for the whole method
        var network = new HashSet<HexCoord>(city.Roads);

        foreach (var bridge in city.Bridges)
        {
            foreach (var hex in bridge.Footprint)
            {
                // CARDINAL alignment, shared with RoadDresser so a bridge
                // lands on the same straight centerline as the road it
                // meets and its own neighboring bridge hexes -- bridge
                // hexes are ordinary members of city.Roads, so a
                // "vertical" corridor crossing the river must resolve to
                // the same due-N/S geometry the approach road does, not a
                // per-hex diagonal kink. Facing = the road direction from
                // this hex's own cardinal neighbors (a bridge across the
                // horizontal river is a vertical street -> faces N/S).
                var card = RoadDresser.CardinalNeighbors(hex, network);
                var center = RoadDresser.CardinalAnchor(builder, hex, card.Vertical);
                var facing = card.Vertical
                    ? new Vector3(0f, 0f, card.N ? -1f : 1f)
                    : new Vector3(card.E ? 1f : -1f, 0f, 0f);
                var perp = new Vector3(facing.z, 0f, -facing.x);
                var deckRot = Quaternion.LookRotation(facing, Vector3.up);

                // deck: a rectangle running WITH the road -- narrow across
                // (rail-to-rail span, so the rails sit right at its edges
                // instead of floating past or recessed into it) and long
                // along the direction of travel (matching the rails' own
                // length, a near-full hex pitch so consecutive bridge
                // hexes tile with no visible gap), rotated to `deckRot`
                var deck = builder.SpawnPrim(PrimitiveType.Cube, center + Vector3.up * DeckY,
                    new Vector3(DeckHalfWidth * 2f * 0.92f, DeckHeight, (float)HexCoord.HexMeters * 0.95f),
                    Deck(), host);
                deck.transform.rotation = deckRot;

                foreach (var side in new[] { 1f, -1f })
                {
                    var rail = builder.SpawnPrim(PrimitiveType.Cube,
                        center + perp * (side * DeckHalfWidth * 0.92f) + Vector3.up * (DeckY + DeckHeight * 0.5f + 0.4f),
                        new Vector3(0.3f, 0.8f, (float)HexCoord.HexMeters * 0.95f), Rail(), host);
                    rail.transform.rotation = deckRot;
                }

                // through-truss arch over water crossings only -- approach/
                // embankment hexes of the same bridge stay open
                var isWater = false;
                foreach (var n in hex.Neighbors())
                    if (water.Contains(n)) { isWater = true; break; }

                if (isWater)
                {
                    // truss anchor heights are expressed relative to the
                    // deck's TOP face, not its center -- so the arch keeps
                    // its proportions if DeckY/DeckHeight ever change again
                    var deckTop = DeckY + DeckHeight * 0.5f;
                    foreach (var side in new[] { 1f, -1f })
                    {
                        var beamBase = center + perp * (side * DeckHalfWidth * 0.9f) + Vector3.up * (deckTop - 0.2f);
                        var beam = builder.SpawnPrim(PrimitiveType.Cube,
                            beamBase + Vector3.up * 2.6f, new Vector3(0.4f, 5.4f, 0.4f), Truss(), host);
                        beam.transform.rotation = Quaternion.LookRotation(perp, Vector3.up) * Quaternion.Euler(0f, 0f, side * 18f);
                    }
                    var topChord = builder.SpawnPrim(PrimitiveType.Cube,
                        center + Vector3.up * (deckTop + 4.5f), new Vector3(DeckHalfWidth * 1.9f, 0.4f, 1.4f), Truss(), host);
                    topChord.transform.rotation = deckRot;

                    // piers: drop from under the deck to the carved riverbed
                    // depth -- TerrainField flat-locks bridge hexes to y=0,
                    // but the water it crosses is still bedded at
                    // WaterBedDepth, so real foundations reach that far down
                    var pierTop = DeckY - DeckHeight * 0.5f;
                    var pierBottom = TerrainField.WaterBedDepth - 0.6f;
                    var pierHeight = pierTop - pierBottom;
                    foreach (var side in new[] { 0.55f, -0.55f })
                    {
                        builder.SpawnPrim(PrimitiveType.Cylinder,
                            center + perp * (side * DeckHalfWidth * 0.6f) + Vector3.up * (pierBottom + pierHeight * 0.5f),
                            new Vector3(0.9f, pierHeight * 0.5f, 0.9f), Pier(), host);
                    }
                }
            }
        }
    }
}
