using VelaShell.PluginSdk.Events;

namespace VelaShell.PluginSdk.Sessions;

/// <summary>会话状态(与宿主内部状态机的脱敏投影)。</summary>
public enum SessionState
{
    /// <summary>正在建立连接。</summary>
    Connecting,

    /// <summary>已连接。</summary>
    Connected,

    /// <summary>已断开。</summary>
    Disconnected,

    /// <summary>连接失败或异常断开。</summary>
    Error
}

/// <summary>
/// 一条 SSH 会话的脱敏信息。不含任何凭据(密码、私钥、口令永不出宿主核心)。
/// </summary>
/// <param name="SessionId">会话的不透明 id,作为其它能力(远程文件、远程执行)的第一参数。</param>
/// <param name="Host">主机名或 IP。</param>
/// <param name="Port">端口。</param>
/// <param name="Username">登录用户名。</param>
/// <param name="State">当前状态。</param>
/// <param name="CreatedAt">会话创建时间(UTC)。</param>
/// <param name="ConnectedAt">最近一次连接成功时间(UTC),未连接过为 <see langword="null" />。</param>
public sealed record SessionInfo(
    string SessionId,
    string Host,
    int Port,
    string Username,
    SessionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConnectedAt);

/// <summary>
/// 一条<b>已保存的连接配置</b>(脱敏投影)。就是用户会话树里的那些条目,
/// 与 <see cref="SessionInfo" /> 的区别是:它此刻不一定连着,甚至从来没连过。
/// </summary>
/// <remarks>
/// 与 <see cref="SessionInfo" /> 同样的铁律:<b>不含任何凭据</b>。
/// 密码、私钥、口令、跳板机的凭据一律留在宿主核心,
/// 插件只拿得到"有这么一台机器"以及一个不透明 id。
/// </remarks>
/// <param name="SavedSessionId">配置的不透明 id;打开它就把这个 id 交给 <see cref="ISessionsApi.OpenAsync" />。</param>
/// <param name="Name">用户给这条配置起的名字(会话树上显示的那个)。</param>
/// <param name="Host">主机名或 IP。</param>
/// <param name="Port">端口。</param>
/// <param name="Username">登录用户名;配置里没填(留到连接时再问)时为空串。</param>
/// <param name="Group">所在分组的路径(未分组为 <see langword="null" />)。</param>
public sealed record SavedSessionInfo(
    string SavedSessionId,
    string Name,
    string Host,
    int Port,
    string Username,
    string? Group);

/// <summary>打开一条已保存会话时的附加要求。</summary>
/// <param name="Reason">
/// 为什么要连。<b>这句话会原样显示给用户</b>(宿主据此弹确认),所以写清楚是谁、要干什么,
/// 例如「AI 助手:飞书群 运维值班 里 张三 要求查看 nginx 日志」。
/// 写成"插件需要连接"这种废话,等于把确认框变成一个只能盲点的按钮。
/// </param>
/// <param name="ReuseConnected">
/// 这条配置已经有连着的会话时,直接返回那一条(默认)而不是再开一条。
/// 关掉它意味着"我要一条自己的通道",代价是用户的会话列表里会多出一项。
/// </param>
public sealed record SessionOpenOptions(string Reason, bool ReuseConnected = true);

/// <summary>
/// 会话能力:枚举与查询当前 SSH 会话,以及<b>请求宿主打开</b>一条已保存的会话。
/// 连接/断开的推送见 <see cref="IHostEvents" />。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么后来加了"能开会话"。</b>在这之前插件只能操作用户已经手动连上的机器,
/// 于是任何"无人值守"的用法都塌了半边:IM 桥接里同事在群里问一句"生产机磁盘满了吗",
/// 而值班的人昨晚关了那个标签页,机器人就只能回一句"你先去连一台";
/// 外部 agent(Claude Code / Codex)经 MCP 调过来时更是如此 —— 它根本不在这台机器前面。
/// </para>
/// <para>
/// <b>但这是一次实打实的权限扩张,所以契约把闸门写死在宿主这一侧:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// 插件<b>只能打开已保存的配置</b>,不能凭主机名/端口凭空发起连接 ——
/// 「连哪些机器」这件事永远是用户先在宿主里定下来的。
/// </item>
/// <item>
/// 凭据一个字节都不经过插件。要密码、要口令、要指纹确认,全由宿主自己弹窗完成。
/// </item>
/// <item>
/// 宿主<b>可以拒绝</b>,且拒绝是契约的一部分(<see cref="PluginPermissionDeniedException" />)——
/// 用户在确认框上点了"不",插件就该体面退回去,而不是换个姿势再试一次。
/// <see cref="SessionOpenOptions.Reason" /> 就是给那个确认框用的。
/// </item>
/// </list>
/// <para>
/// 这几条不是建议:它们是宿主实现这组方法时必须满足的行为,插件作者可以照着它们写代码。
/// </para>
/// </remarks>
public interface ISessionsApi
{
    /// <summary>当前全部会话的快照。</summary>
    Task<IReadOnlyList<SessionInfo>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>按 id 查询会话;不存在时返回 <see langword="null" />。</summary>
    Task<SessionInfo?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 已保存的连接配置快照(用户会话树里的那些,含此刻没连着的)。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ListAsync" /> 配合用:先看有哪些机器可用,再决定要不要
    /// <see cref="OpenAsync" /> 其中一条。宿主可以按自己的策略只返回一部分
    /// (例如用户在设置里勾了"允许插件连接"的那些)—— 返回少了是正常的,不是错误。
    /// </remarks>
    Task<IReadOnlyList<SavedSessionInfo>> ListSavedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求宿主打开一条已保存的会话,连上之后把它返回。
    /// </summary>
    /// <param name="savedSessionId">
    /// <see cref="SavedSessionInfo.SavedSessionId" />。<b>不是</b> <see cref="SessionInfo.SessionId" /> ——
    /// 后者是一次连接的 id,断线重连就换一个。
    /// </param>
    /// <param name="options">附加要求;<see cref="SessionOpenOptions.Reason" /> 会显示给用户。</param>
    /// <param name="cancellationToken">
    /// 取消。<b>取消只作用于"等它连上"这件事</b>:宿主已经发起的连接不保证被撤销,
    /// 所以取消之后应当再 <see cref="ListAsync" /> 看一眼,而不是假定什么都没发生。
    /// </param>
    /// <returns>已连上的会话;可直接作为远程执行 / 远程文件等能力的第一参数。</returns>
    /// <exception cref="PluginSessionNotFoundException">没有这条已保存的配置。</exception>
    /// <exception cref="PluginPermissionDeniedException">用户拒绝,或宿主策略不允许插件开会话。</exception>
    /// <exception cref="PluginSessionOpenException">配置存在、也放行了,但连不上(网络、认证、指纹不符等)。</exception>
    Task<SessionInfo> OpenAsync(string savedSessionId, SessionOpenOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 关掉一条会话。
    /// </summary>
    /// <remarks>
    /// <b>只能关本插件经 <see cref="OpenAsync" /> 打开的那些。</b>用户自己开的标签页不归插件管 ——
    /// 一个能挂断别人正在用的终端的接口,不该存在。关别人的会话时宿主抛
    /// <see cref="PluginPermissionDeniedException" />。
    /// <para>
    /// 会话已经不在了(用户先手动关了)不算错:此方法幂等,直接返回。
    /// </para>
    /// </remarks>
    /// <exception cref="PluginPermissionDeniedException">这条会话不是本插件打开的。</exception>
    Task CloseAsync(string sessionId, CancellationToken cancellationToken = default);
}
