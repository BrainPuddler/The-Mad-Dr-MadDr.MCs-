using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// docs/30: turns one solved <see cref="FacadeModule"/> into geometry on a
/// building face. This is the <em>mesh-swap layer</em> the WFC plan is
/// built around -- the half that knows about Unity, paired with
/// <see cref="FacadeGrammar"/>, which knows only about architecture and is
/// `dotnet test`-able without an engine.
///
/// **Every module spawns through <see cref="PropLibrary"/>.** That is the
/// entire point of this file: docs/23 §10.4 already established
/// "mesh-by-key with a primitive fallback so the game never breaks without
/// assets," and every module here goes through that seam. Today each key
/// falls back to a primitive because no authored mesh exists in this
/// project (no DCC pipeline, no Editor in this environment). The moment a
/// real mesh IS authored, registering it under the matching key upgrades
/// every building in the city with zero changes here and zero changes to
/// the solver -- which is what "compatible with a mesh pipeline" has to
/// mean to be worth anything.
///
/// **Nothing here ever gets a collider.** <see cref="RuntimeCityBuilder.SpawnPrim"/>
/// strips them, and <see cref="PropLibrary.Spawn"/> never adds one. This
/// is load-bearing, not incidental: every `Physics.Raycast` in this project
/// is unmasked, so a stray facade collider would immediately start
/// intercepting cursor picks, monster line-of-sight tests, and
/// `FireCluster`'s surface-pinning ray -- the last of which explicitly
/// discards any hit that isn't its own parent's collider, and would
/// silently fall back to crude fixed-radius flame placement.
/// </summary>
public static class FacadeKit
{
    /// <summary>Massing cubes are 18 m across, so a face plane sits at
    /// ±9 m. Mirrors <see cref="BuildingDresser.Half"/> rather than
    /// re-deriving it.</summary>
    private const float Half = BuildingDresser.Half;

    /// <summary>How far proud of the wall plane facade geometry sits.
    /// Small but non-zero: a flush-mounted trim piece is geometrically
    /// invisible as trim regardless of its size -- a rule this project
    /// learned the hard way and wrote down (docs/12, "'edge trim' only
    /// reads as trim if it actually protrudes").</summary>
    private const float Proud = 0.22f;

    // 2026-08 (docs/30 follow-up): the Large tier's own deco pilasters
    // (DressOffice, full-height strips at u = -5.5/0/5.5, each 1.1m wide)
    // sit on the same faces this grammar dresses. A window bay centered on
    // one of those u-values -- as the old evenly-spaced -6/0/6 layout put
    // one dead-center at u=0 -- read as a pilaster punched straight through
    // a pane of glass. Each pilaster half-width is 0.55m, leaving two
    // ~4.4m-wide clear gaps either side of the center one (u in [0.55,
    // 4.95] and its mirror).
    //
    // 2026-08 (creator direction: "the large buildings need many
    // windows"): a WindowBay used to place exactly ONE window per gap (2
    // total per floor) -- against an 18m-wide face and up to 6 floors on
    // a Large tower, that reads as sparse punched openings rather than a
    // real curtain wall of glass. Two narrower windows now share each
    // gap instead of one (4 per floor total): width dropped from 2.3m to
    // 1.7m so a pair (1.7+1.7=3.4m) plus a 0.3m mullion between plus
    // 0.35m clearance to each pilaster edge (3.4+0.3+0.7=4.4m) still
    // fits the SAME 4.4m gap with the SAME clearance -- the pilaster-
    // avoidance math above is unchanged, just subdivided.
    private static readonly float[] WindowBayU = { -3.75f, -1.75f, 1.75f, 3.75f };
    private const float WindowBayWidth = 1.7f;
    private const float WindowSillWidth = 2.1f;
    // OrielBay is a single wide (4.2m) window per floor -- reuses one of
    // WindowBay's clear gap positions rather than its own dead-center u=0,
    // which used to sit inside the center pilaster exactly like WindowBay's
    // middle bay did.
    private const float OrielU = 2.75f;

    // ---- PropLibrary keys: the mesh-swap points ------------------------
    //
    // Registered lazily below with primitive-shaped procedural fallbacks.
    // An authored .mesh dropped in under any of these keys replaces that
    // module everywhere, instantly, with no other code change.
    public const string KeyShopfront = "facade-shopfront";
    public const string KeyRecessedEntrance = "facade-entrance-recessed";
    public const string KeyStoopEntrance = "facade-entrance-stoop";
    public const string KeyLoadingDock = "facade-loading-dock";
    public const string KeyWindowBay = "facade-window-bay";
    public const string KeyOrielBay = "facade-oriel-bay";
    public const string KeyFireEscape = "facade-fire-escape";
    public const string KeyCornice = "facade-cornice";
    public const string KeyParapet = "facade-parapet";

