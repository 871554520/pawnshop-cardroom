using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnshopCardRoom.Doudizhu;

// 点数：3..15 表示 3..2，16 小王，17 大王。花色：0 黑桃 1 红桃 2 梅花 3 方块，王为 -1。
internal readonly struct DdzCard : IEquatable<DdzCard>
{
    internal readonly int Rank;
    internal readonly int Suit;

    internal DdzCard(int rank, int suit) { Rank = rank; Suit = suit; }

    internal bool IsJoker => Rank >= 16;

    internal string RankText => Rank switch
    {
        11 => "J",
        12 => "Q",
        13 => "K",
        14 => "A",
        15 => "2",
        16 => "小王",
        17 => "大王",
        _ => Rank.ToString(),
    };

    internal string SuitText => Suit switch
    {
        0 => "♠",
        1 => "♥",
        2 => "♣",
        3 => "♦",
        _ => "",
    };

    internal string Label => IsJoker ? RankText : SuitText + RankText;

    public bool Equals(DdzCard other) => Rank == other.Rank && Suit == other.Suit;
    public override bool Equals(object obj) => obj is DdzCard other && Equals(other);
    public override int GetHashCode() => (Rank * 5) + Suit + 1;
    public override string ToString() => Label;
}

internal static class Deck
{
    internal static List<DdzCard> Create()
    {
        var cards = new List<DdzCard>(54);
        for (int rank = 3; rank <= 15; rank++)
            for (int suit = 0; suit < 4; suit++)
                cards.Add(new DdzCard(rank, suit));
        cards.Add(new DdzCard(16, -1));
        cards.Add(new DdzCard(17, -1));
        return cards;
    }

    internal static void Shuffle(List<DdzCard> cards, Random random)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            var swap = cards[i];
            cards[i] = cards[j];
            cards[j] = swap;
        }
    }

    internal static void SortDescending(List<DdzCard> cards) =>
        cards.Sort((a, b) => a.Rank != b.Rank ? b.Rank.CompareTo(a.Rank) : a.Suit.CompareTo(b.Suit));
}

internal enum ComboType
{
    None = 0,
    Single = 1,
    Pair = 2,
    Triple = 3,
    TripleSingle = 4,
    TriplePair = 5,
    Straight = 6,
    PairStraight = 7,
    Plane = 8,
    PlaneSingle = 9,
    PlanePair = 10,
    Bomb = 11,
    Rocket = 12,
}

internal sealed class Combo
{
    internal ComboType Type { get; }
    internal int Main { get; }
    internal int Length { get; }
    internal List<DdzCard> Cards { get; }

    internal Combo(ComboType type, int main, int length, List<DdzCard> cards)
    {
        Type = type;
        Main = main;
        Length = length;
        Cards = cards;
    }

    internal bool IsBomb => Type == ComboType.Bomb || Type == ComboType.Rocket;

    internal string Text => Type switch
    {
        ComboType.Single => $"单张 {RankName(Main)}",
        ComboType.Pair => $"对 {RankName(Main)}",
        ComboType.Triple => $"三张 {RankName(Main)}",
        ComboType.TripleSingle => $"三带一 {RankName(Main)}",
        ComboType.TriplePair => $"三带二 {RankName(Main)}",
        ComboType.Straight => $"顺子 {RankName(Main)}-{RankName(Main + Length - 1)}",
        ComboType.PairStraight => $"连对 {RankName(Main)}-{RankName(Main + Length - 1)}",
        ComboType.Plane => $"飞机 {RankName(Main)}×{Length}",
        ComboType.PlaneSingle => $"飞机带单 {RankName(Main)}×{Length}",
        ComboType.PlanePair => $"飞机带对 {RankName(Main)}×{Length}",
        ComboType.Bomb => $"炸弹 {RankName(Main)}",
        ComboType.Rocket => "王炸",
        _ => "无效牌型",
    };

