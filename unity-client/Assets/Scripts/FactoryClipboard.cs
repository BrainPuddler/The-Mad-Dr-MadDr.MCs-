using UnityEngine;

/// <summary>2026-08 (creator direction: "Implement and integrate a
/// Factory Build Queue / Order Clipboard system... Add a medium-sized
/// clipboard/order-form object physically attached to or positioned on
/// the Factory. The clipboard is the Factory's production queue/order
/// form"): the click/raycast identity for the small clipboard prop <see
/// cref="BaseDresser"/> spawns on every completed Factory -- mirrors
/// <see cref="BuildingIdentity"/>'s own one-field shape (that component
/// identifies "which building did this raycast hit"; this one
/// identifies "did it hit the CLIPBOARD specifically," a smaller,
/// distinct target attached to the same Factory). `GrabCursor` checks
/// for this BEFORE falling back to `BuildingIdentity` on any raycast hit
/// (<see cref="GrabCursor.ResolveDropTarget"/>/`TryPickUp`), so the
/// clipboard reads as "add to queue" and the rest of the Factory body
/// reads as "build this now" -- never the other way around.</summary>
public class FactoryClipboard : MonoBehaviour
{
    public uint FactoryEntityId;
}
