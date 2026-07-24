using UnityEngine;

/// <summary>
/// docs/26 Phase 6: a caught non-heavy target's "being dragged toward
/// its captor" state. Deliberately its own small class rather than
/// fields baked directly into UnitCombat, because the exact same pull
/// logic has to be usable by <see cref="Citizen"/> too -- Citizen has no
/// UnitCombat component at all (docs/26 Phase 4 research: confirmed via
/// grep, not assumed). UnitCombat owns one instance for a combatant
/// target; Citizen owns a separate one for itself.
///
/// v0.1 scope: pull + report arrival. Reaching the captor holds the
/// victim there (TickPull stops once within ArriveRadius, and resumes
/// closing the gap if the captor itself walks further away); TickPull
/// returns true once arrived so a caller can act on it (docs/26 Phase 7:
/// Citizen does, consuming itself -- MonsterAgent/Tank don't yet, since
/// no generic "consume a UnitCombat" path exists).
/// </summary>
public sealed class CaptureState
{
    public const float ArriveRadius = 1.5f;

    public UnitCombat Captor { get; private set; }
    public float Speed { get; private set; }

    /// <summary>False once the captor has died -- lets the owner auto-
    /// release the victim instead of leaving it stuck pulling toward a
    /// corpse (docs/26 risk: "ability interrupted -- captor dies
    /// mid-cast").</summary>
    public bool Active { get { return Captor != null && Captor.Alive; } }

    public void Begin(UnitCombat captor, float speed)
    {
        Captor = captor;
        Speed = speed;
    }

    /// <summary>Moves `self` toward the captor at Speed, never
    /// overshooting -- the same MoveToward idiom Citizen already uses
    /// for its own locomotion. Returns true once within ArriveRadius.</summary>
    public bool TickPull(Transform self, float dt)
    {
        if (!Active) return false;
        var to = Captor.transform.position - self.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist <= ArriveRadius) return true;
        var dir = dist > 0.0001f ? to / dist : Vector3.zero;
        self.position += dir * Mathf.Min(Speed * dt, dist);
        return false;
    }
}
