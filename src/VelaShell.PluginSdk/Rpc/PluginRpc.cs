using VelaShell.PluginSdk.Logging;
using VelaShell.PluginSdk.Sessions;
using VelaShell.PluginSdk.Theming;

namespace VelaShell.PluginSdk.Rpc;

/// <summary>
/// 隔离进程模式的方法名与线协 DTO(apiLevel 2)。
/// 方向:插件 → 宿主为能力调用;宿主 → 插件为生命周期与事件推送。
/// 纪律与 SDK 相同:同 apiLevel 只增不改;DTO 全部为不可变记录。
/// </summary>
public static class PluginRpc
{
    // ---- 插件 → 宿主 ----

    /// <summary>握手(连接后的第一个请求;其它一切在握手完成前被拒)。</summary>
    public const string Handshake = "handshake";

    /// <summary>日志通知。</summary>
    public const string LogWrite = "log/write";

    /// <summary>会话:列表 / 单查。</summary>
    public const string SessionsList = "sessions/list";

    /// <summary>会话单查。</summary>
    public const string SessionsGet = "sessions/get";

    /// <summary>会话:已保存的连接配置列表(含此刻没连着的)。</summary>
    public const string SessionsListSaved = "sessions/listSaved";

    /// <summary>
    /// 会话:请求宿主打开一条已保存的会话。
    /// <b>这一条可以很慢</b> —— 中间夹着一个给用户看的确认框,还有真正的建连;
    /// 调用方的超时要按"人要看一眼再点"来给,而不是按普通能力调用。
    /// </summary>
    public const string SessionsOpen = "sessions/open";

    /// <summary>会话:关掉本插件打开的会话(参数是 <see cref="SessionRef" />)。</summary>
    public const string SessionsClose = "sessions/close";

    /// <summary>远程执行。</summary>
    public const string ExecRun = "exec/run";

    /// <summary>远程流式执行(应答为退出码与行数,输出经 <see cref="ExecOutput" /> 通知回流)。</summary>
    public const string ExecStream = "exec/stream";

    /// <summary>流式执行的输出通知(宿主 → 插件,带 outputToken)。</summary>
    public const string ExecOutput = "exec/output";

    /// <summary>sudo 专用流式执行;凭据不进入 RPC。</summary>
    public const string ExecSudoStream = "exec/sudoStream";

    /// <summary>有序非敏感 sudo 事件(宿主 → 插件)。</summary>
    public const string ExecSudoEvent = "exec/sudoEvent";

    /// <summary>取消该连接的一条执行操作(插件 → 宿主通知,幂等)。</summary>
    public const string ExecCancel = "exec/cancel";

    /// <summary>远程文件:列目录。</summary>
    public const string FsList = "fs/list";

    /// <summary>远程文件:stat(不存在返回 null)。</summary>
    public const string FsStat = "fs/stat";

    /// <summary>远程文件:存在性。</summary>
    public const string FsExists = "fs/exists";

    /// <summary>远程文件:工作目录。</summary>
    public const string FsWorkingDirectory = "fs/cwd";

    /// <summary>远程文件:下载到本地路径(同机文件系统)。</summary>
    public const string FsDownload = "fs/download";

    /// <summary>远程文件:上传本地路径。</summary>
    public const string FsUpload = "fs/upload";

    /// <summary>启动独立上传任务;仅返回登记快照,不等待文件传输。</summary>
    public const string FsTransferStartUpload = "fs/transfer/startUpload";

    /// <summary>启动独立下载任务;仅返回登记快照,不等待文件传输。</summary>
    public const string FsTransferStartDownload = "fs/transfer/startDownload";

    /// <summary>读取所属插件的传输快照。</summary>
    public const string FsTransferGet = "fs/transfer/get";

    /// <summary>短暂等待快照更新;等待结束不取消传输。</summary>
    public const string FsTransferWait = "fs/transfer/wait";

    /// <summary>显式请求取消所属插件的传输。</summary>
    public const string FsTransferCancel = "fs/transfer/cancel";

