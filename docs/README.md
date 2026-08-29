# 文档索引

本仓库只放**契约层**的文档。英文版见 [`../docs-en/`](../docs-en/)。

| 文档 | 内容 |
| --- | --- |
| [sdk-reference.md](sdk-reference.md) | **SDK 参考**:包结构、入口契约、能力域一览、SDK 版本历史、测试替身、装载模型 |
| [release-process.md](release-process.md) | **本仓库自己怎么发版**:Release 流程、NuGet 可信发布配置、apiLevel 与 Avalonia 版本纪律 |

## 不在这里的东西

拆库之后(2026-08-27),面向插件作者的那几篇跟着各自的包走了:

| 文档 | 去了哪 |
| --- | --- |
| **开发指南**(教程式,写第一个插件) | [velashell-plugin-templates / docs/dev-guide.md](https://github.com/VelaShellLabs/velashell-plugin-templates/blob/main/docs/dev-guide.md) |
| **打包与发布**(`.vpx`、签名、发到插件商店) | [velashell-plugin-templates / docs/publishing.md](https://github.com/VelaShellLabs/velashell-plugin-templates/blob/main/docs/publishing.md) |
| **`vela-plugin` 手册** | [velashell-plugin-cli / docs/cli.md](https://github.com/VelaShellLabs/velashell-plugin-cli/blob/main/docs/cli.md) |

它们各自带着**自己那个包的版本号横幅**,所以必须跟包同仓库 —— 留在这里的话,
CLI 发一版就要来改本仓库的文档,正是拆库要消掉的那种牵连。

插件系统的**架构蓝图**(进程模型、IPC 协议、权限系统、UI 扩展、威胁模型、路线图,
编号 01–15 的那批)留在主仓库:
<https://github.com/joesdu/VelaShell/tree/main/docs/plugins>

那些文档描述的是**宿主侧**的设计与实现 —— 读它是为了理解插件为什么长这样,
写插件本身用不到。
