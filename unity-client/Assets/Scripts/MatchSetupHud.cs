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
/// AI opponents can now be any of the four races (2026-08, creator
/// direction: "the roster needs to be able to generate enemies for all
/// races") -- <see cref="ArmyGenerator"/> gained real roster data for
/// MadDoctor (a flagged generic-creature placeholder, not the real
/// bred-creature pipeline) and Mixed (the union of every faction's
/// roster, reusing the same per-unit RaceOverride resolution a Mixed
/// HUMAN player already gets). Mixed is still gated behind <see
/// cref="MixedFactionUnlock.IsUnlocked"/> for AI opponents too, same as
/// the human's own race row just above -- an unearned unlock shouldn't
/// let the player fight it before they can play it.
///
/// Each opponent slot also gets a Difficulty button (2026-08, creator
/// direction: "scale the ai intelligence for Difficulty... in tutorial
/// and early levels players can get a sense of achievement") -- a
/// SKILL dial (<see cref="AiDifficulty"/>), separate from Personality's
/// FLAVOR dial, so a "Reckless" opponent reads the same at every
/// difficulty, just executed better or worse. Defaults to Normal, which
/// reproduces every pre-2026-08 opponent's exact behavior.
///
/// A Match Length row (2026-08, creator direction: "Start of game add a
/// game duration selector 15,30,45 minutes or unlimited") sits with "own
/// race" -- a MATCH-level setting, not per-opponent -- cycling
/// <see cref="RuntimeCityBuilder.matchDurationMinutes"/> through 15/30/45/
/// Unlimited (0 = Unlimited, matching that field's own sentinel), which
/// <see cref="RuntimeCityBuilder.BeginMatch"/> converts to <see
/// cref="MatchState.TimeCapTicks"/> (null for Unlimited) before starting
/// the match.
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

    // All 4 real choices when Mixed is unlocked; MadDoctor/HumanArmy/
    // AlienHive only otherwise -- Mixed AI opponents are gated the same
    // way the human's own Mixed race row is (see this file's own
    // header). Built once per Init() (a fresh match-setup screen), not a
    // static field any more, since the unlock state can change between
    // sessions.
    private FactionId[] _aiFactionChoices;

    // CommanderPersonality.Archetypes() order, plus a trailing "Random"
    // slot this UI adds on top.
    private static readonly string[] PersonalityNames = { "Balanced", "Berserker", "Turtle", "Hoarder", "Warlord", "Opportunist", "Random" };

    // 2026-08 (creator direction: "scale the ai intelligence for
    // Difficulty"): same order as the AiDifficulty enum itself (Tutorial=0
    // .. Brutal=4), deliberately -- DifficultyChoice casts straight to
    // AiDifficulty with no translation table needed, one less place for
    // the UI and the enum to drift apart. No trailing "Random" entry --
    // unlike faction/personality, an AI opponent's SKILL isn't something
    // a player picking difficulty would ever want left to chance.
    private static readonly string[] DifficultyNames = { "Tutorial", "Easy", "Normal", "Hard", "Brutal" };

    // 2026-08 (creator direction: "Start of game add a game duration
    // selector 15,30,45 minutes or unlimited"): a MATCH-level setting
    // (one choice for the whole match, not per-opponent), so it lives in
    // the top section alongside "own race" rather than inside the
    // per-opponent row loop below. Index-parallel with DurationMinutes --
    // 0 there is RuntimeCityBuilder.matchDurationMinutes's own "Unlimited"
    // sentinel (see that field's own doc comment).
    private static readonly string[] DurationNames = { "15 min", "30 min", "45 min", "Unlimited" };
    private static readonly int[] DurationMinutes = { 15, 30, 45, 0 };

    private struct OpponentSlot
    {
        /// <summary>Index into <see cref="_aiFactionChoices"/>, or
        /// <see cref="_aiFactionChoices"/>.Length for "Random".</summary>
        public int FactionChoice;
        /// <summary>Index into <see cref="CommanderPersonality.Archetypes"/>,
        /// or that list's Count for "Random".</summary>
        public int PersonalityChoice;
        /// <summary>Index into <see cref="DifficultyNames"/> -- casts
        /// directly to <see cref="AiDifficulty"/>, see that array's own
        /// comment.</summary>
        public int DifficultyChoice;
    }

    public const int MinOpponents = 1;
    public const int MaxOpponents = 4;

    private RuntimeCityBuilder _builder;
    private bool _confirmed;
    private FactionId _ownRace = FactionId.MadDoctor;
    private int _durationChoice;
    private readonly List<OpponentSlot> _opponents = new List<OpponentSlot>();

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        _confirmed = false;
        _ownRace = builder.chosenFaction;
        // Resolve builder.matchDurationMinutes to its own index in
        // DurationMinutes -- falls back to index 0 (15 min) if the
        // Inspector value doesn't exactly match one of the four menu
        // options (e.g. a developer typed a custom number directly),
        // same "menu wins outright once it's shown" contract every other
        // field in this class already has via Init's own overwrite.
        _durationChoice = 0;
        for (var i = 0; i < DurationMinutes.Length; i++)
            if (DurationMinutes[i] == builder.matchDurationMinutes) { _durationChoice = i; break; }
        // HumanArmy stays index 0 (existing default-slot behavior below
        // relies on it); MadDoctor/Mixed appended after so a save/replay
        // of an old 2-choice config still resolves the same faction at
        // the same index.
        _aiFactionChoices = MixedFactionUnlock.IsUnlocked
            ? new[] { FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor, FactionId.Mixed }
            : new[] { FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor };
        _opponents.Clear();
        // one HumanArmy/Balanced/Normal slot to start -- DifficultyChoice
        // 2 is AiDifficulty.Normal, same "index == enum value" contract
        // DifficultyNames documents.
        _opponents.Add(new OpponentSlot { FactionChoice = 0, PersonalityChoice = 0, DifficultyChoice = (int)AiDifficulty.Normal });
    }

    // 2026-08: widened from 460 to fit the new per-opponent Difficulty
    // button (see the opponent-row layout below) without cramping the
    // existing Faction/Personality buttons.
    private const float PanelWidth = 560f;
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

        // own race (1 row of 4 buttons) + match-length row + AI opponents
        // header + up to 4 opponent rows + add/remove row + Begin Match
        // button.
        var opponentRows = _opponents.Count * (RowHeight + Gap);
        var panelHeight = TitleHeight + SectionGap
            + RowHeight + Gap                       // own race row
            + RowHeight + Gap + SectionGap          // match-length row
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
        y += RowHeight + Gap;

        // ---- match length (2026-08: "add a game duration selector
        // 15,30,45 minutes or unlimited") -- a whole-match setting, so it
        // sits here with "own race" rather than per-opponent below. ----
        var durationLabelWidth = 110f;
        var durationLabelRect = new Rect(panelRect.x + Gap, y + 6f, durationLabelWidth, RowHeight);
        GUI.Label(durationLabelRect, "Match Length:");
        var durationButtonRect = new Rect(durationLabelRect.xMax, y, PanelWidth - durationLabelWidth - Gap * 2f, RowHeight);
        if (GUI.Button(durationButtonRect, DurationNames[_durationChoice]))
            _durationChoice = (_durationChoice + 1) % DurationNames.Length;
        y += RowHeight + SectionGap;

        // ---- AI opponent slots ----
        DrawShadowedLabel(new Rect(panelRect.x, y, panelRect.width, TitleHeight),
            $"AI Opponents ({_opponents.Count}/{MaxOpponents})", new Color(0.95f, 0.9f, 0.7f, 1f));
        y += TitleHeight;

        for (var i = 0; i < _opponents.Count; i++)
        {
            var slot = _opponents[i];
            var rowRect = new Rect(panelRect.x + Gap, y, PanelWidth - Gap * 2f, RowHeight);

            var labelWidth = 70f;
            var factionWidth = 120f;
            var personalityWidth = 130f;
            var difficultyWidth = 110f;
            var removeWidth = rowRect.width - labelWidth - factionWidth - personalityWidth - difficultyWidth - Gap * 4f;

            GUI.Label(new Rect(rowRect.x, rowRect.y + 6f, labelWidth, RowHeight), $"Opponent {i + 1}:");

            var factionRect = new Rect(rowRect.x + labelWidth + Gap, rowRect.y, factionWidth, RowHeight);
            var factionLabel = slot.FactionChoice < _aiFactionChoices.Length
                ? FactionDef.Get(_aiFactionChoices[slot.FactionChoice]).DisplayName
                : "Random";
            if (GUI.Button(factionRect, factionLabel))
                slot.FactionChoice = (slot.FactionChoice + 1) % (_aiFactionChoices.Length + 1);

            var personalityRect = new Rect(factionRect.xMax + Gap, rowRect.y, personalityWidth, RowHeight);
            if (GUI.Button(personalityRect, PersonalityNames[slot.PersonalityChoice]))
                slot.PersonalityChoice = (slot.PersonalityChoice + 1) % PersonalityNames.Length;

            var difficultyRect = new Rect(personalityRect.xMax + Gap, rowRect.y, difficultyWidth, RowHeight);
            if (GUI.Button(difficultyRect, DifficultyNames[slot.DifficultyChoice]))
                slot.DifficultyChoice = (slot.DifficultyChoice + 1) % DifficultyNames.Length;

            _opponents[i] = slot;

            GUI.enabled = _opponents.Count > MinOpponents;
            var removeRect = new Rect(difficultyRect.xMax + Gap, rowRect.y, removeWidth, RowHeight);
            if (GUI.Button(removeRect, "-")) { _opponents.RemoveAt(i); GUI.enabled = true; break; }
            GUI.enabled = true;

            y += RowHeight + Gap;
        }

        GUI.enabled = _opponents.Count < MaxOpponents;
        var addRect = new Rect(panelRect.x + Gap, y, PanelWidth - Gap * 2f, ButtonHeight);
        if (GUI.Button(addRect, "+ Add Opponent")) _opponents.Add(new OpponentSlot { FactionChoice = 0, PersonalityChoice = 0, DifficultyChoice = (int)AiDifficulty.Normal });
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
        _builder.matchDurationMinutes = DurationMinutes[_durationChoice];
        _builder.aiOpponents.Clear();
        for (var i = 0; i < _opponents.Count; i++)
        {
            var slot = _opponents[i];
            // Fold the slot index into the seed so opponent 1 and opponent
            // 2 don't resolve identically when both are set to Random --
            // same decorrelation formula AiMatchDriver/SpawnOpponentStartingArmy
            // use for their own per-player streams.
            var slotSeed = unchecked((uint)_builder.seed) ^ unchecked((uint)((i + 1) * 0x9E3779B1));

            var faction = slot.FactionChoice < _aiFactionChoices.Length
                ? _aiFactionChoices[slot.FactionChoice]
                : _aiFactionChoices[new SimRng(slotSeed).IntRange(_aiFactionChoices.Length)];

            var personality = slot.PersonalityChoice < CommanderPersonality.Archetypes.Count
                ? CommanderPersonality.Archetypes[slot.PersonalityChoice]
                : CommanderPersonality.Generate(slotSeed);

            var difficulty = (AiDifficulty)slot.DifficultyChoice;

            _builder.aiOpponents.Add(new RuntimeCityBuilder.AiOpponentConfig(faction, personality, difficulty));
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
