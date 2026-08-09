using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "I need a quick way for the player to
/// recognize a monster is a collection unit in both the lab and game"):
/// a small floating badge over every ON-SCREEN monster with real onboard
/// harvest capacity (<see cref="MonsterAgent.IsHarvester"/>) -- ALWAYS
/// visible, not battle-gated like <see cref="HealthBars"/> (a harvester's
/// whole point is foraging in peacetime, so the marker needs to read at a
/// glance without selecting or fighting anything). Same IMGUI billboard
/// idiom as HealthBars: project world position to screen, draw directly
/// with OnGUI -- no real icon sprite art in this project, so a colored
/// swatch + a short abbreviation ("H") IS the badge, same idiom
/// <see cref="SelectionHud"/>/<see cref="BuildingNavHud"/> already use.
/// A drop of the harvest tank's own fill fraction is shown as the badge's
/// own fill bar underneath, reusing <see cref="MonsterAgent.
/// TotalCarriedLoad"/>-style data through a small public accessor rather
/// than duplicating the capacity math.
///
/// 2026-08 follow-up (creator direction: "create a category in the lab
/// stable and in game called scavengers"): this badge is that category's
/// in-game marker -- the letter reads "S" for Scavenger now (the Stable
/// uses the same name for the same units, see `isHarvesterGenome` in
/// site/main.js), even though the underlying gate is still
/// <see cref="MonsterAgent.IsHarvester"/>/<see cref="MonsterAgent.
/// IsScavenger"/> (identical check, two names for the same capability).
///
/// 2026-08 (creator direction: "the ui is not scaling properly to screen
/// sizes"): deliberately NOT wrapped in UiScale's scale -- every position
/// here comes from Camera.WorldToScreenPoint, already true screen pixels
/// regardless of resolution; wrapping it would double-apply the scale
/// and detach the badge from the monster it's meant to float over. See
/// UiScale.cs's own header for the full "screen-space panel vs world-
/// anchored" distinction.
/// </summary>
public class HarvesterMarkerHud : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private static Texture2D _tex;

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
    }

    private void OnGUI()
    {
        if (_builder == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (_tex == null) _tex = Texture2D.whiteTexture;

        const float badgeSize = 16f;
        const float barWidth = 22f;
        const float barHeight = 4f;

        foreach (var m in _builder.Monsters)
        {
            if (m == null || !m.IsHarvester) continue;

            // 2026-08 (creator report: "the indicator on the monsters
            // should be attached to body not to ground indicator...
            // especially important for flying units"): m.transform stays
            // ground-locked in flight (altitude lives on the torso only)
            // -- add FlightLift so an airborne harvester's badge floats
            // at its actual body, not the ground far below it.
            var world = m.transform.position + Vector3.up * (m.Radius + 1.6f + m.FlightLift);
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) continue;   // behind the camera

            var x = sp.x - badgeSize * 0.5f;
            var y = Screen.height - sp.y;   // screen (bottom-left) -> GUI (top-left)

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, badgeSize + 2f, badgeSize + 2f), _tex);
            GUI.color = new Color(0.32f, 0.28f, 0.38f, 1f);   // same muted-violet "Mad Doctor apparatus" hue Collector.cs's own hull uses
            GUI.DrawTexture(new Rect(x, y, badgeSize, badgeSize), _tex);

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(x, y - 1f, badgeSize, badgeSize), "S");
            GUI.color = new Color(0.9f, 0.85f, 0.95f, 1f);
            GUI.Label(new Rect(x, y - 2f, badgeSize, badgeSize), "S");

            var fill = m.HarvestFillFraction;
            var barX = sp.x - barWidth * 0.5f;
            var barY = y + badgeSize + 2f;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(barX - 1f, barY - 1f, barWidth + 2f, barHeight + 2f), _tex);
            GUI.color = new Color(0.25f, 0.2f, 0.3f, 0.9f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), _tex);
            GUI.color = new Color(0.75f, 0.35f, 0.75f, 1f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth * fill, barHeight), _tex);
        }
        GUI.color = Color.white;
    }
}
