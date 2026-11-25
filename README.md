# VelaShell 插件契约 SDK

[VelaShell](https://github.com/joesdu/VelaShell) 插件的**契约层**:插件与宿主唯一共享的那批类型,
以及插件工程引用的那一个构建支持包。

| 包 | 内容 |
| --- | --- |
| [`VelaShell.PluginSdk`](https://www.nuget.org/packages/VelaShell.PluginSdk) | 契约程序集:插件入口、`IPluginContext` 与全部能力接口、DTO、`plugin.json` 清单模型、`.vpx` 容器格式、宿主注册表 |
| [`VelaShell.PluginSdk.Testing`](https://www.nuget.org/packages/VelaShell.PluginSdk.Testing) | 测试替身:`TestPluginContext` 与各能力的内存实现,不起宿主也能测插件 |
| [`VelaShell.PluginSdk.Build`](https://www.nuget.org/packages/VelaShell.PluginSdk.Build) | 插件构建支持:MSBuild targets + 随包分发的打包器(`src/VelaShell.PluginSdk.Packer`,不单独发包)+ 契约程序集 + 宿主 UI 版本校验 |

> **插件作者引用 `VelaShell.PluginSdk.Build`**,契约程序集会随它传递进来 ——
> 见[开发指南](https://github.com/VelaShell/velashell-docs/blob/main/zh/templates/dev-guide.md)。

三个包**同版本、同一次发布**。`.Build` 于 2026-09-11 从 `velashell-plugin-cli` 搬到这里,
换来的正是这一点:它引用契约走 `ProjectReference`,发给插件作者的那份契约就是本仓库这一版,
「.Build 引用的是哪一版契约」这个旋钮连同它漂移的可能一并消失。

## 插件生态的仓库分布

2026-08-27 起工具链按发布节奏拆成三个仓库,各有各的版本号,**不要求同步发版**:

| 仓库 | 产出 | 什么时候发 |
| --- | --- | --- |
| **本仓库** `velashell-plugin-sdk` | `VelaShell.PluginSdk`、`.Testing`、`.Build` | 契约有增删改时,或 MSBuild/打包逻辑变化时 |
| [`velashell-plugin-cli`](https://github.com/VelaShell/velashell-plugin-cli) | `VelaShell.Plugin.Cli`(`vela-plugin`) | 命令行工具本身变化时 |
| [`velashell-plugin-templates`](https://github.com/VelaShell/velashell-plugin-templates) | `VelaShell.Plugin.Templates` | 模板内容变化,或要把新建工程指到新版 Build 包时 |

另外三个相关仓库:[joesdu/VelaShell](https://github.com/joesdu/VelaShell)(宿主主程序)、
[VelaShell/velashell-plugins](https://github.com/VelaShell/velashell-plugins)(第一方插件)、
[VelaShell/velashell-docs](https://github.com/VelaShell/velashell-docs)(**全部文档**,
2026-08-30 起各仓库的 `docs/` 都搬到了那里)。

依赖方向是单向的,没有环 —— **本仓库无上游**:

```
velashell-plugin-sdk                  ← 本仓库(契约 + .Build + 打包器)
        ↓ NuGet: VelaShell.PluginSdk
velashell-plugin-cli                  vela-plugin(面向人的完整工具)
        ↓ NuGet: VelaShell.PluginSdk.Build
velashell-plugin-templates            dotnet new velaplugin / velaplugin-ui
```

插件工程出包用的打包器是本仓库自带的 `VelaShell.PluginSdk.Packer`,**不是** `vela-plugin` ——
`.vpx` 的定义(`VpxContainer`)与清单规则本来就在 `VelaShell.PluginSdk` 里,`vela-plugin`
也只是它的另一个调用方。两边包格式一致由类型保证,不靠跨仓库的版本对齐。

所以:**本仓库发 1.6.0,下游一个都不用动**。它们只在想吃到新契约时才把自己引用的
`VelaShell.PluginSdk` 版本抬上来。

## 本仓库额外承担的两件事

这两件事没有别的地方能做,拆库时刻意留在了契约层:

1. **`apiLevel` 纪律** —— `VelaPluginApi.Level` 是插件兼容性的整数代际,宿主拒载高于自身
   代际的插件。纪律是「SDK 主版本 == apiLevel」,由 `scripts/Set-Version.ps1` 在发版前硬核对。
   apiLevel 是契约的属性,CLI 和模板都无权动它。

2. **共享 UI 程序集的兼容性** —— Avalonia 由目标宿主选版，不由 SDK 发版。使用 UI 的插件显式引用对应版本；构建时核对实际解析版本，并从插件输出排除共享程序集。配置见 [.Build 说明](src/VelaShell.PluginSdk.Build/README.md)。

## 在本仓库里开发

```bash
dotnet build VelaShell.PluginSdk.slnx
dotnet test  VelaShell.PluginSdk.slnx -c Debug

# 端到端冒烟:拿刚打出的包当插件作者走一遍(改了 .Build 的 targets 或打包器版本后必跑)
dotnet pack src/VelaShell.PluginSdk/VelaShell.PluginSdk.csproj             -c Release -o artifacts/nuget
dotnet pack src/VelaShell.PluginSdk.Build/VelaShell.PluginSdk.Build.csproj -c Release -o artifacts/nuget
pwsh scripts/Invoke-Smoke.ps1 -Feed ./artifacts/nuget -Version 2.0.2
```

`-c Debug` 不是随口一说:Release 会打开强名称签名,而测试程序集不是签名友元,
Release 下 `dotnet test` 编不过。签名密钥不入库,CI 从 `STRONG_NAME_KEY` 机密还原。
本地想打 Release 包又没有密钥时加 `-p:SignAssembly=false`。

冒烟的夹具在 [`tests/smoke/`](tests/smoke/):一个手写的最小插件工程,与 `velaplugin-ui`
模板同形。它刻意带两个空的 `Directory.Build.props`/`.targets` 来切断向上查找 ——
**插件工程是仓库外环境,仓库内的构建约定一条也吃不到**,这个冒烟的一半价值就在这里;
另一半是它跑的打包器来自 `.Build` 包的 `tools/`,于是「targets 调的命令面与打包器对不对得上」
这件跨仓库的事也在这里显形。

打包器在 `src/VelaShell.PluginSdk.Packer`(三条命令:`validate` / `pack` / `info`)。
它**刻意不是 MSBuild 任务**:VS 的 MSBuild 跑在 .NET Framework 上,而本仓库是 net11.0,
做成 `<UsingTask>` 的话插件作者在 VS 里一按生成就会加载失败(清单校验是 `AfterTargets="Build"`
的,每次生成都跑)。详见该工程的 README。

## 发版

```powershell
pwsh scripts/Set-Version.ps1 1.6.0     # 落版本号(4 处),连同功能改动合进 main
                                        # 再在 GitHub 上发 Release,标签 v1.6.0
                                        # 一次发出三个包:PluginSdk / .Testing / .Build
```

破坏性变更要先手工把 `VelaPluginApi.Level` +1 —— 脚本会核对但不代改,因为「契约破没破」
是人的判断。完整流程见[发版流程](https://github.com/VelaShell/velashell-docs/blob/main/zh/sdk/release-process.md)(在文档仓库)。

## 文档

文档不在本仓库 —— 2026-08-30 起全部集中到
**[VelaShell/velashell-docs](https://github.com/VelaShell/velashell-docs)**。本仓库这份在
[`zh/sdk/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/sdk)(中文)·
[`en/sdk/`](https://github.com/VelaShell/velashell-docs/tree/main/en/sdk)(English):
[SDK 参考](https://github.com/VelaShell/velashell-docs/blob/main/zh/sdk/sdk-reference.md)与[发版流程](https://github.com/VelaShell/velashell-docs/blob/main/zh/sdk/release-process.md)。

隔壁还有:[开发指南](https://github.com/VelaShell/velashell-docs/blob/main/zh/templates/dev-guide.md)、
[CLI 手册](https://github.com/VelaShell/velashell-docs/blob/main/zh/cli/cli.md)、
[打包发布](https://github.com/VelaShell/velashell-docs/blob/main/zh/templates/publishing.md),
以及插件系统的**架构蓝图**(进程模型、IPC 协议、权限系统、威胁模型)
[`zh/plugins/`](https://github.com/VelaShell/velashell-docs/tree/main/zh/plugins)。

> `scripts/Set-Version.ps1` 会顺带把 `zh|en/sdk/sdk-reference.md` 的版本横幅一起改掉,
> 前提是 velashell-docs 就 clone 在本仓库同级目录(否则跳过并提醒,见其 `-DocsRoot`)。

## 许可

AGPL-3.0-only,见 [LICENSE](LICENSE)。
