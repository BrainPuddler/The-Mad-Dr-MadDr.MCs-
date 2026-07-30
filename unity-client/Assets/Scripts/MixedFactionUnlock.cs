using UnityEngine;

/// <summary>
/// 2026-07 amendment (docs/12, docs/23 §1 2026-07 update): FactionId.Mixed
/// is a real starting-pick faction now, but the creator's own words gate
/// it: "yes it will be an achievement after winning the campaign." A
/// persistent, account-level unlock -- PlayerPrefs, the standard Unity
/// mechanism for a flag that must survive between sessions without a
/// server round-trip, same tier of persistence this project already
/// trusts CityGizmo's Scene-view-only state NOT to need (this one
/// genuinely does, since it must outlive the Editor session).
///
/// Honesty note: this file only wires the FLAG and its unlock hook. A
/// "campaign" mode itself does not exist anywhere in this codebase yet
/// (docs/01/docs/12/docs/17 all mention it only as a future/open Phase-4
/// idea, never a built feature -- grep the whole repo for "campaign" and
/// every hit is a design doc, not code). Rather than fake a campaign
/// system to have something call <see cref="MarkUnlocked"/>, this is
/// left as a real, flagged gap: whatever eventually implements "won a
/// campaign" should call <see cref="MarkUnlocked"/> once, exactly the
/// same "don't invent the missing prerequisite, just wire the real seam"
/// discipline <see cref="RuntimeCityBuilder.SpawnCollector"/>'s own doc
/// comment already established for the Big-Brain-production gap. Until
/// then Mixed simply stays locked for every player, which is the correct,
/// non-broken default -- not a stub that silently unlocks nothing while
/// pretending to gate something.
/// </summary>
public static class MixedFactionUnlock
{
    private const string PrefKey = "MadDr.MixedFactionUnlocked";

    /// <summary>True once <see cref="MarkUnlocked"/> has ever been called
    /// on this device/account. Defaults to false (locked) for a player who
    /// has never won a campaign -- and, today, for EVERY player, since
    /// nothing yet calls MarkUnlocked (see this class's own header).</summary>
    public static bool IsUnlocked => PlayerPrefs.GetInt(PrefKey, 0) != 0;

    /// <summary>Persist the unlock. Idempotent -- safe to call every time
    /// a campaign win fires, not just the first.</summary>
    public static void MarkUnlocked()
    {
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();
    }
}
