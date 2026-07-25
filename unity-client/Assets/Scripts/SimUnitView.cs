using UnityEngine;

/// <summary>
/// docs/27 Phase A: the interpolated view of one `match-core` `SimUnit`.
/// Holds the last two tick snapshots (X/Z only -- the sim is ground-plane
/// only, Y stays Unity-owned terrain-follow, unchanged from every other
/// order kind) and renders a position between them by
/// <see cref="SimBridge.Alpha"/>, the one shared "how far into the
/// current tick interval are we" value every sim-driven unit's view
/// reads from the same place.
///
/// Deliberately holds NO accumulator of its own -- an earlier draft of
/// this design gave each view its own tick accumulator/alpha, which
/// risks two units' views drifting out of step with each other (and with
/// reality) for no benefit, since "how far between ticks" is a property
/// of the MATCH, not of any one unit. <see cref="SimBridge"/> computes it
/// once per frame; every view just reads it.
///
/// A component, not fields on MonsterAgent, on purpose (docs/27 §6.1):
/// keeps the sim-view concern physically separable -- strippable back out
/// with zero risk to the legacy path if this cutover needs reverting.
/// </summary>
public class SimUnitView : MonoBehaviour
{
    private double _prevX, _prevZ;
    private double _currX, _currZ;
    private bool _primed;

    /// <summary>Seed both snapshots to the same point -- called once,
    /// right after spawn, so the very first interpolated frame doesn't
    /// lerp FROM the origin or some other garbage previous position.</summary>
    public void Prime(double x, double z)
    {
        _prevX = _currX = x;
        _prevZ = _currZ = z;
        _primed = true;
    }

    /// <summary>Called by <see cref="SimBridge"/> once per completed sim
    /// tick: the previous "current" becomes the new "previous," and the
    /// fresh sim position becomes the new "current" -- exactly the two
    /// points this frame (and any frame before the next tick) will
    /// interpolate between.</summary>
    public void OnTick(double x, double z)
    {
        if (!_primed) { Prime(x, z); return; }
        _prevX = _currX;
        _prevZ = _currZ;
        _currX = x;
        _currZ = z;
    }

    /// <summary>Render this frame's interpolated position into `t` (Y
    /// untouched -- the caller's own terrain-follow owns it, same as
    /// every other order kind) and return the velocity that produced it,
    /// derived from the ACTUAL measured position delta divided by `dt` --
    /// never a separately-computed "intended" velocity that could
    /// disagree with what actually moved. Same idiom docs/26 Phase 6's
    /// `TickCaptured` already established for a directly-position-writing
    /// order.</summary>
    public Vector3 Advance(float alpha, float dt, Transform t)
    {
        var renderX = Mathf.Lerp((float)_prevX, (float)_currX, alpha);
        var renderZ = Mathf.Lerp((float)_prevZ, (float)_currZ, alpha);
        var before = t.position;
        var after = new Vector3(renderX, before.y, renderZ);
        t.position = after;
        return dt > 1e-5f ? (after - before) / dt : Vector3.zero;
    }
}