    /// <summary>显式恢复已确认停止且可恢复的传输。</summary>
    public const string FsTransferResume = "fs/transfer/resume";

    /// <summary>
    /// 条件撤回:只在这一代的批准还没生效时撤下传输(载荷 <see cref="FsTransferWithdrawRequest" />,
    /// 应答 <see cref="RemoteFs.RemoteTransferWithdrawResult" />)。握手未宣告
    /// <see cref="HandshakeResponse.SupportsTransferWithdraw" /> 时 PluginHost 不发这条,在插件进程内直接抛不支持,
    /// 也不改发 <see cref="FsTransferCancel" />。可用版本:2.1.4。
    /// </summary>
    public const string FsTransferWithdraw = "fs/transfer/withdraw";

    /// <summary>独立传输的快照通知(宿主 → 插件);丢帧后通过 get/wait 重取权威快照。</summary>
    public const string FsTransferProgress = "fs/transfer/progress";

    /// <summary>远程文件:读小文件(base64 内联)。</summary>
    public const string FsReadAll = "fs/readAll";

    /// <summary>远程文件:写小文件(base64 内联)。</summary>
    public const string FsWriteAll = "fs/writeAll";

    /// <summary>远程文件:删除。</summary>
    public const string FsDelete = "fs/delete";

    /// <summary>远程文件:建目录(已存在报错)。</summary>
    public const string FsCreateDirectory = "fs/mkdir";

    /// <summary>远程文件:确保目录(幂等)。</summary>
    public const string FsEnsureDirectory = "fs/ensureDir";

    /// <summary>远程文件:改名/移动。</summary>
    public const string FsRename = "fs/rename";

    /// <summary>传输进度通知(宿主 → 插件,带 progressToken)。</summary>
    public const string FsProgress = "fs/progress";

    /// <summary>流式读取:打开远端文件只读流(应答 streamId + 长度)。</summary>
    public const string FsOpenRead = "fs/openRead";

    /// <summary>流式读取:顺序拉取下一块(base64;Eof 标记结束)。</summary>
    public const string FsStreamRead = "fs/streamRead";

    /// <summary>流式读取:关闭并释放宿主侧流。</summary>
    public const string FsStreamClose = "fs/streamClose";

    /// <summary>命令注册 / 注销 / 执行宿主命令。</summary>
    public const string CommandsRegister = "commands/register";

    /// <summary>命令注销。</summary>
    public const string CommandsUnregister = "commands/unregister";

    /// <summary>执行宿主命令。</summary>
    public const string CommandsTryExecute = "commands/tryExecute";

    /// <summary>
    /// KV 存储:读 / 写 / 删 / 键列举。数据落宿主 SonnetDB(按插件 id 命名空间隔离,
    /// 卸载整体清除),隔离进程不落本地文件。
    /// </summary>
    public const string StorageGet = "storage/get";

    /// <summary>KV 写入。</summary>
    public const string StorageSet = "storage/set";

    /// <summary>KV 删除。</summary>
    public const string StorageRemove = "storage/remove";

    /// <summary>KV 键列举。</summary>
    public const string StorageKeys = "storage/keys";

    /// <summary>
    /// 时序:打开(必要时创建)measurement。宿主侧记住句柄,后续调用按短名寻址;
    /// 数据同样落宿主 SonnetDB(按插件 id 命名空间隔离,卸载整体清除)。
    /// </summary>
    public const string TimeSeriesOpen = "ts/open";

    /// <summary>时序:列出本插件的 measurement 短名。</summary>
    public const string TimeSeriesList = "ts/list";

    /// <summary>时序:删除 measurement 及其数据。</summary>
    public const string TimeSeriesDrop = "ts/drop";

    /// <summary>时序:批量写点。</summary>
    public const string TimeSeriesWrite = "ts/write";

    /// <summary>时序:按条件查询数据点。</summary>
    public const string TimeSeriesQuery = "ts/query";

    /// <summary>时序:按条件计数。</summary>
    public const string TimeSeriesCount = "ts/count";

