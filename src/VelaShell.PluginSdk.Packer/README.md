# VelaShell.PluginSdk.Packer

`.vpx` 打包器。**不发 NuGet 包** —— 它以构建产物的形式被
[`VelaShell.PluginSdk.Build`](../VelaShell.PluginSdk.Build/README.md) 收进那个包的
`tools/net11.0/`,插件工程 `dotnet build -t:PackVpx` 调的就是它。

```
validate <dir>              校验 plugin.json 与入口程序集
pack <dir> [-o <dir|file>] [--no-mask] [-k <key.pem>]
info <package.vpx>          打印容器头、签名状态与清单
```

## 三个设计决定

**为什么是独立进程,不是 MSBuild 任务。** VS 的 MSBuild 跑在 .NET Framework 上,而本仓库是
net11.0。做成 `<UsingTask>` 的话,插件作者在 VS 里一按生成就会因为加载不了任务程序集而失败
—— 而清单校验是 `AfterTargets="Build"` 的,每次生成都跑。`dotnet exec` 一个独立进程与调用方
的 MSBuild 是哪种运行时完全无关。

**为什么在本仓库,不在 velashell-plugin-cli。** 打包器需要的东西——`VpxContainer`(容器格式)
与 `PluginManifestReader`(清单规则)——本来就在 `VelaShell.PluginSdk` 里,它们才是 `.vpx` 的
定义。放在这里,`.Build` 发出去的打包器与它发出去的契约天然同版本,两个仓库之间不需要任何
版本对齐。

**为什么命令面只有三条。** 面向人的完整工具是
[`vela-plugin`](https://github.com/VelaShell/velashell-plugin-cli)(商店、开发内环、
签名、体检)。这里只留 targets 真正会调的那两条,外加冒烟用来把包读回来的 `info`。
多一条命令就多一份要与那边对齐的东西 —— 而两边一致性的根据是它们走同一个 `VpxContainer`,
不是命令面长得像。
