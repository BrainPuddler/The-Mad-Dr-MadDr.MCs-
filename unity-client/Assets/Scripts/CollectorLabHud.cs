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
/// (mnemonic: Lab) or a click on the tab itself.
///
/// 2026-08 follow-up (creator report: "collector lab is hidden under a
/// lot of text... make sure all the buttons are never overwritten or
/// hidden"): used to anchor its tab at a fixed `marginPixels` from the
/// top -- landing it squarely inside HudStatus's always-on text block,
/// which is exactly the bug this report caught. Now the last link in
/// the same chained-anchor idiom <see cref="HudStatus.ContentBottom"/>
/// started: HudStatus publishes its own bottom, <see
/// cref="WindowLightsHud"/> stacks below THAT and publishes its own,
/// <see cref="BuildMenuHud"/> stacks below that one and publishes its
/// own <see cref="BuildMenuHud.Bottom"/> -- this panel stacks below
/// THAT. Four panels sharing the same corner, each genuinely below the
/// last, none guessing a fixed pixel offset.
/// </summary>
public class CollectorLabHud : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private int _playerIndex;

    [Header("Placement (top-left corner, stacked below BuildMenuHud)")]
    public float leftMarginPixels = 12f;
    public float dockGapPixels = 8f;
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

    // 2026-08 follow-up (creator report: "when I hit train nothing
    // happens"): every real reason a Train click can fail -- no Complete
    // Big Brain yet, every owned one already mid-battalion, not enough
    // Bones -- used to be a totally silent no-op (same "bad input is a
    // no-op" discipline this project's real match-core COMMANDS use, but
    // wrong for a direct UI button with no other feedback channel: a
    // command's silence is fine because the ghost-cursor preview or a
    // grayed-out tile already told the player why beforehand; this
    // button had no such preview). `TryTrain` now diagnoses the exact
    // reason and shows it here for a few seconds instead of nothing.
    private string _statusMessage = "";
    private float _statusMessageTimer;

    public void Init(RuntimeCityBuilder builder, int playerIndex)
    {
        _builder = builder;
        _playerIndex = playerIndex;
    }

    private void Update()
    {
        if (_statusMessageTimer > 0f) _statusMessageTimer -= Time.deltaTime;
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

        var tabY = BuildMenuHud.Bottom > 0f ? BuildMenuHud.Bottom + dockGapPixels : WindowLightsHud.Bottom + dockGapPixels;
        var tabRect = new Rect(leftMarginPixels, tabY, tabWidth, tabHeight);
        var e = Event.current;
        PointerOver = e != null && tabRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(tabRect, "Collector Lab (L)")) _open = !_open;

        if (_open)
        {
            var panelRect = new Rect(leftMarginPixels, tabRect.yMax + 4f, panelWidth, PanelHeight());
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
        var statusRows = _statusMessageTimer > 0f ? 1 : 0;
        var gaps = (classCount > 0 ? 4f : 0f) + (orderCount > 0 ? 4f : 0f) + (statusRows > 0 ? 4f : 0f);
        return rowHeight * (fixedRows + classRows + orderRows + statusRows) + gaps + 16f;
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

        if (_statusMessageTimer > 0f)
        {
            y += 4f;
            var statusColor = _statusMessage.StartsWith("Training") ? new Color(0.55f, 0.9f, 0.55f, 1f) : new Color(0.95f, 0.55f, 0.4f, 1f);
            DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), _statusMessage, statusColor);
            y += rowHeight;
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

    /// <summary>Picks the player's own idle (no order already running)
    /// Complete Big Brain building and starts a battalion there --
    /// diagnoses and reports the exact reason via <see cref="SetStatus"/>
    /// when it can't (no Complete Big Brain owned at all, every owned
    /// one already busy, or simply unaffordable), instead of the silent
    /// no-op this used to be.</summary>
    private void TryTrain(CollectorClassDef def)
    {
        var bridge = _builder.SimBridge;
        if (bridge == null || !bridge.HasMatch) { SetStatus("No live match."); return; }

        uint? candidate = null;
        var hasCompleteBigBrain = false;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex != _playerIndex || b.Kind != BuildingKind.BigBrain || b.State != BuildingState.Complete) continue;
            hasCompleteBigBrain = true;
            if (!_builder.CollectorOrders.ContainsKey(b.EntityId)) { candidate = b.EntityId; break; }
        }

        if (!hasCompleteBigBrain) { SetStatus("Need a Complete Big Brain building first."); return; }
        if (candidate == null) { SetStatus("Every Big Brain is already training a battalion."); return; }

        var clampedBatch = Mathf.Clamp(def.BatchSize, CollectorClassDef.MinBatchSize, CollectorClassDef.MaxBatchSize);
        var cost = def.BonesCostPerUnit * clampedBatch;
        if (_builder.WalletBones < cost) { SetStatus("Not enough Bones (need " + cost + ", have " + _builder.WalletBones + ")."); return; }

        SetStatus(_builder.BeginCollectorBattalion(candidate.Value, def)
            ? "Training \"" + def.Name + "\" started."
            : "Couldn't start training.");
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        _statusMessageTimer = 4f;
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color? color = null)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color ?? new Color(0.92f, 0.88f, 0.78f, 1f);
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }
}
