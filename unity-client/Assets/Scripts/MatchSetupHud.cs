using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/30 (selectable races + AI opponents, "the menu should allow the
/// user to choose race and ai opponents, then enable a begin match
/// button"): ONE combined pre-match screen -- the human's own race, then
/// 1-4 AI opponent slots (each its own race + personality), then a single
/// Begin Match button -- replacing the old two-screen <c>FactionPickerHud
/// </c> -> <c>OpponentFactionPickerHud</c> chain (both retired, single-
/// opponent-only and never expressed as one menu with a real confirm
/// button the way the creator asked for). Same conventions every prior
/// picker in this family already established: <see cref="UiScale"/>-scaled
/// IMGUI, opt-in via <see cref="RuntimeCityBuilder.showMatchSetupHud"/>
/// (off by default), chains forward into <see cref="RegionPickerHud"/> or
/// straight to <see cref="RuntimeCityBuilder.BeginMatch"/> on confirm, same
/// as <see cref="RegionPickerHud"/>/the old faction pickers did.
///
/// AI opponent races are restricted to HumanArmy/AlienHive (plus Random,
/// which only ever resolves to one of those two) -- <see
/// cref="ArmyGenerator"/> has no unit-roster data for MadDoctor (bred
/// creatures, never a fixed list) or Mixed (resolves race per-unit a
/// different way), so an AI opponent fielded as either could never
/// generate a starting army or make production decisions. A pre-existing
/// constraint this menu surfaces honestly rather than offering an option
/// that would silently do nothing.
/// </summary>
public class MatchSetupHud : MonoBehaviour
{
    private struct RaceOption
    {
        public FactionId Id;
        public string Blurb;
        public RaceOption(FactionId id, string blurb) { Id = id; Blurb = blurb; }
    }

    // Same blurbs FactionPickerHud used -- FactionLumenTable's real docs/23
    // §7 numbers, not invented flavor text.
    private static readonly RaceOption[] RaceOptions =
    {
        new RaceOption(FactionId.MadDoctor, "Day: -10% regen. Night: +15% regen, +10% speed."),
        new RaceOption(FactionId.HumanArmy, "Day: +15% weapon damage. Night: -15% vision."),
        new RaceOption(FactionId.AlienHive, "Day: -10% speed. Dusk/Dawn: +15% Ichor income."),
        new RaceOption(FactionId.Mixed, "Field any race's units -- each keeps ITS OWN bonuses/handicaps. No faction-wide bonus of its own."),
    };

    // AI opponents only ever get HumanArmy/AlienHive/Random -- see this
    // file's own header for why MadDoctor/Mixed aren't offered here.
    private static readonly FactionId[] AiFactionChoices = { FactionId.HumanArmy, FactionId.AlienHive };

    // CommanderPersonality.Archetypes() order, plus a trailing "Random"
    // slot this UI adds on top.
    private static readonly string[] PersonalityNames = { "Balanced", "Berserker", "Turtle", "Hoarder", "Warlord", "Opportunist", "Random" };

    private struct OpponentSlot
    {
        /// <summary>Index into <see cref="AiFactionChoices"/>, or
        /// <see cref="AiFactionChoices"/>.Length for "Random".</summary>
        public int FactionChoice;
        /// <summary>Index into <see cref="CommanderPersonality.Archetypes"/>,
        /// or that list's Count for "Random".</summary>
        public int PersonalityChoice;
    }

    public const int MinOpponents = 1;
    public const int MaxOpponents = 4;

    private RuntimeCityBuilder _builder;
    private bool _confirmed;
    private FactionId _ownRace = FactionId.MadDoctor;
    private readonly List<OpponentSlot> _opponents = new List<OpponentSlot>();

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        _confirmed = false;
        _ownRace = builder.chosenFaction;
        _opponents.Clear();
        _opponents.Add(new OpponentSlot { FactionChoice = 0, PersonalityChoice = 0 }); // one HumanArmy/Balanced slot to start
    }

    private const float PanelWidth = 460f;
    private const float RowHeight = 40f;
    private const float ButtonHeight = 30f;
    private const float Gap = 8f;
    private const float TitleHeight = 34f;
    private const float SectionGap = 16f;
    private const float SwatchSize = 14f;

    private void OnGUI()
    {
        if (_builder == null || _confirmed) return;
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;

        // own race (1 row of 4 buttons) + AI opponents header + up to 4
        // opponent rows + add/remove row + Begin Match button.
        var opponentRows = _opponents.Count * (RowHeight + Gap);
        var panelHeight = TitleHeight + SectionGap
            + RowHeight + Gap + SectionGap        // own race row
            + TitleHeight + opponentRows            // "AI Opponents" + slots
            + ButtonHeight + Gap                    // add/remove row
            + SectionGap + ButtonHeight + Gap * 2f;  // Begin Match

        var panelRect = new Rect((screenW - PanelWidth) * 0.5f, Mathf.Max(20f, (screenH - panelHeight) * 0.5f), PanelWidth, panelHeight);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.85f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = panelRect.y + Gap * 0.5f;
        DrawShadowedLabel(new Rect(panelRect.x, y, panelRect.width, TitleHeight), "Choose your race", new Color(0.95f, 0.9f, 0.7f, 1f));
        y += TitleHeight;

        // ---- own race: one row of 4 swatch buttons ----
        var raceButtonWidth = (PanelWidth - Gap * 2f - Gap * 3f) / 4f;
        var x = panelRect.x + Gap;
        for (var i = 0; i < RaceOptions.Length; i++)
        {
            var opt = RaceOptions[i];
            var locked = opt.Id == FactionId.Mixed && !MixedFactionUnlock.IsUnlocked;
            var rect = new Rect(x, y, raceButtonWidth, RowHeight);
            var selected = opt.Id == _ownRace;

            DrawSwatch(new Rect(rect.x + 3f, rect.y + 3f, SwatchSize, SwatchSize), FactionDef.Get(opt.Id).ColorRgb);
            if (selected)
            {
                GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.35f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            GUI.enabled = !locked;
            if (GUI.Button(rect, ShortName(opt.Id) + (locked ? "\n(locked)" : "")) && !locked) _ownRace = opt.Id;
            GUI.enabled = true;

            x += raceButtonWidth + Gap;
        }
        y += RowHeight + SectionGap;

        // ---- AI opponent slots ----
        DrawShadowedLabel(new Rect(panelRect.x, y, panelRect.width, TitleHeight),
            $"AI Opponents ({_opponents.Count}/{MaxOpponents})", new Color(0.95f, 0.9f, 0.7f, 1f));
        y += TitleHeight;

        for (var i = 0; i < _opponents.Count; i++)
        {
            var slot = _opponents[i];
            var rowRect = new Rect(panelRect.x + Gap, y, PanelWidth - Gap * 2f, RowHeight);

            var labelWidth = 80f;
            var factionWidth = 140f;
            var personalityWidth = 150f;
            var removeWidth = rowRect.width - labelWidth - factionWidth - personalityWidth - Gap * 3f;

            GUI.Label(new Rect(rowRect.x, rowRect.y + 6f, labelWidth, RowHeight), $"Opponent {i + 1}:");

            var factionRect = new Rect(rowRect.x + labelWidth + Gap, rowRect.y, factionWidth, RowHeight);
            var factionLabel = slot.FactionChoice < AiFactionChoices.Length
                ? FactionDef.Get(AiFactionChoices[slot.FactionChoice]).DisplayName
                : "Random";
            if (GUI.Button(factionRect, factionLabel))
                slot.FactionChoice = (slot.FactionChoice + 1) % (AiFactionChoices.Length + 1);

            var personalityRect = new Rect(factionRect.xMax + Gap, rowRect.y, personalityWidth, RowHeight);
            if (GUI.Button(personalityRect, PersonalityNames[slot.PersonalityChoice]))
                slot.PersonalityChoice = (slot.PersonalityChoice + 1) % PersonalityNames.Length;

            _opponents[i] = slot;

            GUI.enabled = _opponents.Count > MinOpponents;
            var removeRect = new Rect(personalityRect.xMax + Gap, rowRect.y, removeWidth, RowHeight);
            if (GUI.Button(removeRect, "-")) { _opponents.RemoveAt(i); GUI.enabled = true; break; }
            GUI.enabled = true;

            y += RowHeight + Gap;
        }

        GUI.enabled = _opponents.Count < MaxOpponents;
        var addRect = new Rect(panelRect.x + Gap, y, PanelWidth - Gap * 2f, ButtonHeight);
        if (GUI.Button(addRect, "+ Add Opponent")) _opponents.Add(new OpponentSlot { FactionChoice = 0, PersonalityChoice = 0 });
        GUI.enabled = true;
        y += ButtonHeight + Gap + SectionGap;

        // ---- Begin Match ----
        var beginRect = new Rect(panelRect.x + Gap, y, PanelWidth - Gap * 2f, ButtonHeight + 10f);
        GUI.enabled = _opponents.Count >= MinOpponents && _opponents.Count <= MaxOpponents;
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.35f);
        if (GUI.Button(beginRect, "Begin Match")) Confirm();
        GUI.backgroundColor = prevColor;
        GUI.enabled = true;

        UiScale.End(prevMatrix);
    }

    private void Confirm()
    {
        if (_confirmed || _builder == null) return;
        _confirmed = true;

        _builder.chosenFaction = _ownRace;
        _builder.aiOpponents.Clear();
        for (var i = 0; i < _opponents.Count; i++)
        {
            var slot = _opponents[i];
            // Fold the slot index into the seed so opponent 1 and opponent
            // 2 don't resolve identically when both are set to Random --
            // same decorrelation formula AiMatchDriver/SpawnOpponentStartingArmy
            // use for their own per-player streams.
            var slotSeed = unchecked((uint)_builder.seed) ^ unchecked((uint)((i + 1) * 0x9E3779B1));

            var faction = slot.FactionChoice < AiFactionChoices.Length
                ? AiFactionChoices[slot.FactionChoice]
                : AiFactionChoices[new SimRng(slotSeed).IntRange(AiFactionChoices.Length)];

            var personality = slot.PersonalityChoice < CommanderPersonality.Archetypes.Count
                ? CommanderPersonality.Archetypes[slot.PersonalityChoice]
                : CommanderPersonality.Generate(slotSeed);

            _builder.aiOpponents.Add(new RuntimeCityBuilder.AiOpponentConfig(faction, personality));
        }

        // Same chaining shape every prior picker in this family already
        // established: region picker next if it's on, otherwise straight
        // to BeginMatch.
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

    private static string ShortName(FactionId id)
    {
        switch (id)
        {
            case FactionId.MadDoctor: return "Mad Doctor";
            case FactionId.HumanArmy: return "Human Army";
            case FactionId.AlienHive: return "Alien Hive";
            case FactionId.Mixed: return "Mixed";
            default: return id.ToString();
        }
    }

    private static void DrawSwatch(Rect rect, int rgb)
    {
        var r = ((rgb >> 16) & 0xFF) / 255f;
        var g = ((rgb >> 8) & 0xFF) / 255f;
        var b = (rgb & 0xFF) / 255f;
        GUI.color = new Color(r, g, b);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
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
