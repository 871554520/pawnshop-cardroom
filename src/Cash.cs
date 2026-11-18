using System;

namespace PawnshopCardRoom;

// 现金结算。优先走游戏自己的 PlayerStore.ModCash；
// 接口不可用（版本差异或被扫描器拒绝）时退回模组筹码，保证牌桌永远不会因此崩掉。
internal static class Cash
{
    private const string ModId = "pawnshop_cardroom";
    private const string ChipsKey = "chips";
    private const int DefaultChips = 1000;

    private static bool _playerStoreBroken;

    internal static int Get()
    {
        if (!_playerStoreBroken)
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store != null) return ModReflect.Get<int>(store, "cash");
            }
            catch (Exception)
            {
                _playerStoreBroken = true;
            }
        }
        return ModHelper.GetModDataInt(ModId, ChipsKey, DefaultChips);
    }

    internal static void Add(int delta, ModLog log)
    {
        if (delta == 0) return;

        if (!_playerStoreBroken)
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store != null)
                {
                    // ModCash 的签名在不同版本里带过 bool 参数，两种都试一次。
                    try { ModReflect.Call(store, "ModCash", delta, false); return; }
                    catch (Exception) { ModReflect.Call(store, "ModCash", delta); return; }
                }
            }
            catch (Exception error)
            {
                _playerStoreBroken = true;
                log?.Warning("现金结算接口不可用，改用模组筹码：" + error.GetType().Name);
            }
        }

        ModHelper.SetModDataInt(ModId, ChipsKey, ModHelper.GetModDataInt(ModId, ChipsKey, DefaultChips) + delta);
    }

    // DdzTable 里用的是这个名字
    internal static void TryAdd(int delta, ModLog log) => Add(delta, log);
}
