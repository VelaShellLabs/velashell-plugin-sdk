#Requires -Version 7.0
<#
.SYNOPSIS
    把 SDK 版本号写进本仓库里所有需要它的地方。

.DESCRIPTION
    拆库之后本仓库只管**契约包**的版本号(VelaShell.PluginSdk / .Testing),落点缩到四处:

      Directory.Build.props            <VelaSdkVersion>          —— 包版本的默认值
      src/…/VelaPluginApi.cs           SdkVersion 常量           —— 宿主写进 host.json 的值,
                                                                    vela-plugin doctor 拿它跟
                                                                    插件的 minSdkVersion 比对
      docs/sdk-reference.md            版本横幅
      docs-en/sdk-reference.md         版本横幅

    忘了改 VelaPluginApi.SdkVersion:**没有任何东西会报错** —— 只是 `vela-plugin doctor`
    从此汇报一个错的宿主 SDK 版本,插件的 minSdkVersion 门槛跟着判错。这一处最值得自动化。
    docs 那两处不影响功能,但它们是给人照抄的。

    **不在本仓库的落点**(2026-08-27 拆库起,各自由所在仓库的同名脚本管):
      · dotnet new 模板的 sdkVersion 默认值 …… velashell-plugin-templates
      · docs/dev-guide.md 的 PackageReference 片段 …… velashell-plugin-templates
      · docs/cli.md 的版本横幅 …………………………… velashell-plugin-cli
    那几处跟的是 VelaShell.PluginSdk.Build / VelaShell.Plugin.Cli 的版本,与本仓库无关 ——
    这正是拆库要的效果:SDK 发 1.6.0 不必惊动模板和 CLI。

    发版流水线在解析出 Release 标签之后**第一件事**也会跑本脚本
    (见 .github/workflows/release.yml),因此产物永远与标签一致,与仓库里当时提交了什么
    无关。正常路径上那一次是空操作;它只改 runner 上的工作区,**不回写仓库**。
    忘了在本地跑的兜底是 CI 的 -Check 体检。

.PARAMETER Version
    目标版本,SemVer(1.5.0 或 1.5.0-preview.1)。

.PARAMETER Check
    只报告不落盘;有任何一处不同步就以退出码 1 结束。CI 用它做"仓库是否已同步"的体检。

.EXAMPLE
    pwsh scripts/Set-Version.ps1 1.6.0

.EXAMPLE
    pwsh scripts/Set-Version.ps1 1.6.0 -Check
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)] [string] $Version,
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$') {
    throw "'$Version' 不是合法 SemVer。用 1.6.0 或 1.6.0-preview.1 这种形式。"
}
$major = [int]$Matches[1]

$root = Split-Path -Parent $PSScriptRoot

# ── apiLevel 纪律的硬检查 ────────────────────────────────────────────────────
# 纪律:SDK 主版本 == apiLevel。主版本变意味着契约破了,那一刻 VelaPluginApi.Level 必须
# 同步 +1 —— 老宿主于是在**发现期**按 apiLevel 干净拒载(可读原因 + Incompatible 状态),
# 而不是等装载时抛一个看不懂的程序集绑定异常。
#
# 这一步刻意**不自动改** Level:「契约破没破」是人的判断,不是版本号的推论。
# 但判断做完之后忘了落到代码里,是完全可能的 —— 所以在这里挡住。
#
# 拆库之后这条纪律**只在本仓库**有落点:apiLevel 是契约的属性,CLI 和模板都无权动它。
$apiFile = Join-Path $root 'src/VelaShell.PluginSdk/VelaPluginApi.cs'
$levelMatch = [regex]::Match((Get-Content -Raw $apiFile), 'public const int Level = (\d+);')
if (-not $levelMatch.Success) { throw "在 src/VelaShell.PluginSdk/VelaPluginApi.cs 里找不到 VelaPluginApi.Level。" }
$level = [int]$levelMatch.Groups[1].Value
if ($level -ne $major) {
    throw @"
版本 $Version 的主版本是 $major,但 VelaPluginApi.Level 是 $level。
纪律是「SDK 主版本 == apiLevel」:
  · 要发 $major.x.x,先把 VelaPluginApi.Level 改成 $major —— 但那等于宣布契约破了,
    确认破坏性变更确实存在再动;
  · 若契约其实没破,那就不该跳主版本,发 $level.x.x 系列即可。
"@
}