    private static string RankName(int rank) => rank switch
    {
        11 => "J",
        12 => "Q",
        13 => "K",
        14 => "A",
        15 => "2",
        16 => "小王",
        17 => "大王",
        _ => rank.ToString(),
    };
}

internal static class ComboDetector
{
    private const int MaxStraightRank = 14; // A；2 与王不入顺

    internal static Combo Detect(IReadOnlyList<DdzCard> cards)
    {
        int n = cards.Count;
        if (n == 0) return new Combo(ComboType.None, 0, 0, new List<DdzCard>());

        var counts = new int[18];
        var ranks = new List<int>();
        for (int i = 0; i < n; i++)
        {
            int rank = cards[i].Rank;
            if (counts[rank] == 0) ranks.Add(rank);
            counts[rank]++;
        }
        ranks.Sort();

        var singles = new List<int>();
        var pairs = new List<int>();
        var triples = new List<int>();
        var quads = new List<int>();
        foreach (int rank in ranks)
        {
            switch (counts[rank])
            {
                case 1: singles.Add(rank); break;
                case 2: pairs.Add(rank); break;
                case 3: triples.Add(rank); break;
                case 4: quads.Add(rank); break;
            }
        }

        var list = new List<DdzCard>(cards);

        if (n == 2 && counts[16] == 1 && counts[17] == 1)
            return new Combo(ComboType.Rocket, 16, 1, list);
        if (n == 4 && quads.Count == 1)
            return new Combo(ComboType.Bomb, quads[0], 1, list);
        if (n == 1)
            return new Combo(ComboType.Single, ranks[0], 1, list);
        if (n == 2 && pairs.Count == 1)
            return new Combo(ComboType.Pair, pairs[0], 1, list);
        if (n == 3 && triples.Count == 1)
            return new Combo(ComboType.Triple, triples[0], 1, list);
        if (n == 4 && triples.Count == 1 && singles.Count == 1)
            return new Combo(ComboType.TripleSingle, triples[0], 1, list);
        if (n == 5 && triples.Count == 1 && pairs.Count == 1)
            return new Combo(ComboType.TriplePair, triples[0], 1, list);

        if (n >= 5 && singles.Count == ranks.Count)
        {
            int? main = ConsecutiveRun(ranks, 5);
            if (main.HasValue) return new Combo(ComboType.Straight, main.Value, n, list);
        }

        if (n >= 6 && n % 2 == 0 && pairs.Count == ranks.Count && pairs.Count >= 3)
        {
            int? main = ConsecutiveRun(pairs, 3);
            if (main.HasValue) return new Combo(ComboType.PairStraight, main.Value, pairs.Count, list);
        }

        if (n >= 6 && n % 3 == 0 && triples.Count == n / 3)
        {
            int? main = ConsecutiveRun(triples, 2);
            if (main.HasValue) return new Combo(ComboType.Plane, main.Value, n / 3, list);
        }

        // 飞机带单：k 组连续三张 + k 张不同点数的杂牌
        for (int k = 2; k <= 4; k++)
        {
            if (n != 4 * k) continue;
            foreach (int start in triples)
            {
                if (!HasConsecutiveTriples(counts, start, k)) continue;
                var rest = new List<int>();
                int total = 0;
                foreach (int rank in ranks)
                {
                    if (rank >= start && rank < start + k) continue;
                    rest.Add(rank);
                    total += counts[rank];
                }
                if (total != k || rest.Count != k) continue;
                return new Combo(ComboType.PlaneSingle, start, k, list);
            }
        }

        // 飞机带对：k 组连续三张 + k 个对子
        for (int k = 2; k <= 3; k++)
        {
            if (n != 5 * k) continue;
            foreach (int start in triples)
            {
                if (!HasConsecutiveTriples(counts, start, k)) continue;
                int wings = 0;
                foreach (int rank in ranks)
                {
                    if (rank >= start && rank < start + k) continue;
                    if (counts[rank] == 2) wings++;
                }
                if (wings != k) continue;
                return new Combo(ComboType.PlanePair, start, k, list);
            }
        }

        return new Combo(ComboType.None, 0, 0, list);
    }

