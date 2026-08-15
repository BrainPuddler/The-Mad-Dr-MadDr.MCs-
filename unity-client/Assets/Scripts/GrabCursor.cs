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
/// IMGUI-free: this component draws no screen-space UI itself (the claw is
/// a REAL OS cursor, not a screen-space icon; <see cref="ProductionQueueHud"/>
/// and <see cref="FactoryOrdersHud"/> own every OnGUI draw call this
/// feature needs, including the ones reading this class's own public
/// state) -- only Cursor.SetCursor, Update() logic, and (2026-08, factory
/// selection / drag-target highlights) a handful of lazily-built
/// world-space highlight props, the same kind of procedural GameObject
/// every other script in this project spawns directly, not an IMGUI draw.
///
/// 2026-08 follow-up (creator direction: "Implement and integrate a
/// Factory Build Queue / Order Clipboard system into the existing
/// factory-building functionality"): every Factory now has its OWN
/// production queue (was one shared queue for "any" own Factory) and a
/// small physical clipboard prop (<see cref="FactoryClipboard"/>,
/// spawned by <see cref="BaseDresser"/>) standing in for "add to queue"
/// as a drop target distinct from the Factory body itself ("build this
/// now" -- see <see cref="ResolveDropTarget"/>'s own doc comment for the
/// exact targeting rule). Dragging a Battalion off the existing bottom-
/// left menu rows (<see cref="BattalionHud"/>/<see
/// cref="LabBattalionHud"/>) now works the same way dragging a live
/// monster already did, via a new "carrying an abstract order" variant
/// of the existing Armed/Carrying state machine (see <see
/// cref="BeginCarryingOrder"/>) -- no second drag system, no second
/// Battalion representation.
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

    [Tooltip("2026-08 (creator direction: \"if the user presses the space bar a number on the grab increments, denoting the number of that monster you want to build\"): ceiling on how high the space-bar-dialed build count can go for one carry, AND the hard cap on clones spawned from one drop regardless of how much Blood is banked -- a very full wallet shouldn't be able to flood the field in a single click. Before this feature, every drop queued exactly this many flat; now it's just the upper bound on what the player can dial up to.")]
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

    /// <summary>Armed (claw cursor, waiting to pick something up) or
    /// Carrying (a monster OR an abstract order is already in hand) --
    /// either way, THIS script already owns the left mouse button for
    /// its own pick-up/drop semantics. Public so <see
    /// cref="WaypointCommander"/> can suppress its own left-drag marquee
    /// box-select while grab mode has the button claimed (creator
    /// direction: "when cursor is in grab mode, disable lasso rectangle
    /// select") -- without this, the same drag that repositions a
    /// carried monster before dropping it would ALSO draw and apply a
    /// selection-changing marquee underneath it.</summary>
    public bool IsGrabModeActive { get { return _mode != Mode.Off; } }

    // 2026-08 (creator direction, verbatim: "if the user presses the
    // space bar a number on the grab increments, denoting the number of
    // that monster you want to build"): how many clones THIS carry will
    // queue if dropped onto a Factory -- starts fresh at 1 on every
    // TryPickUp (see its own comment), climbs by 1 per space press while
    // Carrying (capped at maxClonesPerDrop, same safety ceiling that
    // used to be the flat amount every drop queued outright), and is
    // read once by CloneOnto at the moment of an actual drop. Separate
    // from the Space/+/- adjustment a SELECTED factory takes (see
    // TickFactorySelectionKeys) -- that one edits an ALREADY-queued
    // item's own RemainingCount directly, a different number with a
    // different lifetime, which just happens to share the same keys.
    private int _pendingBuildCount = 1;

    /// <summary>0 when nothing is being carried (ProductionQueueHud's own
    /// badge draw gates on this being &gt; 0 rather than separately
    /// checking Carrying), otherwise the live dial from the field
    /// above.</summary>
    public int PendingBuildCount { get { return _mode == Mode.Carrying ? _pendingBuildCount : 0; } }

    // 2026-08 ("click on the factory, highlighting it and press space or
    // + or - keys to increase or decrease the number of monsters to
    // build"): which own Factory (if any) is currently selected -- see
    // SelectFactory/TickFactorySelectionKeys. Distinct from the NEW
    // FactoryOrdersHud popup (opened by clicking the CLIPBOARD, not the
    // factory body) -- both can be live at once, on different Factories
    // even, without conflicting; they read/write the same per-Factory
    // queue data either way.
    private SimBuilding _selectedFactory;
    private GameObject _selectionHighlight;

    /// <summary>Live read for <see cref="ProductionQueueHud"/>'s own cost
    /// badge over a selected Factory -- the FRONT item of THAT Factory's
    /// OWN queue (2026-08: queues are now genuinely per-Factory, see
    /// <see cref="FactoryQueue"/> -- this used to read the one shared
    /// queue every own Factory drained into; that scope simplification
    /// is gone). `HasSelection` false means nothing to draw -- no
    /// Factory selected, or one selected with an empty queue.</summary>
    public (bool HasSelection, string Label, int Count, Vector3 WorldPos) SelectedFactoryBuild
    {
        get
        {
            if (_selectedFactory == null || builder == null) return (false, null, 0, Vector3.zero);
            var q = FactoryQueueFor(_selectedFactory, false);
            if (q == null || q.Items.Count == 0) return (false, null, 0, Vector3.zero);
            var item = q.Items[0];
            return (true, item.Label, RemainingOf(item), builder.WorldOf(_selectedFactory.Hex));
        }
    }

    /// <summary>How many units are still left to build in `item`,
    /// regardless of Kind -- the same three-way branch <see
    /// cref="ProductionQueueFor"/>'s own getter already computes inline
    /// for its `Remaining` tuple field, pulled out here so <see
    /// cref="SelectedFactoryBuild"/> and <see
    /// cref="TickFactorySelectionKeys"/> don't each grow a THIRD copy of
    /// it.</summary>
    private static int RemainingOf(QueueItem item)
    {
        if (item.Kind == QueueItemKind.SingleUnit) return item.RemainingCount;
        if (item.Kind == QueueItemKind.Battalion) return item.BattalionRemaining.Count;
        return item.LabBattalionRemaining.Count;
    }

    // 2026-07 (creator direction: "when a new monster is dropped on a
    // factory, the current monster is booted to the next parking spot
    // closest to the factory and the new monster replaces the old one on
    // the factory roof"): one roof slot per Factory, keyed by its own
    // EntityId. Kept in sync at the two (and only two) places a roof
    // occupant's real state can change -- TryPickUp (grabbed back off the
    // roof) and Drop (bumped by a fresh drop) -- see
    // RemoveFromRoofOccupancy's own header for why nothing else needs to
    // touch this dictionary.
    private readonly Dictionary<uint, MonsterAgent> _roofOccupant = new Dictionary<uint, MonsterAgent>();

    // 2026-08 (creator direction, verbatim: "if a monster is grabbed
    // from the roof of a factory, any movement beyond the factory
    // bounds should result in a snap to a clear ground position close"):
    // set only when the monster currently in `_carried` was pulled off
    // THIS Factory's own roof slot -- null for a normal ground-standing
    // monster grabbed anywhere else on the map, which this new bounds
    // check must never affect. Cleared at every place `_carried` itself
    // gets cleared (Drop, ExitToOff, and the bounds-snap below), same
    // "kept in sync everywhere the state it mirrors changes" discipline
    // `_roofOccupant` already follows.
    private SimBuilding _carriedFromRoofFactory;

    private static Texture2D _clawTexture;

    // ---- production queue (2026-08 creator direction: "Factories, like
    // in StarCraft make x number of units. So the same happens here in
    // the build a battalion... The monsters line up at the cloning door
    // of the factory and one at a time walk get cloned... queued icons
    // with numbers... specify the number of units to make of each type
    // that includes battalions and individual units"; follow-up: "Factory
    // Build Queue / Order Clipboard system") -----------------

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
        public List<(StoredGenomeDto Genome, float Radius)> BattalionRemaining;
        /// <summary>LabBattalion only -- a Lab-defined template's genomes,
        /// with no per-member radius (unlike a live-selection Battalion,
        /// a template has no fielded MonsterAgent to read a real radius
        /// from before spawning; see <see cref="BuildLabBattalion"/>).</summary>
        public List<StoredGenomeDto> LabBattalionRemaining;
        public string Label;   // HUD display: genome Id for SingleUnit, the source battalion's own name for Battalion/LabBattalion
        public readonly List<MonsterAgent> Produced = new List<MonsterAgent>();
    }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard):
    /// EVERY Factory's own queue used to be ONE shared `List&lt;QueueItem&gt;`
    /// draining into whichever own Complete Factory `FindAnyOwnCompleteFactory`
    /// picked -- explicitly flagged in several places as a scope
    /// simplification ("this project has no per-Factory queue model
    /// yet"). This class is that model: one per Factory (keyed by
    /// `EntityId` in <see cref="_factoryQueues"/>), each with its own
    /// timer, so two Factories can each be mid-build on something
    /// different at once. `QueueItem`'s own shape is unchanged -- only
    /// its container moved.</summary>
    private sealed class FactoryQueue
    {
        public readonly List<QueueItem> Items = new List<QueueItem>();
        public float ProductionTimer;
    }

    private readonly Dictionary<uint, FactoryQueue> _factoryQueues = new Dictionary<uint, FactoryQueue>();

    /// <summary>Looks up (and, if `createIfMissing`, lazily creates) the
    /// `FactoryQueue` for `factory`. Every queue-mutating call site uses
    /// `createIfMissing: true`; every read-only call site (HUD draws,
    /// selection reads) uses `false` so merely looking at an idle
    /// Factory's queue doesn't leave a permanent empty dictionary entry
    /// behind for every Factory the player has ever glanced at.</summary>
    private FactoryQueue FactoryQueueFor(SimBuilding factory, bool createIfMissing)
    {
        if (factory == null) return null;
        if (_factoryQueues.TryGetValue(factory.EntityId, out var q)) return q;
        if (!createIfMissing) return null;
        q = new FactoryQueue();
        _factoryQueues[factory.EntityId] = q;
        return q;
    }

    /// <summary>Read-only view for <see cref="ProductionQueueHud"/>'s
    /// always-visible bottom-right tile row -- one entry per queued item
    /// on `factory`'s OWN queue, in build order, with a 0..1 progress
    /// fraction (only meaningful, and only ever nonzero, for the FRONT
    /// item -- everything behind it hasn't started).
    ///
    /// 2026-08 (creator direction: "When Building a battalion it should
    /// show a image of the monster being built, and the battalion name
    /// underneath... use the portrait created in the lab. Export that
    /// with the monster"): `PortraitPng` is the SAME base64 PNG data URL
    /// <see cref="StoredGenomeDto.PortraitPng"/> already carries down
    /// from mutator-service (the Lab's own real WebGL-rendered
    /// thumbnail, not a re-derived icon) -- null when the underlying
    /// genome has none (an old genome saved before portraits existed, or
    /// a failed client-side bake), which <see cref="ProductionQueueHud"/>
    /// falls back to its old text-abbreviation tile for, same
    /// "optional field, never a hard error" contract every other layer
    /// of this pipeline already follows. A Battalion/LabBattalion item
    /// shows its FIRST still-remaining member's portrait as
    /// representative of the whole group -- there's no single "the"
    /// image for a mixed battalion, and the first member is a stable,
    /// deterministic choice (not e.g. "whichever happens to build
    /// next," which would flicker the tile's own image as the queue
    /// drains).</summary>
    public IEnumerable<(string Label, int Remaining, float Progress, string PortraitPng)> ProductionQueueFor(SimBuilding factory)
    {
        var q = FactoryQueueFor(factory, false);
        if (q == null) yield break;
        for (var i = 0; i < q.Items.Count; i++)
        {
            var item = q.Items[i];
            var remaining = RemainingOf(item);
            string portraitPng;
            if (item.Kind == QueueItemKind.SingleUnit)
            {
                portraitPng = item.SingleGenome?.PortraitPng;
            }
            else if (item.Kind == QueueItemKind.Battalion)
            {
                portraitPng = FirstPortrait(item.BattalionRemaining, m => m.Genome?.PortraitPng);
            }
            else
            {
                portraitPng = FirstPortrait(item.LabBattalionRemaining, g => g?.PortraitPng);
            }
            var progress = i == 0 ? Mathf.Clamp01(q.ProductionTimer / Mathf.Max(0.01f, productionSecondsPerUnit)) : 0f;
            yield return (item.Label, remaining, progress, portraitPng);
        }
    }

    /// <summary>Convenience overload preserving every pre-existing call
    /// site (`ProductionQueueHud`'s always-visible tile row, <see
    /// cref="RoofPortraitHologram"/>) unchanged -- both were built
    /// against the single-shared-queue model and read "the" queue with
    /// no Factory argument; both stay anchored on <see
    /// cref="FindAnyOwnCompleteFactory"/> for now rather than becoming
    /// multi-instance in this pass (a deliberately contained scope
    /// choice -- see docs/12's write-up of this feature).</summary>
    public IEnumerable<(string Label, int Remaining, float Progress, string PortraitPng)> ProductionQueue
    {
        get { return ProductionQueueFor(FindAnyOwnCompleteFactory()); }
    }

    /// <summary>First remaining member (in queue order) that actually
    /// has a portrait -- see <see cref="ProductionQueueFor"/>'s own doc
    /// for why index 0 alone isn't good enough for a multi-member item.
    /// Null if none of the remaining members have one, same "optional
    /// field, never a hard error" fallback every other portrait consumer
    /// in this pipeline already uses.</summary>
    private static string FirstPortrait<T>(List<T> members, System.Func<T, string> portraitOf)
    {
        foreach (var m in members)
        {
            var p = portraitOf(m);
            if (!string.IsNullOrEmpty(p)) return p;
        }
        return null;
    }

    /// <summary>True if `FindAnyOwnCompleteFactory`'s own queue (the
    /// same Factory <see cref="ProductionQueue"/>/<see
    /// cref="RoofPortraitHologram"/> already anchor on) has anything in
    /// it -- see <see cref="ProductionQueue"/>'s own doc comment for why
    /// this stays anchored on "any" Factory rather than becoming a
    /// player-wide OR-across-every-Factory check.</summary>
    public bool HasQueuedProduction
    {
        get
        {
            var q = FactoryQueueFor(FindAnyOwnCompleteFactory(), false);
            return q != null && q.Items.Count > 0;
        }
    }

    /// <summary>2026-08: "click on the factory and can abort all
    /// builds" -- clears EVERY Factory's queue outright (the button's
    /// own label is "Cancel All," not "cancel this one Factory," and
    /// that reading predates per-Factory queues existing at all -- kept
    /// literal rather than narrowed now that there's a more specific
    /// option available). Nothing to refund: Blood is only ever spent
    /// the instant a clone actually pops out (see <see
    /// cref="TickProduction"/>), never reserved up front when an item is
    /// queued, so there's nothing owed back for work that never
    /// happened.</summary>
    public void CancelAllProduction()
    {
        _factoryQueues.Clear();
    }

    /// <summary>Queues `count` more clones of `genome` at `factory` --
    /// stacks onto an existing queued run of the SAME genome (by `Id`)
    /// anywhere in THAT Factory's own queue instead of adding a second
    /// entry, so "queued icons... one per type" holds even across repeat
    /// drops. `radius` is captured NOW (from whichever live agent is
    /// being dropped/queued), not re-derived from the genome later --
    /// see <see cref="QueueItem"/>'s own doc for why. Returns the
    /// touched/created item so a direct Factory-body drop can promote it
    /// to the front (see <see cref="PromoteToFront"/>) -- a clipboard
    /// drop just leaves the return value unused.</summary>
    private QueueItem QueueSingleUnit(SimBuilding factory, StoredGenomeDto genome, float radius, int count)
    {
        if (genome == null || count <= 0) return null;
        var q = FactoryQueueFor(factory, true);
        foreach (var item in q.Items)
        {
            if (item.Kind != QueueItemKind.SingleUnit || item.SingleGenome.Id != genome.Id) continue;
            item.RemainingCount += count;
            return item;
        }
        var created = new QueueItem
        {
            Kind = QueueItemKind.SingleUnit,
            SingleGenome = genome,
            SingleRadius = radius,
            RemainingCount = count,
            Label = genome.Id,
        };
        q.Items.Add(created);
        return created;
    }

    /// <summary>2026-08 (creator direction, verbatim: "If an order is
    /// dropped onto the Factory itself, it is an immediate command...
    /// [it] is interrupted/kicked out according to existing rules... [is]
    /// parked nearby / otherwise preserved... [the new order] becomes
    /// current build"): moves `item` to index 0 of `factory`'s own queue
    /// -- whatever WAS at index 0 simply shifts to index 1, its
    /// `RemainingCount`/`BattalionRemaining` completely untouched, so
    /// "kick the current build" is a plain reorder, never a deletion.
    /// This is the ONLY thing that distinguishes a direct Factory-body
    /// drop from a clipboard drop -- both call the exact same Queue*
    /// method first; only a Factory-body drop calls this afterward. A
    /// no-op if `item` is null (nothing was actually queued -- an empty
    /// battalion, a null genome) or already at the front.</summary>
    private void PromoteToFront(SimBuilding factory, QueueItem item)
    {
        if (item == null) return;
        var q = FactoryQueueFor(factory, false);
        if (q == null) return;
        var idx = q.Items.IndexOf(item);
        if (idx <= 0) return;
        q.Items.RemoveAt(idx);
        q.Items.Insert(0, item);
        // a promoted SingleUnit item's timer would otherwise keep
        // whatever fraction of a clone-interval the DISPLACED item had
        // already banked -- reset it so the newly promoted order starts
        // its own first clone from a clean interval, not a stolen partial
        // one.
        q.ProductionTimer = 0f;
    }

    /// <summary>One production tick per frame -- advances EVERY owned
    /// Factory's own queue independently (2026-08: was a single shared
    /// queue/timer; see <see cref="FactoryQueue"/>). Each Factory with a
    /// non-empty queue gets its own timer advanced and, once it crosses
    /// `productionSecondsPerUnit`, spawns exactly one clone at THAT
    /// Factory (same spend/park mechanics <see cref="CloneOnto"/> always
    /// used, just one at a time instead of a tight while-loop) and hands
    /// it the same settle-creep walk-away destination -- staggering real
    /// spawns over real time is what gives "the monsters line up at the
    /// cloning door... and one at a time walk get cloned" its read, with
    /// no separate queueing-animation state needed. Stalls a given
    /// Factory's own timer (leaves it at its current value, retries next
    /// frame) rather than dropping its front item if that Factory is no
    /// longer Complete (destroyed mid-queue -- "do not lose the order"),
    /// there's no parking room this instant, or insufficient Blood --
    /// production simply waits for whichever condition is blocking it to
    /// clear, per-Factory, independently of every other Factory's own
    /// queue.
    ///
    /// No per-frame allocation: iterates the existing `_factoryQueues`
    /// dictionary directly (mutating each `FactoryQueue`'s OWN fields is
    /// safe mid-enumeration; only adding/removing DICTIONARY ENTRIES
    /// during enumeration isn't, so an emptied-out queue is simply left
    /// as an inert dictionary entry rather than pruned -- bounded by
    /// however many distinct Factories the player has ever queued
    /// something at, never unbounded, not worth the extra bookkeeping to
    /// avoid).</summary>
    private void TickProduction(float dt)
    {
        foreach (var kv in _factoryQueues)
        {
            var factory = FindOwnFactoryById(kv.Key);
            var q = kv.Value;
            if (q.Items.Count == 0) { q.ProductionTimer = 0f; continue; }
            if (factory == null || factory.State != BuildingState.Complete) continue;   // destroyed/invalid -- stall, don't lose the order

            q.ProductionTimer += dt;
            if (q.ProductionTimer < productionSecondsPerUnit) continue;

            var item = q.Items[0];
            StoredGenomeDto genome;
            float radius;
            if (item.Kind == QueueItemKind.SingleUnit) { genome = item.SingleGenome; radius = item.SingleRadius; }
            else if (item.Kind == QueueItemKind.Battalion) { var next = item.BattalionRemaining[0]; genome = next.Genome; radius = next.Radius; }
            else { genome = item.LabBattalionRemaining[0]; radius = DefaultParkSearchRadius; }

            var parkSpot = FindOpenHexNear(factory.Hex, new HashSet<HexCoord>(), radius);
            if (parkSpot == null) continue;   // no room this instant -- try again next frame
            if (!builder.TrySpendBlood(cloneCostBlood)) continue;   // can't afford yet -- try again next frame

            q.ProductionTimer = 0f;
            var clone = builder.SpawnMonster(genome, factory.Hex);
            clone.SetSettleTarget(builder.WorldOf(parkSpot.Value));

            if (item.Kind == QueueItemKind.SingleUnit)
            {
                item.RemainingCount--;
                if (item.RemainingCount <= 0)
                {
                    q.Items.RemoveAt(0);
                    EvictRoofOccupantIfDone(item.SingleGenome.Id);
                }
            }
            else if (item.Kind == QueueItemKind.Battalion)
            {
                item.Produced.Add(clone);
                item.BattalionRemaining.RemoveAt(0);
                if (item.BattalionRemaining.Count == 0)
                {
                    if (commander != null) commander.FormBattalionFromProduction(item.Produced);
                    q.Items.RemoveAt(0);
                }
            }
            else
            {
                item.Produced.Add(clone);
                item.LabBattalionRemaining.RemoveAt(0);
                if (item.LabBattalionRemaining.Count == 0)
                {
                    if (commander != null) commander.FormBattalionFromProduction(item.Produced);
                    q.Items.RemoveAt(0);
                }
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
    /// city center -- the fallback anchor for every HUD element that
    /// predates per-Factory queues and still shows "a" Factory's state
    /// rather than a player-chosen one (<see cref="ProductionQueue"/>,
    /// <see cref="HasQueuedProduction"/>, <see cref="RoofPortraitHologram"/>,
    /// the existing "Build" buttons on <see cref="BattalionHud"/>/<see
    /// cref="LabBattalionHud"/>). Null if the player has none yet.</summary>
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

    // ---- carrying an ABSTRACT order (2026-08, Battalion drag from the
    // bottom-left menu) --------------------------------------------------

    private enum CarriedOrderKind { None, Battalion, LabBattalion }
    private CarriedOrderKind _carriedOrderKind = CarriedOrderKind.None;
    private IReadOnlyList<MonsterAgent> _carriedBattalionMembers;
    private string _carriedLabBattalionName;
    private string[] _carriedLabBattalionCreatureIds;

    /// <summary>Live label for the cursor-following badge <see
    /// cref="ProductionQueueHud"/> draws while carrying an abstract
    /// order -- mirrors the Label text a queued item would show, so the
    /// carry preview and the eventual queue tile read as the same
    /// thing.</summary>
    public string CarriedOrderLabel
    {
        get
        {
            if (_carriedOrderKind == CarriedOrderKind.Battalion) return "Battalion";
            if (_carriedOrderKind == CarriedOrderKind.LabBattalion) return _carriedLabBattalionName;
            return null;
        }
    }

    /// <summary>How many members this carried order will actually queue
    /// -- live battalion member count, or a Lab template's own
    /// `CreatureIds` length -- so <see cref="ProductionQueueHud"/>'s
    /// cursor-following badge can show a cost the same way <see
    /// cref="PendingBuildCount"/>'s badge already does for a carried
    /// monster ("Build orders and cost outline also apply to
    /// battalions").</summary>
    public int CarriedOrderCount
    {
        get
        {
            if (_carriedOrderKind == CarriedOrderKind.Battalion) return _carriedBattalionMembers != null ? _carriedBattalionMembers.Count : 0;
            if (_carriedOrderKind == CarriedOrderKind.LabBattalion) return _carriedLabBattalionCreatureIds != null ? _carriedLabBattalionCreatureIds.Length : 0;
            return 0;
        }
    }

    public bool IsCarryingOrder { get { return _mode == Mode.Carrying && _carriedOrderKind != CarriedOrderKind.None; } }

    /// <summary>2026-08 (creator direction: "Grab a Battalion Group from
    /// the existing bottom-left menu. Drag it toward a Factory"): called
    /// by <see cref="BattalionHud"/>/<see cref="LabBattalionHud"/> on a
    /// mouse-down over one of their own rows while Grab Mode is Armed --
    /// enters `Mode.Carrying` exactly like picking up a live monster
    /// does (same state machine, same left-click-to-drop flow), just
    /// with no physical transform to move. The row's own existing data
    /// (`commander.BattalionMembers(slot)` / a Lab template's
    /// `CreatureIds`) is handed in directly -- the SAME representation
    /// the existing "Build" buttons already pass to <see
    /// cref="BuildBattalionAtOwnFactory"/>/<see cref="BuildLabBattalion"/>,
    /// so a dragged Battalion is never a second, parallel Battalion
    /// object. No-op if a monster or another order is already being
    /// carried, or Grab Mode isn't Armed.</summary>
    public void BeginCarryingOrder(IReadOnlyList<MonsterAgent> battalionMembers)
    {
        if (_mode != Mode.Armed || battalionMembers == null || battalionMembers.Count == 0) return;
        _carriedOrderKind = CarriedOrderKind.Battalion;
        _carriedBattalionMembers = battalionMembers;
        _mode = Mode.Carrying;
    }

    /// <summary>Same as <see cref="BeginCarryingOrder(IReadOnlyList{MonsterAgent})"/>
    /// for a Lab-template battalion row.</summary>
    public void BeginCarryingLabOrder(string name, string[] creatureIds)
    {
        if (_mode != Mode.Armed || string.IsNullOrEmpty(name) || creatureIds == null || creatureIds.Length == 0) return;
        _carriedOrderKind = CarriedOrderKind.LabBattalion;
        _carriedLabBattalionName = name;
        _carriedLabBattalionCreatureIds = creatureIds;
        _mode = Mode.Carrying;
    }

    private void EndCarryingOrder()
    {
        _carriedOrderKind = CarriedOrderKind.None;
        _carriedBattalionMembers = null;
        _carriedLabBattalionName = null;
        _carriedLabBattalionCreatureIds = null;
    }

    // ---- drag-target resolution + visual feedback (2026-08, Factory
    // Build Queue / Order Clipboard) --------------------------------------

    public enum DropTarget { None, FactoryBody, Clipboard }

    /// <summary>2026-08 (creator direction: "Factory target highlights as
    /// an immediate-build target. Clipboard highlights as a queue
    /// target"): the ONE targeting rule both the physical-monster carry
    /// and the abstract-order carry share. A precise raycast hit on the
    /// small <see cref="FactoryClipboard"/> collider wins first --
    /// "deliberately drops... onto the clipboard" reads as a smaller,
    /// more precise target than the whole building, so it has to
    /// actually be hit, not just be in the general vicinity. Otherwise
    /// falls back to the EXISTING, unchanged, forgiving hex-proximity
    /// check (<see cref="FindOwnFactoryNear"/>, `dropRangeHexes` rings)
    /// already used for every monster drop before this feature existed
    /// -- so a near-miss of the clipboard, or anywhere else on/near the
    /// Factory, still reads as "build this now," preserving the original
    /// UX exactly for anyone who never aims for the clipboard at
    /// all.</summary>
    private (DropTarget Target, SimBuilding Factory, Vector3 ClipboardWorldPos) ResolveDropTarget(Vector3? groundPoint, Camera cam, Mouse mouse)
    {
        var hit = RaycastCursor(cam, mouse);
        if (hit != null)
        {
            var clip = hit.Value.collider.GetComponentInParent<FactoryClipboard>();
            if (clip != null)
            {
                var cf = FindOwnFactoryById(clip.FactoryEntityId);
                if (cf != null) return (DropTarget.Clipboard, cf, hit.Value.collider.transform.position);
            }
        }
        if (groundPoint.HasValue)
        {
            var hex = builder.HexAt(groundPoint.Value);
            var bf = FindOwnFactoryNear(hex);
            if (bf != null) return (DropTarget.FactoryBody, bf, Vector3.zero);
        }
        return (DropTarget.None, null, Vector3.zero);
    }

    private GameObject _dragFactoryHighlight;
    private GameObject _dragClipboardHighlight;

    /// <summary>2026-08 ("the feedback should be lightweight and
    /// performant... follow the existing game's visual language"):
    /// toggles/repositions two lazily-built glow props (same
    /// `SlowSpin` + transparent-emissive-cylinder recipe <see
    /// cref="EnsureSelectionHighlight"/> already established for the
    /// factory-selection ring) from the ALREADY-computed drag-target
    /// result -- no new geometry created per frame, just SetActive +
    /// reposition on whichever of the two props is relevant this frame.
    /// Green under the Factory body ("build this now"), amber on the
    /// clipboard itself ("add to queue") -- distinct from the existing
    /// factory-SELECTION ring's own amber (a different, non-drag state
    /// that can be live on a different Factory at the same time), so the
    /// two never fight over one shared prop.</summary>
    private void UpdateDragHighlights(DropTarget target, SimBuilding factory, Vector3 clipboardWorldPos)
    {
        if (_dragFactoryHighlight == null && target == DropTarget.FactoryBody)
            _dragFactoryHighlight = BuildDragHighlight(new Color(0.35f, 1f, 0.4f), 6f);
        if (_dragClipboardHighlight == null && target == DropTarget.Clipboard)
            _dragClipboardHighlight = BuildDragHighlight(new Color(1f, 0.7f, 0.15f), 2.2f);

        if (_dragFactoryHighlight != null)
        {
            var show = target == DropTarget.FactoryBody;
            _dragFactoryHighlight.SetActive(show);
            if (show)
            {
                var world = builder.WorldOf(factory.Hex);
                world.y = builder.GroundHeightAt(world) + 0.05f;
                _dragFactoryHighlight.transform.position = world;
            }
        }
        if (_dragClipboardHighlight != null)
        {
            var show = target == DropTarget.Clipboard;
            _dragClipboardHighlight.SetActive(show);
            if (show) _dragClipboardHighlight.transform.position = clipboardWorldPos;
        }
    }

    private void HideDragHighlights()
    {
        if (_dragFactoryHighlight != null) _dragFactoryHighlight.SetActive(false);
        if (_dragClipboardHighlight != null) _dragClipboardHighlight.SetActive(false);
    }

    private GameObject BuildDragHighlight(Color glowColor, float diameter)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "DragTargetHighlight";
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        go.transform.localScale = new Vector3(diameter, 0.02f, diameter);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.55f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glowColor * 1.7f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        go.SetActive(false);
        return go;
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
        // BuildMenuHud/BuildingNavHud/SelectionHud/FactoryOrdersHud.
        if (Minimap.PointerOver || BuildingNavHud.PointerOver || SelectionHud.PointerOver || FactoryOrdersHud.PointerOver) return;

        if (_mode == Mode.Armed)
        {
            if (mouse.leftButton.wasPressedThisFrame) TryPickUp(cam, mouse);
            if (_selectedFactory != null) TickFactorySelectionKeys(keyboard);
            return;
        }

        // Carrying an ABSTRACT ORDER (Battalion dragged off the bottom-left menu)
        if (_carriedOrderKind != CarriedOrderKind.None)
        {
            var orderGroundPoint = GroundUnderCursor(cam, mouse);
            var (target, factory, clipPos) = ResolveDropTarget(orderGroundPoint, cam, mouse);
            UpdateDragHighlights(target, factory, clipPos);
            if (mouse.leftButton.wasPressedThisFrame) DropCarriedOrder(target, factory);
            return;
        }

        // Carrying a live monster
        if (_carried == null) { _mode = Mode.Armed; return; }   // held monster died/was destroyed mid-carry

        // 2026-08 (creator direction: "if the user presses the space bar
        // a number on the grab increments, denoting the number of that
        // monster you want to build"): see _pendingBuildCount's own
        // comment -- consumed once by CloneOnto at the moment of drop.
        if (keyboard.spaceKey.wasPressedThisFrame)
            _pendingBuildCount = Mathf.Min(_pendingBuildCount + 1, maxClonesPerDrop);

        var groundPoint = GroundUnderCursor(cam, mouse);

        // 2026-08 (creator direction, verbatim: "if a monster is grabbed
        // from the roof of a factory, any movement beyond the factory
        // bounds should result in a snap to a clear ground position
        // close" -- see SnapCarriedOffRoofBounds's own doc comment):
        // only for a monster that came off ITS OWN Factory's roof slot,
        // and only once the cursor's own hex is no longer within that
        // SAME Factory's drop range -- the exact "still at/near this
        // Factory" test Drop/HoverTargetFor already use, applied here to
        // decide when the carry itself gets cut short instead of when a
        // drop counts as a clone.
        if (_carriedFromRoofFactory != null && groundPoint.HasValue)
        {
            var hex = builder.HexAt(groundPoint.Value);
            if (FindOwnFactoryNear(hex) != _carriedFromRoofFactory)
            {
                SnapCarriedOffRoofBounds();
                return;
            }
        }

        if (groundPoint.HasValue) _carried.TickHeld(HoverTargetFor(groundPoint.Value), Time.deltaTime);

        // 2026-08 (drag-target highlights): a live monster carry gets
        // the exact same green/amber feedback an abstract order does --
        // one shared targeting rule, one shared highlight pair. Resolved
        // ONCE per frame and reused for both the highlight AND (below)
        // the actual drop -- Drop() used to blindly re-resolve with a
        // null camera/mouse, which silently disabled clipboard detection
        // entirely; passing the already-resolved target through avoids
        // both the bug and a second raycast.
        var (monsterTarget, monsterFactory, monsterClipPos) = ResolveDropTarget(groundPoint, cam, mouse);
        UpdateDragHighlights(monsterTarget, monsterFactory, monsterClipPos);

        if (mouse.leftButton.wasPressedThisFrame) Drop(monsterTarget, monsterFactory);
    }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard): the
    /// abstract-order counterpart to <see cref="Drop"/> -- same
    /// direct-vs-clipboard branching (see <see
    /// cref="ResolveDropTarget"/>), just queueing a Battalion/LabBattalion
    /// instead of moving a physical `MonsterAgent`. Dropping outside any
    /// valid target (`DropTarget.None`) simply cancels the carry --
    /// nothing was ever "picked up" physically, so there's nothing to
    /// return to the world, matching "if there's nothing under the
    /// cursor, the order just isn't placed" rather than inventing a drop
    /// location for an object that has no independent existence outside
    /// the queue.</summary>
    private void DropCarriedOrder(DropTarget target, SimBuilding factory)
    {
        var kind = _carriedOrderKind;
        var members = _carriedBattalionMembers;
        var labName = _carriedLabBattalionName;
        var labIds = _carriedLabBattalionCreatureIds;
        EndCarryingOrder();
        _mode = Mode.Armed;
        HideDragHighlights();

        if (target == DropTarget.None || factory == null) return;

        QueueItem item = null;
        if (kind == CarriedOrderKind.Battalion) item = QueueBattalionAt(factory, members);
        else if (kind == CarriedOrderKind.LabBattalion) item = QueueLabBattalionAt(factory, labName, labIds);

        if (target == DropTarget.FactoryBody) PromoteToFront(factory, item);
    }

    /// <summary>2026-08 (creator direction, verbatim: "if a monster is
    /// grabbed from the roof of a factory, any movement beyond the
    /// factory bounds should result in a snap to a clear ground position
    /// close but also avoid building Mesh collisions or collisions with
    /// other monsters. use the push function of navigation to find a
    /// appropriate parking spot"): ends the carry immediately (same
    /// "gone the instant the drag interrupts it" contract as any other
    /// pickup that's about to be re-anchored -- a roof specimen isn't
    /// meant to be freely flown anywhere on the map the way a normal
    /// ground pickup is, only re-homed near the building it came off of)
    /// and teleports straight to a validated nearby hex rather than the
    /// walking creep <see cref="MonsterAgent.BootFromRoof"/>'s own roof-
    /// eviction uses -- unlike that automatic bump, this interrupts a
    /// carry already hovering mid-air with the cursor, so an instant
    /// reposition ("snap," read literally) reads far more natural than a
    /// creature that was just floating suddenly turning to walk from
    /// wherever the cursor happened to be yanked to.
    ///
    /// Reuses <see cref="FindOpenHexNear"/> verbatim -- the SAME ring-
    /// search "push outward from the Factory until a clear hex turns up"
    /// this project already relies on for clone placement and roof-
    /// eviction parking, so building-footprint clearance (its own
    /// `InsideBuildingFootprint` check) needs no new logic here. It only
    /// checks its own `claimed` set for overlap, though, which normally
    /// tracks one placement BATCH, not the whole battlefield -- so
    /// `NearbyMonsterHexes` pre-seeds that set with every OTHER live
    /// monster's current hex near the Factory, extending the exact same
    /// clearance math (`bodyRadius * 2 + groupSpacing`) to already-
    /// standing monsters instead of just monsters being placed together
    /// right now.</summary>
    private void SnapCarriedOffRoofBounds()
    {
        var agent = _carried;
        var factory = _carriedFromRoofFactory;
        _carried = null;
        _carriedFromRoofFactory = null;
        _mode = Mode.Armed;
        HideDragHighlights();
        if (agent == null) return;

        agent.EndHeld();
        if (factory == null) return;   // defensive only -- shouldn't happen, see this field's own doc comment

        var claimed = NearbyMonsterHexes(factory.Hex, agent);
        var parkHex = FindOpenHexNear(factory.Hex, claimed, agent.Radius);
        if (parkHex.HasValue) agent.TeleportTo(builder.WorldOf(parkHex.Value));
        // no open hex found within the ring search -- leave it wherever
        // EndHeld already put it (the last hover point before the bounds
        // check fired) rather than teleporting into a spot never actually
        // validated.
    }

    /// <summary>Every OTHER live monster's current hex within
    /// <see cref="FindOpenHexNear"/>'s own 8-ring search radius of
    /// `center` -- pre-seeds that method's `claimed` parameter so its
    /// existing per-claimed-hex clearance check (`bodyRadius * 2 +
    /// groupSpacing`) also keeps a parking spot clear of monsters that
    /// were already standing there, not just other hexes being claimed
    /// in the same placement batch.</summary>
    private HashSet<HexCoord> NearbyMonsterHexes(HexCoord center, MonsterAgent exclude)
    {
        var occupied = new HashSet<HexCoord>();
        foreach (var m in builder.Monsters)
        {
            if (m == null || m == exclude || m.IsHeld) continue;
            var hex = builder.HexAt(m.transform.position);
            if (hex.DistanceTo(center) <= 8) occupied.Add(hex);
        }
        return occupied;
    }

    /// <summary>Shared roof-eviction parking search -- reused by both
    /// `Drop`'s "a fresh drop bumps the current roof occupant" gesture
    /// and <see cref="EvictRoofOccupantIfDone"/>'s "the batch this
    /// specimen was displaying finished" one below. Same
    /// <see cref="FindOpenHexNear"/>/<see cref="NearbyMonsterHexes"/>
    /// pair the off-bounds carry snap already uses, so every way a
    /// monster ends up parked near a Factory shares one clearance
    /// standard (buildings AND other live monsters) instead of the
    /// building-only check this used to run inline.</summary>
    private void EvictRoofOccupant(SimBuilding factory, MonsterAgent occupant)
    {
        if (factory == null || occupant == null) return;
        var claimed = NearbyMonsterHexes(factory.Hex, occupant);
        var bootSpot = FindOpenHexNear(factory.Hex, claimed, occupant.Radius);
        if (bootSpot != null) occupant.BootFromRoof(builder.WorldOf(bootSpot.Value));
    }

    /// <summary>2026-08 (creator direction, verbatim: "Once the factory
    /// has built X number of units the monster is kicked out of the
    /// factory and parked nearby to continue monstering"): called the
    /// instant a SingleUnit queue item's `RemainingCount` reaches 0 and
    /// gets removed -- finds whichever Factory currently has a roof
    /// occupant sharing THAT genome (`_roofOccupant` has no direct link
    /// to a queue item, so this is a small linear scan over however many
    /// Factories the player owns, not a hot path) and evicts it exactly
    /// like a fresh drop would. Only ever fires for a genuinely FINISHED
    /// batch -- manually cancelling a build early via <see
    /// cref="TickFactorySelectionKeys"/>'s `-` key, or <see
    /// cref="CancelQueueItem"/> from the order popup, does NOT reach
    /// this, deliberately (see those methods' own doc comments).</summary>
    private void EvictRoofOccupantIfDone(string genomeId)
    {
        if (string.IsNullOrEmpty(genomeId) || bridge == null) return;
        uint? key = null;
        MonsterAgent occupant = null;
        foreach (var kv in _roofOccupant)
        {
            if (kv.Value == null || kv.Value.Creature == null || kv.Value.Creature.Id != genomeId) continue;
            key = kv.Key;
            occupant = kv.Value;
            break;
        }
        if (key == null) return;
        _roofOccupant.Remove(key.Value);

        var factory = FindOwnFactoryById(key.Value);
        EvictRoofOccupant(factory, occupant);
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
        // 2026-08 bugfix (creator report: "still can not see the
        // platform" -- traced to BaseDresser.RoofHeightFor's own
        // faction-blind overload returning the FULL massing envelope,
        // not the actual rendered body height any Factory variant caps
        // at -- see that method's own doc comment). The faction-aware
        // overload needs to know which faction actually built this
        // Factory to pick the right fraction.
        var faction = bridge != null ? bridge.PlayerFaction(factory.PlayerIndex) : FactionId.Mixed;
        roofWorld.y = builder.GroundHeightAt(roofWorld) + BaseDresser.RoofHeightFor(factory.Kind, faction);
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
        _carriedFromRoofFactory = null;
        EndCarryingOrder();
        HideDragHighlights();
        SelectFactory(null);   // grab mode fully exiting clears every grab-mode-scoped selection, same as the carry above
        _mode = Mode.Off;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>2026-08 (creator direction: "the user may also click on
    /// the factory highlighting it and press space or + or - keys to
    /// increase or decrease the number of monsters to build"): a left-
    /// click while Armed that DOESN'T hit a monster now also checks for
    /// one of the player's own Complete Factories (via <see
    /// cref="BuildingIdentity"/>) and selects it -- clicking empty space,
    /// an enemy building, or a non-Factory building of the player's own
    /// all deselect, same "click elsewhere to clear selection" contract
    /// as everything else that can be selected in this project.
    ///
    /// 2026-08 follow-up (Factory Build Queue / Order Clipboard): a
    /// click that hits the Factory's own <see cref="FactoryClipboard"/>
    /// prop instead opens the order popup (<see
    /// cref="FactoryOrdersHud"/>) rather than the lightweight quick-
    /// select highlight -- checked BEFORE the plain factory-body case
    /// so the smaller, more specific target wins.</summary>
    private void TryPickUp(Camera cam, Mouse mouse)
    {
        var hit = RaycastCursor(cam, mouse);
        if (hit == null) { SelectFactory(null); return; }

        var agent = hit.Value.collider.GetComponentInParent<MonsterAgent>();
        if (agent != null)
        {
            if (agent.IsHeld) return;   // already being carried somehow -- defensive no-op, leaves selection as-is
            _carriedFromRoofFactory = FindRoofOccupantFactory(agent);
            RemoveFromRoofOccupancy(agent);
            agent.BeginHeld();
            _carried = agent;
            _mode = Mode.Carrying;
            _pendingBuildCount = 1;
            return;
        }

        var clipboard = hit.Value.collider.GetComponentInParent<FactoryClipboard>();
        if (clipboard != null)
        {
            var cf = FindOwnFactoryById(clipboard.FactoryEntityId);
            if (cf != null) OpenOrdersPopup?.Invoke(cf);
            return;
        }

        var building = hit.Value.collider.GetComponentInParent<BuildingIdentity>();
        SelectFactory(building != null ? FindOwnFactoryById(building.EntityId) : null);
    }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard): fired by
    /// `TryPickUp` when the clipboard collider is clicked -- <see
    /// cref="FactoryOrdersHud"/> subscribes to open its popup on
    /// whichever Factory was hit. A plain event (not a direct method
    /// call/reference) so `GrabCursor` doesn't need to know that
    /// `FactoryOrdersHud` exists, matching this project's existing
    /// "HUDs read the data-owning script's public state; the data-owning
    /// script doesn't reach back into a specific HUD" direction (see how
    /// `ProductionQueueHud` already only ever reads FROM `GrabCursor`,
    /// never the other way).</summary>
    public event System.Action<SimBuilding> OpenOrdersPopup;

    /// <summary>`target`/`factory` are the SAME resolution `Update()`
    /// already computed this frame for the drag highlight (<see
    /// cref="ResolveDropTarget"/>) -- passed straight through instead of
    /// re-raycasting a second time (or, as an earlier draft of this
    /// method did, re-resolving with a null camera, which silently
    /// disabled clipboard detection since <see cref="RaycastCursor"/>
    /// no-ops on a null Camera).</summary>
    private void Drop(DropTarget target, SimBuilding factory)
    {
        var agent = _carried;
        _carried = null;
        _carriedFromRoofFactory = null;
        _mode = Mode.Armed;   // stays armed -- pick up the next one right away
        HideDragHighlights();
        if (agent == null) return;

        if (factory != null)
        {
            var item = CloneOnto(factory, agent);

            // creator direction: "when a new monster is dropped on a
            // factory, the current monster is booted to the next
            // parking spot closest to the factory and the new monster
            // replaces the old one on the factory roof" -- whoever
            // already holds this Factory's roof slot (if anyone, and
            // if it isn't this same agent being re-dropped on its own
            // spot) steps aside to the nearest open hex before the new
            // arrival takes the roof (see EvictRoofOccupant's own doc
            // comment for the shared parking search). Fires on EITHER
            // target (clipboard or body) -- the roof DISPLAY specimen
            // and the queue's own front-of-line are two different
            // things (see PromoteToFront's own doc comment); only the
            // active-build promotion below is Factory-body-only.
            if (_roofOccupant.TryGetValue(factory.EntityId, out var evicted) && evicted != null && evicted != agent)
                EvictRoofOccupant(factory, evicted);

            // creator direction: "it should land on the roof and
            // rotate slowly in the Y axis" -- the ORIGINAL creature
            // (not consumed by cloning) settles on top of the Factory
            // it was just dropped on, instead of hovering wherever
            // the cursor happened to be.
            var roofWorld = builder.WorldOf(factory.Hex);
            var faction = bridge != null ? bridge.PlayerFaction(factory.PlayerIndex) : FactionId.Mixed;
            roofWorld.y = builder.GroundHeightAt(roofWorld) + BaseDresser.RoofHeightFor(factory.Kind, faction);
            agent.BeginRoofDisplay(roofWorld);
            _roofOccupant[factory.EntityId] = agent;

            // 2026-08 (Factory Build Queue / Order Clipboard, creator
            // direction: "If an order is dropped onto the Factory
            // itself, it is an immediate command"): only a Factory-BODY
            // drop promotes to the active build -- a clipboard drop
            // stays exactly the pre-existing "append/stack" behavior.
            if (target == DropTarget.FactoryBody) PromoteToFront(factory, item);
            return;
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

    /// <summary>2026-08 (roof-bounds snap): which Factory, if any, `agent`
    /// was currently occupying the roof of -- called from `TryPickUp`
    /// BEFORE `RemoveFromRoofOccupancy` clears the entry, so the bounds
    /// check in `Update()` knows which Factory's own range to measure
    /// against for as long as this pickup is being carried. Null for a
    /// normal ground-standing monster, same as `_roofOccupant` simply
    /// having no entry for it.</summary>
    private SimBuilding FindRoofOccupantFactory(MonsterAgent agent)
    {
        if (bridge == null) return null;
        foreach (var kv in _roofOccupant)
        {
            if (kv.Value != agent) continue;
            return FindOwnFactoryById(kv.Key);
        }
        return null;
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

    /// <summary>Same ownership/kind/state filter <see
    /// cref="FindOwnFactoryNear"/> already applies, just resolved by
    /// `EntityId` (from a <see cref="BuildingIdentity"/>/<see
    /// cref="FactoryClipboard"/> raycast hit, or a `_roofOccupant`
    /// dictionary key) instead of hex proximity -- an enemy Factory, a
    /// different building kind, or one still under construction all
    /// resolve to null. Also used by `TickProduction`'s own per-Factory
    /// loop (`EntityId -> live SimBuilding` -- match-core's own building
    /// list never removes an entry, even a destroyed one, so this always
    /// resolves for any Factory ever queued at, just possibly with
    /// `State != Complete`).</summary>
    private SimBuilding FindOwnFactoryById(uint entityId)
    {
        if (bridge == null || !bridge.HasMatch) return null;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.EntityId != entityId) continue;
            return b.PlayerIndex == localPlayerIndex && b.Kind == BuildingKind.Factory ? b : null;
        }
        return null;
    }

    /// <summary>2026-08 ("click on the factory, highlighting it"): swaps
    /// the current selection and toggles a simple ground-level glow
    /// under whichever Factory (if any) is now selected -- built lazily
    /// once, then just repositioned/toggled on every later selection
    /// change, same "build once, toggle after" shape every other lazy
    /// prop in this project already follows (<see
    /// cref="MonsterAgent.EnsureRoofGlow"/>'s own precedent).</summary>
    private void SelectFactory(SimBuilding factory)
    {
        _selectedFactory = factory;
        if (factory == null)
        {
            if (_selectionHighlight != null) _selectionHighlight.SetActive(false);
            return;
        }
        EnsureSelectionHighlight();
        var world = builder.WorldOf(factory.Hex);
        world.y = builder.GroundHeightAt(world) + 0.05f;
        _selectionHighlight.transform.position = world;
        _selectionHighlight.SetActive(true);
    }

    private void EnsureSelectionHighlight()
    {
        if (_selectionHighlight != null) return;
        _selectionHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _selectionHighlight.name = "FactorySelectionHighlight";
        var collider = _selectionHighlight.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        _selectionHighlight.transform.localScale = new Vector3(6f, 0.02f, 6f);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        var glowColor = new Color(1f, 0.86f, 0.25f);   // warm amber -- distinct from the cool brass/steel/cyan palette every roof fixture already uses, so a selected Factory reads as a UI state, not more building dressing
        mat.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.5f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glowColor * 1.6f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = _selectionHighlight.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        _selectionHighlight.AddComponent<SlowSpin>().degreesPerSecond = 15f;
    }

    /// <summary>2026-08 (creator direction: "press space or + or - keys
    /// to increase or decrease the number of monsters to build"; follow-
    /// up: "Build orders and cost outline also apply to battalions"):
    /// mutates the FRONT queue item's own remaining-count directly (see
    /// <see cref="SelectedFactoryBuild"/>'s own doc comment for why the
    /// front item specifically). SingleUnit is a flat integer, capped at
    /// `maxClonesPerDrop` the same ceiling the carry dial respects,
    /// floored at 0 (which removes the item outright -- same "cancel the
    /// remainder" outcome <see cref="CancelAllProduction"/> gives the
    /// whole queue, just for one item). Battalion/LabBattalion have no
    /// single integer to move -- growing/shrinking a real composition
    /// means duplicating or dropping its own LAST queued member rather
    /// than inventing a new one from nothing, so `+`/`-` there
    /// duplicates/removes the tail entry of whichever list is still
    /// remaining. Decrementing to empty removes the item, same as
    /// SingleUnit -- but never auto-evicts a roof occupant the way a
    /// NATURALLY finished build does (<see
    /// cref="EvictRoofOccupantIfDone"/>): this is the player explicitly
    /// cancelling early, not the batch actually completing.</summary>
    private void TickFactorySelectionKeys(Keyboard keyboard)
    {
        var increment = keyboard.spaceKey.wasPressedThisFrame || keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame;
        var decrement = keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame;
        if (!increment && !decrement) return;
        var q = FactoryQueueFor(_selectedFactory, false);
        if (q == null || q.Items.Count == 0) return;

        AdjustQuantity(q, 0, increment);
    }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard, "each
    /// queued order should support increasing or decreasing its
    /// quantity... changes must immediately update the queue and
    /// popup"): the SAME per-item +/- logic <see
    /// cref="TickFactorySelectionKeys"/> already used for the front item
    /// only, generalized to any index -- <see cref="FactoryOrdersHud"/>'s
    /// own per-row +/- buttons call this directly instead of duplicating
    /// the SingleUnit/Battalion/LabBattalion branch a third time.</summary>
    private void AdjustQuantity(FactoryQueue q, int index, bool increment)
    {
        if (index < 0 || index >= q.Items.Count) return;
        var item = q.Items[index];
        if (item.Kind == QueueItemKind.SingleUnit)
        {
            item.RemainingCount += increment ? 1 : -1;
            item.RemainingCount = Mathf.Min(item.RemainingCount, maxClonesPerDrop);
            if (item.RemainingCount <= 0) q.Items.RemoveAt(index);
        }
        else if (item.Kind == QueueItemKind.Battalion)
        {
            var list = item.BattalionRemaining;
            if (increment) { if (list.Count > 0 && list.Count < maxClonesPerDrop) list.Add(list[list.Count - 1]); }
            else if (list.Count > 0) { list.RemoveAt(list.Count - 1); if (list.Count == 0) q.Items.RemoveAt(index); }
        }
        else
        {
            var list = item.LabBattalionRemaining;
            if (increment) { if (list.Count > 0 && list.Count < maxClonesPerDrop) list.Add(list[list.Count - 1]); }
            else if (list.Count > 0) { list.RemoveAt(list.Count - 1); if (list.Count == 0) q.Items.RemoveAt(index); }
        }
    }

    // ---- FactoryOrdersHud entry points (2026-08, Factory Build Queue /
    // Order Clipboard) ----------------------------------------------------

    /// <summary>Read-only snapshot for <see cref="FactoryOrdersHud"/>'s
    /// own popup -- index 0 is "NOW BUILDING," the rest is "QUEUE," in
    /// priority order. Empty (not null) if `factory` has no queue at
    /// all, so the popup can still render its own "nothing queued"
    /// state cleanly.</summary>
    public IReadOnlyList<(string Label, int Remaining, bool IsSingleUnit)> OrdersFor(SimBuilding factory)
    {
        var result = new List<(string, int, bool)>();
        var q = FactoryQueueFor(factory, false);
        if (q == null) return result;
        foreach (var item in q.Items)
            result.Add((item.Label, RemainingOf(item), item.Kind == QueueItemKind.SingleUnit));
        return result;
    }

    /// <summary>2026-08 ("Allow queued orders to be moved up/down"): a
    /// plain index swap with its neighbor -- moving index 0 (the active
    /// build) down demotes it exactly the same way <see
    /// cref="PromoteToFront"/> demotes whatever WAS active on a fresh
    /// Factory-body drop, so the active-build concept stays "whatever is
    /// at index 0," never a separate flag to keep in sync.</summary>
    public void MoveOrderUp(SimBuilding factory, int index)
    {
        var q = FactoryQueueFor(factory, false);
        if (q == null || index <= 0 || index >= q.Items.Count) return;
        (q.Items[index - 1], q.Items[index]) = (q.Items[index], q.Items[index - 1]);
    }

    public void MoveOrderDown(SimBuilding factory, int index)
    {
        var q = FactoryQueueFor(factory, false);
        if (q == null || index < 0 || index >= q.Items.Count - 1) return;
        (q.Items[index + 1], q.Items[index]) = (q.Items[index], q.Items[index + 1]);
    }

    public void AdjustOrderQuantity(SimBuilding factory, int index, bool increment)
    {
        var q = FactoryQueueFor(factory, false);
        if (q == null) return;
        AdjustQuantity(q, index, increment);
    }

    /// <summary>2026-08 ("If cancelling the active build, use the
    /// existing factory/build cancellation rules. If cancelling a queued
    /// order, simply remove it from the queue. Do not accidentally
    /// cancel the entire Factory state"): removes exactly ONE item at
    /// `index` -- never touches any other item, never touches the roof
    /// occupant (manual cancellation is a deliberate "never mind," not a
    /// finished batch -- see <see cref="EvictRoofOccupantIfDone"/>'s own
    /// doc comment for why that distinction matters), never clears the
    /// whole Factory's queue the way <see cref="CancelAllProduction"/>
    /// does.</summary>
    public void CancelQueueItem(SimBuilding factory, int index)
    {
        var q = FactoryQueueFor(factory, false);
        if (q == null || index < 0 || index >= q.Items.Count) return;
        q.Items.RemoveAt(index);
    }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard, "Factory
    /// identity/name if the existing UI supports this"): the same
    /// faction/kind lookup `Drop`/`HoverTargetFor` already do inline,
    /// exposed for <see cref="FactoryOrdersHud"/>'s own header line.</summary>
    public string FactoryDisplayName(SimBuilding factory)
    {
        if (factory == null) return "Factory";
        var faction = bridge != null ? bridge.PlayerFaction(factory.PlayerIndex) : FactionId.Mixed;
        return faction + " Factory";
    }

    /// <summary>2026-08 (creator direction: "Grab a Battalion Group from
    /// the existing bottom-left menu. Drag it toward a Factory"): queues
    /// a snapshot of the battalion's own genomes/radii at `factory`
    /// specifically -- the shared implementation behind BOTH the
    /// existing "Build" button (<see cref="BuildBattalionAtOwnFactory"/>,
    /// which resolves `factory` to <see cref="FindAnyOwnCompleteFactory"/>
    /// to preserve its exact original behavior) and the new drag-and-
    /// drop path (<see cref="DropCarriedOrder"/>, which already knows
    /// the exact Factory that was dropped on). NOT deduped -- "build
    /// that battalion GROUP" reads as reproducing the whole squad's own
    /// composition/proportions, e.g. 3 Tetrapods + 2 Winged queues 3 more
    /// Tetrapods + 2 more Winged, not just one of each distinct type.
    /// Returns the created item (for `PromoteToFront`) or null if the
    /// battalion is empty/all members have since died.</summary>
    private QueueItem QueueBattalionAt(SimBuilding factory, IReadOnlyList<MonsterAgent> battalion)
    {
        if (battalion == null || battalion.Count == 0 || factory == null) return null;

        var snapshot = new List<(StoredGenomeDto Genome, float Radius)>();
        var label = "Battalion build";
        foreach (var m in battalion)
        {
            if (m == null || m.Creature == null) continue;
            snapshot.Add((m.Creature, m.Radius));
            if (m.BattalionSlot.HasValue) label = "Battalion " + m.BattalionSlot.Value + " build";
        }
        if (snapshot.Count == 0) return null;

        var item = new QueueItem { Kind = QueueItemKind.Battalion, BattalionRemaining = snapshot, Label = label };
        FactoryQueueFor(factory, true).Items.Add(item);
        return item;
    }

    /// <summary>Existing "Build" button entry point (<see
    /// cref="BattalionHud"/>) -- unchanged behavior, now implemented via
    /// <see cref="QueueBattalionAt"/> against <see
    /// cref="FindAnyOwnCompleteFactory"/> (this button has no drag
    /// context, so it can't know which specific Factory the player
    /// means; "nearest to center" was always its own existing
    /// answer).</summary>
    public void BuildBattalionAtOwnFactory(IReadOnlyList<MonsterAgent> battalion)
    {
        QueueBattalionAt(FindAnyOwnCompleteFactory(), battalion);
    }

    /// <summary>2026-08 (docs/12 "Lab stable" half of battalion grouping):
    /// the LabBattalion counterpart to <see cref="QueueBattalionAt"/> --
    /// resolves a Lab-defined template's genome ids against the player's
    /// own fetched roster (<see cref="RuntimeCityBuilder.RosterCreatures"/>)
    /// and queues them at `factory`. A creatureId that doesn't resolve --
    /// removed from the Stable, or a fetch race -- is silently skipped,
    /// same "don't crash on a stale reference" posture the Lab's own
    /// template storage already takes; the rest of the battalion still
    /// builds. Duplicate ids resolve to duplicate queue entries (the
    /// template legitimately allows "3 Tetrapods + 2 Winged"). Returns
    /// the created item (for `PromoteToFront`) or null.</summary>
    private QueueItem QueueLabBattalionAt(SimBuilding factory, string name, string[] creatureIds)
    {
        if (string.IsNullOrEmpty(name) || creatureIds == null || creatureIds.Length == 0 || builder == null || factory == null) return null;

        var byId = new Dictionary<string, StoredGenomeDto>();
        foreach (var g in builder.RosterCreatures) byId[g.Id] = g;

        var resolved = new List<StoredGenomeDto>();
        var missing = 0;
        foreach (var id in creatureIds)
        {
            if (byId.TryGetValue(id, out var g)) resolved.Add(g);
            else missing++;
        }
        if (missing > 0)
            Debug.LogWarning("GrabCursor: Lab battalion \"" + name + "\" has " + missing
                + " creature id(s) that don't resolve against the fetched roster (removed from the Stable?) -- skipping them.");
        if (resolved.Count == 0) return null;

        var item = new QueueItem { Kind = QueueItemKind.LabBattalion, LabBattalionRemaining = resolved, Label = name };
        FactoryQueueFor(factory, true).Items.Add(item);
        return item;
    }

    /// <summary>Existing "Build" button entry point (<see
    /// cref="LabBattalionHud"/>) -- unchanged behavior, now implemented
    /// via <see cref="QueueLabBattalionAt"/> against <see
    /// cref="FindAnyOwnCompleteFactory"/>, same reasoning as <see
    /// cref="BuildBattalionAtOwnFactory"/>.</summary>
    public void BuildLabBattalion(string name, string[] creatureIds)
    {
        QueueLabBattalionAt(FindAnyOwnCompleteFactory(), name, creatureIds);
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
    private HexCoord? FindOpenHexNear(HexCoord from, HashSet<HexCoord> claimed, float bodyRadius)
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

    /// <summary>Null-safe on `cam`/`mouse` -- defensive only (`Update()`
    /// already early-returns before either caller, `TryPickUp`/<see
    /// cref="ResolveDropTarget"/>, can run with either null), so a null
    /// reference here reads as "no raycast this call," never a
    /// crash.</summary>
    private static RaycastHit? RaycastCursor(Camera cam, Mouse mouse)
    {
        if (cam == null || mouse == null) return null;
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

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard, "The
    /// newly dropped Monster/Battalion becomes the Factory's current
    /// build"): the physical-monster counterpart to <see
    /// cref="QueueSingleUnit"/>'s Battalion siblings -- queues
    /// `_pendingBuildCount` clones of `original`'s own genome at
    /// `factory` and returns the touched/created item so `Drop` can
    /// promote it on a Factory-body target.</summary>
    private QueueItem CloneOnto(SimBuilding factory, MonsterAgent original)
    {
        var creature = original.Creature;
        if (creature == null || builder == null || factory == null) return null;
        return QueueSingleUnit(factory, creature, original.Radius, _pendingBuildCount);
    }
}
