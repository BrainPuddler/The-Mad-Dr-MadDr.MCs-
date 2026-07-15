using UnityEngine;

/// <summary>
/// Floating health bars, drawn only for combatants that are IN BATTLE
/// (fired or took a hit in the last few seconds) so the peaceful roster
/// isn't cluttered with bars. IMGUI billboard: project each unit's aim
/// point to screen and draw a red backing with a green (monster) or amber
/// (tank) fill. Same OnGUI-is-fine-with-the-new-Input-System note as
/// HudStatus.
/// </summary>
public class HealthBars : MonoBehaviour
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

        const float w = 46f;
        const float h = 6f;
        foreach (var c in _builder.Combatants)
        {
            if (c == null || !c.Alive || !c.InBattle) continue;
            var world = c.AimPoint + Vector3.up * (c.Radius + 0.9f);
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) continue;   // behind the camera

            var x = sp.x - w * 0.5f;
            var y = Screen.height - sp.y;   // screen (bottom-left) -> GUI (top-left)

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, w + 2f, h + 2f), _tex);
            GUI.color = new Color(0.5f, 0.06f, 0.06f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, w, h), _tex);
            GUI.color = c.Faction == "human"
                ? new Color(0.92f, 0.72f, 0.20f, 1f)
                : new Color(0.35f, 0.9f, 0.42f, 1f);
            GUI.DrawTexture(new Rect(x, y, w * c.HealthFraction, h), _tex);
        }
        GUI.color = Color.white;
    }
}
