using UnityEngine;

/// <summary>
/// Continuous rotation around a local axis at a fixed rate -- the shared
/// "small mechanical/energy element visibly moves" building block reused
/// across every faction's Factory/Control Centre visual treatment
/// (docs/12 2026-08 "apply Big Brain-level refinement to Factory/Control
/// Centre, per faction" pass): the Mad Doctor faction's flywheels/gears,
/// the Human Alliance's radar dishes, the Alien faction's orbiting
/// crystal rings. Deliberately tiny and generic -- no ParticleSystem,
/// no Inspector-configured module graph, matching this project's own
/// established "hand-rolled Update-driven animation, no Editor to verify
/// a complex component graph" convention (see DamageFx.cs's own class
/// header, and BrainJarBubbles.cs/EerieChamberGlow.cs for the same
/// idiom applied to the earlier Big Brain pass).
/// </summary>
public class SlowSpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 20f;

    private void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