    /// <summary>World-space outward normal for a cardinal face.</summary>
    public static Vector3 Normal(FacadeFace face)
    {
        switch (face)
        {
            case FacadeFace.PlusX: return Vector3.right;
            case FacadeFace.MinusX: return Vector3.left;
            case FacadeFace.PlusZ: return Vector3.forward;
            default: return Vector3.back;
        }
    }

    /// <summary>In-plane horizontal axis for a cardinal face -- the
    /// direction a shopfront's width or a cornice's run extends along.</summary>
    public static Vector3 Tangent(FacadeFace face)
    {
        switch (face)
        {
            case FacadeFace.PlusX:
            case FacadeFace.MinusX: return Vector3.forward;
            default: return Vector3.right;
        }
    }

    /// <summary>Build one solved face. `baseWorld` is the hex's ground-level
    /// world centre (the dressing holder's own position), `height` the
    /// massing cube's full height.
    ///
    /// Returns the number of GameObjects spawned, so the caller can hold
    /// the whole system to a stated per-building budget rather than
    /// discovering the cost in a profiler later.</summary>
    public static int BuildFace(
        RuntimeCityBuilder b, Transform parent, Vector3 baseWorld,
        FacadeFace face, FacadeSolution solution, float height,
        FacadeMaterials mats)
    {
        if (solution == null || solution.Role == FaceRole.PartyWall) return 0;

        var n = Normal(face);
        var tan = Tangent(face);
        var cells = solution.Cells;
        var upperCount = Mathf.Max(0, cells.Count - 2);

        // Ground band gets a real, generous share of the elevation -- a
        // period commercial street is legible precisely because the ground
        // floor is taller and different from what sits on it.
        var groundH = Mathf.Min(height * 0.34f, 5.2f);
        var upperSpan = Mathf.Max(0.01f, height - groundH);
        var floorH = upperCount > 0 ? upperSpan / upperCount : upperSpan;

        var spawned = 0;

        spawned += BuildGround(b, parent, baseWorld, n, tan, cells[0], groundH, mats);

        for (var f = 0; f < upperCount; f++)
        {
            var y = groundH + (f + 0.5f) * floorH;
            spawned += BuildUpper(b, parent, baseWorld, n, tan, cells[f + 1], y, floorH, mats,
                isTopFloor: f == upperCount - 1);
        }

        spawned += BuildCrown(b, parent, baseWorld, n, tan, cells[cells.Count - 1], height, mats);

        return spawned;
    }

    // ---- ground band ---------------------------------------------------

