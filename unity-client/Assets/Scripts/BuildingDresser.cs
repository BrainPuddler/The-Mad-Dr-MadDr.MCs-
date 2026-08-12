using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// 1950s miniature-set dressing for the generated buildings (docs/21
/// Phase 3). The collider cube stays the massing core and the click/
/// damage handle; this adds a colliderless DRESSING HOLDER per building
/// hex, registered into the same cubes list RuntimeCityBuilder's damage
/// pipeline walks -- so water towers crush into the rubble pancake and
/// signage tints with the cracked walls, exactly like the massing.
///
/// Style targets (creator brief): mid-century Americana read from RTS
/// camera height -- suburban gables and gas stations, brick walk-ups
/// with fire escapes, stepped deco office towers, marquee'd civic
/// landmarks -- with a rooftop kit (water towers, antenna masts, vents,
/// billboards) doing most of the silhouette work. Everything is
/// primitives + shared cached materials (SRP-batcher friendly, no asset
/// pipeline -- the project's established prefab-free workflow), and
/// every choice hashes off the building's first footprint hex, so the
/// same seed always dresses the same city.
/// </summary>
public static class BuildingDresser
{
    // ---- 1950s palette (shared cached materials) ------------------------------
    private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

    // docs/23 Phase 10.3: a SEPARATE small cache for the PBR-atlas-backed
    // materials (see MTextured) -- keyed by name rather than reusing the
    // packed-color int key above, so this never risks colliding with M()'s
    // existing bit-packing scheme.
    private static readonly Dictionary<string, Material> TexturedCache = new Dictionary<string, Material>();

    // 2026-08 (creator direction: "Add a real triangle primitive to roof
    // generator NOT a Cube on it's side"): one shared mesh instance for
    // every pitched-roof spawn (house gable, apartment hip) -- unlike
    // ProceduralMeshKit.FlameShard/CloudShard, this shape has no per-call
    // jitter, so there's nothing instance-specific to regenerate; every
    // caller just varies position/scale/material on the transform, same
    // as CreatePrimitive's own shared-mesh-per-type behavior.
    private static Mesh _gableRoofMesh;

    private static Mesh GableRoofMesh()
    {
        if (_gableRoofMesh == null) _gableRoofMesh = ProceduralMeshKit.GableRoof();
        return _gableRoofMesh;
    }

