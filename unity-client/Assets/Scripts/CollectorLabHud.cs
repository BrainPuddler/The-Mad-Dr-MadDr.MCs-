using MadDr.MatchCore;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2026-08 (docs/12 "Collector Lab classes" entry, creator direction:
/// "let's fix that, create collectors. I wanted to define them in the
/// lab, as a class. Like a battalion." ... "but also add a way to do it
/// in game"): the one panel that closes both halves of that ask.
///
/// Top section ("define... as a class"): name a loadout, pick its
/// Speed/Range/Trim tiers and its batch ("battalion") size, save it --
/// pure local data (<see cref="RuntimeCityBuilder.DefineCollectorClass"/>),
/// no genome, no server round trip (<see cref="CollectorClassDef"/>'s own
/// header explains why this differs from a genome Lab battalion
/// template).
///
/// Bottom section ("a way to do it in game"): every saved class gets a
/// "Train" row -- a real, gated Bones purchase
/// (<see cref="RuntimeCityBuilder.BeginCollectorBattalion"/>) against the
/// player's own nearest Complete Big Brain building, plus a live
/// progress readout for whatever's currently training. This is the
/// worker-economy epic's own long-flagged bootstrapping gap, finally
/// closed: before this, `SpawnCollector` was a manual/dev-only call with
/// no in-match production path at all.
///
/// Mad Doctor only (Collector is Mad-Doctor apparatus, docs/17) -- a
/// no-op for any other local faction, or before a match exists.
/// Collapsed to a small corner tab by default, toggled by the 'L' key
/// (mnemonic: Lab) or a click on the tab itself, top-left corner (every
/// other docked panel in this project claims the bottom edge around the
/// minimap or the top-right resource wallet, so top-left is clear).
/// </summary>
public class CollectorLabHud : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private int _playerIndex;

    [Header("Placement (top-left corner)")]
    public float marginPixels = 12f;
    public float tabWidth = 150f;
    public float tabHeight = 24f;
    public float panelWidth = 320f;
    public float rowHeight = 22f;

    public static bool PointerOver { get; private set; }

    private bool _open;
    private string _nameField = "Ravagers";
    private CollectorSpeedTier _speed = CollectorSpeedTier.Standard;
    private CollectorRangeTier _range = CollectorRangeTier.Standard;
    private CollectorTrim _trim = CollectorTrim.Standard;
    private int _batchSize = 3;

    public void Init(RuntimeCityBuilder builder, int playerIndex)
    {
        _builder = builder;
        _playerIndex = playerIndex;
    }

    private void Update()
    {
        if (_builder == null || _builder.SimBridge == null || !_builder.SimBridge.HasMatch) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.lKey.wasPressedThisFrame) _open = !_open;
    }

    private bool IsMadDoctor()
    {
        return _builder != null && _builder.SimBridge != null && _builder.SimBridge.HasMatch
            && _builder.SimBridge.PlayerFaction(_playerIndex) == FactionId.MadDoctor;
    }

    private void OnGUI()
    {
        if (!IsMadDoctor()) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        var tabRect = new Rect(marginPixels, marginPixels, tabWidth, tabHeight);
        var e = Event.current;
        PointerOver = e != null && tabRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(tabRect, "Collector Lab (L)")) _open = !_open;

        if (_open)
        {
            var panelRect = new Rect(marginPixels, tabRect.yMax + 4f, panelWidth, PanelHeight());
            if (e != null && panelRect.Contains(e.mousePosition)) PointerOver = true;
            DrawPanel(panelRect);
        }

        UiScale.End(prevMatrix);
    }

    private float PanelHeight()
    {
        // Fixed define-section rows (label/name/tiers/batch/cost/save) +
        // one header row per NON-EMPTY list section plus its rows --
        // mirrors DrawPanel's own conditionals exactly, since an empty
        // classes or orders list draws no header at all.
        const int fixedRows = 6;
        var classCount = _builder.CollectorClasses.Count;
        var orderCount = _builder.CollectorOrders.Count;
        var classRows = classCount > 0 ? 1 + classCount : 0;
        var orderRows = orderCount > 0 ? 1 + orderCount : 0;
        var gaps = (classCount > 0 ? 4f : 0f) + (orderCount > 0 ? 4f : 0f);
        return rowHeight * (fixedRows + classRows + orderRows) + gaps + 16f;
    }

    private void DrawPanel(Rect panelRect)
    {
        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.78f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = panelRect.x + 8f;
        var y = panelRect.y + 6f;
        var innerWidth = panelRect.width - 16f;

        DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), "Define a Collector class:");
        y += rowHeight;

        var nameRect = new Rect(x, y, innerWidth, rowHeight - 4f);
        _nameField = GUI.TextField(nameRect, _nameField, 24);
        y += rowHeight;

        var third = innerWidth / 3f;
        if (GUI.Button(new Rect(x, y, third - 2f, rowHeight - 4f), "Speed: " + _speed))
            _speed = _speed == CollectorSpeedTier.Standard ? CollectorSpeedTier.Swift : CollectorSpeedTier.Standard;
        if (GUI.Button(new Rect(x + third, y, third - 2f, rowHeight - 4f), "Range: " + _range))
            _range = _range == CollectorRangeTier.Standard ? CollectorRangeTier.Extended : CollectorRangeTier.Standard;
        if (GUI.Button(new Rect(x + third * 2f, y, third - 2f, rowHeight - 4f), "Trim: " + _trim))
            _trim = (CollectorTrim)(((int)_trim + 1) % 3);
        y += rowHeight;

        var stepperLabelRect = new Rect(x, y, innerWidth - 70f, rowHeight - 4f);
        GUI.Label(stepperLabelRect, "Battalion size: " + _batchSize);
        if (GUI.Button(new Rect(x + innerWidth - 68f, y, 30f, rowHeight - 4f), "-"))
            _batchSize = Mathf.Max(CollectorClassDef.MinBatchSize, _batchSize - 1);
        if (GUI.Button(new Rect(x + innerWidth - 34f, y, 30f, rowHeight - 4f), "+"))
            _batchSize = Mathf.Min(CollectorClassDef.MaxBatchSize, _batchSize + 1);
        y += rowHeight;

        var previewDef = new CollectorClassDef { Name = _nameField, Speed = _speed, Range = _range, Trim = _trim, BatchSize = _batchSize };
        DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), "Cost: " + previewDef.TotalBonesCost + " Bones, "
            + (previewDef.TrainSecondsPerUnit * _batchSize).ToString("0") + "s to train");
        y += rowHeight;

        if (GUI.Button(new Rect(x, y, innerWidth, rowHeight - 2f), "Save Class"))
            _builder.DefineCollectorClass(new CollectorClassDef { Name = _nameField, Speed = _speed, Range = _range, Trim = _trim, BatchSize = _batchSize });
        y += rowHeight;

        var classes = _builder.CollectorClasses;
        if (classes.Count > 0)
        {
            y += 4f;
            DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), "Saved classes:");
            y += rowHeight;

            foreach (var c in classes)
            {
                var labelRect = new Rect(x, y, innerWidth - 70f, rowHeight - 2f);
                GUI.Label(labelRect, c.Name + " x" + c.BatchSize + " (" + c.TotalBonesCost + " Bones)");
                var trainRect = new Rect(x + innerWidth - 68f, y, 68f, rowHeight - 2f);
                if (GUI.Button(trainRect, "Train")) TryTrain(c);
                y += rowHeight;
            }
        }

        var orders = _builder.CollectorOrders;
        if (orders.Count > 0)
        {
            y += 4f;
            DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), "Training now:");
            y += rowHeight;
            foreach (var kv in orders)
            {
                var order = kv.Value;
                var label = order.Def.Name + ": " + order.Remaining + " left, next in " + Mathf.Max(0f, order.TimeToNextUnit).ToString("0.0") + "s";
                DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), label);
                y += rowHeight;
            }
        }
    }

    /// <summary>Picks the player's own nearest-to-idle Complete Big
    /// Brain building (preferring one with no order already running) and
    /// starts a battalion there. Silent no-op on failure (no Big Brain
    /// built yet, or unaffordable) -- same "bad input is a no-op"
    /// discipline every other command surface in this project follows;
    /// the cost/afford preview above is what tells the player why before
    /// they click.</summary>
    private void TryTrain(CollectorClassDef def)
    {
        var bridge = _builder.SimBridge;
        if (bridge == null || !bridge.HasMatch) return;

        uint? candidate = null;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex != _playerIndex || b.Kind != BuildingKind.BigBrain || b.State != BuildingState.Complete) continue;
            if (!_builder.CollectorOrders.ContainsKey(b.EntityId)) { candidate = b.EntityId; break; }
            if (candidate == null) candidate = b.EntityId;
        }
        if (candidate == null) return;
        _builder.BeginCollectorBattalion(candidate.Value, def);
    }

    private static void DrawShadowedLabel(Rect rect, string text)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = new Color(0.92f, 0.88f, 0.78f, 1f);
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }
}
