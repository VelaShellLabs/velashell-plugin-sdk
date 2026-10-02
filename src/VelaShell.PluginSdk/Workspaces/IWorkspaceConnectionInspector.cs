using VelaShell.PluginSdk.Protocols;

namespace VelaShell.PluginSdk.Workspaces;

/// <summary>
/// 连接对话框里的「连接检查」:工作台提供方(<see cref="IWorkspaceProvider" />)**可选**实现它,
/// 宿主的新建 / 编辑连接对话框就多出右侧栏 —— 随输入实时更新的连接串预览、逐步的测试结果、测试时发现的成员。
/// <para>
/// 为什么需要:声明式字段说得清"要哪些参数",说不清"这些参数拼起来是什么、连上去发生了什么"。
/// 原先测试连接只有"成功 / 失败 + 一句原因"两态 —— 一条走 SSH 隧道、带副本集与 SCRAM 认证的 MongoDB 连接,
/// 失败时用户只能从一句"选服超时"里猜是隧道、端口、认证还是权限的问题;成功时也看不到连上的是主还是从、
/// 账号到底有哪些库的权限。
/// </para>
/// <para>
/// 形状仍然是**声明式**的:插件交出的是结构化数据(片段、步骤、端点),右侧栏怎么画、用什么颜色归宿主 ——
/// 与 <see cref="ProtocolSettingField" /> "是参数表、不是控件树"的取舍一致。插件不往对话框里塞任何控件,
/// 也因此碰不到对话框里的其它东西(口令框、别的连接类型的表单)。
/// </para>
/// <para>
/// 不实现它的工作台照旧:测试连接 = 宿主调一次 <see cref="IWorkspaceProvider.OpenAsync" /> 再把文档关掉。
/// </para>
/// </summary>
public interface IWorkspaceConnectionInspector
{
    /// <summary>
    /// 按表单当前内容生成连接目标的预览(MongoDB 给连接串)。宿主在用户改动表单时反复调用 ——
    /// **必须同步、廉价、不联网、不抛异常**;返回 <see langword="null" /> 表示这一刻没什么可预览的。
    /// </summary>
    /// <param name="draft">
    /// 表单草稿。<see cref="WorkspaceConnectRequest.Password" /> 一律为空串、机密字段
    /// (<see cref="ProtocolSettingField.IsSecret" />)不在 <see cref="WorkspaceConnectRequest.Settings" /> 里 ——
    /// 预览会被显示、被复制,口令不该出现在里面;有没有口令看 <see cref="WorkspaceConnectRequest.Username" /> 就够了。
    /// <see cref="WorkspaceConnectRequest.Tunnel" /> 为空:预览描述的是用户填的目标,不是隧道的本地端点。
    /// </param>
    /// <returns>预览;没有可预览的内容时为 <see langword="null" />。</returns>
    WorkspaceConnectionPreview? Preview(WorkspaceConnectRequest draft);

