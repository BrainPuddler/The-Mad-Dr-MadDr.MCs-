using MadDr.MatchCore;
using UnityEngine;

/// <summary>2026-08 (creator direction, verbatim: "I need to see the
/// image rotating on the roof" -- a follow-up on the brass roof
/// platform's own "the platform should always be there even when there
/// is no monster on the roof!" direction): the Lab-exported portrait of
/// whatever's currently at the front of the build queue (<see
/// cref="GrabCursor.ProductionQueue"/>'s own `PortraitPng`, the SAME
/// field <see cref="ProductionQueueHud"/>'s queue tiles already show)
/// now floats above the permanent brass roof platform (<see
/// cref="BaseDresser.SpawnRoofDisplayPlatform"/>) as a slowly spinning
/// vertical hologram card, for as long as production is actually
/// running -- unlike <see cref="MonsterAgent"/>'s own roof-display beat,
/// this needs no specimen to have ever been physically dropped there,
/// so it's the one piece of "what's being built" that's visible on an
/// empty roof mid-production.
///
/// Double-sided (`_Cull` off, same as `PropLibrary`'s own transparent
/// variants) so the image still reads as it spins past edge-on, rather
/// than vanishing for half of every rotation the way a normal
/// backface-culled quad would.
///
/// One instance total (mirrors <see cref="ProductionQueueHud"/>'s own
/// single-queue, single-badge assumption via <see
/// cref="GrabCursor.FindAnyOwnCompleteFactory"/> -- this project has no
/// per-Factory queue model yet, so there is exactly one hologram to
/// show, anchored over whichever Factory that queue is actually
/// draining into).</summary>
public class RoofPortraitHologram : MonoBehaviour
{
    public GrabCursor grabCursor;

    private const float SpinDegPerSec = 30f;

    // matches BaseDresser.SpawnRoofDisplayPlatform's own
    // platformHalfHeight (0.22) * 2 -- the permanent platform's real top
    // face height above the roof surface -- plus clearance so the
    // hologram floats visibly above the platform's own rivets and the
    // monster-triggered glow disc instead of intersecting either.
    private const float HologramHeightAboveRoof = 0.9f;
    private const float HologramSize = 2.4f;

    private GameObject _quad;
    private Material _mat;
    private string _lastPortraitPng;

    public void Init(GrabCursor grabCursorRef)
    {
        grabCursor = grabCursorRef;
    }

    private void Update()
    {
        if (grabCursor == null || grabCursor.builder == null || !grabCursor.HasQueuedProduction)
        {
            SetVisible(false);
            return;
        }

        string frontPortrait = null;
        foreach (var item in grabCursor.ProductionQueue) { frontPortrait = item.PortraitPng; break; }
        var portrait = PortraitTexture.For(frontPortrait);
        if (portrait == null) { SetVisible(false); return; }

        var factory = grabCursor.FindAnyOwnCompleteFactory();
        if (factory == null) { SetVisible(false); return; }

        EnsureQuad();
        SetVisible(true);
        if (frontPortrait != _lastPortraitPng)
        {
            _mat.mainTexture = portrait;
            _lastPortraitPng = frontPortrait;
        }

        var faction = grabCursor.bridge != null ? grabCursor.bridge.PlayerFaction(factory.PlayerIndex) : FactionId.Mixed;
        var roofWorld = grabCursor.builder.WorldOf(factory.Hex);
        roofWorld.y = grabCursor.builder.GroundHeightAt(roofWorld)
            + BaseDresser.RoofHeightFor(factory.Kind, faction) + HologramHeightAboveRoof;
        _quad.transform.position = roofWorld;
        _quad.transform.Rotate(Vector3.up, SpinDegPerSec * Time.deltaTime, Space.World);
    }

    private void EnsureQuad()
    {
        if (_quad != null) return;

        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quad.name = "RoofPortraitHologram";
        var collider = _quad.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        _quad.transform.localScale = Vector3.one * HologramSize;

        _mat = new Material(ShaderUtil.FindRenderableShader());
        _mat.color = new Color(1f, 1f, 1f, 0.9f);
        _mat.EnableKeyword("_EMISSION");
        _mat.SetColor("_EmissionColor", Color.white * 0.5f);   // a touch of glow so it reads clearly at night, without washing the portrait out
        LabMeshBuilder.MakeTransparent(_mat);
        if (_mat.HasProperty("_Cull")) _mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        var renderer = _quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = _mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_quad != null) _quad.SetActive(visible);
    }
}
