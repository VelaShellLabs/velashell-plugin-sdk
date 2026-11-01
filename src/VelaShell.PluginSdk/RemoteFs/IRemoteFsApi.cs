namespace VelaShell.PluginSdk.RemoteFs;

/// <summary>远端文件或目录的元数据。</summary>
/// <param name="Name">名称(不含路径)。</param>
/// <param name="FullPath">完整远端路径。</param>
/// <param name="IsDirectory">
/// 是否为目录。条目是符号链接时描述的是<b>链接指向的对象</b>(指向目录的链接为 true,可以直接进入),
/// 见 <see cref="IsSymbolicLink" />。
/// </param>
/// <param name="Size">大小(字节);目录为 0 或实现定义值。</param>
/// <param name="LastModified">最后修改时间。</param>
/// <param name="Permissions">权限字符串(如 <c>rwxr-xr-x</c>)。</param>
/// <param name="Owner">属主。</param>
/// <param name="Group">属组。</param>
/// <remarks>
/// 链接两个字段(<see cref="IsSymbolicLink" /> / <see cref="LinkTarget" />,可用版本:2.1.3)
/// 是 <c>init</c> 属性而不是位置参数:八参构造与 <c>Deconstruct</c> 的签名保持不变,
/// 老插件、老宿主照旧编译与装载;线上 JSON 缺这两个字段时读成 <see langword="false" /> / <see langword="null" />。
/// </remarks>
public sealed record RemoteFileEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTimeOffset LastModified,
    string Permissions,
    string Owner,
    string Group)
{
    /// <summary>
    /// 这个路径<b>本身</b>是否为符号链接(lstat 口径),默认 <see langword="false" />。
    /// <para>
    /// 为 true 时 <see cref="IsDirectory" />、<see cref="Size" /> 与 <see cref="LastModified" />
    /// 描述的是链接指向的对象 —— 浏览照常、指向目录的链接点进去就是目标目录;
    /// 断链(目标不存在或不可达)保留链接自身的属性,<see cref="IsDirectory" /> 为 false。
    /// </para>
    /// <para>
    /// 消费方据此<b>只处理链接本身</b>:删除删的是链接、不沿链接递归(否则删一个指向目录的链接会删光目标);
    /// 复制得到链接;递归下载 / 同步不跟进指向目录的链接。实现方没有链接概念(对象存储)时一律保持 false。
    /// </para>
    /// <para>可用版本:2.1.3;老宿主 / 老插件给出的条目恒为 false。</para>
    /// </summary>
    public bool IsSymbolicLink { get; init; }

    /// <summary>
    /// 链接的<b>原始</b>目标文本(readlink 的结果,可能是相对路径,相对链接所在目录解析,与 <c>ln -s</c> 一致)。
    /// 非链接、或后端给不出目标时为 <see langword="null" />。只用于展示与复制链接,
    /// 不要拿它自行拼路径去访问 —— 要访问目标直接用条目的 <see cref="FullPath" />(服务器负责跟随)。
    /// <para>可用版本:2.1.3。</para>
    /// </summary>
    public string? LinkTarget { get; init; }
}

/// <summary>文件传输进度。宿主对回调做了节流(≥100ms 一次),回调频率不代表 I/O 频率。</summary>
/// <param name="TransferredBytes">已传输字节数。</param>
/// <param name="TotalBytes">总字节数;未知时为 -1。</param>
public readonly record struct RemoteTransferProgress(long TransferredBytes, long TotalBytes);

/// <summary>
/// 远程文件能力:复用宿主既有 SSH 会话的 SFTP 通道(不重复认证)。
/// 会话不存在或未连接时抛 <see cref="PluginSessionNotFoundException" />。
/// 语义约定:<see cref="StatAsync" /> 对不存在的路径返回 <see langword="null" />,不以异常判存在。
/// </summary>
public interface IRemoteFsApi
{
    /// <summary>宿主是否实现独立传输任务;旧宿主默认不支持,不可退回长时间同步调用。</summary>
    bool SupportsTransferOperations => false;

    /// <summary>宿主是否实现独立下载任务;旧宿主默认不支持,不可退回长时间同步调用。</summary>
    bool SupportsDownloadOperations => false;

