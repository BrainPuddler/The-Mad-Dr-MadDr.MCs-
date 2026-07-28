using UnityEngine;

/// <summary>
/// docs/23 Phase 8's own still-open "region picker" item (see docs/00-
/// index's own Phase 8 status note: "Unity-side dressing... and a region
/// picker... explicitly deferred"). citygen-core has had real New York/
/// Paris/Montreal presets since Phase 8; nothing in Unity ever let a
/// player choose one -- <see cref="RuntimeCityBuilder.preset"/> was an
/// Inspector-only field a developer set before hitting Play, never a
/// runtime choice.
///
/// Only exists in a scene when <see cref="RuntimeCityBuilder.
/// showRegionPicker"/> opts in (off by default, so every existing
/// workflow -- the Inspector preset field, CityGizmo sync -- keeps
/// working byte-for-byte unchanged): <see cref="RuntimeCityBuilder.
/// Start"/> then adds this component and returns BEFORE doing any city
/// generation, camera setup, or roster fetch, all of which now live in
/// the extracted <see cref="RuntimeCityBuilder.BeginMatch"/>. Picking a
/// region here sets <see cref="RuntimeCityBuilder.preset"/> and calls
/// that same method -- the exact generation path every non-picker scene
/// already uses, not a second one.
///
/// IMGUI, same as every other HUD element in this project (see
/// HudStatus's header for why). Centered on screen since (unlike every
/// other HUD element here) there is no city/camera/gameplay yet for a
/// corner-anchored overlay to avoid overlapping.
/// </summary>
public class RegionPickerHud : MonoBehaviour
{
    private struct Option
    {
        public RuntimeCityBuilder.PresetChoice Choice;
        public string Label;
        public string Blurb;
        public Option(RuntimeCityBuilder.PresetChoice choice, string label, string blurb)
        {
            Choice = choice;
            Label = label;
            Blurb = blurb;
        }
    }

    private static readonly Option[] Options =
    {
        new Option(RuntimeCityBuilder.PresetChoice.Village, "Village", "1950s small-town grid, Main Street, a roundabout"),
        new Option(RuntimeCityBuilder.PresetChoice.SmallTown, "Small Town", "a modest downtown core"),
        new Option(RuntimeCityBuilder.PresetChoice.BigCity, "Big City", "dense high-rise downtown"),
        new Option(RuntimeCityBuilder.PresetChoice.NewYork, "New York", "1950s Manhattan-scale grid, named landmarks"),
        new Option(RuntimeCityBuilder.PresetChoice.Paris, "Paris", "boulevards, diagonal avenues, a grand roundabout"),
        new Option(RuntimeCityBuilder.PresetChoice.Montreal, "Montreal", "a distinct regional layout and landmark set"),
    };

    private RuntimeCityBuilder _builder;
    private bool _confirmed;

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        _confirmed = false;
    }

    private const float ButtonWidth = 260f;
    private const float ButtonHeight = 46f;
    private const float Gap = 10f;
    private const float TitleHeight = 40f;

    private void OnGUI()
    {
        if (_builder == null || _confirmed) return;

        var screenW = Screen.width > 0 ? Screen.width : 1920;
        var screenH = Screen.height > 0 ? Screen.height : 1080;

        var panelHeight = TitleHeight + Options.Length * (ButtonHeight + Gap) + Gap;
        var panelWidth = ButtonWidth + Gap * 2f;
        var panelRect = new Rect((screenW - panelWidth) * 0.5f, (screenH - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.8f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawShadowedLabel(new Rect(panelRect.x, panelRect.y + Gap * 0.5f, panelRect.width, TitleHeight), "Choose your city", new Color(0.95f, 0.9f, 0.7f, 1f));

        var y = panelRect.y + TitleHeight + Gap;
        var x = panelRect.x + Gap;
        for (var i = 0; i < Options.Length; i++)
        {
            var rect = new Rect(x, y, ButtonWidth, ButtonHeight);
            if (GUI.Button(rect, Options[i].Label + "\n" + Options[i].Blurb)) Confirm(Options[i].Choice);
            y += ButtonHeight + Gap;
        }
    }

    private void Confirm(RuntimeCityBuilder.PresetChoice choice)
    {
        if (_confirmed || _builder == null) return;
        _confirmed = true;
        _builder.preset = choice;
        _builder.BeginMatch();
        Object.Destroy(this);
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
