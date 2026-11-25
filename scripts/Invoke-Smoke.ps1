#Requires -Version 7.0
<#
.SYNOPSIS
    端到端冒烟:拿刚打出来的包,站在**插件作者**的位置上走一遍。

.DESCRIPTION
    验的是 VelaShell.PluginSdk.Build:插件作者直接引用目标宿主的 Avalonia，通过本包得到契约与打包工具。
    (本脚本随该包一起从 velashell-plugin-cli 搬到本仓库,2026-09-11。)

    拆库(2026-08-27)之前这一步是"装模板 → dotnet new → 构建 → 出 .vpx"。模板搬去
    VelaShell/velashell-plugin-templates 之后,不能再依赖那个包来验自己的包
    —— 否则模板仓库出问题会让这里的 CI 无端变红,而且发包时还得先有模板包。

    所以夹具改成自带:tests/smoke/ 下是一个**手写的最小插件工程**,与 velaplugin-ui
    模板同形(命令 + 面板 + 编译期 AXAML)。本脚本把它复制到临时目录、把包版本填进去、
    从给定的本地源还原,然后:

      1. `dotnet build -t:PackVpx`  —— 覆盖 targets 的全部四件事:共享程序集不落地、
                                       Avalonia 版本核对、清单编译期校验、一步出包;
      2. 确认 bin/vpx/ 下真的有 .vpx;
      3. 用打包器的 info 把容器读回来(魔数、摘要、清单);
      4. 确认插件输出目录里**没有**共享程序集 —— 出现了就说明 exclude=Runtime 的链路断了。

    夹具刻意放在临时目录而不是原地构建:tests/smoke/ 里那两个空的
    Directory.Build.props/.targets 已经切断了向上查找,但复制出去更贴近真实
    (插件作者的工程不在本仓库里)。

.PARAMETER Feed
    本地 NuGet 源目录,里面应有刚打出的 VelaShell.PluginSdk.Build.<版本>.nupkg
    与 VelaShell.PluginSdk.<版本>.nupkg(.Build 依赖同版本的契约包,而那一版还没上 nuget.org)。
    本仓库三个包之外不需要任何别的东西 —— 打包器就在 .Build 包的 tools/ 里。

.PARAMETER Version
    要验的 VelaShell.PluginSdk.Build 版本号。

.PARAMETER PackerDll
    用来做第 3 步读回校验的打包器。默认**从刚打出的 .Build 包里取**
    (tools/net11.0/VelaShell.PluginSdk.Packer.dll)。

    刻意不用 src/VelaShell.PluginSdk.Packer 的 bin 产物:从包里取顺带核对了**打包器真的
    进了包**。漏了的话,插件作者 `-t:PackVpx` 时才会拿到 VELA1003,而那时已经发出去了。

.PARAMETER WorkDirectory
    工作目录。默认取 RUNNER_TEMP(CI)或系统临时目录。

.EXAMPLE
    pwsh scripts/Invoke-Smoke.ps1 -Feed ./artifacts/nuget -Version 1.5.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Feed,
    [Parameter(Mandatory)] [string] $Version,
    [string] $PackerDll,
    [string] $WorkDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$feedPath = (Resolve-Path $Feed).Path
$fixture = Join-Path $root 'tests/smoke'
if (-not (Test-Path $fixture)) { throw "冒烟夹具不存在:$fixture" }

if (-not $WorkDirectory) {
    $WorkDirectory = Join-Path ($env:RUNNER_TEMP ?? [IO.Path]::GetTempPath()) 'vela-plugin-smoke'
}
# 打包器解到**工作区之外**的兄弟目录:解进去的话,那些文件会成为插件工程的默认 None 项,
# 平白给这个"模拟插件作者"的工程加一堆它本来不会有的东西。
$packerDirectory = "$WorkDirectory.packer"
foreach ($stale in @($WorkDirectory, $packerDirectory)) {
    if (Test-Path $stale) { Remove-Item -Recurse -Force $stale }
}
New-Item -ItemType Directory -Force $WorkDirectory | Out-Null

