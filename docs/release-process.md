# 发版流程(契约 SDK)

> 本篇只讲**本仓库**怎么发版。`vela-plugin` / `VelaShell.PluginSdk.Build` 见
> [velashell-plugin-cli](https://github.com/joesdu/velashell-plugin-cli/blob/main/docs/release-process.md),
> `dotnet new` 模板见
> [velashell-plugin-templates](https://github.com/joesdu/velashell-plugin-templates/blob/main/docs/release-process.md)。

本仓库一次发布产出两个包,共用 Release 标签里的版本号:

| 包 | 内容 |
| --- | --- |
| `VelaShell.PluginSdk` | 契约程序集 |
| `VelaShell.PluginSdk.Testing` | 测试替身 |

**下游不必跟着发。** 拆库(2026-08-27)之后 cli 与 templates 两个仓库各有各的版本号,
它们只在想吃到新契约时才把自己引用的 `VelaShell.PluginSdk` 版本抬上来。SDK 发 1.6.0
不代表 `vela-plugin` 要变成 1.6.0 —— 那正是拆库要的效果。

---

## 一、怎么发

三步:

1. **破坏性变更**才需要:手工把 `src/VelaShell.PluginSdk/VelaPluginApi.cs` 里的
   `VelaPluginApi.Level` +1。脚本会核对但**不代改** —— 「契约破没破」是人的判断,
   不是版本号的推论。

2. 本地落版本号,连同功能改动一起合进 `main`:

   ```powershell
   pwsh scripts/Set-Version.ps1 1.6.0
   ```

3. 在 GitHub 上**发 Release**,标签填 `v1.6.0`(带不带 `v` 都行,流水线会 `TrimStart`,
   但建议统一带)。预发布勾 prerelease,标签用 `v1.6.0-preview.1`。

流水线在解析出标签之后**第一件事**也会跑一遍 `Set-Version.ps1`,只改 runner 上的工作区、
**不回写仓库** —— 于是产物版本号永远等于 Release 标签,与仓库里当时提交了什么无关。

### 忘了在发版前落版本号怎么办

不影响这一次发布(Stamp 步骤已经兜住了),但 `main` 落后了:`main` 上的 CI
「Version consistency check」会红一次。照它给的命令本地跑一遍 `Set-Version.ps1`,
补一个 PR 合掉即可。

### 手动补跑

`release` 事件偶尔不触发。Actions 页面 → 选 Release 工作流 → Run workflow → 填标签。
推送用 `--skip-duplicate`,对同一标签重复跑是幂等的。想只验不推,勾上 `dryRun`。

---

## 二、NuGet 可信发布(Trusted Publishing)怎么调

推送不存 API Key:工作流拿本次运行的 GitHub OIDC 令牌去 nuget.org 换一把 1 小时有效的
临时密钥。nuget.org 那边靠一条**策略**决定「哪个仓库的哪个工作流可以代表我推包」。

⚠️ **拆库之后需要三条策略,一个仓库一条** —— 策略是按 (owner, repository, workflow file)
匹配的,三个仓库这三项各不相同。策略的 owner 覆盖该账号名下**全部**包,所以不必按包开。

本仓库这一条填:

| 策略字段 | 值 |
| --- | --- |
| Policy name | `velashell-plugin-sdk`(随意,能认出来就行) |
| Policy owner | `joes_du` |
| Repository Owner | `VelaShellLabs` |
| **Repository** | `velashell-plugin-sdk` |
| **Workflow File** | `release.yml` —— **只填文件名**,不要写 `.github/workflows/` 前缀 |
| Environment | 留空(工作流没用 GitHub Environments) |

建法:登录 nuget.org → 右上角用户名 → **Trusted Publishing** → **Add**。

顺带把拆库前那条指向 `velashell-plugin-toolchain` 的策略删掉 —— 那个仓库不再推包,
留着就是一条多余的信任面。

### ⚠️ 新策略有 7 天窗口

私有仓库上新建的策略是「**临时激活**」状态,7 天内必须成功发布一次,否则自动失效
(可以随时重开窗口)。原因是 nuget.org 要在第一次成功发布时把 GitHub 的 repository ID
与 owner ID 记进策略,用来把它钉死在那个仓库上(防「删库重建同名仓库」的复活攻击)——
没有一次真实发布就拿不到那两个 ID。所以**建好策略就尽快发一次**,哪怕是 preview 版。

公开仓库通常直接是永久激活状态,但发一次验证仍然是省事的做法。

### 换不到密钥时先看这三样

`NuGet login` 那一步失败,九成是策略对不上:

* Repository 还写着 `velashell-plugin-toolchain`;
* Workflow File 写成了 `.github/workflows/release.yml`;
* `NUGET_USER` 填成了邮箱 —— 要的是 nuget.org 的**用户名**(profile name)。
  工作流默认取 `vars.NUGET_USER`,没配则回落到 `joes_du`。

另外 job 上的 `permissions: id-token: write` 不能少,否则 GitHub 根本不签发 OIDC 令牌。

---

## 三、版本号纪律

`Directory.Build.props` 的 `VelaSdkVersion` 是本仓库的默认版本,发布时由标签覆盖。

### 三个版本号各司其职,别混

| 属性 | 值 | 作用 |
| --- | --- | --- |
| `AssemblyVersion` | `<主版本>.0.0.0` | **绑定标识**,只随主版本动。插件是编译期绑到这个标识上的 |
| `FileVersion` | 完整数字版(`1.6.0`) | 资源管理器属性页看到的那个 |
| `InformationalVersion` | 完整版本含预发布后缀 | `vela-plugin` 报的版本 |

`AssemblyVersion` 钉在主版本上,是因为让它跟着补丁号动等于每发一次补丁就要所有已编译
插件重新绑定,毫无收益。而主版本变了意味着契约破了 —— 那一刻 **apiLevel 必须同步 +1**,
于是老宿主在**发现期**就按 apiLevel 干净拒载,而不是等装载时抛一个看不懂的绑定异常。

### 「SDK 主版本 == apiLevel」

这条纪律由 `Set-Version.ps1` 硬核对:要发 `2.x.x` 就必须先把 `VelaPluginApi.Level` 改成 `2`,
反之亦然。apiLevel 是**契约的属性**,所以这条检查只在本仓库有落点 —— CLI 和模板都无权动它。

### 版本号的落点(由脚本统一维护)

拆库之后本仓库只剩四处:

| 落点 | 漏改的后果 |
| --- | --- |
| `Directory.Build.props` 的 `VelaSdkVersion` | 包版本不对 |
| `src/VelaShell.PluginSdk/VelaPluginApi.cs` 的 `SdkVersion` | **什么都不会报错**,只是 `vela-plugin doctor` 从此汇报一个错的宿主 SDK 版本,插件的 `minSdkVersion` 门槛跟着判错 |
| `docs/sdk-reference.md` 版本横幅 | 给人照抄的过期数字 |
| `docs-en/sdk-reference.md` 版本横幅 | 同上 |

跑 `pwsh scripts/Set-Version.ps1 <版本>` 一次全改。CI 用 `-Check` 做体检。

---

## 四、Avalonia 版本锁:本仓库是权威

插件工程编译期拿到的 Avalonia,与它在宿主进程里运行时被强制共享的那份宿主 Avalonia,
必须是同一个版本 —— 装载器让 `Avalonia*` 一律回落到装载方,版本漂了就是跨 ALC 的
控件类型对不上,而且要等到用户装上插件才炸。

单仓库时代这个核对是 `VelaShell.PluginSdk.Build` 直接 `XmlPeek` 宿主的
`src/Directory.Packages.props` 做的。拆库之后读不到对方的文件了,于是反过来:

```
Directory.Build.props: <VelaAvaloniaVersion>12.1.1</VelaAvaloniaVersion>
        ↓ 打包时写进 buildTransitive/VelaShell.PluginSdk.props
$(VelaSdkPinnedAvaloniaVersion)
        ↓ 引用 VelaShell.PluginSdk 的工程都吃得到
  ├── 宿主 src/Directory.Build.targets 的 VerifyAvaloniaMatchesSdk
  └── cli 仓库 VelaShell.PluginSdk.Build 的 VerifyAvaloniaVersionPin
```

包里那个 props 文件是这个事实传给下游的**唯一通道**,它没打进包也不会让 `pack` 失败 ——
所以 CI 与 Release 都有一步「Verify the pin props shipped in the package」,拆开 nupkg 核对。

**改 `VelaAvaloniaVersion` 的完整动作**:

1. 与宿主同一波发布(宿主必须引用同一版本);
2. 发一版 SDK;
3. 去 cli 仓库把 `VelaSdkPackageVersion` 抬到这一版,并同步它的 `VelaAvaloniaVersion` ——
   否则那道 `VerifyAvaloniaVersionPin` 会红;
4. 发一版 `VelaShell.PluginSdk.Build`,插件工程才拿得到新锁。

---

## 五、为什么测试必须 Debug

`src/Directory.Build.props` 在 Release 下打开强名称签名(宿主已签名,已签名程序集不能
引用未签名程序集,所以 SDK 必须用与宿主同一把钥匙签)。而签名程序集的
`InternalsVisibleTo` 必须带友元公钥、友元本身也得用同一把钥匙签 —— 测试程序集两条都不满足。

这些用例验的是契约,与优化级别无关,Debug 等价;且 Debug 的 `obj/bin` 与 `pack` 用的
Release 目录彼此隔离,不会把未签名产物混进要发布的包里。

密钥不入库(`.gitignore` 里 `*.snk`),CI 从 `STRONG_NAME_KEY` 机密还原到仓库根。
这是本仓库**唯一**需要的机密。

> cli 与 templates 两个仓库都不签名,因此**不需要**这把钥匙 ——
> 未签名程序集可以引用已签名的,方向是对的。