    /// <summary>
    /// 宿主是否实现 <see cref="WithdrawPendingTransferAsync" />(只撤批准还没生效的传输);旧宿主默认不支持。
    /// 与上传、下载能力各自独立:实现了独立任务的宿主不一定实现它。
    /// <para>可用版本:2.1.4。</para>
    /// </summary>
    bool SupportsTransferWithdraw => false;

    /// <summary>一次进度等待的最大秒数;等待可重复,不代表传输执行死线。</summary>
    public const int MaxTransferWaitSeconds = 30;

    /// <summary>
    /// 启动宿主持有的流式上传,不设固定文件大小上限。返回任务快照,不等待上传完成。
    /// options.OperationId 在同一插件生命周期内幂等;重试必须复用同一值及参数,
    /// 参数不符应拒绝,不能开始第二次上传。调用前由插件完成审批,宿主再次校验权限。
    /// </summary>
    /// <remarks>
    /// 令牌只取消启动请求,不是任务寿命。请求取消或丢失响应不证明任务没有启动;
    /// 应以相同 OperationId 重取回执,不得换新 id 自动重传。
    /// 任务归属于宿主绑定的插件身份,存活到显式取消、插件停用、权限撤销或宿主退出。
    /// 所有任务访问均复核本地读取、远端写入权限;不能通过别的插件的 transferId 访问任务。
    /// 审批绑定规范化的源/目标路径、实际源对象及覆盖策略;排队开始前与传输期间必须保证源稳定,
    /// 不能通过替换路径、符号链接或并发写入改变已批准内容,无法保证时明确失败。
    /// 启动、取消、恢复在任务内串行化;先复核授权和幂等记录,再校验当前状态。
    /// start/resume 的 OperationId 共用插件生命周期命名空间,记录请求种类和全部参数;
    /// 幂等重取返回当前快照但绝不重复执行。幂等记录/墓碑保留整个插件生命周期,
    /// 不能回收后以同一 id 重执行;额度满应拒绝新任务。
    /// 相同 id 参数冲突抛 InvalidOperationException;无效或不属于本插件的 id 统一抛 FileNotFoundException。
    /// </remarks>
    /// <exception cref="PluginTransferRejectedException">
    /// 登记之前就被宿主明确拒绝(源被占用、已删除、额度满、本机空间不足):什么都没开始、回执未登记,
    /// 与「结果未知」「用户拒绝」处置不同,见该类型说明。可用版本:2.1.3。
    /// </exception>
    Task<RemoteTransferSnapshot> StartUploadAsync(string sessionId, string localPath, string remotePath,
        RemoteUploadOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement transfer operations.");

    /// <summary>
    /// 启动宿主持有的流式下载,不设固定文件大小上限,不等待传输完成。RemotePath 为远端源,
    /// LocalPath 为最终本地目标;下载不支持恢复。OperationId 与上传共享插件生命周期命名空间,
    /// 按方向、会话、路径及全部选项幂等去重。
    /// </summary>
    /// <remarks>
    /// 请求令牌只取消本次启动回执等待,不取消已登记的后台任务;回执丢失时必须复用同一 OperationId。
    /// 宿主在传输前展示并校验远端读取、本地写入、覆盖策略及真实会话的单任务审批;权限撤销会使待审批
    /// 与活动任务失效并请求取消,提交前重新校验。默认 Overwrite=false 必须使用宿主临时文件和原子无替换提交,
    /// 不能只做预先存在性检查或先删除目标。无支持时明确抛 NotSupportedException,不退回 DownloadFileAsync。
    /// </remarks>
    /// <exception cref="PluginTransferRejectedException">登记之前就被宿主明确拒绝,见该类型说明。可用版本:2.1.3。</exception>
    Task<RemoteTransferSnapshot> StartDownloadAsync(string sessionId, string remotePath, string localPath,
        RemoteDownloadOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement download transfer operations.");

    /// <summary>读取所属插件任务的权威快照;无效或越权 id 不泄漏任务元数据。</summary>
    Task<RemoteTransferSnapshot> GetTransferAsync(string transferId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement transfer operations.");

    /// <summary>
    /// 等待 Sequence 大于 afterSequence 或到达终态,返回最新快照。
    /// afterSequence 非负,waitTimeout 在零到 MaxTransferWaitSeconds 秒之间;超界应拒绝。
    /// 等待到期仍返回当前快照,取消令牌仅取消此等待,两者都不停止后台传输。
    /// </summary>
    Task<RemoteTransferSnapshot> WaitTransferAsync(string transferId, long afterSequence, TimeSpan waitTimeout,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement transfer operations.");

    /// <summary>
    /// 幂等请求停止所属插件当前执行代次,返回当前状态,不保证此时已停止。
    /// 令牌只取消本次请求;已登记的取消意图不回滚。确认停止才标 Cancelled,
    /// 无法确认则标 Unknown;用户目标不会因取消而被宣称已回滚。
    /// expectedGeneration 必须为正且等于当前执行代次;旧代次取消抛 InvalidOperationException,
    /// 绝不停止恢复后的新代次。相同代次的重复取消幂等。
    /// 这是无条件的取消:已经批准、正在传的也停。只想撤「还没批准」的,用 <see cref="WithdrawPendingTransferAsync" />。
    /// </summary>
    Task<RemoteTransferSnapshot> CancelTransferAsync(string transferId, long expectedGeneration, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement transfer operations.");

    /// <summary>
    /// 条件取消:只在这一代的宿主批准<b>还没生效</b>时撤下传输,批准已经生效的什么都不动。
    /// 与「先 <see cref="GetTransferAsync" /> 看到 <see cref="RemoteTransferPhase.Approval" />、再
    /// <see cref="CancelTransferAsync" />」不同,判断与撤下在宿主那边是同一步 —— 中间没有一个往返的空当,
    /// 不会出现宿主恰在这时批准、刚批准的又被撤掉。
    /// </summary>
    /// <param name="transferId">所属插件的任务 id。</param>
    /// <param name="expectedGeneration">
    /// 期望的执行代次,必须为正且等于当前代次;不符抛 <see cref="InvalidOperationException" />。
    /// 恢复产生的新代次要重新批准,旧代次的撤回碰不到它。
    /// </param>
    /// <param name="cancellationToken">只取消本次请求的等待;宿主已经登记的撤回不回滚。</param>
    /// <returns>结论与调用之后的权威快照,结论的划分见 <see cref="RemoteTransferWithdrawOutcome" />。</returns>
    /// <remarks>
    /// <para>
    /// 宿主把「批准生效」与「条件撤回」放在同一个串行点上判定,先到者赢。批准先生效:返回
    /// <see cref="RemoteTransferWithdrawOutcome.PastApproval" />,任务照常进行。撤回先到:返回
    /// <see cref="RemoteTransferWithdrawOutcome.Withdrawn" />,宿主登记取消意图,已弹出的确认框关掉、排在确认闸后面的不再弹,
    /// 随后到达的批准作废;这一代什么都不读、什么都不写,以 <see cref="RemoteTransferState.Cancelled" /> 收尾,
    /// <see cref="RemoteTransferSnapshot.RemoteStopConfirmed" /> 为 true。实现它的宿主在批准生效的同一步把
    /// <see cref="RemoteTransferSnapshot.Phase" /> 移出 <see cref="RemoteTransferPhase.Approval" />(上传与下载同一口径),
    /// 所以快照里的 Approval 就等于「批准还没生效」。
    /// </para>
    /// <para>
    /// 幂等:同一代次重复调用得到同一结论,回执丢了就原样再调一次(或 <see cref="GetTransferAsync" />)。
    /// 无效或不属于本插件的 id 与其它任务方法一样抛 <see cref="System.IO.FileNotFoundException" />;
    /// 权限撤销之后与 <see cref="CancelTransferAsync" /> 一样仍可用于清理,快照按撤权口径脱敏。
    /// </para>
    /// <para>
    /// 不支持的宿主(<see cref="SupportsTransferWithdraw" /> 为 false)明确抛 <see cref="NotSupportedException" />,
    /// <b>不</b>退化成无条件的 <see cref="CancelTransferAsync" />。插件在这样的宿主上要么照旧「先取快照、按代次取消」并接受
    /// 那一个往返的空当,要么不撤、让确认框照弹 —— 选哪条是插件的取舍。契约程序集由宿主提供:装着更旧 SDK 的宿主上
    /// 连这个成员都不存在,用到它的插件要声明对应的 <c>minSdkVersion</c>。
    /// </para>
    /// <para>可用版本:2.1.4。只增不改,<c>apiLevel</c> 不变。</para>
    /// </remarks>
    Task<RemoteTransferWithdrawResult> WithdrawPendingTransferAsync(string transferId, long expectedGeneration,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement conditional transfer withdrawal.");

    /// <summary>
    /// 显式恢复可恢复的终态任务,保持 transferId 与递增 Sequence;须重新审批及校验授权。
    /// 先按 options.OperationId 查已登记回执,再检查 ExpectedGeneration 与 CanResume,防止重复恢复。
    /// 宿主先确认旧传输已停止、尚未提交最终目标、源文件身份未变化及临时检查点归属有效,
    /// 才能恢复。TargetSessionId 为明确选择的当前连接;宿主校验服务器密钥身份、账户及远端
    /// 文件命名空间与原连接一致,仅地址相同不足以恢复,无法证明一致则明确拒绝。
    /// 调用方不能指定路径、偏移或源文件身份绕过校验;不满足时明确失败,不从头重传。
    /// 新代次 Generation 加一、清除取消意图与完成时刻,按原 Timeout 获得新的显式执行预算。
    /// 请求令牌只取消本次请求;丢失回执时复用同一 OperationId,不得自动创建新上传。
    /// </summary>
    Task<RemoteTransferSnapshot> ResumeTransferAsync(string transferId, RemoteTransferResumeOptions options,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This host does not implement transfer operations.");

    /// <summary>列举远端目录。</summary>
    Task<IReadOnlyList<RemoteFileEntry>> ListDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default);

    /// <summary>获取单个远端条目的元数据;路径不存在时返回 <see langword="null" />。</summary>
    Task<RemoteFileEntry?> StatAsync(string sessionId, string path, CancellationToken cancellationToken = default);

    /// <summary>远端路径是否存在(文件或目录)。</summary>
    Task<bool> ExistsAsync(string sessionId, string path, CancellationToken cancellationToken = default);

    /// <summary>会话的 SFTP 工作目录(通常为登录账户的 home 目录)。</summary>
    Task<string> GetWorkingDirectoryAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>下载远端文件到本地路径。</summary>
    Task DownloadFileAsync(string sessionId, string remotePath, string localPath,
        IProgress<RemoteTransferProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>上传本地文件到远端路径。</summary>
    Task UploadFileAsync(string sessionId, string localPath, string remotePath,
        IProgress<RemoteTransferProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 打开远端文件的**只读顺序流**(边读边处理,不经本地临时文件;调用方负责释放)。
    /// 大文件解析(日志扫描、逐行处理)优先用它;隔离模式下流经 RPC 分块拉取,
    /// 不支持 Seek。
    /// </summary>
    Task<Stream> OpenReadAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取远端小文件内容。文件大于 <paramref name="maxBytes" />(默认 16MB)时抛
    /// <see cref="InvalidOperationException" /> —— 大文件请用 <see cref="DownloadFileAsync" />。
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string sessionId, string remotePath, int maxBytes = 16 * 1024 * 1024, CancellationToken cancellationToken = default);

    /// <summary>将内容写入远端文件(覆盖)。适合小文件;大文件请用 <see cref="UploadFileAsync" />。</summary>
    Task WriteAllBytesAsync(string sessionId, string remotePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>删除远端文件,或递归删除目录。</summary>
    Task DeleteAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>创建远端目录(已存在时报错;幂等版见 <see cref="EnsureDirectoryAsync" />)。</summary>
    Task CreateDirectoryAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>确保远端目录存在(幂等)。</summary>
    Task EnsureDirectoryAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>重命名或移动远端条目。</summary>
    Task RenameAsync(string sessionId, string oldPath, string newPath, CancellationToken cancellationToken = default);
}
