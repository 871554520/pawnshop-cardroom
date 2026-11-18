using System;
using System.Collections.Generic;
using PawnshopCardRoom.Doudizhu;

namespace PawnshopCardRoom;

// 一局斗地主的牌桌状态：三家手牌、当前该谁、上一手、倍数与结算。
internal sealed class DdzTable
{
    internal const int SeatPlayer = 0;

    private readonly Random _random;
    private readonly List<DdzCard> _bottom = new List<DdzCard>();
    private int _passes;

    internal readonly List<DdzCard>[] Hands =
    {
        new List<DdzCard>(),
        new List<DdzCard>(),
        new List<DdzCard>(),
    };

    internal readonly string[] Names = { "你", "老赵", "阿岚" };

    internal Combo LastCombo;
    internal int LastPlayer = -1;
    internal int Current;
    internal int Landlord = -1;
    internal int Multiplier = 1;
    internal int Winner = -1;
    internal int BaseBet;
    internal string Message = "";
    internal float PayoutBonus;

    internal DdzTable(Random random, int baseBet)
    {
        _random = random ?? new Random();
        BaseBet = baseBet;
    }

    internal bool IsOver => Winner >= 0;
    internal bool PlayerToAct => Winner < 0 && Current == SeatPlayer;

    internal void Deal()
    {
        for (int i = 0; i < 3; i++) Hands[i].Clear();
        _bottom.Clear();

        var deck = Deck.Create();
        Deck.Shuffle(deck, _random);
        for (int i = 0; i < 51; i++) Hands[i % 3].Add(deck[i]);
        for (int i = 51; i < 54; i++) _bottom.Add(deck[i]);

        Landlord = _random.Next(3);
        Hands[Landlord].AddRange(_bottom);
        foreach (var hand in Hands) Deck.SortDescending(hand);

        Current = Landlord;
        LastCombo = null;
        LastPlayer = -1;
        _passes = 0;
        Multiplier = 1;
        Winner = -1;

        string bottom = string.Join(" ", _bottom.ConvertAll(card => card.Label).ToArray());
        Message = Landlord == 0
            ? $"你抢到了地主。底牌：{bottom}"
            : $"{Names[Landlord]} 当了地主。你是农民，底牌是 {bottom}。";
    }

    // 出牌。返回 false 表示这副牌压不过上一手。
    internal bool Play(int seat, Combo combo)
    {
        if (combo == null || combo.Type == ComboType.None) return false;
        if (Winner >= 0) return false;
        if (!ComboDetector.Beats(combo, LastCombo)) return false;

        foreach (var card in combo.Cards) Hands[seat].Remove(card);

        LastCombo = combo;
        LastPlayer = seat;
        _passes = 0;
        if (combo.IsBomb) Multiplier *= 2;
        Message = $"{Names[seat]} 打出 {combo.Text}" + (combo.IsBomb ? "，倍数翻倍。" : "。");

        if (Hands[seat].Count == 0)
        {
            Winner = seat;
            Message += $"\n{Names[seat]} 走完了，这局结束。";
        }
        else
        {
            Current = (seat + 1) % 3;
        }
        return true;
    }

    internal void Pass(int seat)
    {
        if (Winner >= 0) return;
        _passes++;
        Message = $"{Names[seat]} 不要。";
        if (_passes >= 2 && LastPlayer >= 0)
        {
            LastCombo = null;
            _passes = 0;
            Current = LastPlayer;
            Message += $" 两家都过，{Names[LastPlayer]} 重新领出。";
        }
        else
        {
            Current = (seat + 1) % 3;
        }
    }

    // 一直推进到轮到玩家，或者本局结束。
    internal void RunAi()
    {
        int guard = 0;
        while (Winner < 0 && Current != SeatPlayer && guard++ < 300)
        {
            int seat = Current;
            var combo = DdzAi.Decide(Hands[seat], LastCombo, _random);

            // 农民之间不互相压：同一边的就放过去
            if (combo != null && LastCombo != null && LastPlayer >= 0 && seat != Landlord && LastPlayer != Landlord)
                combo = null;

            if (combo == null) Pass(seat);
            else Play(seat, combo);
        }
    }

    internal string StatusText() =>
        $"你 {Hands[0].Count} 张 ｜ {Names[1]} {Hands[1].Count} 张 ｜ {Names[2]} {Hands[2].Count} 张 ｜ 倍数 ×{Multiplier} ｜ 底注 {BaseBet}";

    internal string TurnText()
    {
        if (Winner >= 0) return "这一局已经结束。";
        if (LastCombo == null)
            return Current == SeatPlayer ? "轮到你了，这一轮由你领出。" : $"轮到 {Names[Current]} 领出。";
        string who = LastPlayer >= 0 ? Names[LastPlayer] : "上家";
        string tail = Current == SeatPlayer ? "轮到你了。" : $"轮到 {Names[Current]}。";
        return $"{who} 上一手：{LastCombo.Text}。{tail}";
    }

    // 结算：地主对两家农民，各付 stake；炸弹与王炸翻倍。
    internal string Settle(ModLog log)
    {
        if (Winner < 0) return "这局没有结果。";
        int stake = (int)Math.Round(BaseBet * Multiplier * (1f + PayoutBonus));
        bool landlordWon = Winner == Landlord;
        int delta = SeatPlayer == Landlord
            ? (landlordWon ? 2 * stake : -2 * stake)
            : (landlordWon ? -stake : stake);

        Cash.TryAdd(delta, log);

        string side = landlordWon ? "地主" : "农民";
        string outcome = delta >= 0 ? $"你赢了 {delta} 信用点。" : $"你输了 {-delta} 信用点。";
        string bonus = PayoutBonus > 0f ? $"（手气之夜加成 {PayoutBonus:P0}）" : "";
        return $"{side}一边赢了。{outcome}{bonus} 底注 {BaseBet} × 倍数 {Multiplier}。";
    }
}