    /// <summary>
    /// 测试连接,逐步回报。请求与 <see cref="IWorkspaceProvider.OpenAsync" /> 收到的完全一样
    /// (含一次性凭据;走隧道时宿主已经把隧道建好,<see cref="WorkspaceConnectRequest.Host" /> 是本地转发端点)。
    /// <para>
    /// **连不上不抛异常**:失败是报告里某一步的状态,原因写在那一步的 <see cref="WorkspaceProbeStep.Detail" /> 里,
    /// 后面的步骤报 <see cref="WorkspaceProbeState.Skipped" />。只有取消抛 <see cref="OperationCanceledException" />。
    /// 测试结束前必须释放它建的所有连接 —— 测试连接不是打开会话。
    /// </para>
    /// </summary>
    /// <param name="request">连接请求。</param>
    /// <param name="progress">
    /// 进度:每一步开始(<see cref="WorkspaceProbeState.Running" />)与结束时各报一次,宿主按
    /// <see cref="WorkspaceProbeStep.Key" /> 原地更新那一行。可为 <see langword="null" />。
    /// </param>
    /// <param name="cancellationToken">取消令牌(用户关掉对话框、或再点一次测试)。</param>
    /// <returns>完整报告。</returns>
    Task<WorkspaceProbeReport> ProbeAsync(
        WorkspaceConnectRequest request,
        IProgress<WorkspaceProbeStep>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>预览片段的角色。宿主按角色着色(颜色取自宿主令牌),插件不碰颜色。</summary>
public enum WorkspacePreviewRole
{
    /// <summary>普通文字(分隔符、标点)。</summary>
    Plain,

    /// <summary>协议头(<c>mongodb://</c>)。</summary>
    Scheme,

    /// <summary>用户名。</summary>
    User,

    /// <summary>被遮住的机密(<c>:****@</c>)。</summary>
    Secret,

    /// <summary>主机与端口。</summary>
    Host,

    /// <summary>路径 / 库名(<c>/shop</c>)。</summary>
    Path,

    /// <summary>参数名(<c>?replicaSet=</c>)。</summary>
    Key,

    /// <summary>参数值(<c>rs0</c>)。</summary>
    Value
}

/// <summary>预览里的一段。</summary>
/// <param name="Text">文字。</param>
/// <param name="Role">角色。</param>
public sealed record WorkspacePreviewSpan(string Text, WorkspacePreviewRole Role = WorkspacePreviewRole.Plain);

/// <summary>连接目标的预览(右侧栏的「连接字符串」卡片,也是对话框页脚那一行目标预览)。</summary>
public sealed record WorkspaceConnectionPreview
{
    /// <summary>卡片标题(插件自行本地化,如「连接字符串」)。</summary>
    public required string Title { get; init; }

    /// <summary>着色片段;拼起来就是完整的预览文字。</summary>
    public required IReadOnlyList<WorkspacePreviewSpan> Spans { get; init; }

    /// <summary>卡片底部的一行说明(可选,如「密码不会写入连接字符串」)。</summary>
    public string? Note { get; init; }

    /// <summary>完整文字(复制、页脚预览用)。</summary>
    public string Text => string.Concat(Spans.Select(static span => span.Text));
}

/// <summary>测试的一步的状态。</summary>
public enum WorkspaceProbeState
{
    /// <summary>还没轮到。</summary>
    Pending,

    /// <summary>进行中。</summary>
    Running,

    /// <summary>通过。</summary>
    Passed,

    /// <summary>通过,但有需要注意的地方(如账号只有只读权限)。</summary>
    Warning,

    /// <summary>失败(原因在 <see cref="WorkspaceProbeStep.Detail" />)。</summary>
    Failed,

    /// <summary>因为前面失败了而没做。</summary>
    Skipped
}

/// <summary>测试的一步(「SSH 隧道」「TCP 连接」「SCRAM-SHA-256 认证」「hello」「权限检查」)。</summary>
/// <param name="Key">稳定标识:同一步的进度更新与最终结果用同一个键,宿主据此原地更新。</param>
/// <param name="Title">标题(插件自行本地化)。</param>
/// <param name="State">状态。</param>
/// <param name="Detail">一行说明(<c>3 / 3 个成员可达</c>、失败原因);可为空。</param>
/// <param name="ElapsedMs">这一步的耗时(毫秒);不适用时为 <see langword="null" />。</param>
public sealed record WorkspaceProbeStep(
    string Key,
    string Title,
    WorkspaceProbeState State,
    string? Detail = null,
    int? ElapsedMs = null);

/// <summary>
/// 测试时发现的一个端点(副本集成员、集群节点)。<see cref="Address" /> 与某个
/// <see cref="ProtocolSettingKind.HostList" /> 行(或主机 / 端口那一行)相同时,宿主在那一行旁边标出 <see cref="Role" />。
/// </summary>
/// <param name="Address"><c>host:port</c>。</param>
/// <param name="Role">角色徽章(<c>PRIMARY</c> / <c>SECONDARY</c>);没有为 <see langword="null" />。</param>
/// <param name="Tone">徽章的语气色。</param>
/// <param name="Detail">右侧小字(<c>延迟 0.8 s</c>);可为空。</param>
public sealed record WorkspaceProbeEndpoint(
    string Address,
    string? Role = null,
    ProtocolTone Tone = ProtocolTone.Neutral,
    string? Detail = null);

/// <summary>一次测试的完整报告。</summary>
public sealed record WorkspaceProbeReport
{
    /// <summary>连得上(有 <see cref="WorkspaceProbeState.Warning" /> 也算连得上)。</summary>
    public required bool Succeeded { get; init; }

    /// <summary>一句话结论(<c>连接成功 · 38 ms</c>);为空时宿主用自己的「连接成功 / 连接失败」。</summary>
    public string? Summary { get; init; }

    /// <summary>各步骤,按执行顺序。</summary>
    public IReadOnlyList<WorkspaceProbeStep> Steps { get; init; } = [];

    /// <summary>端点列表的标题(插件自行本地化,如「发现的成员」);没有端点时忽略。</summary>
    public string? EndpointsTitle { get; init; }

    /// <summary>发现的端点。</summary>
    public IReadOnlyList<WorkspaceProbeEndpoint> Endpoints { get; init; } = [];

    /// <summary>第一条失败的步骤;都通过时为 <see langword="null" />。</summary>
    public WorkspaceProbeStep? FirstFailure => Steps.FirstOrDefault(static step => step.State == WorkspaceProbeState.Failed);
}