    internal static bool Beats(Combo candidate, Combo target)
    {
        if (candidate == null || candidate.Type == ComboType.None) return false;
        if (target == null || target.Type == ComboType.None) return true;
        if (candidate.Type == ComboType.Rocket) return true;
        if (target.Type == ComboType.Rocket) return false;
        if (candidate.Type == ComboType.Bomb && target.Type != ComboType.Bomb) return true;
        if (target.Type == ComboType.Bomb && candidate.Type != ComboType.Bomb) return false;
        if (candidate.Type != target.Type) return false;
        if (candidate.Length != target.Length) return false;
        return candidate.Main > target.Main;
    }

    private static bool HasConsecutiveTriples(int[] counts, int start, int k)
    {
        if (start + k - 1 > MaxStraightRank) return false;
        for (int i = 0; i < k; i++)
            if (counts[start + i] != 3) return false;
        return true;
    }

    // 连续且不超过 A，返回最小点数
    private static int? ConsecutiveRun(List<int> ranks, int need)
    {
        if (ranks.Count < need) return null;
        ranks.Sort();
        for (int i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] > MaxStraightRank) return null;
            if (i > 0 && ranks[i] != ranks[i - 1] + 1) return null;
        }
        return ranks[0];
    }
}

internal static class ComboFinder
{
    // 跟牌：找能压过 target 的最小牌型；没有就返回 null（要不起）。
    internal static Combo FindBeating(List<DdzCard> hand, Combo target)
    {
        if (target == null) return Lead(hand);

        var same = FindSameShape(hand, target);
        if (same != null) return same;

        var bomb = FindBomb(hand, target.Type == ComboType.Bomb ? target.Main : -1);
        if (bomb != null) return bomb;

        if (target.Type != ComboType.Rocket) return FindRocket(hand);
        return null;
    }

    // 领出：手牌越少越倾向于一次甩掉，炸弹留到最后。
    internal static Combo Lead(List<DdzCard> hand)
    {
        var byRank = GroupByRank(hand);
        if (hand.Count == 0) return null;
        if (hand.Count <= 2)
        {
            var pair = LowestRankWith(byRank, 2, 0);
            if (pair.HasValue && hand.Count == 2) return Take(byRank[pair.Value], 2, ComboType.Pair, pair.Value, 1);
            return Take(byRank[byRank.Keys.Min()], 1, ComboType.Single, byRank.Keys.Min(), 1);
        }

        int? single = null;
        int? smallestPair = LowestRankWith(byRank, 2, 0);
        int? smallestTriple = LowestRankWith(byRank, 3, 0);

        foreach (var entry in byRank)
            if (entry.Value.Count == 1 && (!single.HasValue || entry.Key < single.Value)) single = entry.Key;

        // 手上只剩炸弹/三张时才动大牌
        if (single.HasValue) return Take(byRank[single.Value], 1, ComboType.Single, single.Value, 1);
        if (smallestPair.HasValue) return Take(byRank[smallestPair.Value], 2, ComboType.Pair, smallestPair.Value, 1);
        if (smallestTriple.HasValue) return Take(byRank[smallestTriple.Value], 3, ComboType.Triple, smallestTriple.Value, 1);
        return FindBomb(hand, -1) ?? FindRocket(hand);
    }

