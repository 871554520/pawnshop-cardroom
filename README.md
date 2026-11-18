# 深空当铺：夜间棋牌室（pawnshop_cardroom）

给《深空当铺：可能是偷的》（Probably Stolen）做的官方 Mod：卷帘门落下之后，除了垃圾场、配给站和杰克逊博士处，再添一个**棋牌室**。

当前版本 **0.1.0**，只做斗地主。

## 现在能做什么

- 夜晚交通界面多一个「棋牌室」目的地，进去有个面板
- 坐下开一局三人斗地主：你 + 老赵 + 阿岚，随机地主，三张底牌归地主
- 牌型：单张、对子、三张、三带一、三带二、顺子、连对、飞机、飞机带单、飞机带对、炸弹、王炸
- 炸弹和王炸打出来倍数翻倍，结算跟着翻
- 输赢直接结算到**游戏的现金**（`PlayerStore.ModCash`）；接口不可用时自动退回模组筹码，不会报错崩游戏
- 「手气之夜」夜间事件：20% 概率出现，赢了多拿 10%

## 还没做（下一步）

- 叫地主叫分流程（目前是随机地主）
- 四带二、四带两对牌型
- 炸金花、德州扑克、麻将（规则引擎要一个个写）
- 联机：官方没有网络同步 API，见文末

## 用 git 仓库开发

游戏只认编译好的 DLL，不会直接读仓库。但仓库文件夹可以**就是** Mod 文件夹：官方的模组扫描器会跳过 `.git`、`.vs`、`.idea` 这些点开头的文件夹，所以把 clone 下来的目录放进 `Mods/` 完全没问题。

三种用法，选一种：

**A. 仓库放 Mods 里（改完直接编译）**

```powershell
cd "D:\Steam\steamapps\common\Probably Stolen\Mods"
git clone https://github.com/871554520/pawnshop-cardroom.git pawnshop_cardroom
cd pawnshop_cardroom
pwsh ./build.ps1
```

`build.ps1` 会自动找到游戏目录、编译、把 DLL 和 `manifest.xml` 拷进 `Mods\pawnshop_cardroom\`。

**B. 仓库放别处，编译完自动装机**

```powershell
git clone https://github.com/871554520/pawnshop-cardroom.git D:\code\pawnshop-cardroom
cd D:\code\pawnshop-cardroom
pwsh ./build.ps1
```

一样是一条命令，区别只是源码不在 Mods 里。

**C. 手动**

```powershell
dotnet build .\PawnshopCardRoom.csproj -c Release '-p:GameManaged=D:\Steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
copy .\bin\Release\netstandard2.1\PawnshopCardRoom.dll "D:\Steam\steamapps\common\Probably Stolen\Mods\pawnshop_cardroom\"
copy .\manifest.xml "D:\Steam\steamapps\common\Probably Stolen\Mods\pawnshop_cardroom\"
```

只写了 `build.ps1` 的用法，因为 Windows 上 `dotnet build` 就够了，**不需要 Visual Studio**，装个 .NET SDK 8 即可。

首次启用本地 Mod，游戏会说「未加载：自你批准后已更改」，点一次**审核并启用**；然后打开这个 Mod 的**开发者模式**，以后每次重新编译都会直接加载，不用反复批准。

出问题看 `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`，或者进游戏按 F8 看控制台。

## 编译

需要 .NET SDK 8 或更高，并且这台机器上装着游戏。

```powershell
dotnet build .\PawnshopCardRoom.csproj -c Release '-p:GameManaged=D:\Steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
```

`GameManaged` 指向游戏目录下的 `Probably Stolen_Data\Managed`。编译只引用那里的 DLL，不会改动游戏文件。

## 安装

把编译产物装进游戏认的 Mod 目录：

```
PawnshopCardRoom/
  manifest.xml
  PawnshopCardRoom.dll
```

具体放到哪个文件夹取决于你游戏版本的 Mod 目录（有的版本是 `Mods/`，有的在用户数据目录下）。装完进游戏，在主菜单的 Mod 列表里启用，然后开新局或读档。

## 这个版本没被验证过的地方

我写代码的环境里没有 Windows、没有游戏 DLL，**没法编译，也没法进游戏实测**。下面这些是按官方文档写的，第一次跑最可能出问题：

1. `AddAfterhourLocation` / `AddAfterhourEvent` 的参数名与签名，按官方文档 `ae161038` 对齐；如果你的游戏版本不同会报编译错误，把错误贴出来我改。
2. `imageKey` / `backgroundKey` / `ambienceId` 传了 `null`，意思是卡片和场景用占位板。如果游戏不接受 `null`，改成 `""` 或补两张图。
3. `PlayerStore.ModCash` 的签名在不同版本里可能带 `bool` 参数，代码里两种都试了；都失败就退回模组筹码。
4. 牌型规则我在 JavaScript 里跑过 22 个用例（含边界：2 和王不入顺、长度不同的顺子不能互压、炸弹压一切、王炸压炸弹），全部通过后才翻成 C#。逻辑应该是对的，但 C# 这版没编译过。

## 联机为什么没做

官方 Mod API 的跨 Mod 通信（`Expose` / `Lookup` / `SendMessage`）是同一个游戏进程内的消息，不是网络同步。真联网要自己写网络层、声明 `network` 能力、过 Steam 身份验证；而「白天去别人店里串门」要改商店的核心循环，官方在不支持清单里明确写了不支持。硬上就是每次游戏更新都崩，所以先不做。

## 许可

代码自己写的，随便用。游戏的官方接口文档版权归原作者，本仓库不包含官方文档正文。
