# VelaShell.PluginSdk.Testing

> 插件单元测试替身包：`TestPluginContext` + 全部能力接口的内存实现。

插件项目引用本包后，**无需启动宿主、无需数据库、不发一次网络请求**即可对业务逻辑做纯内存单测。

## 🗂️ 替身清单

| 类型 | 替代的能力 |
|------|-----------|
| `TestPluginContext` | `IPluginContext` 本体，聚合下列全部替身并暴露为强类型字段。 |
| `InMemoryStorage` | `IPluginStorage`（KV 存储）。 |
| `FakeSecretsAndClipboard` | `ISecretsApi`（机密）与 `IClipboardApi`（剪贴板）。`FakeSecrets.IsLocked = true` 模拟主密码还没解锁：读、写抛 `SecretsLockedException`，删照常，`Values` 原样保留、解锁后值都还在；`SupportsLockedState = false` 模拟旧宿主（锁着时改抛 `PluginPermissionDeniedException`，即用户拒绝了解锁提示）。 |
| `InMemoryTimeSeries` | `ITimeSeriesApi`（时序写入与查询）。 |
| `FakeSessions` | `ISessionsApi`：`AddConnected(...)` 造一条已连接会话，`AddSaved(...)` 造一条已保存配置；`DenyOpen` / `OpenFailure` 模拟「用户点了不」与「连不上」，`LastOpenReason` 用来断言那句给用户看的理由确实传上去了。 |
| `FakeRemoteExec` | `IRemoteExecApi`：用 `Handler` 委托按命令文本给定输出。 |
| `FakeRemoteFs` | `IRemoteFsApi`（远程文件读写与列目录）。独立传输任务的每个方法都要显式打开能力位（`SupportsTransferOperations` / `SupportsDownloadOperations` / `SupportsTransferWithdraw`）并配上对应的 `*Handler` 才可用，否则明确抛不支持；请求逐条记在 `UploadsStarted` / `TransfersCancelled` / `TransfersWithdrawn` 等列表里。宿主的幂等、审批与「批准与撤回先到者赢」由测试脚本自己模拟，替身不编造结论。 |
| `FakeTerminal` | `ITerminalApi`（读终端尾部输出、回写终端）。 |
| `FakeUi` | `IUiApi`（面板与消息中心通知）：`PostedNotifications` 是实际进列表的消息，`NotificationRequests` 是全部投递；与宿主同一套校验与同 id 去重，`NotificationOutcome` 脚本模拟限流 / 静音，`SupportsNotifications = false` 模拟旧宿主。 |
| `RecordingCommands` | `ICommandsApi`：记录注册的命令，可用 `RunAsync(id)` 直接触发。 |
| `TestHostEvents` | `IHostEvents`：手动投递宿主事件。 |
| `CollectingLogger` | `IPluginLogger`：收集日志行供断言。 |

## 用法

```csharp
using var ctx = new TestPluginContext();
SessionInfo session = ctx.FakeSessions.AddConnected(host: "prod-1");
ctx.FakeRemoteExec.Handler = (_, cmd) => cmd == "docker ps" ? "CONTAINER ID ..." : "";

var plugin = new MyPlugin();
await plugin.ActivateAsync(ctx, CancellationToken.None);
await ctx.RecordingCommands.RunAsync("my.plugin.refresh");
```

真实用例见 [`tests/VelaShell.Plugin.Ai.Tests`](../../tests/VelaShell.Plugin.Ai.Tests) —— AI 插件的工具审批闸门、设置/机密存取与会话历史全部靠这套替身覆盖。

> 用法详解见 [开发指南](https://github.com/VelaShellLabs/velashell-docs/blob/main/zh/templates/dev-guide.md)，测试策略见 [13-testing-strategy.md](https://github.com/VelaShellLabs/velashell-docs/blob/main/zh/plugins/13-testing-strategy.md)。
