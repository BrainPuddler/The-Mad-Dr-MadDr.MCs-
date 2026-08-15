using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse orders, StarCraft 2 control model. New Input System API
/// exclusively -- this project's activeInputHandler is set to Input
/// System Package only, so the legacy UnityEngine.Input class would throw
/// at runtime.
///
///   Left click          : select one monster (empty ground = deselect)
///   Left DRAG            : marquee box-select every unit in the rectangle
///   Left DOUBLE-click    : select all units of that type on screen
///   Shift + left (any of : add to the current selection instead of
///     the above)           replacing it (double-click adds all of type)
///   Right click          : order the WHOLE selection --
///   (or Ctrl + left click,  on a citizen  -> chase and eat it
///    trackpad support)      on a building WALL -> walk to it and attack
///                            on a building ROOF -> winged units fly to it
///                              and land (perch); everyone else attacks
///                            on the ground -> waypoint (Shift queues)
///   A + left click       : attack-move -- walk to the ground point,
///                            auto-engaging any enemy spotted en route
///                            instead of walking past it (Shift queues)
///   P + left click       : patrol -- attack-move back and forth forever
///                            between here and the clicked ground point
///   J                    : glide the camera to the unit nearest the cursor
///                            (2026-07: moved off G -- see GrabCursor.cs's
///                            own header for what G does now)
///   Ctrl + [0-9]         : battalion control group -- bind the CURRENT
///                            selection to that slot, auto-named
///                            "Battalion N" (N increments globally, never
///                            reused). Rebinding a slot replaces whatever
///                            was there with a fresh battalion.
///   Alt + [0-9]          : quick-select that battalion (2026-08: plain
///                            [0-9] is already permanently claimed by
///                            BuildMenuHud's own always-on build hotkeys
///                            -- see AssignBattalion's own comment for why
///                            this couldn't use the classic bare-digit-to-
///                            select convention)
/// </summary>
public class WaypointCommander : MonoBehaviour
{
    private const float DragThresholdSq = 36f;    // 6 px: below this a press is a click, not a box
    private const float DoubleClickTime = 0.35f;
    private const float DoubleClickDistSq = 100f;  // 10 px: a second click must land near the first

    private RuntimeCityBuilder _builder;

    /// <summary>2026-08 (creator direction: "when cursor is in grab
    /// mode, disable lasso rectangle select"): wired by
    /// `RuntimeCityBuilder` right after both components exist, so
    /// <see cref="HandleSelection"/> can check
    /// <see cref="GrabCursor.IsGrabModeActive"/> before starting a
    /// left-drag marquee -- grab mode already owns the left button for
    /// its own pick-up/drop, and letting a marquee track underneath it
    /// meant dragging a carried monster into position also silently
    /// drew and applied a box-select. Null-checked everywhere it's read
    /// (same "optional collaborator" posture every other cross-script
    /// reference in this file already takes) so a scene missing a
    /// GrabCursor just behaves as if grab mode never activates.</summary>
    public GrabCursor grabCursor;

    private readonly List<MonsterAgent> _selected = new List<MonsterAgent>();

    // 2026-08 (Zombie/SCV-style "cannon fodder I choose," docs/12): a
    // SEPARATE, parallel selection for Worker/Zombie units rather than
    // widening `_selected` -- that list (and everything built on it:
    // battalions, AssignFormation, SelectionHud, HudStatus's own detail
    // lines) is deeply MonsterAgent-typed throughout this file, and
    // Worker's real order vocabulary is intentionally much smaller (move
    // only -- combat/scavenge/build are automatic, see Worker.cs's own
    // header). Mutually exclusive with `_selected`: selecting a Worker
    // clears any Monster selection and vice versa, enforced by
    // `ClearMonsterSelection`/`ClearWorkerSelection` at the start of
    // every selection-mutating method on both sides.
    private readonly List<Worker> _selectedWorkers = new List<Worker>();

    // left-drag marquee state
    private bool _leftDown;
    private Vector2 _dragStart;

    // double-click detection
    private float _lastClickTime = -1f;
    private Vector2 _lastClickPos;

    // ---- battalions (2026-08 creator direction: "battalion grouping
    // system... group select using drag highlight... assign battalion
    // groups to the number keys zero through 9 for quick selection") ------

    private sealed class Battalion
    {
        public string Name;
        public readonly List<MonsterAgent> Members = new List<MonsterAgent>();
    }

    // slots 0-9, index == the bound digit key. Null = unbound.
    private readonly Battalion[] _battalions = new Battalion[10];

    // "Naming of in game battalion groups is automatic with an incremental
    // number" -- ONE running counter shared across every slot, so rebinding
    // slot 3 twice gives "Battalion 1" then later "Battalion 5" (whatever
    // the count was at THAT moment), never reusing a name or restarting
    // from the slot's own digit.
    private int _nextBattalionNumber = 1;

    /// <summary>Every currently-defined battalion, for
    /// <see cref="BattalionHud"/> to list -- pruned of dead/despawned
    /// members and empty slots on read, same "prune lazily when read, not
    /// eagerly every frame" discipline <see cref="PruneSelection"/>
    /// already uses for the plain selection.</summary>
    public IEnumerable<(int Slot, string Name, int Count)> Battalions
    {
        get
        {
            for (var i = 0; i < _battalions.Length; i++)
            {
                var b = _battalions[i];
                if (b == null) continue;
                PruneBattalion(b);
                if (b.Members.Count == 0) { _battalions[i] = null; continue; }
                yield return (i, b.Name, b.Members.Count);
            }
        }
    }