    /// <summary>时序:列举某标签列的去重取值。</summary>
    public const string TimeSeriesDistinct = "ts/distinct";

    /// <summary>时序:按标签删除序列数据。</summary>
    public const string TimeSeriesDelete = "ts/delete";

    /// <summary>
    /// 机密:读 / 写 / 删(机密只存宿主侧,值仅在本机管道上瞬时传输)。
    /// 握手宣告了 <see cref="HandshakeResponse.SupportsSecretsLocked" /> 的宿主在机密存储锁着时
    /// 对读 / 写应答 <see cref="RpcErrorCodes.SecretsLocked" />、删照常;无论新旧,都不会对一个已存的机密应答 null。
    /// </summary>
    public const string SecretsGet = "secrets/get";

    /// <summary>机密写入。</summary>
    public const string SecretsSet = "secrets/set";

    /// <summary>机密删除。</summary>
    public const string SecretsDelete = "secrets/delete";

    /// <summary>剪贴板:读文本 / 写文本(经宿主执行)。</summary>
    public const string ClipboardGetText = "clipboard/getText";

    /// <summary>剪贴板写文本。</summary>
    public const string ClipboardSetText = "clipboard/setText";

    /// <summary>终端:读输出。</summary>
    public const string TerminalGetOutput = "terminal/getOutput";

    /// <summary>终端:搜索输出。</summary>
    public const string TerminalSearch = "terminal/search";

    /// <summary>终端:回写输入(需用户授权;拒绝时应答 permission-denied)。</summary>
    public const string TerminalWrite = "terminal/write";

    /// <summary>面板数变化通知(插件 → 宿主):驱动"无打开面板"的空闲回收条件。</summary>
    public const string UiSurfaces = "ui/surfaces";

    /// <summary>
    /// 停靠嵌入请求(插件 → 宿主,仅 Windows):把插件进程的无边框窗口(HWND)
    /// 收养进宿主的停靠文档区,用户可拖拽分栏。宿主经握手宣告是否支持。
    /// </summary>
    public const string UiEmbedPanel = "ui/embed";

    /// <summary>关闭嵌入面板(插件 → 宿主,程序性)。</summary>
    public const string UiClosePanel = "ui/close";

    /// <summary>
    /// 往消息中心发一条消息(插件 → 宿主请求,载荷 <see cref="UiNotifyRequest" />,应答 <see cref="UiNotifyResponse" />)。
    /// 发送方插件身份由连接绑定;握手未宣告 <see cref="HandshakeResponse.SupportsNotifications" /> 时 PluginHost 不发这条。
    /// 可用版本:2.1.3。
    /// </summary>
    public const string UiNotify = "ui/notify";

    // 注:界面不走 RPC —— 隔离插件的 UI 由其进程内自带的 Avalonia 直接呈现
    // (PluginHost 内建派发循环),原生控件无需也无法跨进程传输。

    // ---- 宿主 → 插件 ----

    /// <summary>激活插件(握手成功后宿主发起)。</summary>
    public const string PluginActivate = "plugin/activate";

    /// <summary>停用插件(应答后插件进程自行退出)。</summary>
    public const string PluginDeactivate = "plugin/deactivate";

    /// <summary>命令被用户触发(通知)。</summary>
    public const string CommandExecute = "command/execute";

    /// <summary>宿主事件通知:kind = sessionConnected / sessionDisconnected / themeChanged / localeChanged。</summary>
    public const string HostEvent = "host/event";

    /// <summary>嵌入面板已在宿主侧关闭(宿主 → 插件通知:用户关标签/插件停用)。</summary>
    public const string UiPanelClosed = "ui/closed";