    private static Combo FindSameShape(List<DdzCard> hand, Combo target)
    {
        var byRank = GroupByRank(hand);
        switch (target.Type)
        {
            case ComboType.Single:
            {
                int? rank = LowestRankWith(byRank, 1, target.Main);
                return rank.HasValue ? Take(byRank[rank.Value], 1, ComboType.Single, rank.Value, 1) : null;
            }
            case ComboType.Pair:
            {
                int? rank = LowestRankWith(byRank, 2, target.Main);
                return rank.HasValue ? Take(byRank[rank.Value], 2, ComboType.Pair, rank.Value, 1) : null;
            }
            case ComboType.Triple:
            {
                int? rank = LowestRankWith(byRank, 3, target.Main);
                return rank.HasValue ? Take(byRank[rank.Value], 3, ComboType.Triple, rank.Value, 1) : null;
            }
            case ComboType.TripleSingle:
            {
                int? rank = LowestRankWith(byRank, 3, target.Main);
                if (!rank.HasValue) return null;
                var cards = TakeCards(byRank[rank.Value], 3);
                var extra = LowestOtherCard(byRank, rank.Value, 1);
                if (extra == null) return null;
                cards.Add(extra);
                return new Combo(ComboType.TripleSingle, rank.Value, 1, cards);
            }
            case ComboType.TriplePair:
            {
                int? rank = LowestRankWith(byRank, 3, target.Main);
                if (!rank.HasValue) return null;
                var cards = TakeCards(byRank[rank.Value], 3);
                int? pairRank = LowestRankWith(byRank, 2, 0, rank.Value);
                if (!pairRank.HasValue) return null;
                cards.AddRange(TakeCards(byRank[pairRank.Value], 2));
                return new Combo(ComboType.TriplePair, rank.Value, 1, cards);
            }
            case ComboType.Straight:
                return FindRun(byRank, target, 1, ComboType.Straight, target.Length);
            case ComboType.PairStraight:
                return FindRun(byRank, target, 2, ComboType.PairStraight, target.Length);
            case ComboType.Plane:
                return FindRun(byRank, target, 3, ComboType.Plane, target.Length);
            case ComboType.PlaneSingle:
            {
                var plane = FindRun(byRank, target, 3, ComboType.Plane, target.Length);
                if (plane == null) return null;
                var used = new List<DdzCard>(plane.Cards);
                var cards = new List<DdzCard>(plane.Cards);
                for (int i = 0; i < target.Length; i++)
                {
                    var extra = LowestOtherCard(byRank, used, cards);
                    if (extra == null) return null;
                    cards.Add(extra);
                    used.Add(extra);
                }
                return new Combo(ComboType.PlaneSingle, plane.Main, target.Length, cards);
            }
            case ComboType.PlanePair:
            {
                var plane = FindRun(byRank, target, 3, ComboType.Plane, target.Length);
                if (plane == null) return null;
                var used = new List<DdzCard>(plane.Cards);
                var cards = new List<DdzCard>(plane.Cards);
                for (int i = 0; i < target.Length; i++)
                {
                    int? pairRank = null;
                    foreach (var entry in byRank)
                    {
                        if (contains(used, entry.Key)) continue;
                        if (entry.Value.Count - CountUsed(used, entry.Key) < 2) continue;
                        if (!pairRank.HasValue || entry.Key < pairRank.Value) pairRank = entry.Key;
                    }
                    if (!pairRank.HasValue) return null;
                    var picked = TakeRemaining(byRank[pairRank.Value], used, 2);
                    if (picked.Count != 2) return null;
                    cards.AddRange(picked);
                    used.AddRange(picked);
                }
                return new Combo(ComboType.PlanePair, plane.Main, target.Length, cards);
            }
            case ComboType.Bomb:
                return FindBomb(hand, target.Main);
            default:
                return null;
        }

        static bool contains(List<DdzCard> cards, int rank)
        {
            foreach (var card in cards) if (card.Rank == rank) return true;
            return false;
        }
    }

    private static Combo FindRun(SortedDictionary<int, List<DdzCard>> byRank, Combo target, int perRank,
        ComboType type, int length)
    {
        for (int start = target.Main + 1; start + length - 1 <= 14; start++)
        {
            bool ok = true;
            for (int i = 0; i < length; i++)
            {
                if (!byRank.TryGetValue(start + i, out var group) || group.Count < perRank)
                {
                    ok = false;
                    break;
                }
            }
            if (!ok) continue;
            var cards = new List<DdzCard>();
            for (int i = 0; i < length; i++) cards.AddRange(TakeCards(byRank[start + i], perRank));
            return new Combo(type, start, length, cards);
        }
        return null;
    }

