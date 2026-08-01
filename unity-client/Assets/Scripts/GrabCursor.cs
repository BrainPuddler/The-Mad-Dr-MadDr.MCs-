using MadDr.CityGen;
using MadDr.MatchCore;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Creator direction, verbatim: "when the user press the g key it will
/// change the pointer to a claw, and I can click on a monster, pick it
/// up, it will wiggle and squirm. Then I can drop it onto the factory to
/// clone that monster. spawning more based on the amount of resources
/// required."
///
/// G is a TOGGLE, not a held modifier -- press once to arm claw mode (the
/// real OS cursor swaps to a procedurally-drawn claw glyph via
/// <see cref="Cursor.SetCursor"/>, since this repo has no icon/cursor
/// asset files anywhere, same "generate it, don't invent an asset
/// pipeline" discipline <see cref="RegionPickerHud"/>'s own thumbnail
/// baking already established), press again to disarm (drops whatever's
/// currently carried right where it is, cursor reverts to normal).
///
/// While armed: left-click a monster to pick it up -- <see
/// cref="MonsterAgent.BeginHeld"/> suspends its own Update() entirely and
/// hands control of its transform to <see cref="MonsterAgent.TickHeld"/>,
/// called from here every frame with the cursor's current ground point
/// (the "wiggle and squirm" itself lives in that method, not here). A
/// second left-click drops it: if the drop point lands on/near one of
/// THIS player's own Complete Factory buildings, <see cref="CloneOnto"/>
/// fires; the carried monster is never consumed either way -- dropping it
/// is what CLONES more, not a sacrifice of the original (no instruction
/// says otherwise, and destroying the player's own creature on every
/// drop would be a needlessly punishing reading of "drop it onto the
/// factory to clone that monster"). Claw mode stays armed after a drop,
/// same "repeatable action, not a one-shot" precedent <see
/// cref="BuildGhostCursor"/>'s own build-then-build-again flow already
/// follows.
///
/// "Spawning more based on the amount of resources required": v0.1
/// placeholder economy (CLAUDE.md's standing policy for every invented
/// cost number in this project) -- a flat Blood cost per clone, spent
/// from <see cref="RuntimeCityBuilder.WalletBlood"/> (the SAME wallet
/// eating citizens already fills, so cloning literally spends the Blood
/// harvested from citizens, a real thematic fit rather than an arbitrary
/// choice), keeps spawning clones for as long as the wallet affords the
/// next one, capped at <see cref="maxClonesPerDrop"/> so a very full
/// wallet can't spawn an unbounded pile in one drop. Not routed through
/// match-core: the Mad Doctor has no fixed `RosterUnitKind` roster at all
/// (bred creatures only, per `FactionRoster.cs`'s own header) -- cloning
/// an already-live genome doesn't fit that model, and a full new
/// match-core `CommandKind` for it is real, separate, not-yet-attempted
/// scope, flagged here rather than silently built as a parallel spend
/// path match-core's own wallet never sees.
///
/// IMGUI-free: this component draws nothing itself (the claw is a REAL OS
/// cursor, not a screen-space icon) -- only Cursor.SetCursor and Update()
/// logic.
/// </summary>
public class GrabCursor : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;
    public int localPlayerIndex = 0;

    [Header("Cloning (v0.1 placeholder economy)")]
    [Tooltip("Blood spent per clone -- an invented v0.1 placeholder number (CLAUDE.md's standing policy for every unsourced cost in this project), not from any design doc.")]
    public int cloneCostBlood = 60;

    [Tooltip("Hard cap on clones spawned from one drop, regardless of how much Blood is banked -- a very full wallet shouldn't be able to flood the field in a single click.")]
    public int maxClonesPerDrop = 10;

    [Tooltip("How many hex-rings out from the Factory's own hex a drop still counts as \"on it\" -- some forgiveness, same idea as any real drag-and-drop target having a hit margin bigger than its exact pixel bounds.")]
    public int dropRangeHexes = 1;

    private enum Mode { Off, Armed, Carrying }
    private Mode _mode = Mode.Off;
    private MonsterAgent _carried;

    // 2026-07 (creator direction: "when a new monster is dropped on a
    // factory, the current monster is booted to the next parking spot
    // closest to the factory and the new monster replaces the old one on
    // the factory roof"): one roof slot per Factory, keyed by its own
    // EntityId. Kept in sync at the two (and only two) places a roof
    // occupant's real state can change -- TryPickUp (grabbed back off the
    // roof) and Drop (bumped by a fresh drop) -- see
    // RemoveFromRoofOccupancy's own header for why nothing else needs to
    // touch this dictionary.
    private readonly System.Collections.Generic.Dictionary<uint, MonsterAgent> _roofOccupant =
        new System.Collections.Generic.Dictionary<uint, MonsterAgent>();

    private static Texture2D _clawTexture;

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder, int playerIndex)
    {
        bridge = simBridge;
        builder = cityBuilder;
        localPlayerIndex = playerIndex;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null || builder == null) return;

        if (keyboard.gKey.wasPressedThisFrame)
        {
            if (_mode == Mode.Off) EnterArmed();
            else ExitToOff();   // from Armed OR Carrying -- always a full, immediate exit
            return;
        }

        if (_mode == Mode.Off) return;

        var cam = Camera.main;
        if (cam == null) return;

        // same "OnGUI already claimed this click" guard every other
        // click-handling script in this project applies for Minimap/
        // BuildMenuHud/BuildingNavHud/SelectionHud.
        if (Minimap.PointerOver || BuildingNavHud.PointerOver || SelectionHud.PointerOver) return;

        if (_mode == Mode.Armed)
        {
            if (mouse.leftButton.wasPressedThisFrame) TryPickUp(cam, mouse);
            return;
        }

        // Carrying
        if (_carried == null) { _mode = Mode.Armed; return; }   // held monster died/was destroyed mid-carry

        var groundPoint = GroundUnderCursor(cam, mouse);
        if (groundPoint.HasValue) _carried.TickHeld(HoverTargetFor(groundPoint.Value), Time.deltaTime);

        if (mouse.leftButton.wasPressedThisFrame) Drop(groundPoint);
    }

    /// <summary>Creator direction: "when I move the pointer with the
    /// grabbed monster it should automatically position the monster
    /// above the roof of the factory." A drag-and-drop snap preview: once
    /// the cursor's ground point falls within <see cref="dropRangeHexes"/>
    /// of the local player's own Factory (the SAME check <see
    /// cref="Drop"/> uses to decide whether to clone), the carried
    /// monster hovers centered above THAT Factory's roof instead of
    /// following the raw cursor position -- the same "the preview can
    /// never disagree with the actual outcome" principle
    /// `BuildGhostCursor`'s own placement preview already follows for
    /// building placement, applied here to the grab/clone drop target
    /// instead. Falls back to the raw ground point when no Factory is in
    /// range, i.e. everywhere else on the map behaves exactly as
    /// before.</summary>
    private Vector3 HoverTargetFor(Vector3 groundPoint)
    {
        var hex = builder.HexAt(groundPoint);
        var factory = FindOwnFactoryNear(hex);
        if (factory == null) return groundPoint;

        var roofWorld = builder.WorldOf(factory.Hex);
        roofWorld.y = builder.GroundHeightAt(roofWorld) + BaseDresser.RoofHeightFor(factory.Kind);
        return roofWorld;
    }

    private void EnterArmed()
    {
        _mode = Mode.Armed;
        // Cursor.SetCursor's hotspot is top-down pixel coords, but the
        // Color32 array ClawTexture builds (via SetPixels32) is bottom-up
        // (row 0 = bottom row) -- the claw's own grab point sits at
        // texture-space (16, 6) in that bottom-up drawing, which is
        // (16, 32-1-6) = (16, 25) once flipped to the top-down convention
        // SetCursor actually expects.
        Cursor.SetCursor(ClawTexture(), new Vector2(16f, 25f), CursorMode.Auto);
    }

    private void ExitToOff()
    {
        if (_carried != null)
        {
            _carried.EndHeld();
            _carried = null;
        }
        _mode = Mode.Off;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void TryPickUp(Camera cam, Mouse mouse)
    {
        var hit = RaycastCursor(cam, mouse);
        if (hit == null) return;
        var agent = hit.Value.collider.GetComponentInParent<MonsterAgent>();
        if (agent == null || agent.IsHeld) return;

        RemoveFromRoofOccupancy(agent);
        agent.BeginHeld();
        _carried = agent;
        _mode = Mode.Carrying;
    }

    private void Drop(Vector3? groundPoint)
    {
        var agent = _carried;
        _carried = null;
        _mode = Mode.Armed;   // stays armed -- pick up the next one right away
        if (agent == null) return;

        if (groundPoint.HasValue)
        {
            var dropHex = builder.HexAt(groundPoint.Value);
            var factory = FindOwnFactoryNear(dropHex);
            if (factory != null)
            {
                CloneOnto(agent, factory.Hex);

                // creator direction: "when a new monster is dropped on a
                // factory, the current monster is booted to the next
                // parking spot closest to the factory and the new monster
                // replaces the old one on the factory roof" -- whoever
                // already holds this Factory's roof slot (if anyone, and
                // if it isn't this same agent being re-dropped on its own
                // spot) steps aside to the nearest open hex before the new
                // arrival takes the roof, the SAME FindOpenHexNear parking
                // search CloneOnto's own fan-out already uses.
                if (_roofOccupant.TryGetValue(factory.EntityId, out var evicted) && evicted != null && evicted != agent)
                {
                    var bootSpot = FindOpenHexNear(factory.Hex, new System.Collections.Generic.HashSet<HexCoord>(), evicted.Radius);
                    if (bootSpot != null) evicted.BootFromRoof(builder.WorldOf(bootSpot.Value));
                }

                // creator direction: "it should land on the roof and
                // rotate slowly in the Y axis" -- the ORIGINAL creature
                // (not consumed by cloning) settles on top of the Factory
                // it was just dropped on, instead of hovering wherever
                // the cursor happened to be.
                var roofWorld = builder.WorldOf(factory.Hex);
                roofWorld.y = builder.GroundHeightAt(roofWorld) + BaseDresser.RoofHeightFor(factory.Kind);
                agent.BeginRoofDisplay(roofWorld);
                _roofOccupant[factory.EntityId] = agent;
                return;
            }
        }
        agent.EndHeld();
    }

    /// <summary>Roof occupancy only ever changes at TWO real events -- a
    /// monster gets grabbed back off the roof (TryPickUp), or a fresh
    /// drop bumps it (Drop, above) -- a roof-displaying monster never
    /// leaves on its own (Update() early-returns for as long as
    /// `_roofDisplay` is true, so idle target-acquisition/orders never
    /// fire on it). Called from TryPickUp so a monster grabbed off one
    /// Factory's roof and dropped somewhere else (a different Factory, or
    /// nowhere at all) doesn't leave a stale reference behind that would
    /// wrongly evict it a second time later.</summary>
    private void RemoveFromRoofOccupancy(MonsterAgent agent)
    {
        uint? key = null;
        foreach (var kv in _roofOccupant)
        {
            if (kv.Value != agent) continue;
            key = kv.Key;
            break;
        }
        if (key.HasValue) _roofOccupant.Remove(key.Value);
    }

    private SimBuilding FindOwnFactoryNear(HexCoord hex)
    {
        if (bridge == null || !bridge.HasMatch) return null;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex != localPlayerIndex || b.Kind != BuildingKind.Factory || b.State != BuildingState.Complete) continue;
            if (b.Hex.DistanceTo(hex) <= dropRangeHexes) return b;
        }
        return null;
    }

    /// <summary>Spend down `builder.WalletBlood` one clone at a time, each
    /// an exact copy of `original`'s own genome. Creator direction: "when
    /// clones pop out they should emerge and park themselves around the
    /// factory" -- each clone SPAWNS at the Factory's own hex (it visibly
    /// comes out of the building that made it) and is immediately handed
    /// a settle-creep destination to a nearby open hex via <see
    /// cref="MonsterAgent.SetSettleTarget"/>, the SAME direct-line "walk
    /// to a point and stop" mechanism group-move arrival already uses --
    /// reused rather than reinvented. Each clone claims a distinct parking
    /// hex so N of them fan out around the Factory instead of walking to
    /// the same spot and stacking.</summary>
    private void CloneOnto(MonsterAgent original, HexCoord factoryHex)
    {
        var creature = original.Creature;
        if (creature == null || builder == null) return;

        var claimed = new System.Collections.Generic.HashSet<HexCoord>();
        var spawned = 0;
        while (spawned < maxClonesPerDrop)
        {
            // find the parking spot BEFORE spending -- an unaffordable-or-
            // nowhere-to-park check must never debit Blood for a clone
            // that then has nowhere to go.
            var parkSpot = FindOpenHexNear(factoryHex, claimed, original.Radius);
            if (parkSpot == null) break;
            if (!builder.TrySpendBlood(cloneCostBlood)) break;
            claimed.Add(parkSpot.Value);

            var clone = builder.SpawnMonster(creature, factoryHex);
            clone.SetSettleTarget(builder.WorldOf(parkSpot.Value));
            spawned++;
        }

        if (spawned > 0)
            Debug.Log("Cloned " + spawned + "x " + creature.Id + " at the Factory (" + cloneCostBlood * spawned + " Blood spent).");
    }

    /// <summary>2026-08 (creator direction: "increase the boundary around
    /// building so parking spots take into account monster size"): a hex
    /// not being individually `IsBlocked` doesn't mean a body `bodyRadius`
    /// wide actually clears the building once it's standing there --
    /// small/medium creatures were fine at ring 1 (a hex's ~20m step
    /// comfortably clears the building's own ~9m footprint half-extent),
    /// but a big-bodied monster's own collision radius could still reach
    /// back into the building's real rendered footprint (corner overhang
    /// included -- same `InsideBuildingFootprint` geometry `TickSettle`
    /// checks) or crowd an already-claimed neighbour closer than both
    /// bodies actually need. Checks the NEAR EDGE of where this monster's
    /// body would sit (`world` offset `bodyRadius` back toward the
    /// building), not just the hex's own centre point, so the effective
    /// search boundary grows with the monster automatically instead of a
    /// fixed ring count -- a small monster still parks at ring 1 exactly
    /// as before (byte-identical for `bodyRadius` well under the
    /// clearance ring-1 already has), a huge one is pushed out to
    /// whichever ring first has genuine room.</summary>
    private HexCoord? FindOpenHexNear(HexCoord from, System.Collections.Generic.HashSet<HexCoord> claimed, float bodyRadius)
    {
        var buildingWorld = builder.WorldOf(from);
        var minGap = bodyRadius * 2f + builder.groupSpacing;
        var minGapSq = minGap * minGap;
        for (var ring = 1; ring <= 8; ring++)
            foreach (var n in from.Ring(ring))
            {
                if (!builder.CityContains(n) || builder.IsBlocked(n) || claimed.Contains(n)) continue;
                var world = builder.WorldOf(n);

                var towardBuilding = buildingWorld - world;
                towardBuilding.y = 0f;
                var nearEdge = towardBuilding.sqrMagnitude > 1e-4f
                    ? world + towardBuilding.normalized * bodyRadius
                    : world;
                if (builder.InsideBuildingFootprint(nearEdge)) continue;

                var tooCloseToClaimed = false;
                foreach (var c in claimed)
                {
                    if ((builder.WorldOf(c) - world).sqrMagnitude < minGapSq) { tooCloseToClaimed = true; break; }
                }
                if (!tooCloseToClaimed) return n;
            }
        return null;
    }

    private static bool GroundUnderCursorRay(Ray ray, out Vector3 world)
    {
        world = Vector3.zero;
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
        var t = -ray.origin.y / ray.direction.y;
        if (t <= 0f) return false;
        world = ray.origin + ray.direction * t;
        return true;
    }

    private Vector3? GroundUnderCursor(Camera cam, Mouse mouse)
    {
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        Vector3 world;
        if (GroundUnderCursorRay(ray, out world))
        {
            world.y = builder.GroundHeightAt(world);
            return world;
        }
        return null;
    }

    private static RaycastHit? RaycastCursor(Camera cam, Mouse mouse)
    {
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5000f)) return hit;
        return null;
    }

    /// <summary>A small (32x32) procedurally-drawn MECHANICAL claw glyph
    /// -- creator direction: "The Claw should be a mechanical Claw for
    /// all races," a neutral tool cursor rather than anything
    /// faction/origin-flavored (a real design correction from the first
    /// pass's own gothic-red creature-pincer look). A cable-mount hub at
    /// the top with three riveted metal prongs curving down to a
    /// convergence point, arcade-claw-machine silhouette -- since this
    /// repo has no cursor/icon asset files anywhere (same reasoning
    /// BuildingNavHud's own header gives for its colored-swatch "icons").
    /// Honesty note: legibility at real cursor size can't be verified
    /// without a real Editor/Player render in this environment; the shape
    /// is a best-effort recognizable mechanical claw, not a claimed
    /// pixel-perfect result.</summary>
    private static Texture2D ClawTexture()
    {
        if (_clawTexture != null) return _clawTexture;

        const int size = 32;
        var pixels = new Color32[size * size];
        var clear = new Color32(0, 0, 0, 0);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

        var metal = new Color32(195, 198, 205, 255);    // brushed-steel gray, race-neutral
        var metalDark = new Color32(120, 124, 132, 255); // shaded underside of each prong
        var outline = new Color32(35, 36, 40, 255);
        var rivet = new Color32(70, 72, 78, 255);

        void Plot(int x, int y, Color32 c)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            pixels[y * size + x] = c;
        }
        void PlotThick(float fx, float fy, Color32 c, int r)
        {
            var cx = Mathf.RoundToInt(fx);
            var cy = Mathf.RoundToInt(fy);
            for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r) Plot(cx + dx, cy + dy, c);
        }

        // cable-mount hub near the top -- where the claw's "arm" would
        // continue off-screen -- plus 3 rivets around its rim.
        var hubX = 16f; var hubY = 27f;
        PlotThick(hubX, hubY, outline, 5);
        PlotThick(hubX, hubY, metal, 4);
        for (var i = 0; i < 3; i++)
        {
            var ra = (i / 3f) * Mathf.PI * 2f;
            PlotThick(hubX + Mathf.Cos(ra) * 3f, hubY + Mathf.Sin(ra) * 3f, rivet, 1);
        }

        // three curved prongs sweeping down from the hub and converging
        // toward the hotspot (the actual grab point) below-center -- the
        // classic arcade-claw-machine silhouette. Each prong drawn as a
        // thick arc plus a hinge knuckle partway down, shaded on one edge
        // so the round cross-section reads even at pixel scale.
        var hotspotX = 16f; var hotspotY = 6f;
        float[] spread = { -7f, 0f, 7f };   // horizontal offset of each prong's outer sweep
        foreach (var s in spread)
        {
            for (var t = 0; t <= 16; t++)
            {
                var u = t / 16f;
                var px = Mathf.Lerp(hubX + s, hotspotX, u * u);          // eased inward -- prongs bow outward before closing on the hotspot
                var py = Mathf.Lerp(hubY - 2f, hotspotY, u);
                var bow = Mathf.Sin(u * Mathf.PI) * Mathf.Abs(s) * 0.5f * Mathf.Sign(s == 0f ? 1f : s);
                PlotThick(px + bow, py, outline, 2);
                PlotThick(px + bow, py, u > 0.45f && u < 0.55f ? rivet : metal, 1);   // a hinge knuckle at the midpoint
            }
            // shaded underside stroke, offset slightly, for a rounded read
            for (var t = 0; t <= 16; t++)
            {
                var u = t / 16f;
                var px = Mathf.Lerp(hubX + s, hotspotX, u * u);
                var py = Mathf.Lerp(hubY - 2f, hotspotY, u);
                var bow = Mathf.Sin(u * Mathf.PI) * Mathf.Abs(s) * 0.5f * Mathf.Sign(s == 0f ? 1f : s);
                Plot(Mathf.RoundToInt(px + bow) + 1, Mathf.RoundToInt(py) - 1, metalDark);
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        _clawTexture = tex;
        return tex;
    }
}