    /// <summary>
    /// 主题状态通知(握手后与每次**生效配色变化**时下发):PluginHost 把宿主的
    /// <c>Vela*</c> 资源令牌注入本进程 Application 资源,插件的
    /// <c>{DynamicResource VelaXxx}</c> 在隔离模式下同样生效;同一条消息里带上
    /// <see cref="HostThemeInfo" />,驱动 <see cref="Theming.IHostThemeApi" />。
    /// <para>
    /// 令牌与身份必须走**同一条**消息:插件在 <c>Changed</c> 里读 <c>Colors</c>,
    /// 拆成两条就会出现“身份已经是新主题、颜色还是旧主题”的中间态。
    /// </para>
    /// <para>
    /// “生效配色变化”是三件事的并集:换具名主题、“跟随系统”下系统明暗翻转、用户改强调色。
    /// 后两者不经过 <see cref="HostEvent" /> 的 <c>themeChanged</c>,只由本通知承载。
    /// </para>
    /// </summary>
    public const string ThemeTokens = "theme/tokens";
}

/// <summary>握手请求(插件 → 宿主)。</summary>
/// <param name="Token">宿主拉起进程时经环境变量下发的一次性令牌。</param>
/// <param name="PluginId">插件 id(必须与宿主拉起的目标一致)。</param>
/// <param name="PluginVersion">插件版本。</param>
/// <param name="ApiLevels">PluginHost 侧支持的 apiLevel 集合。</param>
public sealed record HandshakeRequest(string Token, string PluginId, string PluginVersion, int[] ApiLevels);

/// <summary>握手应答(宿主 → 插件)。</summary>
/// <param name="ApiLevel">协商结果(交集内取最高)。</param>
/// <param name="HostVersion">宿主版本。</param>
/// <param name="Locale">当前语言。</param>
/// <param name="Theme">当前主题的明暗名(<c>dark</c>/<c>light</c>/<c>system</c>),见 <see cref="IHostInfo.Theme" />。</param>
/// <param name="SupportsEmbedding">宿主是否支持停靠嵌入(HWND 收养,仅 Windows)。</param>
/// <param name="ThemeInfo">
/// 已解析的主题身份(id / 显示名 / 明暗 / 强调色)。<see cref="Theming.IHostThemeApi.Current" />
/// 的初值 —— 握手就要给,否则插件在 <c>Activate</c> 里建界面时还不知道自己长在哪套主题上。
/// 老宿主不带此字段时为 <see langword="null" />,PluginHost 退回按 <paramref name="Theme" /> 猜。
/// </param>
public sealed record HandshakeResponse(int ApiLevel, string HostVersion, string Locale, string Theme,
    bool SupportsEmbedding = false, HostThemeInfo? ThemeInfo = null)
{
    /// <summary>支持执行 operationId、真实取消及有序通知;缺席即旧宿主,不可假定支持sudo。</summary>
    public bool SupportsExecutionOperations { get; init; }

    /// <summary>支持独立上传任务、幂等启动与查询/等待/取消/恢复;缺席即不支持,不可回退长 RPC。</summary>
    public bool SupportsTransferOperations { get; init; }

    /// <summary>支持独立下载任务;缺席即不支持,不可回退长 RPC。</summary>
    public bool SupportsDownloadOperations { get; init; }

    /// <summary>支持 <see cref="PluginRpc.UiNotify" />(插件往消息中心发消息);缺席即旧宿主,不支持。可用版本:2.1.3。</summary>
    public bool SupportsNotifications { get; init; }

    /// <summary>
    /// 支持 <see cref="PluginRpc.FsTransferWithdraw" />(只撤批准还没生效的传输);缺席即旧宿主,不支持,
    /// 不可退化成无条件取消。可用版本:2.1.4。
    /// </summary>
    public bool SupportsTransferWithdraw { get; init; }

    /// <summary>
    /// 机密存储锁着(用户设了主密码、此刻没解锁)时,<see cref="PluginRpc.SecretsGet" /> / <see cref="PluginRpc.SecretsSet" />
    /// 不弹解锁框、直接应答 <see cref="RpcErrorCodes.SecretsLocked" />;PluginHost 据此给出
    /// <see cref="Secrets.ISecretsApi.SupportsLockedState" />。缺席即旧宿主,不会应答这个错误码。可用版本:TBD(发版时替换)。
    /// </summary>
    public bool SupportsSecretsLocked { get; init; }
}