    private static Combo FindBomb(List<DdzCard> hand, int aboveRank)
    {
        var byRank = GroupByRank(hand);
        foreach (var entry in byRank)
        {
            if (entry.Key > 15) continue;
            if (entry.Value.Count < 4) continue;
            if (entry.Key <= aboveRank) continue;
            return Take(entry.Value, 4, ComboType.Bomb, entry.Key, 1);
        }
        return null;
    }

    private static Combo FindRocket(List<DdzCard> hand)
    {
        DdzCard? small = null, big = null;
        foreach (var card in hand)
        {
            if (card.Rank == 16) small = card;
            else if (card.Rank == 17) big = card;
        }
        if (small == null || big == null) return null;
        return new Combo(ComboType.Rocket, 16, 1, new List<DdzCard> { small.Value, big.Value });
    }

    internal static SortedDictionary<int, List<DdzCard>> GroupByRank(List<DdzCard> hand)
    {
        var map = new SortedDictionary<int, List<DdzCard>>();
        foreach (var card in hand)
        {
            if (!map.TryGetValue(card.Rank, out var list))
            {
                list = new List<DdzCard>();
                map.Add(card.Rank, list);
            }
            list.Add(card);
        }
        return map;
    }

    private static int? LowestRankWith(SortedDictionary<int, List<DdzCard>> byRank, int count, int aboveRank, int? except = null)
    {
        foreach (var entry in byRank)
        {
            if (entry.Key <= aboveRank) continue;
            if (except.HasValue && entry.Key == except.Value) continue;
            if (entry.Value.Count >= count) return entry.Key;
        }
        return null;
    }

    private static DdzCard? LowestOtherCard(SortedDictionary<int, List<DdzCard>> byRank, int exceptRank, int count)
    {
        foreach (var entry in byRank)
        {
            if (entry.Key == exceptRank) continue;
            if (entry.Value.Count >= count) return entry.Value[0];
        }
        return null;
    }

    private static DdzCard? LowestOtherCard(SortedDictionary<int, List<DdzCard>> byRank, List<DdzCard> used, List<DdzCard> take)
    {
        foreach (var entry in byRank)
        {
            int consumed = CountUsed(used, entry.Key);
            if (entry.Value.Count - consumed <= 0) continue;
            foreach (var card in entry.Value)
                if (!used.Contains(card)) return card;
        }
        return null;
    }

    private static int CountUsed(List<DdzCard> used, int rank)
    {
        int count = 0;
        foreach (var card in used) if (card.Rank == rank) count++;
        return count;
    }

    private static List<DdzCard> TakeCards(List<DdzCard> group, int count)
    {
        var taken = new List<DdzCard>(count);
        for (int i = 0; i < count && i < group.Count; i++) taken.Add(group[i]);
        return taken;
    }

    private static List<DdzCard> TakeRemaining(List<DdzCard> group, List<DdzCard> used, int count)
    {
        var taken = new List<DdzCard>(count);
        foreach (var card in group)
        {
            if (used.Contains(card)) continue;
            taken.Add(card);
            if (taken.Count == count) break;
        }
        return taken;
    }

    private static Combo Take(List<DdzCard> group, int count, ComboType type, int main, int length) =>
        new Combo(type, main, length, TakeCards(group, count));
}

internal static class DdzAi
{
    internal static Combo Decide(List<DdzCard> hand, Combo lastCombo, Random random)
    {
        if (lastCombo == null) return ComboFinder.Lead(hand);

        var best = ComboFinder.FindBeating(hand, lastCombo);
        if (best == null) return null;

        // 队友打出的牌不抢：农民之间不互相压
        // （这里只做了最小判断，座位关系交给牌桌处理）
        return best;
    }
}
