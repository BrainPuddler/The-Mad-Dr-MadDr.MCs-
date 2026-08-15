using UnityEngine;

/// <summary>2026-08 (creator direction, verbatim: "the user may also
/// click on the factory highlighting it and press space or + or - keys
/// to increase or decrease the number of monsters to build"): the ONLY
/// thing a raycast hit against a completed `SimBuilding`'s root needs in
/// order to know which building it hit -- mirrors `MonsterAgent` being
/// the click-identity every monster raycast already resolves via
/// `GetComponentInParent`. Before this, completed buildings carried no
/// identifying component at all (`ProductionQueueHud`'s own header
/// already flagged this exact gap: "there is currently no click-to-
/// select machinery for RTS/SimBuilding buildings at all"). Deliberately
/// just the `EntityId` -- callers re-resolve the live `SimBuilding` from
/// `SimBridge.BuildingAt` each time (the same building list a destroyed
/// entry never gets removed from, so a stale reference here would only
/// ever go stale by pointing at a NOW-destroyed building, still safely
/// resolvable) rather than caching a reference that could drift from
/// match-core's own authoritative state.</summary>
public class BuildingIdentity : MonoBehaviour
{
    public uint EntityId;
}
