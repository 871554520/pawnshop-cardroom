#requires -Version 5.1
<#
  深空当铺：夜间棋牌室 —— 一键编译并安装。
  用法（在仓库根目录）：
      pwsh ./build.ps1
      pwsh ./build.ps1 -GameDir "D:\Steam\steamapps\common\Probably Stolen"
#>
param(
    [string]$GameDir,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$modFolderName = "pawnshop_cardroom"

function Find-GameDir {
    if ($GameDir) { return $GameDir }

    $candidates = New-Object System.Collections.Generic.List[string]

    # 1) Steam 注册表里登记的主库
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $steamPath = (Get-ItemProperty $key -ErrorAction SilentlyContinue).SteamPath
        if ($steamPath) {
            $candidates.Add((Join-Path $steamPath 'steamapps\common\Probably Stolen'))
            $vdf = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
            if (Test-Path $vdf) {
                foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                    $library = $m.Groups[1].Value -replace '\\\\', '\'
                    $candidates.Add((Join-Path $library 'steamapps\common\Probably Stolen'))
                }
            }
        }
    }

    # 2) 常见安装位置
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, 'D:\', 'E:\', 'F:\')) {
        if (-not $root) { continue }
        $candidates.Add((Join-Path $root 'Steam\steamapps\common\Probably Stolen'))
        $candidates.Add((Join-Path $root 'SteamLibrary\steamapps\common\Probably Stolen'))
        $candidates.Add((Join-Path $root 'Games\Probably Stolen'))
    }

    foreach ($path in $candidates) {
        if ($path -and (Test-Path (Join-Path $path 'Probably Stolen.exe'))) { return $path }
    }
    throw "没找到游戏目录。请用 -GameDir 指定放着 Probably Stolen.exe 的文件夹。"
}

$game = Find-GameDir
$managed = Join-Path $game 'Probably Stolen_Data\Managed'
if (-not (Test-Path (Join-Path $managed 'Assembly-CSharp.dll'))) {
    throw "在 $managed 里没找到 Assembly-CSharp.dll，确认这是游戏根目录。"
}

Write-Host "游戏目录：$game"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw "没找到 dotnet。先装 .NET SDK 8：https://dotnet.microsoft.com/download" }
Write-Host "dotnet：$(& dotnet --version)"

Write-Host "编译中……"
& dotnet build (Join-Path $PSScriptRoot 'PawnshopCardRoom.csproj') -c $Configuration "-p:GameManaged=$managed"
if ($LASTEXITCODE -ne 0) { throw "编译失败，上面的报错先修掉。" }

$built = Join-Path $PSScriptRoot "bin\$Configuration\netstandard2.1\PawnshopCardRoom.dll"
if (-not (Test-Path $built)) { throw "没找到编译产物：$built" }

$target = Join-Path $game "Mods\$modFolderName"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $built $target -Force
Copy-Item (Join-Path $PSScriptRoot 'manifest.xml') $target -Force

Write-Host ""
Write-Host "已安装到：$target" -ForegroundColor Green
Write-Host "进游戏后："
Write-Host "  1. Mod 菜单里找到「深空当铺：夜间棋牌室」，点「审核并启用」"
Write-Host "  2. 详情里打开「开发者模式」，以后重新编译就不用再批准"
Write-Host "  3. 关店之后在交通界面能看到「棋牌室」"
Write-Host "  出问题看日志：$env:USERPROFILE\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log"
