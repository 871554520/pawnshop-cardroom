using System;
using System.Collections.Generic;
using HarmonyLib;
using PawnshopCardRoom.Doudizhu;

namespace PawnshopCardRoom;

public sealed partial class ModEntry
{    private const string ModId = "pawnshop_cardroom";
    private const string LocationId = "pawnshop_cardroom.cardroom";
    private const string TableWindowId = "pawnshop_cardroom.table";
    private const string LuckyEventId = "pawnshop_cardroom.lucky_night";
    private const int BaseBet = 100;

    private readonly Random _random = new Random();
    private readonly HashSet<DdzCard> _selected = new HashSet<DdzCard>();
    private DdzTable _table;
    private bool _settled;
    private string _resultText = "";
    private string _uiMessage = "";

    partial void InstallCore(RuntimeLifetime lifetime, Harmony harmony)
    {
        // 本 Mod 只用官方 API，不需要 Harmony 补丁；保留 harmony 参数与模板一致。
        lifetime.Own(RegisterLocation, UnregisterEverything);
    }

    partial void InstallAssets(RuntimeLifetime lifetime, LoadedMod mod)
    {
        // 当前没有自带贴图或音效，资源就绪后不需要额外步骤。
        if (mod.Manifest.ID != ModId) return;
    }

    private void RegisterLocation()
    {
        var location = ModHelper.AddAfterhourLocation(ModId, LocationId,
            title: () => "棋牌室",
            imageKey: null,
            backgroundKey: null,
            ambienceId: null,
            isDiscovered: () => true,
            onArrive: OnArrive,
            buildPanel: BuildPanel);

        location.leaveWarning = () => "桌上的筹码不带出门。";
        location.allowsHazardousWaste = false;

        ModHelper.AddAfterhourEvent(ModId, LuckyEventId, LocationId,
            title: () => "手气之夜",
            durationDays: 1,
            description: () => "今晚牌桌上的人格外大方，赢了多拿一成。",
            effects: new[]
            {
                ModHelper.AfterhourEffect(() => "牌桌赢钱 +10%", "cardroom_payout", 10f, AfterhourEffectPolarity.Good),
            },
            dailyChancePercent: 20f);
    }

    private void UnregisterEverything()
    {
        ModHelper.RemoveAfterhourEvents(ModId);
        ModHelper.RemoveAfterhourLocations(ModId);
        CloseTableWindow();
    }

    private void OnArrive()
    {
        _table = null;
        _selected.Clear();
        _settled = false;
        _resultText = "";
        _uiMessage = "椅子还是热的。";
        Log.Log("到达棋牌室");
    }

    private void BuildPanel(CustomUIBuilder builder)
    {
        builder.AddLabel("牌桌是旧的，烟灰缸没倒，但椅子坐着舒服。");
        builder.AddLabel($"你身上：{Cash.Get()} 信用点。底注 {BaseBet}。");

        if (ModHelper.IsAfterhourEventActive(LuckyEventId))
            builder.AddLabel("今晚是手气之夜：赢了多拿一成。");

        builder.AddButton("坐下打一局", StartOrResume);
        builder.AddButton("看看规矩", ShowRules);
    }

    private void ShowRules()
    {
        if (CustomUIManager.Instance == null) return;
        CustomUIManager.Window("pawnshop_cardroom.rules", "牌桌规矩", CustomUIManager.LAYER_PAPER)
            .SetSize(520, 420)
            .SetDraggable(true)
            .AddLabel("三人斗地主，地主对两家农民。")
            .AddLabel("炸弹和王炸打出来，倍数翻倍，结算跟着翻。")
            .AddLabel("底注直接跟你的信用点走：赢了进账，输了扣钱。")
            .AddLabel("牌型：单张、对子、三张、三带一、三带二、顺子、连对、飞机、炸弹、王炸。")
            .AddButton("知道了", () => CustomUIManager.Instance.CloseWindow("pawnshop_cardroom.rules"))
            .Show();
    }