/// <summary>会话查询参数。</summary>
public sealed record SessionRef(string SessionId);

/// <summary>打开一条已保存会话的参数。</summary>
/// <param name="SavedSessionId"><see cref="Sessions.SavedSessionInfo.SavedSessionId" />,不是会话 id。</param>
/// <param name="Reason">给用户看的理由,见 <see cref="Sessions.SessionOpenOptions.Reason" />。</param>
/// <param name="ReuseConnected">已有连着的同配置会话时直接复用。</param>
public sealed record SessionOpenRequest(string SavedSessionId, string Reason, bool ReuseConnected);

/// <summary>远程执行参数。</summary>
public sealed record ExecRunRequest(string SessionId, string Command, double TimeoutSeconds);

/// <summary>远程流式执行参数。</summary>
/// <param name="SessionId">会话 id。</param>
/// <param name="Command">命令行。</param>
/// <param name="OutputToken">输出通知令牌(宿主按它把行回流给正确的调用点)。</param>
/// <param name="TimeoutSeconds">超时秒数;<c>0</c> 表示不限时(由插件侧取消驱动)。</param>
/// <param name="IncludeStandardError">是否也回流标准错误。</param>
public sealed record ExecStreamRequest(
    string SessionId,
    string Command,
    string OutputToken,
    double TimeoutSeconds,
    bool IncludeStandardError)
{
    /// <summary>可取消的执行 id;旧调用方省略时退回 OutputToken。保留原构造及 Deconstruct 的二进制签名。</summary>
    public string? OperationId { get; init; }
}

/// <summary>
/// 流式执行的输出通知载荷。
/// </summary>
/// <param name="OutputToken">对应 <see cref="ExecStreamRequest.OutputToken" />。</param>
/// <param name="IsStandardError">这一行是否来自标准错误。</param>
/// <param name="Line">一行文本(不含行尾换行),超长行可分片。</param>
/// <remarks>
/// 一行一条通知。看起来很碎,但管道上跑的是长度前缀 + JSON 的轻量帧,而日志的价值
/// 就在于即时 —— 攒批会让"跟随"变成"每秒抖一下"。真需要攒批的插件自己在回调里攒。
/// </remarks>
public sealed record ExecOutputNotification(string OutputToken, bool IsStandardError, string Line)
{
    /// <summary>从 1 开始递增的执行内序号;0 表示旧宿主未提供。保留原构造及 Deconstruct 的二进制签名。</summary>
    public long Sequence { get; init; }
}

/// <summary>sudo 请求;来源插件身份由连接绑定,不允许调用方传入。</summary>
public sealed record ExecSudoStreamRequest(string SessionId, string Command, string OperationId, double TimeoutSeconds);

/// <summary>非敏感 sudo 事件;Sequence 从 1 开始严格递增。</summary>
public sealed record ExecSudoEventNotification(string OperationId, long Sequence, RemoteExec.SudoExecEvent Event);

/// <summary>执行取消通知;只能作用于同一插件连接的 operationId。</summary>
public sealed record ExecCancelNotification(string OperationId);

/// <summary>远程文件通用参数(路径类)。</summary>
public sealed record FsPathRequest(string SessionId, string Path);

/// <summary>远程文件传输参数(远端 ↔ 本地路径;同一台机器的文件系统)。</summary>
/// <param name="SessionId">会话 id。</param>
/// <param name="RemotePath">远端路径。</param>
/// <param name="LocalPath">本地路径。</param>
/// <param name="ProgressToken">进度通知令牌;null = 不要进度。</param>
public sealed record FsTransferRequest(string SessionId, string RemotePath, string LocalPath, string? ProgressToken);

/// <summary>传输进度通知载荷。</summary>
public sealed record FsProgressNotification(string ProgressToken, long TransferredBytes, long TotalBytes);

/// <summary>启动独立上传;身份由宿主绑定,OperationId 随 Options 传递并幂等去重。</summary>
public sealed record FsTransferStartUploadRequest(string SessionId, string LocalPath, string RemotePath,
    RemoteFs.RemoteUploadOptions Options);

