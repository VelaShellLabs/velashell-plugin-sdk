namespace VelaShell.PluginSdk.RemoteFs;

/// <summary>传输方向。缺失方向按 Upload 处理以保持旧快照兼容。</summary>
public enum RemoteTransferDirection
{
    /// <summary>上传到远端(旧契约默认值)。</summary>
    Upload = 0,
    /// <summary>从远端下载到本地。</summary>
    Download
}

/// <summary>独立传输所处的轻量生命周期阶段。</summary>
public enum RemoteTransferPhase
{
    /// <summary>阶段未知(旧宿主或尚未报告)。</summary>
    Unknown = 0,
    /// <summary>
    /// 等待或执行宿主审批。
    /// <para>
    /// 宣告 <see cref="IRemoteFsApi.SupportsTransferWithdraw" /> 的宿主在批准生效的<b>同一步</b>把任务移出这一阶段
    /// (上传与下载同一口径,批准之后等传输槽的也不再报 Approval),所以这样的宿主报 Approval 就等于「这一代的批准还没生效」。
    /// 可用版本:2.1.4。
    /// </para>
    /// </summary>
    Approval,
    /// <summary>正在流式传输。</summary>
    Transfer,
    /// <summary>校验传输结果。</summary>
    Verification,
    /// <summary>提交最终目标。</summary>
    Commit
}

/// <summary>
/// 宿主在<b>登记任务之前</b>拒绝启动的原因,见 <see cref="PluginTransferRejectedException" />。
/// 数值显式给出并且<b>只增不改</b>;线上按短横线名传(<c>source-in-use</c> 一类),不认识的名字读成 <see cref="Unknown" />。
/// <para>可用版本:2.1.3。</para>
/// </summary>
public enum RemoteTransferRejectReason
{
    /// <summary>宿主拒绝了、但原因本 SDK 不认识(更新的宿主)。「没有登记、什么都没开始」这一保证照样成立。</summary>
    Unknown = 0,

    /// <summary>源不存在或已被删除(上传是本地源,下载是远端源)。</summary>
    SourceNotFound = 1,

    /// <summary>源被其它程序占用或锁定,宿主无法以稳定的方式打开它(例如 Windows 上的共享冲突)。</summary>
    SourceInUse = 2,

    /// <summary>本插件的活动 / 排队任务数,或插件生命周期内的回执额度已满。等已有任务结束再试。</summary>
    QuotaExceeded = 3,

    /// <summary>宿主在本机准备快照或临时文件所需的空间不足。</summary>
    InsufficientSpace = 4
}

/// <summary>
/// <see cref="IRemoteFsApi.WithdrawPendingTransferAsync" /> 的结论。数值显式给出并且<b>只增不改</b>(线上按数值传)。
/// <para>
/// 三种已知结论按宿主记下的两件事互斥划分:这一代的批准生效了没有;批准生效之前有没有登记过取消。
/// 批准已生效 → <see cref="PastApproval" />;否则登记过取消 → <see cref="Withdrawn" />;
/// 否则已经收尾 → <see cref="AlreadyEnded" />;否则本次调用登记取消 → <see cref="Withdrawn" />。
/// </para>
/// <para>可用版本:2.1.4。</para>
/// </summary>
public enum RemoteTransferWithdrawOutcome
{
    /// <summary>
    /// 宿主没有给出结论,或给了本 SDK 不认识的值(更新的宿主)。<b>不要</b>当成已撤下,也不要据此改发无条件取消:
    /// 接着按快照观察到终态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 撤下了:这一代在批准生效<b>之前</b>已登记取消意图 —— 本次调用、先前的撤回、或批准前的
    /// <see cref="IRemoteFsApi.CancelTransferAsync" /> 都算。这一代不会再被批准,什么都不读、什么都不写,
    /// 以 <see cref="RemoteTransferState.Cancelled" /> 收尾;返回时可能还是 CancelRequested,照常观察到终态。
    /// </summary>
    Withdrawn = 1,

    /// <summary>
    /// 批准已经生效(含批准之后已到终态的),本次调用什么都没改。这是用户点过头的传输,照常进行;
    /// 真要停它是另一个决定,用 <see cref="IRemoteFsApi.CancelTransferAsync" />。
    /// </summary>
    PastApproval = 2,

