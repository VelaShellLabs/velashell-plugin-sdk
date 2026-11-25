# VelaShell.PluginSdk.Build

插件引用 `VelaShell.PluginSdk.Build` 即可得到契约与打包工具。升级到不再传递 Avalonia 的 `.Build` 时，原来依赖它编译界面的插件需要显式引用 Avalonia；命令行等不使用 Avalonia 的插件不需要改。

按目标宿主 `src/Directory.Packages.props` 选版本。例如，针对使用 Avalonia 12.2.0 的宿主：

```xml
<PropertyGroup>
  <VelaAvaloniaVersion>12.2.0</VelaAvaloniaVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Avalonia" Version="$(VelaAvaloniaVersion)" ExcludeAssets="runtime" />
</ItemGroup>
```

采用集中包管理时，在 `Directory.Packages.props` 的 `PackageVersion` 上设版本，工程引用省略 `Version`。依赖 AvaloniaEdit 时，用 `VelaAvaloniaEditVersion` 单独声明宿主使用的 `Avalonia.AvaloniaEdit` 包版本。使用新版宿主才有的 API 时，将 `plugin.json` 的 `minHostVersion` 设为首个支持它的**宿主版本**，而非 Avalonia 或 SDK 版本。

| 它做了什么 | 为什么 |
| --- | --- |
| 传递引入 `VelaShell.PluginSdk` 契约，不引入 Avalonia | 无 UI 插件无需还原 UI 框架；UI 插件直接引用目标宿主版本，保留 AXAML 编译资产 |
| `EnableDynamicLoading=true`、`plugin.json` 进输出目录 | 插件是被动态装载的组件,ALC 靠 `deps.json` 解析自带依赖 |
| 共享程序集的运行时资产不落插件目录 | 放进去也不会被加载,只是撑大包体 |
| Avalonia 版本冲突从警告升为错误(NU1608/NU1605) | 版本漂移必须在构建期就红,而不是装机后才现形 |
| 构建后按宿主同一套规则校验 `plugin.json` | 杜绝"本机构建过、宿主装不上" |
| `dotnet build -t:PackVpx` 一步出 `.vpx` | 打包器随包分发,不必安装任何全局工具 |

## 出包

```bash
dotnet build -c Release -t:PackVpx
# → bin/vpx/<插件id>-<版本>.vpx

# 带签名(密钥用 `vela-plugin keygen` 生成,不要提交进仓库)
dotnet build -c Release -t:PackVpx -p:VelaSigningKey=/path/to/key.pem
```

## 可调属性

| 属性 | 默认 | 说明 |
| --- | --- | --- |
| `VelaPluginManifest` | `$(MSBuildProjectDirectory)\plugin.json` | 清单路径 |
| `VelaVpxOutputDirectory` | `bin\vpx\` | `.vpx` 产物目录 |
| `VelaSigningKey` | 空 | 打包时用的 PEM 私钥 |
| `VelaPackMask` | `true` | 是否对载荷做掩码变换 |
| `VelaValidateManifestOnBuild` | `true` | 构建后是否校验清单 |
| `VelaSkipAvaloniaVersionCheck` | `false` | 跳过 Avalonia 版本一致性检查 |

完整开发指南:<https://github.com/VelaShell/velashell-docs/blob/main/zh/templates/dev-guide.md>
