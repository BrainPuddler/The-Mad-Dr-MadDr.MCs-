using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-07 amendment (docs/12, docs/23 §1 2026-07 update): "player first
/// must choose faction from one of the races or the mixed" -- a pre-match
/// screen offering the four <see cref="FactionId"/> options, wired the
/// exact same way <see cref="RegionPickerHud"/> already established for
/// the region choice: an opt-in component (<see cref="RuntimeCityBuilder.
/// showFactionPicker"/>, off by default so every existing scene keeps
/// working byte-for-byte unchanged), IMGUI, centered on screen (no
/// city/camera exists yet), sets one field on the builder then calls back
/// into the SAME entry point every non-picker scene already uses.
///
/// Shown BEFORE the region picker when both are enabled (<see
/// cref="RuntimeCityBuilder.Start"/>'s own ordering) -- the creator's own
/// words put faction first ("player first must choose faction... [then]
/// choose your city" reads naturally in that order), and a chosen
/// faction's bonuses/handicaps don't depend on which city gets picked
/// next, so there's no correctness reason either could not go first --
/// this is a deliberate UX ordering choice, not a technical constraint.
///
/// <see cref="FactionId.Mixed"/> is drawn greyed-out and unclickable
/// unless <see cref="MixedFactionUnlock.IsUnlocked"/> -- see that class's
/// own header for why it's gated and what still needs to call
/// MarkUnlocked for real.
/// </summary>
public class FactionPickerHud : MonoBehaviour
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

    // Blurbs summarize FactionLumenTable's real docs/23 §7 numbers, not
    // invented flavor text -- see FactionLumenModifier.cs for the source.
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

        DrawShadowedLabel(new Rect(panelRect.x, panelRect.y + Gap * 0.5f, panelRect.width, TitleHeight), "Choose your faction", new Color(0.95f, 0.9f, 0.7f, 1f));

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
        _builder.chosenFaction = choice;

        // same chaining shape RegionPickerHud.Confirm already established:
        // if the opponent-faction picker is ALSO enabled, show it next
        // (2026-08 -- both are "which faction" questions, naturally
        // grouped before the region picker's "which city"); otherwise
        // fall through to the region-picker-or-BeginMatch check exactly
        // as before this addition.
        if (_builder.showOpponentPicker)
        {
            var opponent = _builder.gameObject.GetComponent<OpponentFactionPickerHud>();
            if (opponent == null) opponent = _builder.gameObject.AddComponent<OpponentFactionPickerHud>();
            opponent.Init(_builder);
        }
        else if (_builder.showRegionPicker)
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