/// <summary>启动独立下载;身份由宿主绑定,OperationId 与上传共享命名空间。</summary>
public sealed record FsTransferStartDownloadRequest(string SessionId, string RemotePath, string LocalPath,
    RemoteFs.RemoteDownloadOptions Options);

/// <summary>所属插件的传输引用;用于查询。</summary>
public sealed record FsTransferRef(string TransferId);

/// <summary>当前执行代次的传输引用;旧代次取消不能作用于恢复后的新代次。</summary>
public sealed record FsTransferCancelRequest(string TransferId, long ExpectedGeneration);

/// <summary>
/// 条件撤回请求:只在这一代的批准还没生效时撤下,判断与撤下由宿主在同一步完成;旧代次撤回不能作用于恢复后的新代次。
/// 可用版本:2.1.4。
/// </summary>
public sealed record FsTransferWithdrawRequest(string TransferId, long ExpectedGeneration);

/// <summary>等待快照版本变化;WaitSeconds 在 0 到 IRemoteFsApi.MaxTransferWaitSeconds 之间。</summary>
public sealed record FsTransferWaitRequest(string TransferId, long AfterSequence, double WaitSeconds);

/// <summary>显式恢复请求;OperationId 用于去重,调用方不能指定路径、检查点或源文件身份。</summary>
public sealed record FsTransferResumeRequest(string TransferId, RemoteFs.RemoteTransferResumeOptions Options);

/// <summary>
/// 独立传输最新快照;按 TransferId + Sequence 丢弃旧帧。至少 100ms 节流,
/// 状态变化及时通知,满格不能代替 Succeeded;通知为尽力语义,必须支持 get/wait 补读。
/// </summary>
public sealed record FsTransferProgressNotification(RemoteFs.RemoteTransferSnapshot Snapshot);

/// <summary>读小文件参数。</summary>
public sealed record FsReadAllRequest(string SessionId, string Path, int MaxBytes);

/// <summary>流式读取:打开应答(长度未知时为 -1)。</summary>
public sealed record FsOpenReadResponse(string StreamId, long Length);

/// <summary>流式读取:拉块参数(顺序读,单块上限由宿主钳制)。</summary>
public sealed record FsStreamReadRequest(string StreamId, int MaxBytes);

/// <summary>流式读取:块应答(<see cref="Eof" /> 为 true 时流已尽,宿主侧已自动释放)。</summary>
public sealed record FsStreamReadResponse(string DataBase64, bool Eof);

/// <summary>流 id 载荷。</summary>
public sealed record FsStreamRef(string StreamId);

/// <summary>写小文件参数(内容 base64)。</summary>
public sealed record FsWriteAllRequest(string SessionId, string Path, string ContentBase64);

/// <summary>改名参数。</summary>
public sealed record FsRenameRequest(string SessionId, string OldPath, string NewPath);

/// <summary>命令注册参数(回调留在插件进程,宿主触发时发 <see cref="PluginRpc.CommandExecute" /> 通知)。</summary>
public sealed record CommandRegistration(string Id, string Title, string Category);

/// <summary>命令 id 载荷。</summary>
public sealed record CommandRef(string Id);

/// <summary>日志通知载荷。</summary>
public sealed record LogNotification(PluginLogLevel Level, string Message, string? Exception);

/// <summary>宿主事件通知载荷(kind 决定有效字段)。</summary>
/// <param name="Kind">sessionConnected / sessionDisconnected / themeChanged / localeChanged。</param>
/// <param name="Session">会话事件的载荷。</param>
/// <param name="Value">主题/语言事件的载荷。</param>
public sealed record HostEventNotification(string Kind, SessionInfo? Session, string? Value);

/// <summary>激活参数(预留激活原因;v1 恒为启动激活)。</summary>
public sealed record ActivateRequest(string Reason);