    private static Material M(float r, float g, float b, float emissive = 0f)
    {
        var key = ((int)(r * 255) << 20) | ((int)(g * 255) << 10) | (int)(b * 255) | ((int)(emissive * 3) << 30);
        Material mat;
        if (Cache.TryGetValue(key, out mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(r, g, b);
        if (emissive > 0.01f)
        {
            mat.EnableKeyword("_EMISSION");
            var baseEmission = new Color(r, g, b) * emissive;
            mat.SetColor("_EmissionColor", baseEmission);
            NeonRegistry.Register(mat, baseEmission);
        }
        Cache[key] = mat;
        return mat;
    }

    /// <summary>docs/23 Phase 10.3: same idea as M(), but also applies a
    /// PbrTextureAtlas placeholder texture -- "dressers keep their
    /// geometry logic, gain material richness."
    ///
    /// 2026-08 (docs/30 Tier 1): the (3,3) scale set here is now only the
    /// FALLBACK, applied if a caller somehow bypasses `RuntimeCityBuilder.
    /// SpawnPrim`. Every real call site goes through `SpawnPrim`, which
    /// now overrides `_BaseMap_ST` per instance via a `MaterialPropertyBlock`
    /// sized from that instance's own world scale -- the fix for the
    /// exact "SAME 0..1 UV rect stretches across a 1m curb prop or a 30m
    /// building wall equally" gap this comment used to flag as an open
    /// v0.1 simplification. See `SpawnPrim.ApplyWorldScaledTiling`'s own
    /// doc comment for the reasoning; this material's own SHARED tiling
    /// stays a harmless, never-actually-visible default now that every
    /// consumer overrides it per instance.</summary>
    private static Material MTextured(string key, float r, float g, float b, Texture2D tex)
    {
        Material mat;
        if (TexturedCache.TryGetValue(key, out mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(r, g, b);
        if (tex != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
        }
        TexturedCache[key] = mat;
        return mat;
    }

    private static Material Brick() { return MTextured("brick", 0.55f, 0.27f, 0.2f, PbrTextureAtlas.Brick); }
    // 2026-08 (creator direction: "apply all texture and displacement
    // map details to city building"): these three were the last flat,
    // untextured WALL colors in this file -- every other wall/trim
    // material already went through MTextured, these three alone still
    // rendered as pure flat color. PbrTextureAtlas.Limestone (same
    // "one texture, several tinted material variants" precedent
    // Concrete()/the faction-stone materials already use) reads fine as
    // painted clapboard/stucco surface variation too, not just cut
    // stone -- the mottling is generic enough weathering to serve
    // either read.
    private static Material Cream() { return MTextured("cream-clapboard", 0.87f, 0.82f, 0.68f, PbrTextureAtlas.Limestone); }
    private static Material Seafoam() { return MTextured("seafoam-clapboard", 0.62f, 0.78f, 0.68f, PbrTextureAtlas.Limestone); }
    private static Material Mustard() { return MTextured("mustard-clapboard", 0.82f, 0.66f, 0.25f, PbrTextureAtlas.Limestone); }
    private static Material Concrete() { return MTextured("limestone", 0.62f, 0.6f, 0.55f, PbrTextureAtlas.Limestone); }
    private static Material Chrome() { return MTextured("chrome", 0.78f, 0.8f, 0.82f, PbrTextureAtlas.Chrome); }
    private static Material WindowBand() { return MTextured("glass", 0.16f, 0.2f, 0.28f, PbrTextureAtlas.Glass); }

    // docs/28 (city lighting system): "house and apartment windows."
    // ONE shared emissive material for every "this window is lit"
    // instance (SRP-batcher friendly, same cache-and-reuse convention as
    // every other material here) -- per-instance variation (which
    // windows are lit, and their independent flicker) comes from
    // EmissiveAnimator's MaterialPropertyBlock layer on top, not from
    // minting a separate Material per window.
    private static readonly Color WindowGlowColor = new Color(1f, 0.85f, 0.55f);
    private static Material WindowGlow() { return M(1f, 0.85f, 0.55f, CityLightingProfile.Active.BulbEmissiveBase * 0.9f); }

    // 2026-08 ("AAA upgrades" pass, creator direction: "Create texture
    // maps. Brick and lime stone, roof shingles that match the b-movie
    // style"): PbrTextureAtlas.RoofShingle's neutral gray base tinted
    // per use, same "one texture, several material-color variants"
    // precedent Concrete()/faction-stone materials already use for
    // Limestone. RoofTar (the flat cap every tier still spawns) and the
    // gable/hip pitched-roof shapes (ProceduralMeshKit.GableRoof) all
    // read as real shingle coursing now instead of a flat color.
    private static Material RoofTar() { return MTextured("roof-shingle-tar", 0.24f, 0.23f, 0.21f, PbrTextureAtlas.RoofShingle); }
    private static Material RoofShingleWarm() { return MTextured("roof-shingle-warm", 0.5f, 0.24f, 0.16f, PbrTextureAtlas.RoofShingle); }
    private static Material RoofShingleCool() { return MTextured("roof-shingle-cool", 0.35f, 0.42f, 0.5f, PbrTextureAtlas.RoofShingle); }
    // Genuinely a structural surface color (water-tower tanks, dome
    // roofs, canopy trim -- see call sites), not a sign/neon color, so
    // it gets the same texture treatment as Cream/Seafoam/Mustard above
    // rather than staying flat.
    private static Material RustRed() { return MTextured("rust-red-surface", 0.5f, 0.24f, 0.16f, PbrTextureAtlas.Limestone); }
    private static Material NeonRed() { return M(0.95f, 0.25f, 0.3f, 1.6f); }
    private static Material NeonTeal() { return M(0.3f, 0.9f, 0.85f, 1.6f); }
    private static Material SignWhite() { return M(0.92f, 0.9f, 0.82f, 0.6f); }
    private static Material GardenGreen() { return M(0.3f, 0.42f, 0.24f); }

    private static int Hash(HexCoord hex, int salt)
    {
        unchecked
        {
            var h = hex.Q * 374761393 + hex.R * 668265263 + salt * 974711;
            h = (h ^ (h >> 13)) * 1274126177;
            return h & 0x7FFFFFFF;
        }
    }

    // ---- docs/30: facade grammar (WFC) integration -----------------------

    /// <summary>Master switch for the docs/30 facade grammar. ON: Medium
    /// and Large tiers get street/alley/party-wall-aware facades solved by
    /// <see cref="FacadeGrammar"/> and built through <see cref="FacadeKit"/>'s
    /// PropLibrary mesh-swap points. OFF: the pre-docs/30 window-strip
    /// dressing, byte-for-byte.
    ///
    /// Deliberately defaults ON -- the creator's ask was to slot this into
    /// the game, not to ship it dormant behind a flag nobody flips (a
    /// failure mode this project has hit before: every pre-match picker
    /// ever built sat switched off in the only real scene). It stays a
    /// single bool so a bad visual verdict is one edit to revert, and the
    /// legacy path below is untouched and still reachable.</summary>
    public static bool UseFacadeGrammar = true;

    /// <summary>Small tier keeps its whole-building archetypes (house,
    /// gas station, diner) and Landmark keeps its civic set pieces --
    /// those are already period-specific silhouettes, and a facade grid
    /// would fight them rather than improve them. The grammar targets
    /// exactly the tiers that are currently an extruded block with one
    /// continuous window strip, which is where the "procedurally
    /// assembled" read actually comes from.</summary>
    private static bool GrammarAppliesTo(BuildingTier tier)
    {
        return tier == BuildingTier.Medium || tier == BuildingTier.Large;
    }

    // City-wide occupancy index, rebuilt once per CityModel (reference-
    // compared, so a new city rebuilds it and a re-dress of the same city
    // reuses it). Face classification needs "is the neighbouring hex a
    // road / another building?", and asking that per building against the
    // full building list would be O(n^2) over ~19k BigCity buildings.
    private static CityModel _indexedCity;
    private static HashSet<HexCoord> _roadIndex;
    private static HashSet<HexCoord> _buildingIndex;

    private static void EnsureCityIndex(CityModel city)
    {
        if (city == null || ReferenceEquals(_indexedCity, city)) return;
        _indexedCity = city;
        _roadIndex = new HashSet<HexCoord>(city.Roads);
        _buildingIndex = new HashSet<HexCoord>();
        for (var i = 0; i < city.Buildings.Count; i++)
        {
            var fp = city.Buildings[i].Footprint;
            for (var j = 0; j < fp.Count; j++) _buildingIndex.Add(fp[j]);
        }
    }

    private static Material DarkRecess() { return M(0.09f, 0.09f, 0.11f); }
    private static Material IronDark() { return M(0.15f, 0.15f, 0.17f); }

    /// <summary>Solve and build every face of one footprint hex.
    ///
    /// Cost discipline, stated rather than discovered later: only STREET
    /// faces get the full grammar. Alley faces get their crown only (a
    /// cornice genuinely does run around a corner, so this is period-
    /// correct rather than a pure saving) and party walls get nothing at
    /// all. That keeps a grammar-dressed hex in the same order of
    /// magnitude as the window-strip dressing it replaces instead of
    /// multiplying it by four faces.</summary>
    private static void DressFacadeGrammar(RuntimeCityBuilder b, Building building, HexCoord hex,
        Transform t, float height, int h, bool industrial, bool suburb, Material wall)
    {
        EnsureCityIndex(b.City);
        if (_roadIndex == null || _buildingIndex == null) return;

        var footprint = new HashSet<HexCoord>(building.Footprint);
        var roles = FacadeGrammar.ClassifyFaces(hex, footprint, _roadIndex, _buildingIndex);
        var style = FacadeGrammar.StyleFor(building.Tier, industrial, suburb);

        var floors = Mathf.Clamp(Mathf.RoundToInt(height / 4.2f) - 1, 1, 8);

        // One fire escape per BUILDING, not per face -- a constraint that
        // spans faces and so can't live inside a single-face solve. The
        // hash decides which street face is eligible, deterministically.
        var escapeFace = h % 4;

        var mats = new FacadeMaterials
        {
            Wall = wall,
            Stone = Concrete(),
            Trim = (h / 5) % 2 == 0 ? Cream() : Mustard(),
            Glass = WindowBand(),
            GlassLit = WindowGlow(),
            DarkRecess = DarkRecess(),
            Iron = IronDark(),
            Rust = RustRed(),
            Sign = SignWhite(),
            GlowColor = WindowGlowColor,
            // Same "roughly 2 in 5 are lit" read the strip dressing had,
            // decided per building rather than per strip.
            WindowsLit = (h / 11) % 5 < 2,
            // 2026-08, two rounds. Round 1 ("2 window per building is not
            // enough, at least 30-40% of the lights should be on in the
            // building at night"): the flat `GlowBudget = 2` this used to
            // be was sized (docs/12) so a grammar-dressed building
            // registered no MORE glow entries than the single per-floor
            // strip pair it replaced -- a real docs/30 perf constraint at
            // the time, smaller now that EmissiveAnimator.Tick()'s
            // distance culling (added since) already skips any
            // registration outside camera range for free. First fix
            // scaled the budget with `floors` instead -- still a
            // sequential FIRST-N-windows counter, which turned out to
            // have its own bug (round 2 below).
            //
            // Round 2, immediate creator correction with ASCII art showing
            // the actual on-screen pattern: solid lit rows stacked under
            // solid dark rows ("XX XX XX / OO OO OO"), not scattered --
            // "it should be more like this: XX OO OX ... Random." Root
            // cause: `GlowBudget` was consumed by a sequential counter
            // (`TryTakeGlowBudget`) that grants to the FIRST N windows
            // encountered in build order (bottom floor upward, left to
            // right) and refuses every one after -- so the SAME
            // contiguous block (whichever floors happened to build before
            // the budget ran out) lit every time, and every floor above it
            // was permanently dark, never just probabilistically off.
            // Replaced with `GlowChance`, an independent per-window roll
            // (`FacadeMaterials.RollGlow`) -- no ordering, no clumping,
            // each window's own hash decides for itself, same mechanism
            // `SpawnWindowStrip` already uses for Small-tier buildings
            // (which never had this bug for exactly that reason). 0.45
            // targets roughly the same registered fraction the old
            // `floors * 2` scaling aimed for, now scattered instead of
            // blocked -- see FacadeMaterials.GlowChance's own doc comment.
            GlowChance = 0.45f,
        };

        for (var f = 0; f < 4; f++)
        {
            var role = roles[f];
            var face = (FacadeFace)f;

            if (role != FaceRole.Street)
            {
                // 2026-08, two rounds. Round 1 ("windows on every side
                // except those where there is a very narrow distance
                // between separate building walls that are very close
                // together"): gave Alley faces (open ground that isn't a
                // road) real windows instead of the old floors-forced-to-0
                // crown-only strip, but kept skipping `FaceRole.PartyWall`
                // faces entirely (a neighbor hex belongs to another
                // building's footprint, or to this SAME building's own
                // footprint for a multi-hex Medium/Large building) as the
                // genuine "very narrow/touching" case the request wanted
                // excluded.
                //
                // Round 2, immediate follow-up: "if it's more
                // computationally more expensive ignore - remove the
                // close building window rule." Rather than keep the
                // PartyWall/Alley split (which needs its own solve branch
                // and its own IsFallback check), PartyWall now folds into
                // this SAME branch -- every non-Street face gets windows,
                // full stop, no distinction left for "how close is the
                // neighbor." `FacadeGrammar.Solve` itself still hardcodes
                // an all-Blank result if passed `FaceRole.PartyWall`
                // (`role == FaceRole.PartyWall` short-circuits to blanks
                // regardless of `floors` -- see that method), so this
                // deliberately passes `FaceRole.Alley`, not `role`, to get
                // the real windowed solve either way.
                //
                // Known, accepted consequence: for a multi-hex Large/
                // Medium building, the face between two hexes of the SAME
                // building is genuinely interior -- physically pressed
                // against its own neighboring massing cube with no gap,
                // never visible from outside. Windows spawned there are
                // wasted geometry/registrations, not a visual bug (nothing
                // renders where it'd be seen), but also not free -- the
                // simplification this round asked for accepts that cost
                // rather than keeping the extra classification logic
                // needed to tell "my own other hex" apart from "someone
                // else's close wall."
                var alley = FacadeGrammar.Solve(FaceRole.Alley, floors, style,
                    new Rng(unchecked((uint)(h + f * 7919))), allowFireEscape: false);
                if (!alley.IsFallback) FacadeKit.BuildFace(b, t, t.position, face, alley, height, mats);
                continue;
            }

            var solution = FacadeGrammar.Solve(role, floors, style,
                new Rng(unchecked((uint)(h + f * 7919))),
                allowFireEscape: f == escapeFace);

            // A contradiction falls back to legacy dressing for this face
            // rather than a half-built one -- the current dresser IS the
            // emergency layout, already shipped and known-good.
            if (solution.IsFallback) continue;

            FacadeKit.BuildFace(b, t, t.position, face, solution, height, mats);
        }
    }

    /// <summary>Dress one building. Adds one holder GameObject per
    /// footprint hex into `cubes` (the damage pipeline's list). The
    /// FIRST hex is the "primary": it carries the rooftop kit and
    /// signage; secondary hexes of multi-hex buildings get facade work
    /// only, so a 3-hex office doesn't sprout 3 water towers. `industrial`
    /// (docs/21 batch 2, item 6) re-skins Small/Medium tiers as warehouse
    /// stock inside a rail_depot landmark's radius -- Large/Landmark keep
    /// their usual look, since a factory reads fine as a stepped office
    /// shell and the depot itself already carries the archetype set piece.
    /// `suburb` (docs/21 batch 2, item 10 -- previously only tinted the
    /// massing cube; this extends the same bias one level deeper into the
    /// dressing's own wall/roof material choice) shifts Small/Medium's
    /// palette warmer toward the outskirts and cooler downtown, WITHOUT
    /// going monotone -- still hash-varied, just reweighted; skipped for
    /// `industrial` (a warehouse stays utilitarian regardless of district)
    /// and for Large/Landmark (they cluster near downtown by construction
    /// anyway, per the massing-tint precedent this mirrors).</summary>
    public static void Dress(RuntimeCityBuilder builder, Building building, float height,
        List<GameObject> cubes, Transform parent, bool industrial = false, bool suburb = false,
        CityRegion region = CityRegion.Generic)
    {
        var footprint = building.Footprint;
        for (var i = 0; i < footprint.Count; i++)
        {
            var hex = footprint[i];
            var holder = new GameObject("Dressing_" + hex.Q + "_" + hex.R);
            holder.transform.SetParent(parent, false);
            holder.transform.position = builder.WorldOf(hex);
            cubes.Add(holder);

            var h = Hash(hex, 1);
            var primary = i == 0;
            switch (building.Tier)
            {
                case BuildingTier.Landmark:
                    DressLandmark(builder, building.Archetype, holder.transform, height, h, primary, region);
                    break;
                case BuildingTier.Large:
                    DressOffice(builder, building, hex, holder.transform, height, h, primary, industrial, suburb, region);
                    break;
                case BuildingTier.Medium:
                    if (industrial) DressIndustrial(builder, holder.transform, height, h, primary);
                    else DressApartment(builder, building, hex, holder.transform, height, h, primary, suburb, region);
                    break;
                default:
                    if (industrial) DressIndustrial(builder, holder.transform, height, h, primary);
                    else DressSmall(builder, hex, holder.transform, height, h, primary, suburb);
                    break;
            }
        }
    }

    /// <summary>2026-08 (docs/30 Tier 1, "route Region into the dressers"):
    /// `CityModel.Region` was already threaded to `LumenCycleController`
    /// (the lighting GRADE) but never reached the buildings themselves --
    /// New York, Paris and Montreal rendered with identical architecture
    /// despite `CityPreset`'s own doc comment promising a
    /// "BuildingDresser/RoadDresser palette + prop switches." This is that
    /// switch, applied to the wall-material weighted pick specifically
    /// (the highest-visibility, most-repeated color choice a Medium
    /// building makes).
    ///
    /// `CityRegion.Generic` -- every pre-docs/30 preset (Village/SmallTown/
    /// BigCity) -- reproduces the ORIGINAL `(h/7)%4` expression verbatim,
    /// not an approximation of it, so nothing about an existing city's
    /// appearance changes. NY/Paris/Montreal reweight the SAME three-
    /// material palette rather than inventing new colors, mirroring
    /// `LumenCycleController.ApplyRegionTint`'s own NY=cooler/steel,
    /// Paris=warmer/cream, Montreal=cooler/flatter read one layer
    /// deeper -- the same semantic mapping the lighting grade already
    /// established, not a separately invented one.</summary>
    private static Material RegionWall(int h, bool suburb, CityRegion region)
    {
        if (region == CityRegion.Generic)
        {
            return suburb
                ? ((h / 7) % 4 < 2 ? Cream() : (h / 7) % 4 == 2 ? Brick() : Seafoam())
                : ((h / 7) % 4 < 2 ? Seafoam() : (h / 7) % 4 == 2 ? Cream() : Brick());
        }

        var pct = (h / 7) % 100;
        switch (region)
        {
            case CityRegion.NewYork:
                return pct < 55 ? Seafoam() : pct < 85 ? Brick() : Cream();
            case CityRegion.Paris:
                return pct < 60 ? Cream() : pct < 85 ? Brick() : Seafoam();
            case CityRegion.Montreal:
                return pct < 45 ? Seafoam() : pct < 85 ? Cream() : Brick();
            default:
                return suburb ? Cream() : Seafoam();
        }
    }

    /// <summary>Same region-aware reweighting as <see cref="RegionWall"/>,
    /// applied to the office/civic trim pick (Cream vs. Mustard) --
    /// Generic keeps the exact original `(h/5)%2` expression.</summary>
    private static Material RegionTrim(int h, CityRegion region)
    {
        if (region == CityRegion.Generic) return (h / 5) % 2 == 0 ? Cream() : Mustard();

        var pct = (h / 5) % 100;
        switch (region)
        {
            case CityRegion.NewYork: return pct < 70 ? Mustard() : Cream();     // grittier, less gold trim
            case CityRegion.Paris: return pct < 70 ? Cream() : Mustard();       // pale stone trim dominant
            case CityRegion.Montreal: return pct < 50 ? Cream() : Mustard();
            default: return Cream();
        }
    }

    // massing cubes are hexSize*0.9 = 18m across; faces sit at +-9m.
    // Public: RoadDresser's street-furniture placement needs this to keep
    // poles/lights clear of a building occupying the hex across the curb
    // (see RoadDresser.ClearLateralOffset).
    public const float Half = 9f;

    // 2026-08 (docs/30 Tier 1c, "roof-form variety"): apartment-tier roof
    // massing sits ON TOP of the flat tar cap (which still always spawns --
    // it's the base every variant shares), so its own lift starts from the
    // cap's top surface (0.65 cap center + 0.1 half-thickness), not from
    // `height` directly.
    private const float ApartmentRoofCapTop = 0.75f;
    // 2026-08 (creator direction: "Add a real triangle primitive to roof
    // generator NOT a Cube on it's side"): same ProceduralMeshKit.
    // GableRoof real triangular prism as DressSmall's gable, sized
    // shallower -- an apartment block reads as a modest peaked roof, not
    // a house-sized A-frame stacked on a mid-rise. See GableApexOffset's
    // own doc comment for why the apex offset is now just the height,
    // no sqrt(2) math needed.
    private const float ApartmentHipWidth = 8f;
    private const float ApartmentHipHeight = 4f;
    private const float ApartmentHipLength = 18.6f;
    private const float ApartmentHipApexOffset = ApartmentRoofCapTop + ApartmentHipHeight;
    private const float ApartmentParapetHeight = 1.4f;

    // ---- small tier: suburbia / roadside America ------------------------------

    // 2026-08 (creator direction: "Add a real triangle primitive to roof
    // generator NOT a Cube on it's side"): a real ProceduralMeshKit.
    // GableRoof triangular prism, replacing the old rotated-cube-diamond
    // trick. The mesh's local Y spans -0.5..0.5 (eaves to ridge), so at
    // scale.y = GableRoofHeight, positioning the prim's CENTER at
    // `height + GableRoofHeight * 0.5f` sits its base (the eaves) flush
    // on the roof plane and its apex (the ridge) exactly GableRoofHeight
    // above `height` -- no sqrt(2) needed anymore, the apex offset IS the
    // scale, because this is now an honest triangle instead of half a
    // rotated square.
    private const float GableRoofWidth = 15.5f;
    private const float GableRoofHeight = 7.8f;
    private const float GableRoofLength = 17.5f;
    private const float GableApexOffset = GableRoofHeight;

    private static void DressSmall(RuntimeCityBuilder b, HexCoord hex, Transform t, float height, int h, bool primary, bool suburb = false)
    {
        var basePos = t.position;
        // suburb: house-heavy (60/20/20 house/gas/diner); downtown:
        // commercial-heavy (20/40/40) -- still hash-varied, just reweighted
        var pick = suburb
            ? (h % 5 < 3 ? 0 : h % 5 == 3 ? 1 : 2)
            : (h % 5 < 1 ? 0 : h % 5 < 3 ? 1 : 2);
        switch (pick)
        {
            case 0:   // suburban house: pitched gable roof + chimney
            {
                var roofMat = suburb
                    ? ((h / 3) % 3 != 0 ? RoofShingleWarm() : RoofShingleCool())   // warm roof more often
                    : ((h / 3) % 3 == 0 ? RoofShingleWarm() : RoofShingleCool());  // cool slate more often
                // a real triangular-prism gable roof (ProceduralMeshKit.
                // GableRoof) instead of a rotated cube -- see
                // GableApexOffset's own doc comment for the placement math
                b.SpawnMesh(GableRoofMesh(),
                    basePos + Vector3.up * (height + GableRoofHeight * 0.5f),
                    new Vector3(GableRoofWidth, GableRoofHeight, GableRoofLength), roofMat, t, matte: true);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(4f, height + 4.3f, 3f),
                    new Vector3(1.4f, 2.6f, 1.4f), Brick(), t);   // chimney, poking through the roof plane
                // 2026-07 bug fix: this IS solid, walkable roof massing
                // (not rooftop-kit clutter) -- a flyer should be able to
                // land on the ridge, not sink to the flat tier height.
                b.RegisterRoofLandingHeight(hex, height + GableApexOffset);
                break;
            }
            case 1:   // gas station: forecourt canopy on poles + pylon sign
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 5.4f, 11f),
                    new Vector3(14f, 0.7f, 8f), Chrome(), t);   // canopy
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-5f, 2.7f, 13f),
                    new Vector3(0.5f, 2.7f, 0.5f), Concrete(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(5f, 2.7f, 13f),
                    new Vector3(0.5f, 2.7f, 0.5f), Concrete(), t);
                if (primary)
                {
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-8f, 4.5f, 8f),
                        new Vector3(0.4f, 4.5f, 0.4f), Concrete(), t);   // pylon
                    b.SpawnPrim(PrimitiveType.Sphere, basePos + new Vector3(-8f, 9.6f, 8f),
                        new Vector3(2.6f, 2.6f, 0.9f), NeonRed(), t);    // round sign
                }
                break;
            }
            default:  // diner: chrome band + rooftop sign
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height - 0.8f),
                    new Vector3(18.6f, 0.9f, 18.6f), Chrome(), t);   // wraparound chrome trim
                if (primary)
                {
                    b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 2.2f, 0f),
                        new Vector3(9f, 3f, 0.6f), SignWhite(), t);      // roof sign board
                    b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 2.2f, 0.5f),
                        new Vector3(7.4f, 1.2f, 0.4f), NeonTeal(), t);   // neon script strip
                }
                break;
            }
        }
    }

    // ---- railyard/industrial re-skin (docs/21 batch 2, item 6) -----------------

    // Corrugated metal siding/roofing -- a real structural surface (the
    // industrial re-skin's own roof/tank/drum shapes below), so it gets
    // PbrTextureAtlas.PaintedMetal (the same texture RoadDresser's own
    // PoleMetal() already tints for painted street furniture) rather
    // than staying flat.
    private static Material Corrugated() { return MTextured("corrugated-metal", 0.46f, 0.44f, 0.42f, PbrTextureAtlas.PaintedMetal); }

    private static void DressIndustrial(RuntimeCityBuilder b, Transform t, float height, int h, bool primary)
    {
        var basePos = t.position;
        // flat corrugated roof band + a loading dock canopy out front --
        // reads as a warehouse from RTS height, not a walk-up
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.3f),
            new Vector3(19f, 0.5f, 19f), Corrugated(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 2.4f, Half * 1.1f),
            new Vector3(9f, 3.2f, 2.2f), RustRed(), t);
        b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-6f, 1.6f, 9.6f),
            new Vector3(0.35f, 1.6f, 0.35f), Concrete(), t);
        b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(6f, 1.6f, 9.6f),
            new Vector3(0.35f, 1.6f, 0.35f), Concrete(), t);

        if (primary)
        {
            // smokestack + roof vents -- the industrial silhouette
            b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-5f, height + 4.5f, -4f),
                new Vector3(0.9f, 4.5f, 0.9f), Concrete(), t);
            for (var i = 0; i < 2; i++)
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(3f + i * 3f, height + 0.9f, 3f),
                    new Vector3(1.3f, 1.1f, 1.3f), Corrugated(), t);
        }
    }

    /// <summary>docs/28: one window strip, deterministically "lit" or
    /// dark based on (h, slot) -- roughly 2 in 5 lit, no actual
    /// randomness (same seed always dresses the same city, same as every
    /// other hash-keyed choice in this file). A lit strip gets the shared
    /// WindowGlow() material plus its own independent EmissiveAnimator
    /// Flicker registration and a GlowPointRegistry entry so it competes
    /// fairly for DynamicLightBudget's shared real-light budget alongside
    /// streetlamps/neon/marquee bulbs.</summary>
    private static void SpawnWindowStrip(RuntimeCityBuilder b, Transform t, Vector3 pos, Vector3 scale, int h, int slot)
    {
        int floorSeed;
        unchecked { floorSeed = (h * 397) ^ (slot * 8191 + 17); }
        var lit = (floorSeed & 0x7fffffff) % 5 < 2;
        if (!lit)
        {
            b.SpawnPrim(PrimitiveType.Cube, pos, scale, WindowBand(), t);
            return;
        }

        var go = b.SpawnPrim(PrimitiveType.Cube, pos, scale, WindowGlow(), t);
        var renderer = go.GetComponent<Renderer>();
        var seed = ((floorSeed & 0x7fffffff) % 10007) / 10007f;
        // 2026-07 creator direction: "the building can turn on randomly
        // approaching night time, as if real humans were in the room...
        // the same goes for late at night, imagine people going to bed
        // and shutting off their house light... not all lights go off."
        // Window (was Flicker) adds a per-instance randomized on/off
        // occupancy schedule on top of the existing wobble -- purely an
        // emissive/MaterialPropertyBlock effect, same mechanism as the
        // bulb geometry, not a second real-light system.
        EmissiveAnimator.Register(renderer, WindowGlowColor * CityLightingProfile.Active.BulbEmissiveBase * 0.9f,
            LightBehaviorKind.Window, seed);
        GlowPointRegistry.Register(go.transform, WindowGlowColor);
    }

    // ---- medium tier: brick walk-up apartments ---------------------------------

    private static void DressApartment(RuntimeCityBuilder b, Building building, HexCoord hex, Transform t,
        float height, int h, bool primary, bool suburb = false, CityRegion region = CityRegion.Generic)
    {
        var basePos = t.position;
        var wall = RegionWall(h, suburb, region);

        if (UseFacadeGrammar && GrammarAppliesTo(building.Tier))
        {
            // docs/30: the grammar owns the walls now -- punched window
            // bays that know which face is a street, a real ground-floor
            // band, party walls left blank, a cornice that overhangs.
            // Everything below the grammar call is roof/structure work it
            // deliberately does NOT own.
            DressFacadeGrammar(b, building, hex, t, height, h, industrial: false, suburb: suburb, wall: wall);
        }
        else
        {
            // legacy: continuous window strips on two opposite faces
            var floors = Mathf.Max(2, Mathf.RoundToInt(height / 4f));
            for (var f = 0; f < floors; f++)
            {
                var y = (f + 0.55f) * (height / floors);
                SpawnWindowStrip(b, t, basePos + new Vector3(0f, y, Half * 1.01f), new Vector3(15f, 1.5f, 0.35f), h, f * 2);
                SpawnWindowStrip(b, t, basePos + new Vector3(0f, y, -Half * 1.01f), new Vector3(15f, 1.5f, 0.35f), h, f * 2 + 1);
            }
            // fire escape: zig-zag ladder strip down one face
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(Half * 1.04f, height * 0.5f, 0f),
                new Vector3(0.25f, height * 0.86f, 3.2f), M(0.15f, 0.15f, 0.17f), t);
        }

        // repaint accent: thin corner pilasters in the era wall color
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(Half * 0.98f, height / 2f, Half * 0.98f),
            new Vector3(1.2f, height, 1.2f), wall, t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(-Half * 0.98f, height / 2f, -Half * 0.98f),
            new Vector3(1.2f, height, 1.2f), wall, t);
        // tar roof (the cornice is the grammar's crown module when it's on)
        if (!UseFacadeGrammar || !GrammarAppliesTo(building.Tier))
            b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.3f),
                new Vector3(19.4f, 0.6f, 19.4f), Concrete(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.65f),
            new Vector3(17.6f, 0.2f, 17.6f), RoofTar(), t);

        // docs/30 Tier 1c: flat stays the plurality (the shipped, known-
        // good silhouette), the other three add real roof-form variety on
        // top of it. Own hash slot (h/11) so this pick doesn't correlate
        // with the wall/trim picks drawn from the same `h`.
        switch ((h / 11) % 6)
        {
            case 3:   // hip roof: shallow pitched ridge (solid -- registers a real landable apex)
            {
                var ridgeMat = (h / 13) % 2 == 0 ? RoofShingleWarm() : RoofShingleCool();
                b.SpawnMesh(GableRoofMesh(),
                    basePos + Vector3.up * (height + ApartmentRoofCapTop + ApartmentHipHeight * 0.5f),
                    new Vector3(ApartmentHipWidth, ApartmentHipHeight, ApartmentHipLength), ridgeMat, t, matte: true);
                b.RegisterRoofLandingHeight(hex, height + ApartmentHipApexOffset);
                break;
            }
            case 4:   // stepped parapet: a raised rim around the roof edge -- boxier
                      // deco-apartment silhouette. Hollow (the flat cap is still the
                      // landable surface underneath it), so no height registration.
            {
                var rimY = height + ApartmentRoofCapTop + ApartmentParapetHeight * 0.5f;
                var edge = 19.4f * 0.5f - 0.3f;
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, rimY, edge), new Vector3(19.4f, ApartmentParapetHeight, 0.6f), wall, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, rimY, -edge), new Vector3(19.4f, ApartmentParapetHeight, 0.6f), wall, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(edge, rimY, 0f), new Vector3(0.6f, ApartmentParapetHeight, 19.4f), wall, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(-edge, rimY, 0f), new Vector3(0.6f, ApartmentParapetHeight, 19.4f), wall, t);
                break;
            }
            case 5:   // penthouse: a small off-center rooftop volume -- decorative
                      // massing (same class as the water tower/vents below), not a
                      // registered landable surface -- the flat cap around it still is.
            {
                var boxAt = basePos + new Vector3(3.5f, height + ApartmentRoofCapTop + 1.6f, -2f);
                b.SpawnPrim(PrimitiveType.Cube, boxAt, new Vector3(6f, 3.2f, 5f), wall, t);
                b.SpawnPrim(PrimitiveType.Cube, boxAt + new Vector3(0f, 1.75f, 0f), new Vector3(6.4f, 0.3f, 5.4f), Concrete(), t);
                break;
            }
        }

        if (primary) Rooftop(b, t, basePos, height, h);
    }

    // ---- large tier: stepped deco office ---------------------------------------

    // 2026-07 bug fix: top of the upper setback tier above `height` --
    // 6.5 (its center lift) + half its own 3f depth. Every Large-tier
    // footprint hex gets this unconditionally (see below), matching how
    // the setback itself is drawn on every hex, not just the primary one.
    private const float SetbackTopOffset = 6.5f + 1.5f;   // = 8f

    private static void DressOffice(RuntimeCityBuilder b, Building building, HexCoord hex, Transform t,
        float height, int h, bool primary, bool industrial = false, bool suburb = false, CityRegion region = CityRegion.Generic)
    {
        var basePos = t.position;
        var trim = RegionTrim(h, region);

        // deco setbacks: two shrinking tiers on the roof -- real, solid,
        // walkable massing on every high-rise, not decoration; register
        // its actual top as landable (2026-07 bug fix).
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 2.5f),
            new Vector3(13f, 5f, 13f), Concrete(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 6.5f),
            new Vector3(8f, 3f, 8f), Concrete(), t);
        b.RegisterRoofLandingHeight(hex, height + SetbackTopOffset);

        if (UseFacadeGrammar && GrammarAppliesTo(building.Tier))
        {
            // Pilasters stay -- they're the deco vertical rhythm framing
            // the grammar's bays -- but they used to run the full height
            // from y=0, which put a solid pier straight through the
            // grammar's own ground-floor modules (a 13.5m-wide shopfront
            // window, or the 17.4m blank plinth, both centered on x=0 --
            // exactly where the middle pilaster sits). Starting the
            // pilaster at the SAME ground-band height FacadeKit.BuildFace
            // uses internally (mirrored here, not re-derived) keeps them
            // out of the ground floor entirely -- upper-floor bay
            // placement (see FacadeKit.BuildUpper) is what keeps them
            // clear of the punched windows above that line.
            var pilasterGroundH = Mathf.Min(height * 0.34f, 5.2f);
            var pilasterH = height - pilasterGroundH;
            var pilasterY = pilasterGroundH + pilasterH * 0.5f;
            for (var i = -1; i <= 1; i++)
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(i * 5.5f, pilasterY, Half * 1.01f),
                    new Vector3(1.1f, pilasterH, 0.35f), trim, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(i * 5.5f, pilasterY, -Half * 1.01f),
                    new Vector3(1.1f, pilasterH, 0.35f), trim, t);
            }
            DressFacadeGrammar(b, building, hex, t, height, h, industrial, suburb, wall: Concrete());
        }
        else
        {
            // vertical pilaster strips -- the deco silhouette from a distance
            for (var i = -1; i <= 1; i++)
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(i * 5.5f, height / 2f, Half * 1.01f),
                    new Vector3(1.1f, height, 0.35f), trim, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(i * 5.5f, height / 2f, -Half * 1.01f),
                    new Vector3(1.1f, height, 0.35f), trim, t);
            }
            // window bands between pilasters (darker, recessed feel)
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height / 2f, Half * 0.995f),
                new Vector3(16.8f, height * 0.9f, 0.15f), WindowBand(), t);
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height / 2f, -Half * 0.995f),
                new Vector3(16.8f, height * 0.9f, 0.15f), WindowBand(), t);
        }

        if (primary)
        {
            // antenna mast with a beacon -- the King-of-the-City silhouette
            b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 11f),
                new Vector3(0.3f, 3.2f, 0.3f), Chrome(), t);
            b.SpawnPrim(PrimitiveType.Sphere, basePos + Vector3.up * (height + 14.4f),
                new Vector3(0.9f, 0.9f, 0.9f), NeonRed(), t);
            Rooftop(b, t, basePos, height, h);
            // side billboard frame + period ad art
            var boardCenter = basePos + new Vector3(Half * 1.08f, height * 0.7f, 0f);
            b.SpawnPrim(PrimitiveType.Cube, boardCenter, new Vector3(0.4f, 6f, 10f), SignWhite(), t);
            DressPoster(b, t, boardCenter, h);
        }
    }

    // ---- billboard art: period-poster stripes on the office billboard --------

    private static Material AdRed() { return M(0.82f, 0.18f, 0.16f); }
    private static Material AdBlue() { return M(0.22f, 0.35f, 0.6f); }

    /// <summary>Fakes period ad graphics with flat color blocks -- no
    /// texture pipeline here, so the "ATOMIC COLA" bullseye and the movie
    /// one-sheet are read purely through primitive silhouette and color,
    /// same discipline as the rest of the primitive-kit dressing.</summary>
    private static void DressPoster(RuntimeCityBuilder b, Transform t, Vector3 boardCenter, int h)
    {
        switch (h % 3)
        {
            case 0:   // soda bullseye: a red disc over a mustard band
            {
                var disc = b.SpawnPrim(PrimitiveType.Cylinder, boardCenter + new Vector3(0.45f, 1.2f, 0f),
                    new Vector3(2.4f, 0.06f, 2.4f), AdRed(), t);
                disc.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, -1.8f, 0f),
                    new Vector3(0.1f, 1f, 8.5f), Mustard(), t);
                break;
            }
            case 1:   // movie one-sheet: stacked teal/mustard color blocks
            {
                for (var i = 0; i < 3; i++)
                    b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, 2f - i * 1.8f, 0f),
                        new Vector3(0.08f, 1.5f, 8.6f), i % 2 == 0 ? NeonTeal() : Mustard(), t);
                break;
            }
            default:  // headline bands: bold red over concrete gray
            {
                b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, 1f, 0f),
                    new Vector3(0.08f, 1.2f, 9f), AdRed(), t);
                b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, -1.2f, 0f),
                    new Vector3(0.08f, 0.8f, 9f), AdBlue(), t);
                break;
            }
        }
    }

    // ---- landmark tier: archetype-aware civic set pieces ------------------------

    private static void DressLandmark(RuntimeCityBuilder b, string archetype, Transform t,
        float height, int h, bool primary, CityRegion region = CityRegion.Generic)
    {
        var basePos = t.position;
        // gold cornice band keeps the tier's RTS color read
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.4f),
            new Vector3(19.6f, 0.8f, 19.6f), Mustard(), t);
        if (!primary) return;

        switch (archetype)
        {
            case "church":
                // parish spire: tapering stacked boxes + a needle
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 4f),
                    new Vector3(7f, 8f, 7f), Concrete(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 10f),
                    new Vector3(4f, 5f, 4f), Concrete(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 15f),
                    new Vector3(0.4f, 3.5f, 0.4f), Chrome(), t);
                break;
            case "cathedral":
                // grander than a parish church: twin flanking towers
                // (each taller than the single church spire) plus a rose
                // window on the front face -- the "downtown Notre Dame"
                // silhouette, so cathedral reads as an upgrade, not a
                // recolor of the same building
                foreach (var side in new[] { 1f, -1f })
                {
                    var towerBase = basePos + new Vector3(side * 6.5f, 0f, 0f);
                    b.SpawnPrim(PrimitiveType.Cube, towerBase + Vector3.up * (height + 5f),
                        new Vector3(5f, 10f, 5f), Concrete(), t);
                    b.SpawnPrim(PrimitiveType.Cube, towerBase + Vector3.up * (height + 12f),
                        new Vector3(3f, 4f, 3f), Concrete(), t);
                    b.SpawnPrim(PrimitiveType.Cylinder, towerBase + Vector3.up * (height + 16.5f),
                        new Vector3(0.4f, 4.5f, 0.4f), Chrome(), t);
                }
                var rose = b.SpawnPrim(PrimitiveType.Cylinder,
                    basePos + new Vector3(0f, height * 0.6f, Half * 1.02f),
                    new Vector3(2.6f, 0.1f, 2.6f), NeonTeal(), t);
                rose.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            case "town_hall":
                // columned portico + pediment + flagpole
                for (var i = -2; i <= 2; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3.4f, 5f, Half * 1.15f),
                        new Vector3(0.9f, 5f, 0.9f), Cream(), t);
                // 2026-08 (creator direction: "Add a real triangle
                // primitive to roof generator NOT a Cube on it's side"):
                // the classical pediment is the same triangular-prism
                // shape as a roof (ProceduralMeshKit.GableRoof), just
                // thin and facing forward instead of long and ridge-run
                // -- the mesh's ridge axis (local Z) becomes the
                // pediment's shallow front-to-back depth here, no
                // rotated-cube diamond trick needed.
                b.SpawnMesh(GableRoofMesh(),
                    basePos + new Vector3(0f, 14.25f, Half * 1.15f), new Vector3(11f, 5.5f, 2.4f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 3.5f),
                    new Vector3(0.25f, 3.5f, 0.25f), Chrome(), t);
                break;
            case "rail_depot":
                // long arched trainshed roof: a half-sunk lying cylinder
                var shed = b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * height,
                    new Vector3(9f, 9.6f, 9f), RustRed(), t);
                shed.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                // platform + a small ticket booth out front -- ties the
                // depot into the railyard district (docs/21 batch 3)
                // instead of reading as an isolated trainshed
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 0.5f, Half * 1.3f),
                    new Vector3(16f, 1f, 5f), Concrete(), t);      // platform slab
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(-6f, 2.4f, Half * 1.3f),
                    new Vector3(3f, 3.8f, 3f), Cream(), t);        // ticket booth
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(-6f, 4.6f, Half * 1.3f),
                    new Vector3(3.6f, 0.4f, 3.6f), RustRed(), t);  // booth roof
                break;
            case "hospital":
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 4f, 0f),
                    new Vector3(6f, 1.8f, 1.2f), NeonRed(), t);   // red cross, horizontal bar
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 4f, 0f),
                    new Vector3(1.8f, 6f, 1.2f), NeonRed(), t);   // vertical bar
                break;
            case "plaza":
                // a grand civic building FRONTING the square, not the
                // square itself (the massing cube is still a 40m
                // landmark-tier block by construction -- this dresses it
                // as the formal building anchoring the plaza): a
                // colonnade, a clock cupola, and a small fountain out
                // front on the plaza pavement itself
                for (var i = -2; i <= 2; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3f, 6f, Half * 1.1f),
                        new Vector3(0.7f, 6f, 0.7f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 12.4f, Half * 1.1f),
                    new Vector3(16f, 0.8f, 2.6f), Cream(), t);   // entablature over the columns
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 2.5f),
                    new Vector3(2.2f, 2.5f, 2.2f), Concrete(), t);   // clock cupola drum
                b.SpawnPrim(PrimitiveType.Sphere, basePos + Vector3.up * (height + 5.4f),
                    new Vector3(1.6f, 1.6f, 1.6f), Chrome(), t);
                var fountainAt = basePos + new Vector3(0f, 0f, Half * 1.7f);
                b.SpawnPrim(PrimitiveType.Cylinder, fountainAt + Vector3.up * 0.3f,
                    new Vector3(2.6f, 0.3f, 2.6f), Concrete(), t);   // basin
                b.SpawnPrim(PrimitiveType.Cylinder, fountainAt + Vector3.up * 1.1f,
                    new Vector3(1.2f, 0.8f, 1.2f), Chrome(), t);     // pedestal
                b.SpawnPrim(PrimitiveType.Sphere, fountainAt + Vector3.up * 2f,
                    new Vector3(0.5f, 0.9f, 0.5f), NeonTeal(), t);   // stylized water jet
                break;
            case "school":
                // a modest columned entrance -- plainer and smaller than
                // the plaza/town_hall civic scale -- plus the schoolyard
                // silhouette: bell cupola and a flagpole
                for (var i = -1; i <= 1; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3.6f, 4f, Half * 1.1f),
                        new Vector3(0.6f, 4f, 0.6f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 8.3f, Half * 1.1f),
                    new Vector3(13f, 0.6f, 2.2f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 1.8f),
                    new Vector3(2.4f, 2.2f, 2.4f), Concrete(), t);   // bell cupola housing
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 3.6f),
                    new Vector3(0.15f, 0.6f, 0.15f), RustRed(), t);  // bell
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-6f, height + 3f, -6f),
                    new Vector3(0.15f, 3f, 0.15f), Chrome(), t);     // flagpole
                break;
            case "old_age_home":
                // quieter and homelier than the other institutions: a
                // wraparound porch roof instead of a civic cornice, plus
                // a garden trellis on the ground -- reads residential
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 5.4f, Half * 1.15f),
                    new Vector3(17f, 0.5f, 4f), RustRed(), t);
                for (var i = -1; i <= 1; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 5f, 2.6f, Half * 1.25f),
                        new Vector3(0.35f, 2.6f, 0.35f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 1.5f),
                    new Vector3(6f, 1.6f, 4f), Cream(), t);   // dormer-ish roof projection
                var trellisAt = basePos + new Vector3(0f, 0f, Half * 1.6f);
                b.SpawnPrim(PrimitiveType.Cube, trellisAt + Vector3.up * 1.4f,
                    new Vector3(3.2f, 2.6f, 0.15f), GardenGreen(), t);
                break;

            // docs/30 Tier 1d: the five region-specific landmark archetypes
            // CityPreset.NewYork()/Paris()/Montreal() have named since
            // docs/23 Phase 8, previously falling through to the generic
            // movie-palace default below -- each region now gets its own
            // silhouette instead of looking architecturally identical.
            case "liberty_statuette_plaza":
                // NY: a civic statue-on-plinth fronting a small plaza --
                // echoes the raised-torch silhouette without literally
                // reproducing a specific copyrighted statue design
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 3f, Half * 1.3f),
                    new Vector3(6f, 6f, 6f), Concrete(), t);   // plinth
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 6.4f, Half * 1.3f),
                    new Vector3(7f, 0.8f, 7f), Cream(), t);    // plinth cap
                var statueAt = basePos + new Vector3(0f, 6.8f, Half * 1.3f);
                b.SpawnPrim(PrimitiveType.Cylinder, statueAt + Vector3.up * 4f,
                    new Vector3(1.6f, 4f, 1.6f), Seafoam(), t);   // robed body
                b.SpawnPrim(PrimitiveType.Sphere, statueAt + Vector3.up * 8.4f,
                    new Vector3(1.1f, 1.1f, 1.1f), Seafoam(), t);   // head
                b.SpawnPrim(PrimitiveType.Cube, statueAt + new Vector3(1.3f, 8.5f, 0f),
                    new Vector3(0.5f, 3.6f, 0.5f), Seafoam(), t);   // raised torch arm
                var torchAt = statueAt + new Vector3(1.3f, 10.5f, 0f);
                b.SpawnPrim(PrimitiveType.Cylinder, torchAt, new Vector3(0.55f, 0.5f, 0.55f), Chrome(), t);
                var flame = b.SpawnPrim(PrimitiveType.Sphere, torchAt + Vector3.up * 0.8f,
                    new Vector3(0.6f, 0.8f, 0.6f), NeonRed(), t);
                EmissiveAnimator.Register(flame.GetComponent<Renderer>(), new Color(0.95f, 0.4f, 0.2f) * 1.6f, LightBehaviorKind.Buzz, 0.35f);
                break;
            case "grand_terminal":
                // NY: a monumental passenger-hall facade -- tall arched
                // colonnade + an oversized ceremonial clock. Grander and
                // front-facing, unlike rail_depot's plain platform shed.
                for (var i = -2; i <= 2; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3.2f, 7f, Half * 1.12f),
                        new Vector3(0.9f, 7f, 0.9f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 14.6f, Half * 1.12f),
                    new Vector3(19f, 1.2f, 3f), Cream(), t);   // entablature over the colonnade
                var clockFace = b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(0f, 10.5f, Half * 1.32f),
                    new Vector3(2.6f, 0.25f, 2.6f), SignWhite(), t);
                clockFace.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var clockRim = b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(0f, 10.5f, Half * 1.42f),
                    new Vector3(2.1f, 0.1f, 2.1f), IronDark(), t);
                clockRim.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 3.5f),
                    new Vector3(0.4f, 3.5f, 0.4f), Chrome(), t);   // flagmast
                break;
            case "iron_tower":
                // Paris: a tapering lattice tower echoing the silhouette of
                // a wrought-iron viewing tower, not a literal replica
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 3f),
                    new Vector3(11f, 6f, 11f), IronDark(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 9f),
                    new Vector3(7f, 6f, 7f), IronDark(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 14.5f),
                    new Vector3(4f, 5f, 4f), IronDark(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 19f),
                    new Vector3(0.6f, 4f, 0.6f), IronDark(), t);
                var beacon = b.SpawnPrim(PrimitiveType.Sphere, basePos + Vector3.up * (height + 21.2f),
                    new Vector3(0.8f, 0.8f, 0.8f), NeonRed(), t);
                EmissiveAnimator.Register(beacon.GetComponent<Renderer>(), new Color(0.95f, 0.25f, 0.3f) * 1.6f, LightBehaviorKind.Buzz, 0.9f);
                break;
            case "marche_tower":
                // Montreal: a market-hall clock tower with a silvered dome,
                // echoing Montreal's 19th-century market halls
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 3f),
                    new Vector3(3.4f, 3f, 3.4f), Cream(), t);   // drum
                var domeAt = basePos + Vector3.up * (height + 6.6f);
                b.SpawnPrim(PrimitiveType.Sphere, domeAt, new Vector3(3.6f, 2.6f, 3.6f), Chrome(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, domeAt + Vector3.up * 2.4f,
                    new Vector3(0.35f, 2.4f, 0.35f), Chrome(), t);
                b.SpawnPrim(PrimitiveType.Sphere, domeAt + Vector3.up * 4.9f,
                    new Vector3(0.5f, 0.5f, 0.5f), Mustard(), t);   // gilded finial ball
                var marketClockFace = b.SpawnPrim(PrimitiveType.Cylinder,
                    basePos + new Vector3(0f, height + 3f, Half * 1.05f), new Vector3(1.4f, 0.15f, 1.4f), SignWhite(), t);
                marketClockFace.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            case "forum_arena":
                // Montreal: a barrel-roofed arena, echoing the rounded shed
                // roof of a classic ice-hockey forum
                var barrel = b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 1f),
                    new Vector3(10f, 10.5f, 10f), Corrugated(), t);
                barrel.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 4f, Half * 1.2f),
                    new Vector3(13f, 8f, 3f), Concrete(), t);   // entrance block
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 8.6f, Half * 1.25f),
                    new Vector3(12f, 1.4f, 3.4f), SignWhite(), t);   // marquee
                for (var i = -1; i <= 1; i++)
                {
                    var mastAt = basePos + new Vector3(i * 4.5f, 0f, Half * 1.35f);
                    b.SpawnPrim(PrimitiveType.Cylinder, mastAt + Vector3.up * 5f,
                        new Vector3(0.2f, 5f, 0.2f), Chrome(), t);
                    b.SpawnPrim(PrimitiveType.Cube, mastAt + new Vector3(0.9f, 8.5f, 0f),
                        new Vector3(1.8f, 1.2f, 0.1f), AdRed(), t);   // pennant flag
                }
                break;

            default:
                // any future/unlisted archetype -> THE MOVIE PALACE: a
                // marquee slab out front and a big neon rooftop sign --
                // every b-movie city needs one theater downtown
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 6.5f, Half * 1.25f),
                    new Vector3(14f, 2.4f, 4.5f), SignWhite(), t);      // marquee
                var underglow = b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 5.2f, Half * 1.25f),
                    new Vector3(13f, 0.4f, 4.2f), NeonTeal(), t);       // marquee underglow
                var bladeSign = b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 5f, 0f),
                    new Vector3(2f, 10f, 0.8f), SignWhite(), t);        // vertical blade sign
                var letters = b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 5f, 0.7f),
                    new Vector3(1.2f, 8.6f, 0.4f), NeonRed(), t);       // neon letters strip

                // docs/28: "neon that buzz and flicker" -- the palace's own
                // signature neon, each independently buzzing (a different
                // seed per strip so they don't stutter in lockstep). Base
                // emission matches each material's own M(r,g,b,emissive)
                // color * emissive -- NOT a Color(r,g,b,a) alpha (there's
                // no transparency involved here).
                EmissiveAnimator.Register(underglow.GetComponent<Renderer>(), new Color(0.3f, 0.9f, 0.85f) * 1.6f, LightBehaviorKind.Buzz, 0.2f);
                EmissiveAnimator.Register(bladeSign.GetComponent<Renderer>(), new Color(0.92f, 0.9f, 0.82f) * 0.6f, LightBehaviorKind.Buzz, 0.55f);
                EmissiveAnimator.Register(letters.GetComponent<Renderer>(), new Color(0.95f, 0.25f, 0.3f) * 1.6f, LightBehaviorKind.Buzz, 0.8f);

                // docs/28: "clique light that flash on and off in sequence"
                // -- a row of small chaser bulbs along the marquee's front
                // edge, each one slot further along a shared sequence.
                const int chaseBulbs = 10;
                for (var i = 0; i < chaseBulbs; i++)
                {
                    var bx = Mathf.Lerp(-6f, 6f, i / (float)(chaseBulbs - 1));
                    var bulb = b.SpawnPrim(PrimitiveType.Sphere, basePos + new Vector3(bx, 5.2f, Half * 1.45f),
                        new Vector3(0.3f, 0.3f, 0.3f), M(1f, 0.9f, 0.6f, CityLightingProfile.Active.BulbEmissiveBase), t);
                    EmissiveAnimator.Register(bulb.GetComponent<Renderer>(),
                        new Color(1f, 0.9f, 0.6f) * CityLightingProfile.Active.BulbEmissiveBase,
                        LightBehaviorKind.Chase, 0f, i, chaseBulbs);
                }
                break;
        }
        Rooftop(b, t, basePos, height, h + 3);
    }

    // ---- the rooftop kit: what sells the miniature from RTS height -------------

    private static void Rooftop(RuntimeCityBuilder b, Transform t, Vector3 basePos, float height, int h)
    {
        // water tower on stilts (the classic): ~45% of roofs
        if (h % 9 < 4)
        {
            var wt = basePos + new Vector3((h % 5) - 2f, 0f, (h % 7) - 3f);
            for (var i = 0; i < 4; i++)
            {
                var lx = i < 2 ? -1.1f : 1.1f;
                var lz = i % 2 == 0 ? -1.1f : 1.1f;
                b.SpawnPrim(PrimitiveType.Cylinder, wt + new Vector3(lx, height + 1.1f, lz),
                    new Vector3(0.22f, 1.1f, 0.22f), RustRed(), t);
            }
            b.SpawnPrim(PrimitiveType.Cylinder, wt + Vector3.up * (height + 3.4f),
                new Vector3(1.9f, 1.3f, 1.9f), RustRed(), t);            // tank
            b.SpawnPrim(PrimitiveType.Sphere, wt + Vector3.up * (height + 4.9f),
                new Vector3(2.5f, 1.2f, 2.5f), M(0.42f, 0.2f, 0.14f), t); // conical-ish cap
        }
        // vents / rooftop machinery
        var vents = 1 + h % 3;
        for (var i = 0; i < vents; i++)
        {
            var vp = basePos + new Vector3(((h + i * 37) % 11) - 5f, height + 0.7f, ((h + i * 61) % 11) - 5f);
            b.SpawnPrim(PrimitiveType.Cube, vp, new Vector3(1.4f, 1.2f, 1.4f), Concrete(), t);
        }
        // whip antenna
        if (h % 4 == 0)
            b.SpawnPrim(PrimitiveType.Cylinder,
                basePos + new Vector3(((h % 13) - 6f) * 0.8f, height + 2.4f, ((h % 17) - 8f) * 0.6f),
                new Vector3(0.08f, 2.4f, 0.08f), Chrome(), t);
    }
}