    private static int BuildGround(RuntimeCityBuilder b, Transform t, Vector3 basePos,
        Vector3 n, Vector3 tan, FacadeModule m, float groundH, FacadeMaterials mats)
    {
        var mid = basePos + n * (Half + Proud * 0.5f) + Vector3.up * (groundH * 0.5f);

        switch (m)
        {
            case FacadeModule.Shopfront:
            {
                // Big plate-glass display window with a bulkhead below and
                // a signboard above -- the single most period-legible thing
                // on a commercial street, and the element the current
                // dresser has no concept of at all.
                PropLibrary.Spawn(b, KeyShopfront, PrimitiveType.Cube,
                    mid + Vector3.up * groundH * 0.06f,
                    Along(tan, n, 13.5f, groundH * 0.62f, Proud), mats.Glass, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + n * (Half + Proud * 0.6f) + Vector3.up * (groundH * 0.11f),
                    Along(tan, n, 13.8f, groundH * 0.22f, Proud * 1.2f), mats.Trim, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + n * (Half + Proud * 0.8f) + Vector3.up * (groundH * 0.9f),
                    Along(tan, n, 14.2f, groundH * 0.16f, Proud * 1.6f), mats.Sign, t);
                return 3;
            }
            case FacadeModule.RecessedEntrance:
            {
                b.SpawnPrim(PrimitiveType.Cube, mid,
                    Along(tan, n, 3.6f, groundH * 0.82f, Proud * 0.7f), mats.DarkRecess, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + n * (Half + Proud) + Vector3.up * (groundH * 0.92f),
                    Along(tan, n, 4.6f, 0.34f, Proud * 2.2f), mats.Trim, t);   // lintel
                return 2;
            }
            case FacadeModule.StoopEntrance:
            {
                b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * groundH * 0.12f,
                    Along(tan, n, 3.2f, groundH * 0.7f, Proud * 0.7f), mats.DarkRecess, t);
                // the stoop itself: two steps out onto the pavement
                for (var s = 0; s < 2; s++)
                    b.SpawnPrim(PrimitiveType.Cube,
                        basePos + n * (Half + 0.5f + s * 0.6f) + Vector3.up * (0.5f - s * 0.22f),
                        Along(tan, n, 3.4f, 0.45f, 1.2f), mats.Stone, t);
                return 3;
            }
            case FacadeModule.LoadingDock:
            {
                b.SpawnPrim(PrimitiveType.Cube, mid,
                    Along(tan, n, 6.5f, groundH * 0.72f, Proud * 0.7f), mats.DarkRecess, t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + n * (Half + 1.4f) + Vector3.up * 0.55f,
                    Along(tan, n, 7.2f, 1.1f, 2.8f), mats.Stone, t);   // dock apron
                PropLibrary.Spawn(b, KeyLoadingDock, PrimitiveType.Cube,
                    basePos + n * (Half + 1.1f) + Vector3.up * (groundH * 1.02f),
                    Along(tan, n, 7.6f, 0.34f, 3.2f), mats.Rust, t);   // canopy
                return 3;
            }
            default: // BlankPlinth
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + n * (Half + Proud * 0.4f) + Vector3.up * (groundH * 0.5f),
                    Along(tan, n, 17.4f, groundH, Proud * 0.8f), mats.Stone, t);
                return 1;
            }
        }
    }

    // ---- upper band ----------------------------------------------------

    private static int BuildUpper(RuntimeCityBuilder b, Transform t, Vector3 basePos,
        Vector3 n, Vector3 tan, FacadeModule m, float y, float floorH, FacadeMaterials mats,
        bool isTopFloor)
    {
        var at = basePos + n * (Half + Proud * 0.5f) + Vector3.up * y;

        switch (m)
        {
            case FacadeModule.WindowBay:
            {
                // Punched windows in a row, not one continuous strip. The
                // continuous strip is what makes the current buildings read
                // as extruded blocks; individual openings with brick
                // between them is what makes them read as built.
                // Four bays (a pair per gap), not one per gap: the Large
                // tier's own deco pilasters (DressOffice, full-height
                // strips at u = -5.5/0/5.5) sit on this exact face, and
                // WindowBayU's two pairs still sit inside the two ~4.4m
                // gaps either side of the center pilaster (see the
                // constant's own comment for the clearance math), so the
                // bays read as glass BETWEEN the piers instead of glass
                // WITH a pier through it.
                var spawned = 0;
                for (var i = 0; i < WindowBayU.Length; i++)
                {
                    var u = WindowBayU[i];
                    var pos = at + tan * u;
                    var go = PropLibrary.Spawn(b, KeyWindowBay, PrimitiveType.Cube, pos,
                        Along(tan, n, WindowBayWidth, floorH * 0.52f, Proud), mats.Glass, t);
                    RegisterWindowGlow(go, mats, pos);
                    spawned++;
                    // sill: the small horizontal that catches light and
                    // reads at distance far better than the window itself
                    b.SpawnPrim(PrimitiveType.Cube, pos + Vector3.down * (floorH * 0.3f) + n * (Proud * 0.6f),
                        Along(tan, n, WindowSillWidth, 0.16f, Proud * 1.8f), mats.Stone, t);
                    spawned++;
                }
                return spawned;
            }
            case FacadeModule.OrielBay:
            {
                // A projecting bay window -- pure silhouette value, which
                // is what actually survives to RTS camera distance.
                // Same pilaster-clearance reasoning as WindowBay: this used
                // to sit dead-center at u=0, exactly where the center
                // pilaster runs, so a projecting bay window would have been
                // clipping straight through a solid pier. OrielU reuses one
                // of WindowBay's two clear gap positions.
                var oat = at + tan * OrielU;
                var go = PropLibrary.Spawn(b, KeyOrielBay, PrimitiveType.Cube,
                    oat + n * 0.75f, Along(tan, n, 4.2f, floorH * 0.66f, 1.7f), mats.Glass, t);
                RegisterWindowGlow(go, mats, oat + n * 0.75f);
                b.SpawnPrim(PrimitiveType.Cube, oat + n * 0.75f + Vector3.down * (floorH * 0.38f),
                    Along(tan, n, 4.6f, 0.28f, 2.0f), mats.Trim, t);
                return 2;
            }
            case FacadeModule.FireEscapeBay:
            {
                // A landing plus its rail, one per floor. Continuity is
                // enforced by the solver, not here -- see
                // FacadeGrammar.Propagate.
                PropLibrary.Spawn(b, KeyFireEscape, PrimitiveType.Cube,
                    at + n * 1.15f + tan * 3.2f,
                    Along(tan, n, 4.4f, 0.16f, 2.2f), mats.Iron, t);       // landing
                b.SpawnPrim(PrimitiveType.Cube, at + n * 2.2f + tan * 3.2f + Vector3.up * 0.55f,
                    Along(tan, n, 4.4f, 1.1f, 0.12f), mats.Iron, t);       // rail
                // the diagonal run down to the floor below
                var stair = b.SpawnPrim(PrimitiveType.Cube,
                    at + n * 1.6f + tan * 0.6f + Vector3.down * (floorH * 0.3f),
                    Along(tan, n, 2.6f, 0.12f, 1.1f), mats.Iron, t);
                stair.transform.rotation = Quaternion.LookRotation(n, Vector3.up) * Quaternion.Euler(0f, 0f, 32f);
                return 3;
            }
            default: // BlindBay -- brick, with just enough relief to catch light
            {
                b.SpawnPrim(PrimitiveType.Cube, at,
                    Along(tan, n, 16.6f, floorH * 0.9f, Proud * 0.5f), mats.Wall, t);
                if (isTopFloor)
                    b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * (floorH * 0.42f),
                        Along(tan, n, 16.9f, 0.2f, Proud * 1.4f), mats.Trim, t);
                return isTopFloor ? 2 : 1;
            }
        }
    }

    // ---- crown ---------------------------------------------------------

    private static int BuildCrown(RuntimeCityBuilder b, Transform t, Vector3 basePos,
        Vector3 n, Vector3 tan, FacadeModule m, float height, FacadeMaterials mats)
    {
        var at = basePos + n * (Half + Proud) + Vector3.up * height;

        switch (m)
        {
            case FacadeModule.Cornice:
            {
                // Two-step overhanging cornice. The overhang is the whole
                // point: it throws a hard shadow line across the top of
                // the facade, which is the single strongest period read
                // available at RTS distance.
                PropLibrary.Spawn(b, KeyCornice, PrimitiveType.Cube, at + Vector3.down * 0.45f,
                    Along(tan, n, 18.6f, 0.5f, 1.5f), mats.Trim, t);
                b.SpawnPrim(PrimitiveType.Cube, at + Vector3.down * 0.95f,
                    Along(tan, n, 18.0f, 0.34f, 0.9f), mats.Stone, t);
                return 2;
            }
            case FacadeModule.SetbackCrown:
            {
                b.SpawnPrim(PrimitiveType.Cube, at + Vector3.down * 0.4f,
                    Along(tan, n, 18.2f, 0.5f, 1.1f), mats.Trim, t);
                b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 1.1f - n * 1.2f,
                    Along(tan, n, 14.5f, 2.2f, 0.6f), mats.Stone, t);
                return 2;
            }
            default: // Parapet -- a plain raised lip, the utilitarian crown
            {
                PropLibrary.Spawn(b, KeyParapet, PrimitiveType.Cube, at + Vector3.up * 0.55f,
                    Along(tan, n, 18.2f, 1.1f, 0.55f), mats.Wall, t);
                return 1;
            }
        }
    }

    // ---- shared helpers -------------------------------------------------

    /// <summary>Build a scale vector for a slab lying in a face plane:
    /// `along` across the face, `up` vertically, `depth` outward. Saves
    /// every call site from re-deriving which world axis is which for
    /// each of the four faces -- exactly the kind of hand-copied offset
    /// math this project's own style notes warn drifts wrong.</summary>
    private static Vector3 Along(Vector3 tan, Vector3 n, float along, float up, float depth)
    {
        var s = new Vector3(
            Mathf.Abs(tan.x) * along + Mathf.Abs(n.x) * depth,
            up,
            Mathf.Abs(tan.z) * along + Mathf.Abs(n.z) * depth);
        // guard against a zero component if a caller passes an odd axis
        if (s.x < 0.01f) s.x = depth;
        if (s.z < 0.01f) s.z = depth;
        return s;
    }

    /// <summary>Hook a window into the city's existing two-tier lighting
    /// model, but only when this window's own independent roll says so.
    ///
    /// docs/28's model is "unlimited free emissive, one shared budgeted
    /// pool of real lights." That holds only while the number of
    /// registered glow points stays sane -- and the existing per-floor
    /// strip registration already produces tens of thousands of entries at
    /// BigCity scale, each iterated every frame by EmissiveAnimator. This
    /// grammar spawns MORE windows per face than the old single strip did,
    /// so registering every one would make a known-strained system
    /// materially worse. Instead the emissive material still renders on
    /// every lit window (free), and only a sampled subset per building
    /// registers for animation and the real-light budget.
    ///
    /// 2026-08 correction: that sample used to be the FIRST `GlowBudget`
    /// windows encountered in build order (bottom floor upward, left to
    /// right within a floor) -- a sequential counter that fills up and
    /// stops, not a random pick. Since floors build bottom-up, this always
    /// lit the SAME contiguous block (whichever floors got processed
    /// before the budget ran out) and left every floor above it
    /// permanently dark, every building, every night -- solid lit rows
    /// stacked under solid dark rows, not the scattered "some windows on,
    /// some off, mixed together" a real occupied building reads as
    /// (creator, showing the exact wrong pattern with ASCII art: "XX XX
    /// XX / OO OO OO" blocks, wanted "XX OO OX ... Random" instead). Fixed
    /// by rolling each window independently against `GlowChance` -- same
    /// mechanism `BuildingDresser.SpawnWindowStrip`'s per-strip roll
    /// already uses for Small-tier buildings, which never had this
    /// clumping bug because it was never budget/order-based to begin
    /// with.</summary>
    private static void RegisterWindowGlow(GameObject go, FacadeMaterials mats, Vector3 pos)
    {
        if (go == null || !mats.WindowsLit) return;
        var seed = mats.NextGlowSeed();
        if (!mats.RollGlow(seed)) return;

        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        renderer.sharedMaterial = mats.GlassLit;
        EmissiveAnimator.Register(renderer, mats.GlowColor * CityLightingProfile.Active.BulbEmissiveBase * 0.9f,
            LightBehaviorKind.Window, seed);
        GlowPointRegistry.Register(go.transform, mats.GlowColor);
    }
}