    /// <summary>The live, pruned member list for one battalion slot (used
    /// by <see cref="GrabCursor.BuildBattalionAtOwnFactory"/> to read what
    /// to reproduce), or null if that slot is unbound/now empty.</summary>
    public IReadOnlyList<MonsterAgent> BattalionMembers(int slot)
    {
        if (slot < 0 || slot >= _battalions.Length) return null;
        var b = _battalions[slot];
        if (b == null) return null;
        PruneBattalion(b);
        if (b.Members.Count == 0) { _battalions[slot] = null; return null; }
        return b.Members;
    }

    /// <summary>The selection's lead unit (first picked) -- what the HUD
    /// details. Null when nothing is selected.</summary>
    public MonsterAgent SelectedAgent { get { return _selected.Count > 0 ? _selected[0] : null; } }
    public int SelectedCount { get { return _selected.Count; } }

    /// <summary>The live selection set, pruned of any agent that despawned
    /// since last frame -- read by SelectionHud to group/count/re-select by
    /// creature type (<see cref="MonsterAgent.BodyPlan"/>).</summary>
    public IReadOnlyList<MonsterAgent> Selected { get { PruneSelection(); return _selected; } }

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
    }

    private void Update()
    {
        // the minimap runs its own OnGUI click handling (recenter camera /
        // order the selection) using the SAME click -- without this guard
        // the New Input System's Mouse.current (read below) has no idea
        // OnGUI already claimed the click, so a minimap click would ALSO
        // fire a world-space select/order underneath it. Same reasoning
        // for the building-nav icon bar (2026-07), the selection-panel
        // icon row next to the minimap (2026-08), the recall button
        // docked above the minimap (2026-08), and the battalion list
        // docked above THAT (2026-08).
        if (Minimap.PointerOver || BuildingNavHud.PointerOver || SelectionHud.PointerOver
            || RecallHud.PointerOver || BattalionHud.PointerOver || ProductionQueueHud.PointerOver
            || LabBattalionHud.PointerOver || CollectorLabHud.PointerOver || HudStatus.PointerOver) return;

        var mouse = Mouse.current;
        if (mouse == null || _builder == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var keyboard = Keyboard.current;

        // 2026-07: moved off G (now GrabCursor's grab-mode key) onto J.
        if (keyboard != null && keyboard.jKey.wasPressedThisFrame)
            JumpToNearestUnit(cam, mouse);

        // trackpad support: Ctrl+left-click stands in for a right click
        // (mirrors macOS's own Control-click-for-secondary-click
        // convention, but works the same on any OS/pointer that lacks a
        // real right button).
        var ctrlHeld = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

        // docs/23 Phase 5 follow-up: A/P held turns the next left click into
        // an order (attack-move / patrol) instead of a selection click --
        // same idea as the Ctrl-left-click-for-right-click stand-in above.
        var attackMoveHeld = keyboard != null && keyboard.aKey.isPressed;
        var patrolHeld = keyboard != null && keyboard.pKey.isPressed;

        if (keyboard != null) HandleBattalionHotkeys(keyboard, ctrlHeld);

        HandleSelection(cam, mouse, keyboard, ctrlHeld, attackMoveHeld, patrolHeld);
        HandleOrders(cam, mouse, keyboard, ctrlHeld, attackMoveHeld, patrolHeld);
    }

    // ---- battalions -----------------------------------------------------------

    /// <summary>Ctrl+[0-9] assigns, Alt+[0-9] selects -- see this class's
    /// own header for the full key table and why plain [0-9] couldn't be
    /// reused (BuildMenuHud's build hotkeys already claim it, unconditionally,
    /// any time a match exists -- confirmed by reading that class's own
    /// Update(), not assumed). Both modifiers are otherwise free in this
    /// file: `ctrlHeld` (passed in, already computed once per frame by the
    /// caller) is a mouse-click stand-in elsewhere, never checked against a
    /// bare key press, so reusing it here for Ctrl+digit doesn't collide;
    /// Alt isn't bound to anything else in this project at all.</summary>
    private void HandleBattalionHotkeys(Keyboard keyboard, bool ctrlHeld)
    {
        var altHeld = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        if (!ctrlHeld && !altHeld) return;   // cheap early-out: no digit check needed most frames

        for (var slot = 0; slot <= 9; slot++)
        {
            if (!DigitKeyPressed(keyboard, slot)) continue;
            if (ctrlHeld) AssignBattalion(slot);
            else if (altHeld) SelectBattalion(slot);
        }
    }

    /// <summary>Binds the CURRENT selection to `slot`, replacing whatever
    /// battalion was there before with a freshly-named one -- "naming of
    /// in game battalion groups is automatic with an incremental number"
    /// (creator direction, 2026-08). A no-op on an empty selection (never
    /// creates/overwrites a slot with zero members).</summary>
    private void AssignBattalion(int slot)
    {
        PruneSelection();
        if (_selected.Count == 0) return;
        CreateBattalion(_selected, slot);
    }

    /// <summary>2026-08 (creator direction: "when the battalion is done,
    /// it parks itself away from factory and assigned a name to it,
    /// adding it to the battalion list"): called by <see
    /// cref="GrabCursor"/>'s production queue the instant a queued
    /// battalion build finishes producing every member -- auto-forms the
    /// freshly-cloned squad into a NEW battalion, same auto-incrementing
    /// name as a manual Ctrl+digit bind, just triggered by production
    /// completing instead of a keypress. Picks the first currently-EMPTY
    /// slot (0-9) rather than requiring the player to have picked one in
    /// advance -- if all 10 are already bound to something else, this is
    /// a silent no-op (logged, not crashed): the produced monsters still
    /// exist and are still selectable/orderable normally, they just don't
    /// get a hotkey until a slot frees up. A no-op on an empty/all-dead
    /// list too.</summary>
    public void FormBattalionFromProduction(List<MonsterAgent> members)
    {
        if (members == null) return;
        members.RemoveAll(m => m == null);
        if (members.Count == 0) return;

        for (var slot = 0; slot < _battalions.Length; slot++)
        {
            if (_battalions[slot] != null) continue;
            CreateBattalion(members, slot);
            return;
        }
        Debug.Log("Battalion production finished, but all 10 battalion slots are already bound -- the new squad exists, just without a hotkey.");
    }

    /// <summary>2026-08 (creator question: "why not just have a variable
    /// in the object saying monster belongs to battalion group id"):
    /// the LIST stays the source of truth here (rebinding a slot means
    /// replacing its whole member list in one shot; an ID-only model
    /// would need a full scan of every monster in the game to find
    /// whoever currently holds the OLD id before it could be cleared),
    /// but each member's own `MonsterAgent.BattalionSlot` is kept in
    /// sync as a mirror -- same relationship `MonsterAgent.Selected`
    /// already has to this class's own `_selected` list, just for
    /// battalion membership instead of the live selection. Clears the
    /// mirror on whoever held this slot before (only if THIS slot is
    /// still what their own field says -- a monster moved to a second
    /// battalion since shouldn't have ITS membership yanked out from
    /// under it by the FIRST battalion's own rebind).
    ///
    /// 2026-08 follow-up (creator direction: "battalion groups remain the
    /// same, if a unit is already part of one group it is excluded from
    /// being part of another"): membership is exclusive -- before adding
    /// an incoming member here, pull it out of whatever OTHER slot's
    /// list currently holds it (its own `BattalionSlot` mirror says
    /// which). This is genuinely a different case from the same-slot
    /// clear above: that one handles "slot N is being rebound to a new
    /// roster," this one handles "a member of slot N is being pulled
    /// INTO slot M" -- both must run for a rebind that also poaches
    /// members from other battalions to end with every unit in exactly
    /// one list.</summary>
    private void CreateBattalion(List<MonsterAgent> members, int slot)
    {
        var old = _battalions[slot];
        if (old != null)
            foreach (var m in old.Members)
                if (m != null && m.BattalionSlot == slot) m.SetBattalionSlot(null);

        foreach (var m in members)
        {
            if (m == null || !m.BattalionSlot.HasValue || m.BattalionSlot.Value == slot) continue;
            var prior = _battalions[m.BattalionSlot.Value];
            if (prior != null) prior.Members.Remove(m);
        }

        var battalion = new Battalion { Name = "Battalion " + _nextBattalionNumber };
        battalion.Members.AddRange(members);
        _battalions[slot] = battalion;
        _nextBattalionNumber++;

        foreach (var m in members)
            if (m != null) m.SetBattalionSlot(slot);
    }

    /// <summary>Re-selects a bound battalion's live members -- public so
    /// <see cref="BattalionHud"/>'s own row buttons can trigger the exact
    /// same effect as the Alt+digit hotkey for players who forget the
    /// binding or prefer the mouse. A no-op (leaves the current selection
    /// untouched) if that slot was never bound, or every member has since
    /// died/despawned.</summary>
    public void SelectBattalion(int slot)
    {
        var b = _battalions[slot];
        if (b == null) return;
        PruneBattalion(b);
        if (b.Members.Count == 0) { _battalions[slot] = null; return; }
        SetSelection(b.Members);
    }

    private static void PruneBattalion(Battalion b)
    {
        for (var i = b.Members.Count - 1; i >= 0; i--)
            if (b.Members[i] == null) b.Members.RemoveAt(i);
    }

    private static bool DigitKeyPressed(Keyboard keyboard, int digit)
    {
        switch (digit)
        {
            case 0: return keyboard.digit0Key.wasPressedThisFrame;
            case 1: return keyboard.digit1Key.wasPressedThisFrame;
            case 2: return keyboard.digit2Key.wasPressedThisFrame;
            case 3: return keyboard.digit3Key.wasPressedThisFrame;
            case 4: return keyboard.digit4Key.wasPressedThisFrame;
            case 5: return keyboard.digit5Key.wasPressedThisFrame;
            case 6: return keyboard.digit6Key.wasPressedThisFrame;
            case 7: return keyboard.digit7Key.wasPressedThisFrame;
            case 8: return keyboard.digit8Key.wasPressedThisFrame;
            default: return keyboard.digit9Key.wasPressedThisFrame;
        }
    }

    // ---- selection (left button) --------------------------------------------

    private void HandleSelection(Camera cam, Mouse mouse, Keyboard keyboard, bool ctrlHeld,
        bool attackMoveHeld, bool patrolHeld)
    {
        // Ctrl+left is claimed by the right-click stand-in above, and
        // A/P-left by the attack-move/patrol order below -- never let any
        // of them start a selection click/drag too. Grab mode (see
        // `grabCursor`'s own doc comment) claims the left button just as
        // exclusively, for pick-up/drop instead of an order -- same
        // reasoning, same guard shape.
        var grabModeActive = grabCursor != null && grabCursor.IsGrabModeActive;
        if (mouse.leftButton.wasPressedThisFrame && !ctrlHeld && !attackMoveHeld && !patrolHeld && !grabModeActive)
        {
            _leftDown = true;
            _dragStart = mouse.position.ReadValue();
        }

        if (!mouse.leftButton.wasReleasedThisFrame || !_leftDown) return;
        _leftDown = false;

        var up = mouse.position.ReadValue();
        var additive = keyboard != null && keyboard.leftShiftKey.isPressed;

        if ((up - _dragStart).sqrMagnitude > DragThresholdSq)
        {
            // a drag: marquee box-select. Monsters take priority over
            // Workers when a box happens to catch both (the common case
            // in practice is a box drawn for one or the other, not a mix)
            // -- only checks for Workers when the box caught zero Monsters.
            var box = ScreenRect(_dragStart, up);
            var hits = UnitsInBox(cam, box);
            if (hits.Count > 0)
            {
                if (additive) AddToSelection(hits); else SetSelection(hits);
            }
            else
            {
                var workerHits = WorkersInBox(cam, box);
                if (additive) AddToWorkerSelection(workerHits); else SetSelectedWorkers(workerHits);
            }
            return;
        }

        // a click: single-select, with double-click -> select-all-of-type
        var agent = AgentUnderCursor(cam, mouse);
        var now = Time.unscaledTime;
        var isDouble = agent != null
            && now - _lastClickTime < DoubleClickTime
            && (up - _lastClickPos).sqrMagnitude < DoubleClickDistSq;
        _lastClickTime = now;
        _lastClickPos = up;

        if (agent == null)
        {
            var worker = WorkerUnderCursor(cam, mouse);
            if (worker != null)
            {
                if (additive) AddWorkerToSelection(worker); else SetSelectedWorkers(new List<Worker> { worker });
                return;
            }
            if (!additive) ClearSelection();   // Shift+click empty keeps the current group
            return;
        }
        if (isDouble)
        {
            var ofType = UnitsOfTypeOnScreen(cam, agent.BodyPlan);
            if (additive) AddToSelection(ofType); else SetSelection(ofType);
        }
        else if (additive)
        {
            ToggleSelection(agent);
        }
        else
        {
            SetSelection(new List<MonsterAgent> { agent });
        }
    }

    // ---- orders (right button, or Ctrl+left for trackpads) ------------------

    private void HandleOrders(Camera cam, Mouse mouse, Keyboard keyboard, bool ctrlHeld,
        bool attackMoveHeld, bool patrolHeld)
    {
        var leftPressed = mouse.leftButton.wasPressedThisFrame;
        var attackMoveClick = attackMoveHeld && leftPressed;
        var patrolClick = patrolHeld && leftPressed;
        var ordered = mouse.rightButton.wasPressedThisFrame
            || (ctrlHeld && leftPressed) || attackMoveClick || patrolClick;
        if (!ordered) return;
        PruneSelection();

        // 2026-08 (Zombie/SCV-style "cannon fodder I choose," docs/12): no
        // selected Monsters -- try the separate, much simpler Worker order
        // path (ground = move, nothing else; combat/scavenge/build are
        // automatic, see Worker.cs's own header) instead of just bailing.
        if (_selected.Count == 0)
        {
            HandleWorkerOrders(cam, mouse);
            return;
        }

        var hit = RaycastCursor(cam, mouse);
        if (!hit.HasValue || hit.Value.collider == null) return;

        var enemy = hit.Value.collider.GetComponentInParent<Tank>();
        if (enemy != null && enemy.Combat != null)
        {
            foreach (var a in _selected) a.OrderAttackUnit(enemy.Combat);
            return;
        }

        var citizen = hit.Value.collider.GetComponentInParent<Citizen>();
        if (citizen != null)
        {
            foreach (var a in _selected) a.OrderEat(citizen);
            return;
        }

        var building = _builder.BuildingFromCollider(hit.Value.collider);
        if (building != null)
        {
            // WHERE on the building you clicked matters: the flat ROOF
            // (upward-facing surface) sends winged units to land on it,
            // while a WALL is an attack order for everyone -- so both
            // verbs stay reachable with a plain right-click and no extra
            // modifier key. Ground units can't perch, so a roof-click is
            // still just an attack for them.
            var roofClick = hit.Value.normal.y > 0.5f;
            List<MonsterAgent> flyers = roofClick ? new List<MonsterAgent>() : null;
            foreach (var a in _selected)
            {
                if (roofClick && a.IsFlyer) flyers.Add(a);
                else a.OrderAttack(building);
            }
            if (flyers != null && flyers.Count > 0) AssignPerch(flyers, building);
            return;
        }

        // 2026-08 (creator direction: "check that monsters can harvest
        // metal and other building salvage"): a destroyed building's own
        // rubble is deliberately collider-less (RuntimeCityBuilder's own
        // Destroyed-branch comment: "clicks fall through to the ground"),
        // so BuildingFromCollider above never resolves a hit here --
        // right-clicking a wreck's footprint needs its own hex-membership
        // check instead, same idea as the plain ground-waypoint case just
        // below but checked FIRST so a wreck's own hex routes to
        // scavenging rather than just a walk-there order.
        var scavengeHex = _builder.HexAt(hit.Value.point);
        var scavengeTarget = _builder.ScavengeableBuildingAt(scavengeHex);
        if (scavengeTarget != null)
        {
            foreach (var a in _selected) a.OrderScavenge(scavengeTarget);
            return;
        }

        // ground: a waypoint for the whole group. Shift queues. A group
        // spreads into a formation around the spot (one hex each) while
        // WALKING, then creeps in close together once everyone's stopped
        // (see OrderMove's settleTarget -- MonsterAgent.TickSettle).
        var hex = _builder.HexAt(hit.Value.point);
        if (_builder.City.Contains(hex))
        {
            var shift = keyboard != null && keyboard.leftShiftKey.isPressed;
            if (patrolClick)
                foreach (var a in _selected) a.OrderPatrol(hex);
            else if (attackMoveClick)
                foreach (var a in _selected) a.OrderAttackMove(hex, shift);
            else if (_selected.Count == 1)
                _selected[0].OrderMove(hex, shift);
            else
                AssignFormation(_builder.FormationHexes(hex, _selected.Count), shift, hit.Value.point);
            _builder.SpawnWaypointMarker(_builder.WorldOf(hex));
        }
    }

    /// <summary>Programmatic equivalent of a ground right-click order, for
    /// callers that already have a world point and aren't driving the
    /// 3D cursor -- the minimap's right-click-to-order. Same single-
    /// unit/formation/settle/marker behavior as a normal ground order
    /// (see HandleOrders' ground branch, which this mirrors).</summary>
    public void OrderSelectionTo(Vector3 worldPoint, bool queue)
    {
        PruneSelection();
        if (_selected.Count == 0 || _builder == null) return;
        var hex = _builder.HexAt(worldPoint);
        if (!_builder.City.Contains(hex)) return;
        if (_selected.Count == 1) _selected[0].OrderMove(hex, queue);
        else AssignFormation(_builder.FormationHexes(hex, _selected.Count), queue, worldPoint);
        _builder.SpawnWaypointMarker(_builder.WorldOf(hex));
    }

    /// <summary>2026-08 (creator direction: "give me a monster recall
    /// button that will gather my troupes in one place"): selects and
    /// orders EVERY currently-alive monster to rally at the player's own
    /// base in one shot. Selecting the group first (rather than leaving
    /// whatever was selected before untouched) doubles this as "select my
    /// whole army" -- the natural next thing a player does right after
    /// mashing a recall button anyway, and it's what makes `AssignFormation`
    /// below (which reads `_selected`, same as every other multi-unit
    /// order path in this file) actually apply to the recalled group
    /// instead of whatever was selected beforehand.
    ///
    /// Rallies to the player's own Factory (the same "home" a laden
    /// harvester's own auto-delivery already walks to --
    /// `MonsterAgent.FindOwnFactory`), falling back to the HQ if no
    /// Factory exists yet. A no-op if the player has neither (an
    /// intentionally silent no-op, matching every other "nothing to do
    /// yet" branch in this file, rather than walking a whole army toward
    /// an arbitrary point). Spreads the group via the SAME
    /// `FormationHexes`/`AssignFormation` machinery a manual multi-select
    /// order already uses, not a plain single shared destination -- a
    /// big army stacking onto one hex is exactly the kind of crowding
    /// this project's whole steering-fix history has been fighting.</summary>
    public void RecallAll()
    {
        if (_builder == null) return;
        var home = FindOwnRallyHex();
        if (!home.HasValue) return;

        var monsters = new List<MonsterAgent>();
        foreach (var m in _builder.Monsters)
            if (m != null) monsters.Add(m);
        if (monsters.Count == 0) return;

        SetSelection(monsters);
        var worldHome = _builder.WorldOf(home.Value);
        if (monsters.Count == 1) monsters[0].OrderMove(home.Value, false);
        else AssignFormation(_builder.FormationHexes(home.Value, monsters.Count), false, worldHome);
        _builder.SpawnWaypointMarker(worldHome);
    }

    /// <summary>The local human player's own Factory hex, falling back to
    /// their own HQ -- same "player index 0 is the local human" convention
    /// every other Unity-side script already uses (e.g. `MonsterAgent.
    /// FindOwnFactory`'s own doc comment, `GrabCursor.localPlayerIndex`'s
    /// default). Null if neither exists yet (no match, or both destroyed)
    /// -- `RecallAll` treats that as "nothing to rally to," not a
    /// fallback to some arbitrary point.</summary>
    private HexCoord? FindOwnRallyHex()
    {
        var bridge = _builder.SimBridge;
        if (bridge == null || !bridge.HasMatch) return null;
        HexCoord? factory = null;
        HexCoord? hq = null;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex != 0 || b.State != MadDr.MatchCore.BuildingState.Complete) continue;
            if (b.Kind == MadDr.MatchCore.BuildingKind.Factory && !factory.HasValue) factory = b.Hex;
            else if (b.Kind == MadDr.MatchCore.BuildingKind.Hq && !hq.HasValue) hq = b.Hex;
        }
        return factory ?? hq;
    }

    /// <summary>Hand out formation slots to the selected group,
    /// nearest-slot-to-nearest-unit, so units mostly walk straight to
    /// their spot instead of crossing paths. Once stopped, each unit
    /// creeps to its OWN point on a ring AROUND `clusterPoint` (the
    /// clicked waypoint) -- distinct per unit, computed in
    /// <see cref="RingTarget"/> -- so the group distributes AROUND the
    /// waypoint and leaves the marker itself clear (creator direction,
    /// 2026-07: "They MUST distribute themselves around the waypoint NOT
    /// ON the Waypoint"). The earlier design passed the SAME centre point
    /// to every unit, so they all crept onto the marker and only body
    /// separation held them apart -- a clump centred on the waypoint,
    /// which is exactly what this replaces. A single GroupFacing token is
    /// shared across the whole group so they settle facing one direction
    /// -- whichever unit gets to its slot first (creator direction,
    /// 2026-07).
    ///
    /// 2026-08 (creator direction: "see if the speed based solution with
    /// coordinate their landing spots is viable"): the greedy pick below
    /// ranks by ETA (distance / this unit's own `WalkSpeed`), not raw
    /// distance -- a mixed-speed group (a lumbering tank-bodied monster
    /// next to a sprinter) previously could get assigned by pure
    /// proximity to a slot the SLOW unit happens to start nearer to but
    /// would take longer to actually reach than a faster unit starting
    /// slightly farther away, so the two crossed paths converging on
    /// their (mismatched) slots. This is a real, worthwhile improvement
    /// for exactly that mismatch, but it's a DESTINATION-assignment fix,
    /// not a moment-to-moment steering one -- it doesn't touch (and isn't
    /// expected to fix) the residual close-quarters circling docs/12's
    /// 2026-08 follow-up entry documents for a tight multi-squad scrum
    /// already converged on nearby, non-conflicting destinations; that's
    /// `MonsterSteeringController.Alignment`/`PredictiveAvoidance`
    /// territory, a different mechanism entirely.</summary>
    private void AssignFormation(System.Collections.Generic.List<MadDr.CityGen.HexCoord> slots, bool queue,
        Vector3 clusterPoint)
    {
        var facing = new MonsterAgent.GroupFacing();
        var remaining = new System.Collections.Generic.List<MonsterAgent>(_selected);
        var ringIndex = 0;
        foreach (var slot in slots)
        {
            if (remaining.Count == 0) break;
            var slotW = _builder.WorldOf(slot);
            var best = -1;
            var bestEtaSq = float.MaxValue;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (remaining[i] == null) continue;
                var d = remaining[i].transform.position - slotW;
                d.y = 0f;
                var speed = Mathf.Max(0.1f, remaining[i].WalkSpeed);
                var etaSq = d.sqrMagnitude / (speed * speed);
                if (etaSq < bestEtaSq) { bestEtaSq = etaSq; best = i; }
            }
            if (best < 0) break;
            var unit = remaining[best];
            remaining.RemoveAt(best);
            var settle = RingTarget(clusterPoint, ringIndex++, _builder.groupSpacing);
            unit.OrderMove(slot, queue, settle, facing);
        }
    }

    /// <summary>Roof "parking" for a group of flyers ordered onto one
    /// building (creator direction, 2026-07: "Same parking, distributions
    /// rules should apply to roof features. If there is not enough
    /// space... it should pick a different roof nearby before landing.").
    /// Same nearest-slot-to-nearest-unit greedy assignment
    /// `AssignFormation` uses for ground formations, but the "slots" are
    /// the target building's own free footprint hexes
    /// (`RuntimeCityBuilder.AvailableRoofSlots`) rather than an open hex
    /// neighbourhood -- a roof has no neighbourhood to spread into, it IS
    /// the footprint. Whatever doesn't fit (the roof's own capacity is
    /// already spoken for, by earlier perchers or by more units than it
    /// holds) rolls over to the nearest OTHER standing building with free
    /// room (`FindNearbyPerchableBuilding`), repeating outward until every
    /// unit has a spot or genuinely nothing nearby has room -- at which
    /// point the leftover units perch on the ORIGINAL roof anyway rather
    /// than being left with no order at all (the same "pad rather than
    /// fail" call `FormationHexes` already makes for ground destinations
    /// that are too hemmed in).</summary>
    private void AssignPerch(List<MonsterAgent> flyers, Building building)
    {
        var remaining = new List<MonsterAgent>(flyers);
        var tried = new HashSet<Building>();
        var target = building;

        while (remaining.Count > 0 && target != null)
        {
            tried.Add(target);
            var slots = _builder.AvailableRoofSlots(target);
            foreach (var slot in slots)
            {
                if (remaining.Count == 0) break;
                var slotW = _builder.WorldOf(slot);
                var best = -1;
                var bestSq = float.MaxValue;
                for (var i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i] == null) continue;
                    var d = remaining[i].transform.position - slotW;
                    d.y = 0f;
                    if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = i; }
                }
                if (best < 0) break;
                var unit = remaining[best];
                remaining.RemoveAt(best);
                unit.OrderPerch(target, slot);
            }
            if (remaining.Count == 0) break;
            target = _builder.FindNearbyPerchableBuilding(building, remaining.Count, tried);
        }

        // nothing nearby had room for the rest -- land them on the
        // clicked roof anyway (graceful degradation, not a stall).
        foreach (var a in remaining) a.OrderPerch(building);
    }

    /// <summary>The `index`-th distinct settle point on a ring around
    /// `center`, laid out by the golden-angle phyllotaxis (sunflower)
    /// pattern so any group size spreads evenly around the waypoint with
    /// a CLEAR central hole -- nobody's target is the centre, so the
    /// marker stays visible and units ring it instead of piling onto it.
    /// `spacing` (RuntimeCityBuilder.groupSpacing) drives both the hole
    /// size and the ring pitch, so the Inspector knob widens the whole
    /// formation coherently. Body separation still enforces the exact
    /// pairwise gap on top of this; the ring only seeds the distribution
    /// off the centre.</summary>
    private static Vector3 RingTarget(Vector3 center, int index, float spacing)
    {
        const float goldenAngle = 2.399963f;              // radians (~137.5 deg)
        var hole = 2.5f + spacing;                         // first unit sits this far off the marker, never on it
        var pitch = 2.5f + spacing;                        // radial growth per unit
        var r = hole + pitch * Mathf.Sqrt(index);
        var theta = index * goldenAngle;
        return center + new Vector3(Mathf.Cos(theta) * r, 0f, Mathf.Sin(theta) * r);
    }

    // ---- selection set management -------------------------------------------

    /// <summary>Replaces the current selection outright -- also called
    /// externally by SelectionHud when the player clicks one of its
    /// per-type icons, to narrow the selection down to just that type.</summary>
    public void SetSelection(List<MonsterAgent> agents)
    {
        ClearWorkerSelection();
        ClearMonsterSelection();
        foreach (var a in agents)
            if (a != null && !_selected.Contains(a)) { _selected.Add(a); a.SetSelected(true); }
    }

    private void AddToSelection(List<MonsterAgent> agents)
    {
        ClearWorkerSelection();
        foreach (var a in agents)
            if (a != null && !_selected.Contains(a)) { _selected.Add(a); a.SetSelected(true); }
    }

    private void ToggleSelection(MonsterAgent agent)
    {
        ClearWorkerSelection();
        if (_selected.Remove(agent)) { agent.SetSelected(false); return; }
        _selected.Add(agent);
        agent.SetSelected(true);
    }

    private void ClearSelection()
    {
        ClearMonsterSelection();
        ClearWorkerSelection();
    }

    private void ClearMonsterSelection()
    {
        foreach (var a in _selected) if (a != null) a.SetSelected(false);
        _selected.Clear();
    }

    /// <summary>Drop any units that died/despawned since last frame so
    /// group orders never dereference a destroyed agent.</summary>
    private void PruneSelection()
    {
        for (var i = _selected.Count - 1; i >= 0; i--)
            if (_selected[i] == null) _selected.RemoveAt(i);
    }

    // ---- Worker/Zombie selection (2026-08, docs/12) -- parallel to the
    // Monster set above, see the `_selectedWorkers` field's own header
    // for why this stays separate rather than widening `_selected`. -----

    public void SetSelectedWorkers(List<Worker> workers)
    {
        ClearMonsterSelection();
        ClearWorkerSelection();
        foreach (var w in workers)
            if (w != null && !_selectedWorkers.Contains(w)) { _selectedWorkers.Add(w); w.SetSelected(true); }
    }

    private void AddToWorkerSelection(List<Worker> workers)
    {
        ClearMonsterSelection();
        foreach (var w in workers)
            if (w != null && !_selectedWorkers.Contains(w)) { _selectedWorkers.Add(w); w.SetSelected(true); }
    }

    private void AddWorkerToSelection(Worker worker)
    {
        ClearMonsterSelection();
        if (worker != null && !_selectedWorkers.Contains(worker)) { _selectedWorkers.Add(worker); worker.SetSelected(true); }
    }

    private void ClearWorkerSelection()
    {
        foreach (var w in _selectedWorkers) if (w != null) w.SetSelected(false);
        _selectedWorkers.Clear();
    }

    private void PruneWorkerSelection()
    {
        for (var i = _selectedWorkers.Count - 1; i >= 0; i--)
            if (_selectedWorkers[i] == null) _selectedWorkers.RemoveAt(i);
    }

    /// <summary>Ground-only order path for a Worker/Zombie selection --
    /// no attack-unit/citizen/building/scavenge routing, since those are
    /// all automatic for a Worker (see Worker.cs's own header); a move
    /// order is the one thing "cannon fodder I choose" actually needs a
    /// player-issued command for.</summary>
    private void HandleWorkerOrders(Camera cam, Mouse mouse)
    {
        PruneWorkerSelection();
        if (_selectedWorkers.Count == 0) return;
        var hit = RaycastCursor(cam, mouse);
        if (!hit.HasValue || hit.Value.collider == null) return;
        var hex = _builder.HexAt(hit.Value.point);
        if (!_builder.City.Contains(hex)) return;
        var dest = _builder.WorldOf(hex);
        foreach (var w in _selectedWorkers) if (w != null) w.OrderMoveTo(dest);
        _builder.SpawnWaypointMarker(dest);
    }

    // ---- picking helpers -----------------------------------------------------

    private List<MonsterAgent> UnitsInBox(Camera cam, Rect boxBottomLeft)
    {
        var hits = new List<MonsterAgent>();
        foreach (var m in _builder.Monsters)
        {
            if (m == null) continue;
            var sp = cam.WorldToScreenPoint(m.transform.position);
            if (sp.z <= 0f) continue;   // behind the camera
            if (boxBottomLeft.Contains(new Vector2(sp.x, sp.y))) hits.Add(m);
        }
        return hits;
    }

    private List<MonsterAgent> UnitsOfTypeOnScreen(Camera cam, string plan)
    {
        var hits = new List<MonsterAgent>();
        foreach (var m in _builder.Monsters)
        {
            if (m == null || m.BodyPlan != plan) continue;
            var sp = cam.WorldToScreenPoint(m.transform.position);
            if (sp.z <= 0f) continue;
            if (sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height) hits.Add(m);
        }
        return hits;
    }

    private MonsterAgent AgentUnderCursor(Camera cam, Mouse mouse)
    {
        var hit = RaycastCursor(cam, mouse);
        if (!hit.HasValue || hit.Value.collider == null) return null;
        return hit.Value.collider.GetComponentInParent<MonsterAgent>();
    }

    private List<Worker> WorkersInBox(Camera cam, Rect boxBottomLeft)
    {
        var hits = new List<Worker>();
        foreach (var w in _builder.Workers)
        {
            if (w == null) continue;
            var sp = cam.WorldToScreenPoint(w.transform.position);
            if (sp.z <= 0f) continue;
            if (boxBottomLeft.Contains(new Vector2(sp.x, sp.y))) hits.Add(w);
        }
        return hits;
    }

    private Worker WorkerUnderCursor(Camera cam, Mouse mouse)
    {
        var hit = RaycastCursor(cam, mouse);
        if (!hit.HasValue || hit.Value.collider == null) return null;
        return hit.Value.collider.GetComponentInParent<Worker>();
    }

    /// <summary>Normalized (positive width/height) rect from two screen
    /// corners, in the bottom-left origin space both Mouse.position and
    /// Camera.WorldToScreenPoint use.</summary>
    private static Rect ScreenRect(Vector2 a, Vector2 b)
    {
        var x = Mathf.Min(a.x, b.x);
        var y = Mathf.Min(a.y, b.y);
        return new Rect(x, y, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    // ---- J: jump camera to nearest unit -------------------------------------

    /// <summary>J-key (moved off G in 2026-07 -- see GrabCursor.cs): find
    /// the monster closest to whatever the cursor is over and glide the
    /// camera to it. "Over" is the physics hit under the
    /// cursor when there is one (a unit, a building, the ground), falling
    /// back to the y=0 ground plane the ray crosses, then to the camera
    /// itself if the ray never dips below the horizon.</summary>
    private void JumpToNearestUnit(Camera cam, Mouse mouse)
    {
        var rig = cam.GetComponent<SimpleCameraRig>();
        if (rig == null) return;

        Vector3 aim;
        var hit = RaycastCursor(cam, mouse);
        if (hit.HasValue) aim = hit.Value.point;
        else if (!GroundUnderCursor(cam, mouse, out aim)) aim = cam.transform.position;

        // 1e6 is an effectively-unbounded search radius (the city is a few
        // hundred units across); NearestMonsterTo compares squared, which
        // stays well within float range.
        var nearest = _builder.NearestMonsterTo(aim, 1e6f);
        if (nearest != null) rig.FocusOn(nearest.transform.position);
    }

    private static bool GroundUnderCursor(Camera cam, Mouse mouse, out Vector3 world)
    {
        world = Vector3.zero;
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
        var t = -ray.origin.y / ray.direction.y;
        if (t <= 0f) return false;
        world = ray.origin + ray.direction * t;
        return true;
    }

    private RaycastHit? RaycastCursor(Camera cam, Mouse mouse)
    {
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5000f)) return hit;
        return null;
    }

    // ---- selection marquee overlay ------------------------------------------

    private static Texture2D _boxTex;

    /// <summary>2026-08 (creator direction: "the ui is not scaling
    /// properly to screen sizes"): deliberately NOT wrapped in
    /// UiScale.Begin() -- unlike every other HUD in this project, this
    /// rect is built from Mouse.current.position (the New Input System's
    /// REAL screen-pixel cursor position), not Event.current.mousePosition
    /// (which IMGUI's own matrix would keep in sync automatically).
    /// Scaling the drawn rect without also correcting its already-real-
    /// pixel input coordinates would double-apply the scale and draw the
    /// marquee in the wrong place relative to the actual cursor. Its
    /// SIZE is inherently correct at any resolution already (it's built
    /// from a live drag distance in real pixels, not an authored
    /// constant), so leaving it in real screen space costs nothing.</summary>
    private void OnGUI()
    {
        if (!_leftDown) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        var cur = mouse.position.ReadValue();
        if ((cur - _dragStart).sqrMagnitude <= DragThresholdSq) return;

        if (_boxTex == null) _boxTex = Texture2D.whiteTexture;

        // GUI space is top-left origin; screen space is bottom-left --
        // flip Y for the on-screen rectangle
        var r = ScreenRect(_dragStart, cur);
        var gui = new Rect(r.x, Screen.height - (r.y + r.height), r.width, r.height);

        var fill = new Color(0.3f, 1f, 0.5f, 0.12f);
        var edge = new Color(0.35f, 1f, 0.55f, 0.9f);
        GUI.color = fill;
        GUI.DrawTexture(gui, _boxTex);
        GUI.color = edge;
        const float t = 1.5f;
        GUI.DrawTexture(new Rect(gui.x, gui.y, gui.width, t), _boxTex);
        GUI.DrawTexture(new Rect(gui.x, gui.yMax - t, gui.width, t), _boxTex);
        GUI.DrawTexture(new Rect(gui.x, gui.y, t, gui.height), _boxTex);
        GUI.DrawTexture(new Rect(gui.xMax - t, gui.y, t, gui.height), _boxTex);
        GUI.color = Color.white;
    }
}