/// <summary>
/// 一个主题令牌的已解析值(按宿主当前明暗变体解析)。
/// Kind:brush(#AARRGGBB)/ color(#AARRGGBB)/ double(不变体区域格式)/ font(字体族回退串)。
/// </summary>
public sealed record ThemeTokenDto(string Key, string Kind, string Value);

/// <summary>主题状态通知载荷:整套已解析令牌 + 主题身份。</summary>
/// <param name="Tokens">全部 <c>Vela*</c> 令牌的已解析值。宿主没有 UI 层令牌提供者时为空数组。</param>
/// <param name="Theme">
/// 已解析的主题身份。老宿主不带此字段时为 <see langword="null" />,
/// 此时 PluginHost 保留上一次的身份、只更新颜色。
/// </param>
public sealed record ThemeTokensNotification(ThemeTokenDto[] Tokens, HostThemeInfo? Theme = null);

/// <summary>KV 键载荷。</summary>
public sealed record StorageKeyRef(string Key);

/// <summary>KV 写入载荷(值为任意 JSON)。</summary>
public sealed record StorageSetRequest(string Key, System.Text.Json.JsonElement Value);

/// <summary>时序:打开 measurement 的载荷。</summary>
public sealed record TimeSeriesOpenRequest(TimeSeries.TimeSeriesDefinition Definition);

/// <summary>时序:measurement 短名载荷(drop / distinct 等)。</summary>
public sealed record TimeSeriesNameRef(string Name);

/// <summary>时序:批量写入载荷。</summary>
public sealed record TimeSeriesWriteRequest(string Name, TimeSeries.TimeSeriesPoint[] Points);

/// <summary>时序:查询载荷。</summary>
public sealed record TimeSeriesQueryRequest(string Name, TimeSeries.TimeSeriesQuery Query);

/// <summary>时序:计数载荷。</summary>
public sealed record TimeSeriesCountRequest(string Name, string Field, TimeSeries.TimeSeriesQuery Query);

/// <summary>时序:标签去重取值载荷。</summary>
public sealed record TimeSeriesDistinctRequest(string Name, string Tag);

/// <summary>时序:按标签删除载荷(标签为空 = 清空该 measurement 的数据)。</summary>
public sealed record TimeSeriesDeleteRequest(string Name, Dictionary<string, string>? Tags);

/// <summary>机密名载荷。</summary>
public sealed record SecretRef(string Name);

/// <summary>机密写入载荷(仅在本机管道上瞬时传输,宿主落盘前加密)。</summary>
public sealed record SecretSetRequest(string Name, string Value);

/// <summary>剪贴板写文本载荷。</summary>
public sealed record ClipboardSetRequest(string Text);

/// <summary>终端读输出载荷。</summary>
public sealed record TerminalGetOutputRequest(string SessionId, int MaxLines);

/// <summary>终端搜索载荷。</summary>
public sealed record TerminalSearchRequest(string SessionId, string Pattern, bool IsRegex, int MaxMatches);

/// <summary>终端搜索命中(线协)。</summary>
public sealed record TerminalMatchDto(int Line, string Text);

/// <summary>终端回写载荷。</summary>
public sealed record TerminalWriteRequest(string SessionId, string Input);

/// <summary>面板数变化通知载荷。</summary>
public sealed record UiSurfacesNotification(int Count);

/// <summary>停靠嵌入请求载荷。</summary>
/// <param name="Title">标签标题。</param>
/// <param name="Hwnd">插件进程无边框窗口的原生句柄。</param>
public sealed record UiEmbedRequest(string Title, long Hwnd);

/// <summary>停靠嵌入应答载荷。</summary>
public sealed record UiEmbedResponse(string PanelId);

/// <summary>面板 id 载荷。</summary>
public sealed record UiPanelRef(string PanelId);

/// <summary>发消息请求载荷;没有「以谁的名义」字段,发送方身份由连接绑定。</summary>
public sealed record UiNotifyRequest(Ui.PluginNotification Notification);

/// <summary>发消息应答载荷。</summary>
public sealed record UiNotifyResponse(Ui.PluginNotificationOutcome Outcome);