/// <summary>The material set and per-building glow chance one facade
/// solve draws from. Passed in rather than looked up per module so every
/// material stays a shared cached instance (SRP-batcher friendly, the
/// project's established convention) and so the glow chance is genuinely
/// per-building rather than a hidden global.</summary>
public sealed class FacadeMaterials
{
    public Material Wall;
    public Material Stone;
    public Material Trim;
    public Material Glass;
    public Material GlassLit;
    public Material DarkRecess;
    public Material Iron;
    public Material Rust;
    public Material Sign;
    public Color GlowColor;

    /// <summary>False during the day-facing build, or for a building whose
    /// hash says it's dark tonight.</summary>
    public bool WindowsLit;

    /// <summary>Independent per-window chance of registering for emissive
    /// animation + the real-light budget -- set by the caller
    /// (BuildingDresser.DressFacadeGrammar). Replaced a sequential
    /// first-N-windows `GlowBudget` counter (2026-08, docs/28 row 35):
    /// that always lit the SAME contiguous block in build order (bottom
    /// floors up) and left everything above it permanently dark, not the
    /// scattered/random mix of on and off windows a real occupied
    /// building reads as. `RollGlow` below decides each window on its
    /// own, independent of every other window on the building, so the
    /// result scatters across floors and positions instead of clumping.</summary>
    public float GlowChance;

    private int _seedCursor;

    /// <summary>seed is itself already a ~uniform pseudo-random value in
    /// [0,1) (see NextGlowSeed) -- re-hashed with its own multiplier/
    /// offset here (same decorrelation trick EmissiveAnimator's
    /// OnCycleFrac/OffCycleFrac use on a shared seed) so this roll doesn't
    /// just reuse the raw seed value the caller ALSO passes to
    /// EmissiveAnimator.Register as its animation phase offset.</summary>
    public bool RollGlow(float seed)
    {
        return Frac(seed * 13.7f + 1.3f) < GlowChance;
    }

    private static float Frac(float v) { return v - Mathf.Floor(v); }

    public float NextGlowSeed()
    {
        _seedCursor++;
        unchecked
        {
            var h = (_seedCursor * 2654435761u) ^ 0x9E3779B9u;
            return (h % 10007u) / 10007f;
        }
    }
}
