using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
using MadDr.RosterClient;
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
/// via <see cref="RuntimeCityBuilder.TrySpendBlood"/> against the REAL
/// match-core wallet (2026-08 fix, docs/12: this and <see
/// cref="RuntimeCityBuilder.OnCitizenEaten"/> both used to read/write a
/// disconnected client-side counter -- now both go through the same
/// real wallet, so cloning genuinely spends the Blood harvested from
/// citizens, a real thematic fit rather than an arbitrary choice), keeps
/// spawning clones for as long as the wallet affords the next one,
/// capped at <see cref="maxClonesPerDrop"/> so a very full wallet can't
/// spawn an unbounded pile in one drop. The CLONE action itself is still
/// not routed through match-core: the Mad Doctor has no fixed
/// `RosterUnitKind` roster at all (bred creatures only, per
/// `FactionRoster.cs`'s own header) -- cloning an already-live genome
/// doesn't fit that model, and a full new match-core `CommandKind` for
/// spawning a duplicate unit is real, separate, not-yet-attempted scope.
/// Only the resource DEBIT needed a real command
/// (`CommandKind.SpendResource`, new) -- that part is generic and
/// doesn't require a roster kind at all.
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

    [Tooltip("2026-08 (creator direction: \"increase the building no parking area to take into account the monster size\"): extra clearance, in meters, ADDED on top of a monster's own body radius when FindOpenHexNear checks a candidate parking spot's near edge against the building's real footprint -- the exact-fit geometric check alone leaves zero breathing room (a spot that JUST clears the wall still reads as uncomfortably tight once a body is actually standing there). Widening this widens the effective no-parking zone around every building for every monster size at once, without touching the per-monster radius math itself.")]
    public float buildingClearanceMargin = 2f;

    [Header("Production queue (2026-08: \"factories, like in StarCraft, make x number of units\")")]
    public WaypointCommander commander;

    [Tooltip("Seconds of production time per clone, regardless of whether it's part of a single-unit run or a battalion build -- a real v0.1 placeholder (CLAUDE.md's standing policy), not sourced from any design doc.")]
    public float productionSecondsPerUnit = 4f;

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

    // ---- production queue (2026-08 creator direction: "Factories, like
    // in StarCraft make x number of units. So the same happens here in
    // the build a battalion... The monsters line up at the cloning door
    // of the factory and one at a time walk get cloned... queued icons
    // with numbers... specify the number of units to make of each type
    // that includes battalions and individual units") -----------------

    private enum QueueItemKind { SingleUnit, Battalion, LabBattalion }

    /// <summary>One queued production run. SingleUnit reproduces ONE
    /// genome `RemainingCount` times (repeat drops of the SAME creature
    /// onto the Factory stack onto this SAME item's count instead of
    /// adding a second icon -- see <see cref="QueueSingleUnit"/>).
    /// Battalion reproduces a SNAPSHOT of a squad's own genomes/radii,
    /// taken at queue time (not read live later -- the original members
    /// could die mid-queue without that changing what gets built), one
    /// member per production tick; `Produced` accumulates the clones so
    /// <see cref="WaypointCommander.FormBattalionFromProduction"/> can
    /// gather them into a fresh battalion the instant the last one pops
    /// out.</summary>
    private sealed class QueueItem
    {
        public QueueItemKind Kind;
        public StoredGenomeDto SingleGenome;
        public float SingleRadius;
        public int RemainingCount;
        public System.Collections.Generic.List<(StoredGenomeDto Genome, float Radius)> BattalionRemaining;
        /// <summary>LabBattalion only -- a Lab-defined template's genomes,
        /// with no per-member radius (unlike a live-selection Battalion,
        /// a template has no fielded MonsterAgent to read a real radius
        /// from before spawning; see <see cref="BuildLabBattalion"/>).</summary>
        public System.Collections.Generic.List<StoredGenomeDto> LabBattalionRemaining;
        public string Label;   // HUD display: genome Id for SingleUnit, the source battalion's own name for Battalion/LabBattalion
        public readonly System.Collections.Generic.List<MonsterAgent> Produced = new System.Collections.Generic.List<MonsterAgent>();
    }

    private readonly System.Collections.Generic.List<QueueItem> _queue = new System.Collections.Generic.List<QueueItem>();
    private float _productionTimer;

    /// <summary>Read-only view for <see cref="ProductionQueueHud"/> --
    /// one entry per queued item, in build order, with a 0..1 progress
    /// fraction (only meaningful, and only ever nonzero, for the FRONT
    /// item -- everything behind it hasn't started).</summary>
    public IEnumerable<(string Label, int Remaining, float Progress)> ProductionQueue
    {
        get
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                var item = _queue[i];
                var remaining = item.Kind == QueueItemKind.SingleUnit ? item.RemainingCount
                    : item.Kind == QueueItemKind.Battalion ? item.BattalionRemaining.Count
                    : item.LabBattalionRemaining.Count;
                var progress = i == 0 ? Mathf.Clamp01(_productionTimer / Mathf.Max(0.01f, productionSecondsPerUnit)) : 0f;
                yield return (item.Label, remaining, progress);
            }
        }
    }

    public bool HasQueuedProduction { get { return _queue.Count > 0; } }

    /// <summary>2026-08: "click on the factory and can abort all
    /// builds" -- clears every queued item outright. Nothing to refund:
    /// Blood is only ever spent the instant a clone actually pops out
    /// (see <see cref="TickProduction"/>), never reserved up front when
    /// an item is queued, so there's nothing owed back for work that
    /// never happened.</summary>
    public void CancelAllProduction()
    {
        _queue.Clear();
        _productionTimer = 0f;
    }

    /// <summary>Queues `count` more clones of `genome` -- stacks onto an
    /// existing queued run of the SAME genome (by `Id`) anywhere in the
    /// queue instead of adding a second entry, so "queued icons... one
    /// per type" holds even across repeat drops. `radius` is captured
    /// NOW (from whichever live agent is being dropped/queued), not
    /// re-derived from the genome later -- see <see cref="QueueItem"/>'s
    /// own doc for why.</summary>
    private void QueueSingleUnit(StoredGenomeDto genome, float radius, int count)
    {
        if (genome == null || count <= 0) return;
        foreach (var item in _queue)
        {
            if (item.Kind != QueueItemKind.SingleUnit || item.SingleGenome.Id != genome.Id) continue;
            item.RemainingCount += count;
            return;
        }
        _queue.Add(new QueueItem
        {
            Kind = QueueItemKind.SingleUnit,
            SingleGenome = genome,
            SingleRadius = radius,
            RemainingCount = count,
            Label = genome.Id,
        });
    }

    /// <summary>One production tick per frame -- advances the FRONT
    /// item's timer and, once it crosses `productionSecondsPerUnit`,
    /// spawns exactly one clone (same spend/park mechanics <see
    /// cref="CloneOnto"/> always used, just one at a time instead of a
    /// tight while-loop) and hands it the same settle-creep walk-away
    /// destination -- staggering real spawns over real time is what
    /// gives "the monsters line up at the cloning door... and one at a
    /// time walk get cloned" its read, with no separate queueing-
    /// animation state needed: each clone's own existing walk-out-and-
    /// park behavior IS the visual. Stalls (leaves the timer at its
    /// current value, retries next frame) rather than dropping the item
    /// if there's no own Factory right now, no parking room this
    /// instant, or insufficient Blood -- production simply waits for
    /// whichever condition is blocking it to clear.</summary>
    private void TickProduction(float dt)
    {
        if (_queue.Count == 0) { _productionTimer = 0f; return; }
        _productionTimer += dt;
        if (_productionTimer < productionSecondsPerUnit) return;

        var factory = FindAnyOwnCompleteFactory();
        if (factory == null) return;

        var item = _queue[0];
        StoredGenomeDto genome;
        float radius;
        if (item.Kind == QueueItemKind.SingleUnit) { genome = item.SingleGenome; radius = item.SingleRadius; }
        else if (item.Kind == QueueItemKind.Battalion) { var next = item.BattalionRemaining[0]; genome = next.Genome; radius = next.Radius; }
        else { genome = item.LabBattalionRemaining[0]; radius = DefaultParkSearchRadius; }

        var parkSpot = FindOpenHexNear(factory.Hex, new System.Collections.Generic.HashSet<HexCoord>(), radius);
        if (parkSpot == null) return;   // no room this instant -- try again next frame
        if (!builder.TrySpendBlood(cloneCostBlood)) return;   // can't afford yet -- try again next frame

        _productionTimer = 0f;
        var clone = builder.SpawnMonster(genome, factory.Hex);
        clone.SetSettleTarget(builder.WorldOf(parkSpot.Value));

        if (item.Kind == QueueItemKind.SingleUnit)
        {
            item.RemainingCount--;
            if (item.RemainingCount <= 0) _queue.RemoveAt(0);
        }
        else if (item.Kind == QueueItemKind.Battalion)
        {
            item.Produced.Add(clone);
            item.BattalionRemaining.RemoveAt(0);
            if (item.BattalionRemaining.Count == 0)
            {
                if (commander != null) commander.FormBattalionFromProduction(item.Produced);
                _queue.RemoveAt(0);
            }
        }
        else
        {
            item.Produced.Add(clone);
            item.LabBattalionRemaining.RemoveAt(0);
            if (item.LabBattalionRemaining.Count == 0)
            {
                if (commander != null) commander.FormBattalionFromProduction(item.Produced);
                _queue.RemoveAt(0);
            }
        }
    }

    /// <summary>UnitCombat's own body-radius default (Fighter.Radius =
    /// 1.5f) -- the best park-spot-search guess available for a
    /// LabBattalion member before it's actually spawned and its real,
    /// body-derived radius (<see cref="MonsterAgent.Radius"/>, only
    /// known once the mesh is built) exists. Worst case a large
    /// creature's real footprint parks a touch tighter than ideal --
    /// never a correctness issue, since the search still confirms the
    /// hex is open before spawning there.</summary>
    private const float DefaultParkSearchRadius = 1.5f;

    /// <summary>Nearest of the player's own Complete Factories to the
    /// city center -- "nearest to what" doesn't have a clean answer for
    /// a QUEUE (unlike a single drop/battalion build, which has a real
    /// drop point/group position to be nearest TO), so this just picks
    /// consistently rather than arbitrarily; most games only ever have
    /// one Factory anyway. Null if the player has none yet. Public so
    /// <see cref="ProductionQueueHud"/> can anchor the floating build-
    /// progress badge over the same Factory this queue actually drains
    /// into, instead of re-deriving its own notion of "the" factory.</summary>
    public SimBuilding FindAnyOwnCompleteFactory()
    {
        if (bridge == null || !bridge.HasMatch || builder == null) return null;
        SimBuilding best = null;
        var bestDist = float.MaxValue;
        var center = builder.WorldOf(builder.City.CenterHex);
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex != localPlayerIndex || b.Kind != BuildingKind.Factory || b.State != BuildingState.Complete) continue;
            var dist = (builder.WorldOf(b.Hex) - center).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; best = b; }
        }
        return best;
    }

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder, int playerIndex)
    {
        bridge = simBridge;
        builder = cityBuilder;
        localPlayerIndex = playerIndex;
    }

    private void Update()
    {
        // production runs regardless of grab-mode state or where the
        // mouse happens to be -- a queue building in the background
        // shouldn't pause just because the player isn't currently
        // dragging a monster.
        if (builder != null) TickProduction(Time.deltaTime);

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
                CloneOnto(agent);

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

    /// <summary>2026-08 (creator direction: "Factories, like in StarCraft
    /// make x number of units... the monsters line up at the cloning
    /// door of the factory and one at a time walk get cloned"): queues
    /// `maxClonesPerDrop` more clones of `original`'s own genome for
    /// <see cref="TickProduction"/> to work through one at a time, rather
    /// than instantly spawning them all in one frame the way this used
    /// to. A repeat drop of the SAME creature stacks onto the existing
    /// queued count instead of adding a second entry -- see <see
    /// cref="QueueSingleUnit"/>'s own doc.</summary>
    private void CloneOnto(MonsterAgent original)
    {
        var creature = original.Creature;
        if (creature == null || builder == null) return;
        QueueSingleUnit(creature, original.Radius, maxClonesPerDrop);
    }

    /// <summary>2026-08 (creator direction: "battalion grouping system...
    /// I can make the factory build that battalion group of monsters",
    /// then "Factories, like in StarCraft make x number of units. So the
    /// same happens here in the build a battalion"): queues a snapshot
    /// of the battalion's own genomes/radii (NOT deduped -- "build that
    /// battalion GROUP" reads as reproducing the whole squad's own
    /// composition/proportions, e.g. 3 Tetrapods + 2 Winged queues 3
    /// more Tetrapods + 2 more Winged, not just one of each distinct
    /// type) for <see cref="TickProduction"/> to produce one member per
    /// tick. The instant the LAST member of this run pops out, <see
    /// cref="WaypointCommander.FormBattalionFromProduction"/> gathers
    /// everything this queue item produced into a fresh battalion --
    /// "when the battalion is done, it parks itself away from factory
    /// and assigned a name to it, adding it to the battalion list."
    /// Silent no-op if the battalion is empty/all members have since
    /// died -- nothing queued means nothing to build.</summary>
    public void BuildBattalionAtOwnFactory(System.Collections.Generic.IReadOnlyList<MonsterAgent> battalion)
    {
        if (battalion == null || battalion.Count == 0) return;

        var snapshot = new System.Collections.Generic.List<(StoredGenomeDto Genome, float Radius)>();
        var label = "Battalion build";
        foreach (var m in battalion)
        {
            if (m == null || m.Creature == null) continue;
            snapshot.Add((m.Creature, m.Radius));
            if (m.BattalionSlot.HasValue) label = "Battalion " + m.BattalionSlot.Value + " build";
        }
        if (snapshot.Count == 0) return;

        _queue.Add(new QueueItem { Kind = QueueItemKind.Battalion, BattalionRemaining = snapshot, Label = label });
    }

    /// <summary>2026-08 (docs/12 "Lab stable" half of battalion grouping:
    /// "in the lab, the stable area, where can shift plus quick select
    /// monsters and hit G key... make the factory build that battalion
    /// group of monsters"): the SAME production queue as <see
    /// cref="BuildBattalionAtOwnFactory"/>, but built from a Lab-defined
    /// TEMPLATE's genome ids resolved against the player's own fetched
    /// roster (<see cref="RuntimeCityBuilder.RosterCreatures"/>) instead
    /// of live fielded monsters. A creatureId that doesn't resolve --
    /// removed from the Stable, or a fetch race -- is silently skipped,
    /// same "don't crash on a stale reference" posture the Lab's own
    /// template storage already takes; the rest of the battalion still
    /// builds. Duplicate ids resolve to duplicate queue entries (the
    /// template legitimately allows "3 Tetrapods + 2 Winged").</summary>
    public void BuildLabBattalion(string name, string[] creatureIds)
    {
        if (string.IsNullOrEmpty(name) || creatureIds == null || creatureIds.Length == 0 || builder == null) return;

        var byId = new System.Collections.Generic.Dictionary<string, StoredGenomeDto>();
        foreach (var g in builder.RosterCreatures) byId[g.Id] = g;

        var resolved = new System.Collections.Generic.List<StoredGenomeDto>();
        var missing = 0;
        foreach (var id in creatureIds)
        {
            if (byId.TryGetValue(id, out var g)) resolved.Add(g);
            else missing++;
        }
        if (missing > 0)
            Debug.LogWarning("GrabCursor: Lab battalion \"" + name + "\" has " + missing
                + " creature id(s) that don't resolve against the fetched roster (removed from the Stable?) -- skipping them.");
        if (resolved.Count == 0) return;

        _queue.Add(new QueueItem { Kind = QueueItemKind.LabBattalion, LabBattalionRemaining = resolved, Label = name });
    }

    /// <summary>2026-08 (creator direction: "increase the boundary around
    /// building so parking spots take into account monster size", then
    /// "increase the building no parking area to take into account the
    /// monster size"): a hex not being individually `IsBlocked` doesn't
    /// mean a body `bodyRadius` wide actually clears the building once
    /// it's standing there -- small/medium creatures were fine at ring 1
    /// (a hex's ~20m step comfortably clears the building's own ~9m
    /// footprint half-extent), but a big-bodied monster's own collision
    /// radius could still reach back into the building's real rendered
    /// footprint (corner overhang included -- same
    /// `InsideBuildingFootprint` geometry `TickSettle` checks) or crowd
    /// an already-claimed neighbour closer than both bodies actually
    /// need. Checks the NEAR EDGE of where this monster's body would sit
    /// (`world` offset `bodyRadius + buildingClearanceMargin` back toward
    /// the building), not just the hex's own centre point, so the
    /// effective search boundary grows with the monster automatically
    /// instead of a fixed ring count -- and the added margin means a spot
    /// has real daylight around it, not just an exact, zero-tolerance
    /// fit. `buildingClearanceMargin` widens the zone for every monster
    /// size at once; a huge body is pushed out further still on top of
    /// that, to whichever ring first has genuine room.</summary>
    private HexCoord? FindOpenHexNear(HexCoord from, System.Collections.Generic.HashSet<HexCoord> claimed, float bodyRadius)
    {
        var buildingWorld = builder.WorldOf(from);
        var edgeClearance = bodyRadius + buildingClearanceMargin;
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
                    ? world + towardBuilding.normalized * edgeClearance
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