# ── 打包器从哪来 ────────────────────────────────────────────────────────────
# 从**刚打出的那个包**里拆一份出来,而不是直接用 src/VelaShell.PluginSdk.Packer 的 bin ——
# 顺带核对打包器真的进了包(它随包分发是本包对插件作者的承诺)。
if (-not $PackerDll) {
    $nupkg = Join-Path $feedPath "VelaShell.PluginSdk.Build.$Version.nupkg"
    if (-not (Test-Path $nupkg)) { throw "本地源里没有 $nupkg;先 dotnet pack 出包再跑冒烟。" }
    New-Item -ItemType Directory -Force $packerDirectory | Out-Null
    # Expand-Archive 只认 .zip 扩展名,.nupkg 直接喂给它会被拒;先复制一份改名。
    $zip = Join-Path $packerDirectory 'pkg.zip'
    Copy-Item $nupkg $zip
    Expand-Archive -Path $zip -DestinationPath (Join-Path $packerDirectory 'x')
    $PackerDll = Join-Path $packerDirectory 'x/tools/net11.0/VelaShell.PluginSdk.Packer.dll'
    if (-not (Test-Path $PackerDll)) {
        throw @"
VelaShell.PluginSdk.Build.$Version.nupkg 里没有 tools/net11.0/VelaShell.PluginSdk.Packer.dll。
打包器随包分发是这个包对插件作者的承诺(`dotnet build -t:PackVpx` 不必装任何全局工具),
漏了的话插件作者只会拿到一句 VELA1003。看 VelaShell.PluginSdk.Build.csproj 的
AddVelaPackerToPackage。
"@
    }
}
if (-not (Test-Path $PackerDll)) { throw "找不到打包器:$PackerDll。" }

Write-Host "== 冒烟:VelaShell.PluginSdk.Build $Version =="
Write-Host "   源     $feedPath"
Write-Host "   打包器 $PackerDll"
Write-Host "   工作区 $WorkDirectory"

# ── 铺夹具 ──────────────────────────────────────────────────────────────────
Copy-Item -Recurse -Force (Join-Path $fixture '*') $WorkDirectory

$csproj = Join-Path $WorkDirectory 'Smoke.csproj'
$text = [IO.File]::ReadAllText($csproj)
if ($text -notmatch 'VELA_BUILD_VERSION') {
    throw "tests/smoke/Smoke.csproj 里找不到 VELA_BUILD_VERSION 占位符;夹具改过了?"
}
[IO.File]::WriteAllText($csproj, $text.Replace('VELA_BUILD_VERSION', $Version))

# CI 的版本号可能已经发布。隔离包缓存并限定来源，确保验的是刚打出的 SDK。
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feedPath" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="VelaShell.PluginSdk*" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $WorkDirectory 'nuget.config')

$previousPackages = $env:NUGET_PACKAGES
Push-Location $WorkDirectory
try {
    $env:NUGET_PACKAGES = Join-Path (Get-Location) 'obj/packages'
    # ── 1. 构建 + 出包 ──────────────────────────────────────────────────────
    dotnet build -c Release -t:PackVpx --nologo
    if ($LASTEXITCODE -ne 0) { throw "插件工程构建失败(见上方输出)。" }

    # ── 2. 产物在不在 ───────────────────────────────────────────────────────
    $vpx = Get-ChildItem 'bin/vpx/*.vpx' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $vpx) { throw "PackVpx 没产出 .vpx。" }
    Write-Host "   产物   $($vpx.Name)"

    # ── 3. 容器读得回来吗 ───────────────────────────────────────────────────
    dotnet $PackerDll info $vpx.FullName
    if ($LASTEXITCODE -ne 0) { throw "打包器 info 读不回刚打出的 .vpx。" }

    # ── 4. 共享程序集有没有漏进插件输出目录 ─────────────────────────────────
    # 漏了就说明 exclude=Runtime 的链路断了。这不是"包大了一点"的问题:
    # 装载器一律让这些程序集回落到宿主那份,插件目录里那些副本只会误导人,
    # 让人以为版本是自己带的那份说了算。
    $leaked = Get-ChildItem 'bin/Release/net11.0' -Filter '*.dll' |
        Where-Object { $_.Name -like 'Avalonia*' -or $_.Name -eq 'VelaShell.PluginSdk.dll' }
    if ($leaked) {
        throw "共享程序集漏进了插件输出目录:$($leaked.Name -join ', ')"
    }

    # UI 由目标宿主选版，不是 .Build 的传递依赖；非 UI 插件不该被迫还原 Avalonia。
    $assets = Get-Content 'obj/project.assets.json' -Raw | ConvertFrom-Json -AsHashtable
    $graph = @($assets.targets.Values)[0]
    if ($graph["VelaShell.PluginSdk.Build/$Version"].dependencies.Contains('Avalonia')) {
        throw '.Build must not select an Avalonia dependency for plugins.'
    }
    $exported = dotnet msbuild Smoke.csproj -nologo -getProperty:VelaSdkPackageVersion
    if ($LASTEXITCODE -ne 0 -or $exported -ne $Version) { throw "Expected SDK version export $Version, got '$exported'." }

    # 不重新还原:保留真实的编译引用，模拟声明了另一版目标宿主的工程。
    $mismatch = dotnet build --no-restore -c Release -p:VelaAvaloniaVersion=0.0.1 --nologo 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $mismatch -notmatch 'VELA1001') { throw "Expected VELA1001 for a host mismatch: $mismatch" }

    Write-Host "== 冒烟通过 =="
}
finally {
    $env:NUGET_PACKAGES = $previousPackages
    Pop-Location
}

exit 0