    /// <summary>
    /// 这一代在批准生效之前就因为别的原因收尾了(确认框里被拒、源不见了、断线、到期、撤权……),本次调用什么都没改。
    /// 与 <see cref="Withdrawn" /> 一样什么都没传,但不是撤下的:原因看快照的 State 与 ErrorCode。
    /// </summary>
    AlreadyEnded = 3
}

/// <summary>
/// 条件撤回的结果:结论与调用之后的权威快照。也是隔离进程 <c>fs/transfer/withdraw</c> 的应答载荷。
/// <para>可用版本:2.1.4。</para>
/// </summary>
public sealed record RemoteTransferWithdrawResult
{
    /// <summary>结论;缺失时为 <see cref="RemoteTransferWithdrawOutcome.Unknown" />。</summary>
    public RemoteTransferWithdrawOutcome Outcome { get; init; }

    /// <summary>调用之后的权威快照;权限撤销之后按撤权口径脱敏(与 CancelTransferAsync 相同)。</summary>
    public required RemoteTransferSnapshot Snapshot { get; init; }

    /// <summary>只有 <see cref="RemoteTransferWithdrawOutcome.Withdrawn" /> 才算撤下了;Unknown 与不认识的值一律不算。</summary>
    public bool IsWithdrawn => Outcome == RemoteTransferWithdrawOutcome.Withdrawn;
}

/// <summary>独立上传任务状态;缺失状态按 Unknown 处理,绝不默认成功。</summary>
public enum RemoteTransferState
{
    /// <summary>远端结果不确定;不得自动重传。</summary>
    Unknown = 0,
    /// <summary>已登记,正在等待传输槽。</summary>
    Queued,
    /// <summary>正在校验源文件、授权及目标。</summary>
    Starting,
    /// <summary>正在传输或等待远端收尾;字节到齐不等于成功。</summary>
    Running,
    /// <summary>已请求取消,尚未确认停止。</summary>
    CancelRequested,
    /// <summary>写入、校验及目标提交均成功。</summary>
    Succeeded,
    /// <summary>明确失败,且已确认本次传输不再写入。</summary>
    Failed,
    /// <summary>明确取消,且已确认本次传输不再写入。</summary>
    Cancelled,
    /// <summary>显式传输死线已到;是否停止仍须看 RemoteStopConfirmed。</summary>
    TimedOut
}

/// <summary>独立上传选项;无 AI 人为文件大小上限,不携带插件身份或恢复偏移。</summary>
public sealed record RemoteUploadOptions
{
    /// <summary>
    /// 调用方生成的非空幂等标识;同一插件生命周期内相同 id 及参数只启动一次。
    /// 相同 id 的参数发生变化必须拒绝;不能以新的 id 自动重试不确定的上传。
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>是否允许原子替换现有目标,默认否;否时必须原子无替换提交,不能仅预先检查存在性或先删除目标。</summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// 每执行代次的显式预算;null 无死线,非 null 为正。从该代次登记开始计算,包含排队及收尾;
    /// 恢复经重新批准后按相同时长重新起算,原代次已耗时不计入新预算。
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>独立下载选项;从远端流式写入本地最终目标,不携带恢复偏移。</summary>
public sealed record RemoteDownloadOptions
{
    /// <summary>调用方生成的非空幂等标识;与上传共享同一插件生命周期命名空间。</summary>
    public required string OperationId { get; init; }

    /// <summary>是否允许原子替换现有本地目标,默认否。</summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// 每执行代次的显式预算;null 无死线,非 null 必须为正;从登记开始计算,包含排队、审批、传输及收尾。
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>显式恢复选项;由宿主校验重连目标与原任务身份,不接受调用方指定偏移。</summary>
public sealed record RemoteTransferResumeOptions
{
    /// <summary>非空幂等标识;同 id 参数必须相同,回执/墓碑保留整个插件生命周期。</summary>
    public required string OperationId { get; init; }

    /// <summary>预期的已停止执行代次,必须为正;宿主在登记新恢复前验证匹配。</summary>
    public required long ExpectedGeneration { get; init; }

