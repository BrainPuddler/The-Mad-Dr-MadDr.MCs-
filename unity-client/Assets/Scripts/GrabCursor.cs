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
        // BuildMenuHud/BuildingNavHud.
        if (Minimap.PointerOver || BuildingNavHud.PointerOver) return;

        if (_mode == Mode.Armed)
        {
            if (mouse.leftButton.wasPressedThisFrame) TryPickUp(cam, mouse);
            return;
        }

        // Carrying
        if (_carried == null) { _mode = Mode.Armed; return; }   // held monster died/was destroyed mid-carry

        var groundPoint = GroundUnderCursor(cam, mouse);
        if (groundPoint.HasValue) _carried.TickHeld(groundPoint.Value, Time.deltaTime);

        if (mouse.leftButton.wasPressedThisFrame) Drop(groundPoint);
    }

    private void EnterArmed()
    {
        _mode = Mode.Armed;
        Cursor.SetCursor(ClawTexture(), new Vector2(6f, 26f), CursorMode.Auto);
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
            if (factory != null) CloneOnto(agent, factory.Hex);
        }
        agent.EndHeld();
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

    /// <summary>Spend down `builder.WalletBlood` one clone at a time,
    /// each an exact copy of `original`'s own genome, spawned at the
    /// nearest open hex to the Factory that isn't already claimed by an
    /// earlier clone from this same drop -- so N clones fan out around
    /// the Factory instead of stacking on one hex.</summary>
    private void CloneOnto(MonsterAgent original, HexCoord factoryHex)
    {
        var creature = original.Creature;
        if (creature == null || builder == null) return;

        var claimed = new System.Collections.Generic.HashSet<HexCoord>();
        var spawned = 0;
        while (spawned < maxClonesPerDrop)
        {
            // find the spot BEFORE spending -- an unaffordable-or-nowhere-
            // to-land check must never debit Blood for a clone that then
            // doesn't actually spawn.
            var spot = FindOpenHexNear(factoryHex, claimed);
            if (spot == null) break;
            if (!builder.TrySpendBlood(cloneCostBlood)) break;
            claimed.Add(spot.Value);
            builder.SpawnMonster(creature, spot.Value);
            spawned++;
        }

        if (spawned > 0)
            Debug.Log("Cloned " + spawned + "x " + creature.Id + " at the Factory (" + cloneCostBlood * spawned + " Blood spent).");
    }

    private HexCoord? FindOpenHexNear(HexCoord from, System.Collections.Generic.HashSet<HexCoord> claimed)
    {
        if (builder.CityContains(from) && !builder.IsBlocked(from) && !claimed.Contains(from)) return from;
        for (var ring = 1; ring <= 6; ring++)
            foreach (var n in from.Ring(ring))
                if (builder.CityContains(n) && !builder.IsBlocked(n) && !claimed.Contains(n)) return n;
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

    /// <summary>A small (32x32) procedurally-drawn claw/pincer glyph --
    /// two curved prongs converging toward the hotspot -- since this repo
    /// has no cursor/icon asset files anywhere (same reasoning
    /// BuildingNavHud's own header gives for its colored-swatch "icons").
    /// Honesty note: legibility at real cursor size can't be verified
    /// without a real Editor/Player render in this environment; the shape
    /// is a best-effort recognizable pincer, not a claimed pixel-perfect
    /// result.</summary>
    private static Texture2D ClawTexture()
    {
        if (_clawTexture != null) return _clawTexture;

        const int size = 32;
        var pixels = new Color32[size * size];
        var clear = new Color32(0, 0, 0, 0);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

        var outline = new Color32(20, 15, 15, 255);
        var fill = new Color32(150, 40, 40, 255);   // a gothic-red claw, not a neutral gray -- matches the project's own palette-discipline preference for its horror-tone reds over a generic UI gray

        void Plot(int x, int y, Color32 c)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            pixels[y * size + x] = c;
        }

        // two mirrored curved prongs (a quarter-circle arc each), tips
        // converging near the top-left hotspot -- a simple, cheap
        // procedural pincer shape, matching this codebase's own
        // primitive-first, no-external-asset dressing convention.
        for (var t = 0; t <= 20; t++)
        {
            var a = Mathf.Lerp(10f, 90f, t / 20f) * Mathf.Deg2Rad;
            var r = 13f;
            var cx = 8f; var cy = 24f;
            var px = cx + Mathf.Cos(a) * r;
            var py = cy - Mathf.Sin(a) * r;
            for (var w = -1; w <= 1; w++)
                Plot(Mathf.RoundToInt(px) + w, Mathf.RoundToInt(py), fill);
            Plot(Mathf.RoundToInt(px), Mathf.RoundToInt(py) - 1, outline);

            // mirrored second prong, offset across the hotspot
            var px2 = cx + 10f + Mathf.Cos(a + Mathf.PI) * r * 0.55f;
            var py2 = cy - 6f - Mathf.Sin(a) * r * 0.6f;
            for (var w = -1; w <= 1; w++)
                Plot(Mathf.RoundToInt(px2) + w, Mathf.RoundToInt(py2), fill);
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        _clawTexture = tex;
        return tex;
    }
}
