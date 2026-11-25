# AGENTS.md

> 给 AI 代理与新加入者的操作约定。**动手之前先读完本文件,以及它指向的文档。**

## 一、开工前必读:velashell-docs

VelaShell 生态的**全部文档**集中在一个仓库:
**[VelaShell/velashell-docs](https://github.com/VelaShell/velashell-docs)**。
本仓库**不放** `docs/`、`docs-en/` —— 设计手册、开发规范与开发文档都在那边。

**在动任何代码之前**,先把下表中与你要改的部分相关的几篇读掉。跳过这一步直接改,
结果通常是两种:与既有设计冲突,或者重复实现一个已经存在的能力。

| 位置 | 内容 |
| --- | --- |
| [`zh/host/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/host) | 宿主分层架构与依赖方向、工程化重构蓝图、交互与界面规格、快捷键参考、设置项审计,以及 SFTP / FTP / Telnet / 串口 / Redis / S3 / 系统密钥链等可行性调研 |
| [`zh/plugins/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/plugins) | 插件系统设计蓝图 01–15(进程模型、IPC 协议、权限系统、UI 扩展、威胁模型、路线图)与[进度总览 STATUS](https://github.com/VelaShell/velashell-docs/blob/main/zh/plugins/STATUS.md) |
| [`zh/sdk/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/sdk) | 插件契约 SDK 参考、SDK 仓库的发版流程 |
| [`zh/cli/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/cli) | `vela-plugin` 命令行手册、CLI 仓库的发版流程 |
| [`zh/templates/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/templates) | 插件开发指南、打包与发布、模板仓库的发版流程 |

英文镜像在 [`en/`](https://github.com/VelaShell/velashell-docs/tree/main/en),与 `zh/` 同构。
[仓库首页](https://github.com/VelaShell/velashell-docs)有按「我想做什么」组织的快速入口表。

## 二、涉及文档的改动一律同步到 velashell-docs

**这是本文件最重要的一条。**

- 本仓库里**不新建** `docs/`、`docs-en/` 或任何成体系的文档目录。要写文档,去 velashell-docs 开 PR。
- 改了代码,而**行为、接口、配置项、命令行、构建流程或版本纪律**与现有文档对不上时,
  必须**同时**在 velashell-docs 提一个 PR 把文档改过来。两个 PR 在正文里互相引用,一起合。
  只改代码不改文档,等于让文档开始骗人 —— 而文档是别人照抄的。
- velashell-docs 的 `zh/` 与 `en/` 是**互为镜像**的两棵树,文件一一对应。改了中文就要改英文,
  反之亦然。漏一边,两棵树就开始漂。
- velashell-docs 内部的互相引用**一律走相对路径**(如 `../templates/dev-guide.md`),
  不要写回 GitHub 绝对 URL —— 文档集中到一个仓库,消掉的正是那种一改路径就断的跨仓库链接。
- **例外**:留在代码仓库里的少数几份文件不适用上述规则,因为它们服务的是「在这个仓库里写代码」
  这件事,搬走只会离使用场景更远。各仓库的例外清单见下面第三节。

## 三、本仓库:velashell-plugin-sdk(插件契约 SDK + 构建支持包)

产出三个**同版本、同一次发布**的 NuGet 包:

| 包 | 是什么 |
| --- | --- |
| `VelaShell.PluginSdk` | 契约程序集 —— 插件与宿主**唯一共享的那批类型** |
| `VelaShell.PluginSdk.Testing` | 测试替身 |
| `VelaShell.PluginSdk.Build` | 插件构建支持:targets + 随包分发的打包器 + 宿主 UI 版本校验 |

外加一个**不发包**的工程:`src/VelaShell.PluginSdk.Packer`(`IsPackable=false`)——
`.vpx` 打包器,只以构建产物的形式躺在 `.Build` 包的 `tools/` 下。

`.Build` 于 2026-09-11 从 `velashell-plugin-cli` 搬来,打包器随后也做进了本仓库。
两者都走 `ProjectReference`,所以发给插件作者的契约与打包器永远就是本仓库这一版 ——
这正是搬过来要的效果:没有任何跨仓库的版本需要对齐。

### 构建与测试

```bash
dotnet build VelaShell.PluginSdk.slnx
dotnet test  VelaShell.PluginSdk.slnx -c Debug

# 端到端冒烟:拿刚打出的包当插件作者走一遍
dotnet pack src/VelaShell.PluginSdk/VelaShell.PluginSdk.csproj             -c Release -o artifacts/nuget
dotnet pack src/VelaShell.PluginSdk.Build/VelaShell.PluginSdk.Build.csproj -c Release -o artifacts/nuget
pwsh scripts/Invoke-Smoke.ps1 -Feed ./artifacts/nuget -Version <版本>
```

`-c Debug` 不是随口一说:Release 会打开强名称签名,而测试程序集不是签名友元,
Release 下 `dotnet test` 编不过。签名密钥不入库,CI 从 `STRONG_NAME_KEY` 机密还原;
本地想打 Release 包又没有密钥时加 `-p:SignAssembly=false`。

冒烟夹具在 `tests/smoke/`:一个手写的最小插件工程,刻意带两个空的
`Directory.Build.props`/`.targets` 来切断向上查找 —— **插件工程是仓库外环境,仓库内的
构建约定一条也吃不到**。**改了 `.Build` 的 targets、或抬了它引用的 vela-plugin 版本,必须跑它。**

### 只有本仓库能动的两件事

1. **`apiLevel` 纪律** —— `VelaPluginApi.Level` 是插件兼容性的整数代际,宿主拒载高于自身代际的插件。
   纪律是「SDK 主版本 == apiLevel」,由 `scripts/Set-Version.ps1` 在发版前硬核对。
   **破坏性变更要先手工把 `Level` +1**:脚本会核对但不代改,因为「契约破没破」是人的判断。
2. **共享 UI 程序集的兼容性** —— 宿主的 `src/Directory.Packages.props` 决定 Avalonia / AvaloniaEdit 版本。使用它们的插件显式声明目标版本与编译依赖；`.Build` 核对实际解析版本，不在 SDK 中新增 UI 依赖或版本常量。宿主升级 Avalonia 不需要发布 SDK。

### 打包器为什么在本仓库、为什么是独立进程

`.vpx` 的定义(`VpxContainer`)与清单规则(`PluginManifestReader`)本来就在
`VelaShell.PluginSdk` 里 —— `vela-plugin` 只是它的另一个调用方。让 `.Build` 绕一趟 cli 仓库
去取打包器,换来的只是一个要人盯着的跨仓库版本旋钮(2026-09-11 短暂存在过,已去掉)。

**刻意不做成 MSBuild 任务**:VS 的 MSBuild 跑在 .NET Framework 上,而本仓库是 net11.0。
做成 `<UsingTask>` 的话,插件作者在 VS 里一按生成就会因为加载不了任务程序集而失败 ——
而清单校验是 `AfterTargets="Build"` 的,每次生成都跑。`dotnet exec` 一个独立进程则与调用方
的 MSBuild 是哪种运行时完全无关。

改了打包器的命令面(`validate` / `pack` 的参数或输出),`.Build` 的 `build/*.targets`
要跟着改,**并跑一次 `scripts/Invoke-Smoke.ps1`** —— 那个冒烟是这条链唯一的守门人。

### 版本号不归你定 —— 也不要为了自证能编译去造本地包

**发版是人的决定,不是实现细节。** 给 SDK 加了新契约面之后:

- **不要**跑 `scripts/Set-Version.ps1`、不要动 `Directory.Build.props` 的 `VelaSdkVersion`、
  不要动 `VelaPluginApi.SdkVersion`。下一版是 1.6.0 还是 1.5.2、还是先发个 preview,
  取决于当时排了什么、要不要跟宿主同波发 —— 这些你不知道。
- **不要** `dotnet pack` 出本地包给**别的仓库**用、不要在任何 `nuget.config` 里加本地源、
  不要把下游仓库的 `Directory.Packages.props` 指到一个还不存在的版本。
  (例外只有一个:`scripts/Invoke-Smoke.ps1` 会把包打进本仓库的临时源自己验自己,
  那是 CI 的常规步骤,产物不出本仓库、也不进任何人的 `nuget.config`。)
  自己造一个包来让编译通过,等于把「这套东西还没发布」这个事实从构建结果里抹掉;
  而且本地包必然**未签名**(`VelaShell.snk` 不在仓库里),下游一编译就是一片
  `CS0012` 强名称不匹配 —— 那是自己制造的噪声,不是真问题。
- 文档、XML 注释、版本历史表里需要写版本号的地方,一律写 `TBD` 并在同一句里说明
  「发版时替换」。`grep -rn TBD` 要能一次找全。
- 交付时**明确说一句**:契约已就位,需要发一版 SDK;下游仓库(宿主、插件)在包发布前
  编译不过是预期内的,发布后把各自的 `Directory.Packages.props` 抬上去即可。

下游同理:宿主与插件仓库的 `Directory.Packages.props` / `plugin.json` 里的
`minSdkVersion`,在包真的发布之前都不要改。

### 发版脚本会写 velashell-docs

`scripts/Set-Version.ps1` 的版本横幅落点有两处在**另一个仓库**:
`zh/sdk/sdk-reference.md` 与 `en/sdk/sdk-reference.md`。脚本按
`-DocsRoot` → `$env:VELASHELL_DOCS` → 同级 `../velashell-docs` 依次找,找不到就跳过并警告。
**本地开发建议把 velashell-docs clone 在本仓库同级目录**,这样发版时横幅一起更新。

完整流程见 [`zh/sdk/release-process.md`](https://github.com/VelaShell/velashell-docs/blob/main/zh/sdk/release-process.md)。

### 留在本仓库的文档

`README.md`、`LICENSE`,以及 `src/**/README.md`(各包自己的说明)。SDK 参考与发版流程都在 velashell-docs。

### 提交约束

- **所有提交信息与 PR 描述都不要附加 Claude Code 等 AI 工具的作者信息** —— 不加 `Co-Authored-By: Claude …` 行,也不加 `🤖 Generated with Claude Code` 之类的署名。VelaShell 下所有仓库一致适用。