# ── 落点清单 ────────────────────────────────────────────────────────────────
# 每条都用**锚定到上下文**的模式,不做"全局替换旧版本号"。后者会误伤示例输出里那些
# 只是碰巧等于当前版本的数字。
$edits = [System.Collections.Generic.List[hashtable]]::new()

$edits.Add(@{
    Path    = 'Directory.Build.props'
    Pattern = '(?<pre><VelaSdkVersion Condition="[^"]*">)(?<val>[^<]+)(?<post></VelaSdkVersion>)'
    What    = 'VelaSdkVersion'
})
$edits.Add(@{
    Path    = 'src/VelaShell.PluginSdk/VelaPluginApi.cs'
    Pattern = '(?<pre>public const string SdkVersion = ")(?<val>[^"]+)(?<post>";)'
    What    = 'VelaPluginApi.SdkVersion'
})
$edits.Add(@{
    Path    = 'docs/sdk-reference.md'
    Pattern = '(?<pre>适用版本:\*\*SDK )(?<val>\S+)(?<post> / apiLevel)'
    What    = '版本横幅'
})
$edits.Add(@{
    Path    = 'docs-en/sdk-reference.md'
    Pattern = '(?<pre>Applies to \*\*SDK )(?<val>\S+)(?<post> / apiLevel)'
    What    = 'version banner'
})

# ── 应用 ────────────────────────────────────────────────────────────────────
$changed = [System.Collections.Generic.List[object]]::new()
foreach ($edit in $edits) {
    $path = Join-Path $root $edit.Path
    if (-not (Test-Path $path)) { throw "落点文件不存在:$($edit.Path)" }

    $text = [IO.File]::ReadAllText($path)
    $found = [regex]::Matches($text, $edit.Pattern)
    if ($found.Count -eq 0) {
        # 模式失配 = 文件结构变了而本脚本没跟上。静默跳过等于把"漏改一处"重新放回来,
        # 所以这里直接断掉,让人当场看见。
        throw "在 $($edit.Path) 里没匹配到「$($edit.What)」。文件结构改过了?请同步更新 scripts/Set-Version.ps1。"
    }

    $stale = @($found | Where-Object { $_.Groups['val'].Value -cne $Version })
    if ($stale.Count -eq 0) { continue }

    $changed.Add([pscustomobject]@{
        File = $edit.Path
        What = $edit.What
        From = (($stale | ForEach-Object { $_.Groups['val'].Value } | Select-Object -Unique) -join ', ')
        To   = $Version
    })
    if ($Check) { continue }

    $updated = [regex]::Replace($text, $edit.Pattern, {
        param($m) $m.Groups['pre'].Value + $Version + $m.Groups['post'].Value
    })
    # 保留文件原有的 BOM 状态:仓库里 .cs 带 BOM、.props/.json/.md 不带,
    # 顺手统一会让 diff 里多出一堆与版本号无关的整文件改动。
    $bytes = [IO.File]::ReadAllBytes($path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($hasBom))
}

if ($changed.Count -eq 0) {
    Write-Host "版本已经是 $Version,全部落点同步,无需改动。"
    exit 0
}

$changed | Format-Table -AutoSize | Out-String | Write-Host

if ($Check) {
    Write-Host "::error::仓库里的版本号与 $Version 不同步(见上表)。跑 ``pwsh scripts/Set-Version.ps1 $Version`` 修正。"
    exit 1
}

Write-Host "已把 $($changed.Count) 处落点更新到 $Version。"

# 显式 exit 0,别靠"脚本正常结束"隐含成功。
# 调用方是 `& ./scripts/Set-Version.ps1 ...` 后面跟一句 if ($LASTEXITCODE) —— 而 .ps1
# **不调用 exit 就根本不会设置 $LASTEXITCODE**,它会原样保留调用方进程里的旧值。
# GitHub 的每个 pwsh 步骤都是全新进程,那里的旧值是 $null,于是 `$LASTEXITCODE -ne 0`
# 求值为真 —— 脚本明明改好了文件,步骤却报 exit code 1。
exit 0
