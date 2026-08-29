# VelaShell 插件契约 SDK

[VelaShell](https://github.com/joesdu/VelaShell) 插件的**契约层**:插件与宿主唯一共享的那批类型。

| 包 | 内容 |
| --- | --- |
| [`VelaShell.PluginSdk`](https://www.nuget.org/packages/VelaShell.PluginSdk) | 契约程序集:插件入口、`IPluginContext` 与全部能力接口、DTO、`plugin.json` 清单模型、`.vpx` 容器格式、宿主注册表 |
| [`VelaShell.PluginSdk.Testing`](https://www.nuget.org/packages/VelaShell.PluginSdk.Testing) | 测试替身:`TestPluginContext` 与各能力的内存实现,不起宿主也能测插件 |

> **插件作者一般不直接引用这两个包。** 写插件只需要引用 `VelaShell.PluginSdk.Build`,
> 契约程序集会随它传递进来 —— 见[开发指南](https://github.com/VelaShellLabs/velashell-plugin-templates/blob/main/docs/dev-guide.md)。

## 插件生态的仓库分布

2026-08-27 起工具链按发布节奏拆成三个仓库,各有各的版本号,**不要求同步发版**:

| 仓库 | 产出 | 什么时候发 |
| --- | --- | --- |
| **本仓库** `velashell-plugin-sdk` | `VelaShell.PluginSdk`、`.Testing` | 契约有增删改时 |
| [`velashell-plugin-cli`](https://github.com/VelaShellLabs/velashell-plugin-cli) | `VelaShell.Plugin.Cli`(`vela-plugin`)、`VelaShell.PluginSdk.Build` | 工具/打包/MSBuild 逻辑变化时 |
| [`velashell-plugin-templates`](https://github.com/VelaShellLabs/velashell-plugin-templates) | `VelaShell.Plugin.Templates` | 模板内容变化,或要把新建工程指到新版 Build 包时 |

另外两个相关仓库:[joesdu/VelaShell](https://github.com/joesdu/VelaShell)(宿主主程序)、
[VelaShellLabs/velashell-plugins](https://github.com/VelaShellLabs/velashell-plugins)(第一方插件)。

依赖方向是单向的,没有环:

```
velashell-plugin-sdk                  ← 本仓库,无上游
        ↓ NuGet: VelaShell.PluginSdk
velashell-plugin-cli                  vela-plugin + VelaShell.PluginSdk.Build
        ↓ NuGet: VelaShell.PluginSdk.Build
velashell-plugin-templates            dotnet new velaplugin / velaplugin-ui
```

所以:**本仓库发 1.6.0,下游一个都不用动**。它们只在想吃到新契约时才把自己引用的
`VelaShell.PluginSdk` 版本抬上来。

## 本仓库额外承担的两件事

这两件事没有别的地方能做,拆库时刻意留在了契约层:

1. **`apiLevel` 纪律** —— `VelaPluginApi.Level` 是插件兼容性的整数代际,宿主拒载高于自身
   代际的插件。纪律是「SDK 主版本 == apiLevel」,由 `scripts/Set-Version.ps1` 在发版前硬核对。
   apiLevel 是契约的属性,CLI 和模板都无权动它。

2. **Avalonia 版本锁的权威** —— 装载器强制让 `Avalonia*` 回落到宿主那一套,插件编译期
   拿到的 Avalonia 必须与宿主运行时那份同版本,否则跨 ALC 的控件类型对不上,而且要等到
   用户装上插件才炸。这个版本号写在 `Directory.Build.props` 的 `VelaAvaloniaVersion`,
   由 `VelaShell.PluginSdk` 包导出成 `$(VelaSdkPinnedAvaloniaVersion)`(`buildTransitive`),
   两个下游在自己的构建期核对:

   | 谁核对 | 在哪 |
   | --- | --- |
   | 宿主 | `src/Directory.Build.targets` 的 `VerifyAvaloniaMatchesSdk` |
   | `VelaShell.PluginSdk.Build` | cli 仓库的 `VerifyAvaloniaVersionPin` |

   改 `VelaAvaloniaVersion` = 改整个插件生态的 Avalonia 版本,必须与宿主同一波发布,
   发完之后让 cli 仓库抬一次它引用的 SDK 版本 —— 否则那道核对会红。

## 在本仓库里开发

```bash
dotnet build VelaShell.PluginSdk.slnx
dotnet test  VelaShell.PluginSdk.slnx -c Debug
```

`-c Debug` 不是随口一说:Release 会打开强名称签名,而测试程序集不是签名友元,
Release 下 `dotnet test` 编不过。签名密钥不入库,CI 从 `STRONG_NAME_KEY` 机密还原。

## 发版

```powershell
pwsh scripts/Set-Version.ps1 1.6.0     # 落版本号(4 处),连同功能改动合进 main
                                        # 再在 GitHub 上发 Release,标签 v1.6.0
```

破坏性变更要先手工把 `VelaPluginApi.Level` +1 —— 脚本会核对但不代改,因为「契约破没破」
是人的判断。完整流程见 [`docs/release-process.md`](docs/release-process.md)。

## 文档

[`docs/`](docs/)(中文)· [`docs-en/`](docs-en/)(English)。本仓库只放契约相关的:
[SDK 参考](docs/sdk-reference.md)与[发版流程](docs/release-process.md)。
开发指南、CLI 手册、打包发布在另外两个仓库,链接见上表。

插件系统的**架构蓝图**(进程模型、IPC 协议、权限系统、威胁模型)留在主仓库的
[`docs/plugins/`](https://github.com/joesdu/VelaShell/tree/main/docs/plugins)。

## 许可

AGPL-3.0-only,见 [LICENSE](LICENSE)。
