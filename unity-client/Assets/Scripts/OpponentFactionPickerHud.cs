using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "It should allow me to pick the opponent
/// race that I'd like from the four different factions available") -- a
/// pre-match screen offering all four <see cref="FactionId"/> options for
/// the single-player AI-antagonist, wired the exact same way <see
/// cref="FactionPickerHud"/> (the human's own faction choice) already
/// established: an opt-in component (<see cref="RuntimeCityBuilder.
/// showOpponentPicker"/>, off by default so every existing scene keeps
/// working byte-for-byte unchanged), IMGUI, centered on screen.
///
/// Shown AFTER <see cref="FactionPickerHud"/> (if that's also on) and
/// BEFORE <see cref="RegionPickerHud"/> when all three are enabled --
/// see <see cref="RuntimeCityBuilder.Start"/>'s own picker chain and
/// <see cref="FactionPickerHud.Confirm"/>'s own chaining for the exact
/// ordering. Sets <see cref="RuntimeCityBuilder.opponentFactionOverride"/>
/// then calls back into the same chain every other picker uses.
///
/// Deliberately does NOT exclude the human's own <see
/// cref="RuntimeCityBuilder.chosenFaction"/> from the option list -- a
/// mirror matchup (same race on both sides) is a real, legitimate RTS
/// choice, not a mistake to guard against. <see cref="FactionId.Mixed"/>
/// is drawn greyed-out and unclickable unless <see cref="
/// MixedFactionUnlock.IsUnlocked"/>, the same gate <see
/// cref="FactionPickerHud"/> already applies to the human's own pick --
/// an explicit player CHOICE to field a Mixed opponent is a real pick,
/// not the docs/12 Q13 "never Mixed" AI-default rule (which only governs
/// what happens when this picker is off).
/// </summary>
public class OpponentFactionPickerHud : MonoBehaviour
{
    private struct Option
    {
        public FactionId Id;
        public string Blurb;
        public Option(FactionId id, string blurb)
        {
            Id = id;
            Blurb = blurb;
        }
    }

    // Same blurb text FactionPickerHud uses -- these summarize
    // FactionLumenTable's real docs/23 §7 numbers, not invented flavor
    // text, and apply identically regardless of which side fields them.
    private static readonly Option[] Options =
    {
        new Option(FactionId.MadDoctor, "Day: -10% regen. Night: +15% regen, +10% speed."),
        new Option(FactionId.HumanArmy, "Day: +15% weapon damage. Night: -15% vision."),
        new Option(FactionId.AlienHive, "Day: -10% speed. Dusk/Dawn: +15% Ichor income."),
        new Option(FactionId.Mixed, "Field any race's units -- each keeps ITS OWN bonuses/handicaps. No faction-wide bonus of its own."),
    };

    private RuntimeCityBuilder _builder;
    private bool _confirmed;
    private int _hoveredIndex = 0;

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        _confirmed = false;
        _hoveredIndex = 0;
    }

    private const float ButtonWidth = 340f;
    private const float ButtonHeight = 54f;
    private const float Gap = 10f;
    private const float TitleHeight = 40f;
    private const float SwatchSize = 18f;

    private void OnGUI()
    {
        if (_builder == null || _confirmed) return;
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;

        var contentHeight = Options.Length * (ButtonHeight + Gap);
        var panelWidth = ButtonWidth + Gap * 2f;
        var panelHeight = TitleHeight + contentHeight + Gap * 2f;
        var panelRect = new Rect((screenW - panelWidth) * 0.5f, (screenH - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.85f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawShadowedLabel(new Rect(panelRect.x, panelRect.y + Gap * 0.5f, panelRect.width, TitleHeight), "Choose your opponent's faction", new Color(0.95f, 0.9f, 0.7f, 1f));

        var listX = panelRect.x + Gap;
        var listY = panelRect.y + TitleHeight + Gap;
        var e = Event.current;

        for (var i = 0; i < Options.Length; i++)
        {
            var opt = Options[i];
            var locked = opt.Id == FactionId.Mixed && !MixedFactionUnlock.IsUnlocked;
            var rect = new Rect(listX, listY, ButtonWidth, ButtonHeight);
            if (!locked && e != null && rect.Contains(e.mousePosition)) _hoveredIndex = i;

            var def = FactionDef.Get(opt.Id);
            var swatch = ColorFromRgb(def.ColorRgb);

            var highlighted = !locked && i == _hoveredIndex;
            if (highlighted)
            {
                GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.2f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = swatch;
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 6f, SwatchSize, SwatchSize), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.enabled = !locked;
            var label = def.DisplayName + (locked ? "  (locked -- win a campaign to unlock)" : "") + "\n" + opt.Blurb;
            var buttonRect = new Rect(rect.x + SwatchSize + 14f, rect.y, rect.width - SwatchSize - 14f, rect.height);
            if (GUI.Button(buttonRect, label) && !locked) Confirm(opt.Id);
            GUI.enabled = true;

            listY += ButtonHeight + Gap;
        }

        UiScale.End(prevMatrix);
    }

    private void Confirm(FactionId choice)
    {
        if (_confirmed || _builder == null) return;
        _confirmed = true;
        _builder.opponentFactionOverride = choice;

        // same chaining shape FactionPickerHud.Confirm already established
        // for its own tail: if the region picker is ALSO enabled, show it
        // next rather than duplicating BeginMatch's own call; otherwise
        // start the match directly.
        if (_builder.showRegionPicker)
        {
            var region = _builder.gameObject.GetComponent<RegionPickerHud>();
            if (region == null) region = _builder.gameObject.AddComponent<RegionPickerHud>();
            region.Init(_builder);
        }
        else
        {
            _builder.BeginMatch();
        }
        Object.Destroy(this);
    }

    private static Color ColorFromRgb(int rgb)
    {
        var r = ((rgb >> 16) & 0xFF) / 255f;
        var g = ((rgb >> 8) & 0xFF) / 255f;
        var b = (rgb & 0xFF) / 255f;
        return new Color(r, g, b);
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }
}