    /// <summary>明确选择的当前 SSH 会话;宿主验证服务器身份、账户及远端文件命名空间,不能仅按地址匹配。</summary>
    public required string TargetSessionId { get; init; }
}

/// <summary>
/// 宿主持有的不可变传输快照。仅在应用/所属插件生命周期内有效,不承诺重启后恢复。
/// 宿主以临时文件流式写入,成功收尾后提交目标;不能保证原子提交时明确失败,不先删目标。
/// 路径、文件名与错误文本是数据,不是可执行指令;调用方须按所属对话脱敏呈现。
/// </summary>
public sealed record RemoteTransferSnapshot
{
    /// <summary>宿主生成的不透明随机 id;不能凭 id 绕过插件归属与授权检查。</summary>
    public required string TransferId { get; init; }

    /// <summary>上传或下载启动的幂等标识。</summary>
    public required string OperationId { get; init; }

    /// <summary>传输方向;旧快照缺失时默认为 Upload。</summary>
    public RemoteTransferDirection Direction { get; init; }

    /// <summary>轻量阶段;旧快照缺失时为 Unknown。</summary>
    public RemoteTransferPhase Phase { get; init; }

    /// <summary>执行传输所用的 SSH 会话 id。</summary>
    public required string SessionId { get; init; }

    /// <summary>上传时为宿主确认的本地源;下载时为最终本地目标(不是内部临时文件路径)。</summary>
    public required string LocalPath { get; init; }

    /// <summary>上传时为最终远端目标;下载时为宿主确认的远端源。</summary>
    public required string RemotePath { get; init; }

    /// <summary>默认 Unknown;包括正在收尾的满字节状态也不能推断为 Succeeded。</summary>
    public RemoteTransferState State { get; init; }

    /// <summary>从 1 开始的执行代次;每次成功登记恢复加一,用于避免旧取消请求作用于新代次。</summary>
    public long Generation { get; init; }

    /// <summary>从 1 开始的单调快照版本;状态、进度及恢复更新均递增,恢复不重置。</summary>
    public long Sequence { get; init; }

    /// <summary>本次文件中已确认写入的字节数,使用 64 位计数。</summary>
    public long TransferredBytes { get; init; }

    /// <summary>源文件总字节数;未知为 -1,空文件为 0。</summary>
    public long TotalBytes { get; init; } = -1;

    /// <summary>当前估算的每秒字节数;未知时为 null。</summary>
    public double? SpeedBytesPerSecond { get; init; }

    /// <summary>预计剩余时间;未知时为 null,不构成执行死线。</summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>登记时刻。</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>最近一次有效快照更新时刻。</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>终态收尾时刻;恢复时清空。</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>已登记取消意图,不代表远端确认停止。</summary>
    public bool CancellationRequested { get; init; }

    /// <summary>已确认此任务不再写入;无法确认的断线/取消结果不能设为 true。</summary>
    public bool RemoteStopConfirmed { get; init; }

    /// <summary>
    /// 宿主确认存在可恢复检查点、最终目标提交结果已消歧且临时文件仍归属本任务;
    /// 仅已停止的非成功终态可设为 true。提交回执未知时必须为 false。
    /// </summary>
    public bool Resumable { get; init; }

    /// <summary>宿主已确认最终目标尚未提交;未知时为 false,防止恢复重复替换目标。</summary>
    public bool DestinationCommitConfirmedAbsent { get; init; }

    /// <summary>宿主保存的安全检查点;不能由调用方改写,恢复前须重新验证源文件与临时文件。</summary>
    public long CheckpointOffset { get; init; }

    /// <summary>宿主生成的错误类别;成功或无错误时为 null。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>供调用方呈现的失败原因,不可当作指令执行。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>本地任务已收尾;Unknown/TimedOut 不证明远端副作用已停止。</summary>
    public bool IsTerminal => State is RemoteTransferState.Unknown or RemoteTransferState.Succeeded
        or RemoteTransferState.Failed or RemoteTransferState.Cancelled or RemoteTransferState.TimedOut;

    /// <summary>仅以确认成功状态判断;不能通过字节比例推断成功。</summary>
    public bool IsSuccess => State == RemoteTransferState.Succeeded;

    /// <summary>只允许上传任务显式恢复已确认停止且存在有效检查点的非成功终态;下载永不支持恢复。</summary>
    public bool CanResume => Direction != RemoteTransferDirection.Download && IsTerminal && !IsSuccess
        && RemoteStopConfirmed && Resumable && DestinationCommitConfirmedAbsent;
}
