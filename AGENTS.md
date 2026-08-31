# AGENTS.md

> 给 AI 代理与新加入者的操作约定。**动手之前先读完本文件,以及它指向的文档。**

## 一、开工前必读:velashell-docs

VelaShell 生态的**全部文档**集中在一个仓库:
**[VelaShellLabs/velashell-docs](https://github.com/VelaShellLabs/velashell-docs)**。
本仓库**不放** `docs/`、`docs-en/` —— 设计手册、开发规范与开发文档都在那边。

**在动任何代码之前**,先把下表中与你要改的部分相关的几篇读掉。跳过这一步直接改,
结果通常是两种:与既有设计冲突,或者重复实现一个已经存在的能力。

| 位置 | 内容 |
| --- | --- |
| [`zh/host/`](https://github.com/VelaShellLabs/velashell-docs/tree/main/zh/host) | 宿主分层架构与依赖方向、工程化重构蓝图、交互与界面规格、快捷键参考、设置项审计,以及 SFTP / FTP / Telnet / 串口 / Redis / S3 / 系统密钥链等可行性调研 |
| [`zh/plugins/`](https://github.com/VelaShellLabs/velashell-docs/tree/main/zh/plugins) | 插件系统设计蓝图 01–15(进程模型、IPC 协议、权限系统、UI 扩展、威胁模型、路线图)与[进度总览 STATUS](https://github.com/VelaShellLabs/velashell-docs/blob/main/zh/plugins/STATUS.md) |
| [`zh/sdk/`](https://github.com/VelaShellLabs/velashell-docs/tree/main/zh/sdk) | 插件契约 SDK 参考、SDK 仓库的发版流程 |
| [`zh/cli/`](https://github.com/VelaShellLabs/velashell-docs/tree/main/zh/cli) | `vela-plugin` 命令行手册、CLI 仓库的发版流程 |
| [`zh/templates/`](https://github.com/VelaShellLabs/velashell-docs/tree/main/zh/templates) | 插件开发指南、打包与发布、模板仓库的发版流程 |

英文镜像在 [`en/`](https://github.com/VelaShellLabs/velashell-docs/tree/main/en),与 `zh/` 同构。
[仓库首页](https://github.com/VelaShellLabs/velashell-docs)有按「我想做什么」组织的快速入口表。

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

## 三、本仓库:velashell-plugin-sdk(插件契约 SDK)

产出 `VelaShell.PluginSdk`(契约程序集)与 `VelaShell.PluginSdk.Testing`(测试替身)两个 NuGet 包。
这是插件与宿主**唯一共享的那批类型**。

### 构建与测试

```bash
dotnet build VelaShell.PluginSdk.slnx
dotnet test  VelaShell.PluginSdk.slnx -c Debug
```

`-c Debug` 不是随口一说:Release 会打开强名称签名,而测试程序集不是签名友元,
Release 下 `dotnet test` 编不过。签名密钥不入库,CI 从 `STRONG_NAME_KEY` 机密还原。

### 只有本仓库能动的两件事

1. **`apiLevel` 纪律** —— `VelaPluginApi.Level` 是插件兼容性的整数代际,宿主拒载高于自身代际的插件。
   纪律是「SDK 主版本 == apiLevel」,由 `scripts/Set-Version.ps1` 在发版前硬核对。
   **破坏性变更要先手工把 `Level` +1**:脚本会核对但不代改,因为「契约破没破」是人的判断。
2. **Avalonia 版本锁的权威** —— 值写在 `Directory.Build.props` 的 `VelaAvaloniaVersion`,
   由本包经 `buildTransitive` 导出成 `$(VelaSdkPinnedAvaloniaVersion)`,宿主与 CLI 仓库在
   各自构建期核对。改它 = 改整个插件生态的 Avalonia 版本,必须与宿主同一波发布。

### 版本号不归你定 —— 也不要为了自证能编译去造本地包

**发版是人的决定,不是实现细节。** 给 SDK 加了新契约面之后:

- **不要**跑 `scripts/Set-Version.ps1`、不要动 `Directory.Build.props` 的 `VelaSdkVersion`、
  不要动 `VelaPluginApi.SdkVersion`。下一版是 1.6.0 还是 1.5.2、还是先发个 preview,
  取决于当时排了什么、要不要跟宿主同波发 —— 这些你不知道。
- **不要** `dotnet pack` 出本地包、不要在任何 `nuget.config` 里加本地源、
  不要把下游仓库的 `Directory.Packages.props` 指到一个还不存在的版本。
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

完整流程见 [`zh/sdk/release-process.md`](https://github.com/VelaShellLabs/velashell-docs/blob/main/zh/sdk/release-process.md)。

### 留在本仓库的文档

`README.md`、`LICENSE`,以及 `src/**/README.md`(各包自己的说明)。SDK 参考与发版流程都在 velashell-docs。
