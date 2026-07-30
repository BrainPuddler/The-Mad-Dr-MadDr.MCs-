using MadDr.MatchCore;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// docs/23 §2 Phase 2's "ghost-placement cursor with red/green validity
/// tint." Owns exactly one live preview GameObject: while
/// <see cref="BuildMenuHud.SelectedKind"/> is non-null, tracks the mouse
/// under the RTS camera, snaps to the hex it's over (<see cref="
/// RuntimeCityBuilder.HexAt"/>), and tints green/red from a live
/// <see cref="SimBridge.CanPlaceBuilding"/> preview -- the SAME check
/// match-core applies when a placement command actually lands (see
/// <see cref="MatchState.CanPlaceBuilding"/>'s own doc comment), so this
/// preview can never show green for something that then silently fails.
///
/// Left click on a green (valid) hex confirms via
/// <see cref="SimBridge.QueueBuildCommand"/>; right click or Escape
/// cancels back to no selection. Selection state itself lives on
/// <see cref="BuildMenuHud"/>, not here -- this script is purely
/// "given a kind, preview and place it," the same split
/// WaypointCommander (orders) keeps from whatever owns unit selection.
///
/// 2026-07 worker-economy epic, Phase 3: a <see cref="BuildingKind.
/// Factory"/> ghost also requires <see cref="RuntimeCityBuilder.Workers"/>
/// to be non-empty -- "Factories are built by possessed human workers"
/// (creator's own words) -- tints red and blocks the confirm click the
/// same way an unaffordable/illegal placement already does, so the two
/// gates read identically to the player. Every other BuildingKind is
/// unaffected -- Factory is the one the creator specifically named. This
/// is a UNITY-SIDE-ONLY gate: match-core's own `ApplyBuildStructure`/
/// `CanPlaceBuilding` have no concept of a Worker at all (Workers are a
/// Unity-legacy-combat-layer unit, `Tank.cs`'s pattern, not a match-core
/// `SimUnit` -- see docs/12's Phase 2 entry for why), so a build command
/// sent directly through `SimBridge.QueueBuildCommand` bypassing this
/// ghost cursor would still succeed with zero workers. A real, flagged
/// gap, not silently faked as sim-enforced -- closing it for real needs
/// either a match-core-side worker concept or Workers becoming real
/// `SimUnit`s, neither built yet.
///
/// The ghost shape is a single placeholder cube for every
/// <see cref="BuildingKind"/> -- <see cref="BaseDresser"/> is the real
/// per-kind visual; reusing its actual shapes for the ghost too is a
/// reasonable future upgrade, not attempted here. Ground elevation
/// follows <see cref="RuntimeCityBuilder.GroundHeightAt"/> (same helper
/// every other ground-following object in this project already uses),
/// so the ghost sits on sculpted terrain correctly, not just at y=0.
/// </summary>
public class BuildGhostCursor : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;
    public BuildMenuHud buildMenu;
    public int localPlayerIndex = 0;

    [Header("Ghost appearance")]
    public Vector3 ghostScale = new Vector3(12f, 6f, 12f);
    public Color validColor = new Color(0.25f, 0.9f, 0.35f, 0.45f);
    public Color invalidColor = new Color(0.9f, 0.25f, 0.25f, 0.45f);

    private GameObject _ghostGo;
    private Material _validMat;
    private Material _invalidMat;

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder, BuildMenuHud menu, int playerIndex)
    {
        bridge = simBridge;
        builder = cityBuilder;
        buildMenu = menu;
        localPlayerIndex = playerIndex;
    }

    private void Update()
    {
        if (bridge == null || !bridge.HasMatch || builder == null || buildMenu == null) { HideGhost(); return; }

        var kind = buildMenu.SelectedKind;
        if (kind == null) { HideGhost(); return; }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            buildMenu.ClearSelection();
            HideGhost();
            return;
        }

        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse == null || cam == null) { HideGhost(); return; }

        // same "OnGUI already claimed this click" guard WaypointCommander
        // applies for Minimap -- also skip while over the build menu's own
        // panel or the building-nav icon bar (2026-07), so clicking either
        // doesn't ALSO fire a world-space confirm click underneath it.
        if (Minimap.PointerOver || buildMenu.PointerOverPanel || BuildingNavHud.PointerOver) return;

        var hit = RaycastGround(cam, mouse);
        if (!hit.HasValue) { HideGhost(); return; }

        var hex = builder.HexAt(hit.Value.point);
        if (!builder.City.Contains(hex)) { HideGhost(); return; }

        // 2026-07 epic: Factory construction requires a possessed Worker
        // on hand -- every other kind is untouched (RequiresWorker(kind)
        // is false for them, so this reduces to the original check).
        var canPlace = bridge.CanPlaceBuilding(localPlayerIndex, kind.Value, hex)
            && (!RequiresWorker(kind.Value) || builder.Workers.Count > 0);
        ShowGhost(builder.WorldOf(hex), canPlace);

        if (mouse.rightButton.wasPressedThisFrame)
        {
            buildMenu.ClearSelection();
            HideGhost();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && canPlace)
        {
            bridge.QueueBuildCommand(localPlayerIndex, kind.Value, hex);
            buildMenu.ClearSelection();
            HideGhost();
        }
    }

    /// <summary>2026-07 epic: which BuildingKind(s) need a Worker on hand
    /// to place -- just Factory, per the creator's own "Factories are
    /// built by possessed human workers." A method (not a static set)
    /// so it reads as an intentional, easily-extended policy point if a
    /// later phase widens the requirement, not a magic inline comparison.</summary>
    private static bool RequiresWorker(BuildingKind kind) => kind == BuildingKind.Factory;

    private void ShowGhost(Vector3 hexWorldPos, bool valid)
    {
        EnsureGhost();
        var groundY = builder.GroundHeightAt(hexWorldPos);
        _ghostGo.SetActive(true);
        _ghostGo.transform.position = new Vector3(hexWorldPos.x, groundY + ghostScale.y * 0.5f, hexWorldPos.z);
        var rend = _ghostGo.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = valid ? _validMat : _invalidMat;
    }

    private void HideGhost()
    {
        if (_ghostGo != null) _ghostGo.SetActive(false);
    }

    private void EnsureGhost()
    {
        if (_validMat == null)
        {
            _validMat = new Material(ShaderUtil.FindRenderableShader());
            _validMat.color = validColor;
            LabMeshBuilder.MakeTransparent(_validMat);
        }
        if (_invalidMat == null)
        {
            _invalidMat = new Material(ShaderUtil.FindRenderableShader());
            _invalidMat.color = invalidColor;
            LabMeshBuilder.MakeTransparent(_invalidMat);
        }
        if (_ghostGo == null)
        {
            _ghostGo = builder.SpawnPrim(PrimitiveType.Cube, Vector3.zero, ghostScale, _validMat, transform);
            _ghostGo.name = "BuildGhost";
        }
    }

    private void OnDestroy()
    {
        if (_ghostGo != null) Object.Destroy(_ghostGo);
    }

    /// <summary>Same technique <c>WaypointCommander.RaycastCursor</c> uses
    /// (ScreenPointToRay + Physics.Raycast, no LayerMask -- hits any
    /// collider, and <see cref="RuntimeCityBuilder.HexAt"/> only reads
    /// world X/Z, so which collider it happens to hit doesn't matter),
    /// duplicated here rather than exposing WaypointCommander's own
    /// private helper since the two scripts have no other coupling.</summary>
    private static RaycastHit? RaycastGround(Camera cam, Mouse mouse)
    {
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5000f)) return hit;
        return null;
    }
}
