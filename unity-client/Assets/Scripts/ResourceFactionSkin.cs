using MadDr.MatchCore;

/// <summary>
/// 2026-08 (creator direction: "Steel is Bone" -- part of the same round
/// that asked for a real Fuel income mechanic). The resource-kind twin of
/// <see cref="BuildingFactionSkin"/>: a display NAME per (kind, faction),
/// same "sim only ever reasons about the generic kind, only names are
/// themed" split that class's own header already established for
/// buildings. Bones/Steel is the only kind that actually changes name --
/// Blood/Fuel/Ichor are already faction-specific by construction (docs/05's
/// own energy-currency split: a Human Army player never sees Blood/Ichor
/// change into anything, they simply never earn them), and Parts/Brains
/// are shared, faction-neutral currencies with no fictional reason to
/// rename per faction the way "scavenged war matériel" (Bones, for a
/// creature-breeding faction) reasonably becomes "Steel" (Bones, for an
/// industrial army) without changing what the resource actually IS.
/// </summary>
public static class ResourceFactionSkin
{
    public static string NameFor(ResourceKind kind, FactionId faction)
    {
        if (kind == ResourceKind.Bones && faction == FactionId.HumanArmy) return "Steel";
        return kind.ToString();
    }
}