    private void StartOrResume()
    {
        if (Cash.Get() < BaseBet)
        {
            _uiMessage = "信用点不够交底注，先去把店开了。";
            RenderTable();
            return;
        }

        if (_table == null || _table.IsOver)
        {
            _table = new DdzTable(_random, BaseBet);
            _settled = false;
            _resultText = "";
            _table.PayoutBonus = ReadPayoutBonus();
            _table.Deal();
            _selected.Clear();
            _table.RunAi();
            _uiMessage = "";
        }
        RenderTable();
    }

    private float ReadPayoutBonus()
    {
        try { return Math.Max(0f, ModHelper.GetAfterhourModifier(LocationId, "cardroom_payout") / 100f); }
        catch (Exception) { return 0f; }
    }

    private void Toggle(DdzCard card)
    {
        if (!_selected.Remove(card)) _selected.Add(card);
        RenderTable();
    }

    private void PlaySelected()
    {
        if (_table == null)
        {
            _uiMessage = "先坐下。";
            RenderTable();
            return;
        }
        if (!_table.PlayerToAct)
        {
            _uiMessage = "还没轮到你。";
            RenderTable();
            return;
        }
        if (_selected.Count == 0)
        {
            _uiMessage = "先点几张牌。";
            RenderTable();
            return;
        }

        var cards = new List<DdzCard>(_selected);
        var combo = ComboDetector.Detect(cards);
        if (combo.Type == ComboType.None)
        {
            _uiMessage = "这不是能打的牌型。";
            RenderTable();
            return;
        }
        if (!ComboDetector.Beats(combo, _table.LastCombo))
        {
            _uiMessage = "压不过上一手。";
            RenderTable();
            return;
        }

        _table.Play(DdzTable.SeatPlayer, combo);
        _selected.Clear();
        _uiMessage = "";
        _table.RunAi();
        CheckSettlement();
        RenderTable();
    }

    private void PassTurn()
    {
        if (_table == null) return;
        if (!_table.PlayerToAct)
        {
            _uiMessage = "还没轮到你。";
            RenderTable();
            return;
        }
        if (_table.LastCombo == null)
        {
            _uiMessage = "这一轮由你领出，不能不要。";
            RenderTable();
            return;
        }
        _table.Pass(DdzTable.SeatPlayer);
        _selected.Clear();
        _uiMessage = "";
        _table.RunAi();
        CheckSettlement();
        RenderTable();
    }

    private void CheckSettlement()
    {
        if (_table == null || _table.IsOver == false || _settled) return;
        _settled = true;
        _resultText = _table.Settle(Log);
    }

    private void RenderTable()
    {
        if (CustomUIManager.Instance == null) return;
        if (_table == null) return;

        var builder = CustomUIManager.Window(TableWindowId, "棋牌室 · 斗地主", CustomUIManager.LAYER_PAPER);
        builder.SetSize(940, 660).SetDraggable(true);

        builder.AddLabel(_table.StatusText());
        builder.AddLabel(_table.TurnText());

        if (_table.IsOver)
        {
            builder.AddSeparator();
            builder.AddLabel(_resultText);
            builder.BeginRow();
            builder.AddButton("再来一局", StartOrResume);
            builder.AddButton("收摊", CloseTableWindow);
            builder.End();
            builder.Show();
            return;
        }

        builder.AddSeparator();
        builder.BeginGrid(9, 88, 40);
        foreach (var card in _table.Hands[DdzTable.SeatPlayer])
        {
            var picked = card;
            bool selected = _selected.Contains(picked);
            builder.AddButton((selected ? "◆" : "") + picked.Label, () => Toggle(picked));
        }
        builder.End();

        builder.AddSeparator();
        builder.BeginRow();
        builder.AddButton("出牌", PlaySelected);
        builder.AddButton("不要", PassTurn);
        builder.AddFlexibleSpace();
        builder.AddButton("收摊", CloseTableWindow);
        builder.End();

        if (_uiMessage.Length > 0) builder.AddLabel(_uiMessage);
        builder.Show();
    }

    private void CloseTableWindow()
    {
        if (CustomUIManager.Instance == null) return;
        CustomUIManager.Instance.CloseWindow(TableWindowId);
    }
}
