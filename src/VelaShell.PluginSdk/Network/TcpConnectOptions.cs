namespace VelaShell.PluginSdk.Network;

/// <summary><see cref="INetworkApi.ConnectTcpAsync" /> 的选项。可用版本:2.2.0。</summary>
public sealed record TcpConnectOptions
{
    /// <summary>建立超时上限(宿主强制夹取)。</summary>
    public static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 建立连接(含代理握手)的超时,默认 15 秒,上限 <see cref="MaxConnectTimeout" />(宿主强制夹取)。
    /// 只管**建立**:连上之后读写由调用方的取消令牌与远端决定。
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 关闭 Nagle 算法(<c>TCP_NODELAY</c>),默认 true。交互式协议(终端、远程桌面输入)需要它,
    /// 否则单个按键会被攒到约 40ms 后才发出。
    /// </summary>
    public bool NoDelay { get; init; } = true;

    /// <summary>
    /// TCP keep-alive 的空闲探测时间;<see cref="TimeSpan.Zero" />(默认)表示不开启。
    /// <para>
    /// 用于长时间静默的连接:NAT、防火墙与负载均衡会在空闲几分钟后悄悄丢弃连接。
    /// 宿主在实际连出的 socket 上设置 —— 经代理时那是到**代理**的一段,代理到目标的一段由代理决定。
    /// 平台不支持时静默忽略。
    /// </para>
    /// </summary>
    public TimeSpan KeepAliveIdle { get; init; } = TimeSpan.Zero;
}
