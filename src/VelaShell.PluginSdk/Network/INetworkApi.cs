namespace VelaShell.PluginSdk.Network;

/// <summary>
/// 出站网络能力:由宿主按**宿主的代理设置**替插件建立到远端的 TCP 连接。
/// <para>
/// <b>为什么插件不该自己 <c>new TcpClient()</c>。</b> 宿主的「设置 → 网络代理」
/// (none / system / http / socks5)只作用于经过宿主拨号的连接。<c>HttpClient</c> 走进程级
/// <c>HttpClient.DefaultProxy</c>,宿主已经把它换成了自己的代理,所以 HTTP 类插件自动跟随;
/// 但裸 TCP 不看任何代理设置 —— 插件直接建的 socket 永远直连,用户配了代理也照样绕过。
/// 插件若自己读代理配置、自己实现 SOCKS5 / HTTP CONNECT,就是又一套与宿主各说各话的实现,
/// 宿主之后加的代理类型、修的握手问题都到不了它那里。
/// </para>
/// <para>
/// 本接口让代理只有一份实现:宿主解析路由(与 SSH / SFTP / FTP 同一个解析器)、完成代理握手与认证,
/// 插件拿到的只是一条已连通的双工流,**看不到代理地址与凭据**。环回目标(含宿主为 SSH 隧道建的
/// 本地转发端口)总是直连,因此「SSH 隧道优先于代理」不需要插件判断。
/// </para>
/// <para>
/// 仅 <c>inProcess</c> 宿主模式可用:返回的是一个活的 <see cref="Stream" />,
/// 跨进程代理一条裸流除了把每个字节多搬一次之外没有别的效果。隔离进程里调用抛
/// <see cref="NotSupportedException" />。
/// </para>
/// <para>可用版本:TBD(发版时替换)。</para>
/// </summary>
public interface INetworkApi
{
    /// <summary>
    /// 按宿主代理设置连接到 <paramref name="host" />:<paramref name="port" />。
    /// <para>
    /// 返回的 <see cref="HostTcpConnection" /> 拥有底层连接,调用方**必须**释放它。
    /// </para>
    /// </summary>
    /// <param name="host">目标主机名或 IP。经代理且代理负责解析 DNS 时,主机名原样交给代理。</param>
    /// <param name="port">目标端口(1–65535)。</param>
    /// <param name="options">选项;为 null 用默认值。</param>
    /// <param name="cancellationToken">取消令牌(只作用于**建立**阶段,含代理握手)。</param>
    /// <returns>已连通的连接。</returns>
    /// <exception cref="ArgumentException">主机为空或端口越界。</exception>
    /// <exception cref="NetworkProxyException">
    /// 用户显式配置的代理不成立(缺主机端口、凭据超长),或代理拒绝 / 握手失败。
    /// 宿主**不会**因此静默改为直连。
    /// </exception>
    /// <exception cref="TimeoutException">在 <see cref="TcpConnectOptions.ConnectTimeout" /> 内未建立。</exception>
    /// <exception cref="System.Net.Sockets.SocketException">目标(直连时)或代理不可达。</exception>
    /// <exception cref="NotSupportedException">宿主实现不支持本能力(隔离进程模式)时。</exception>
    Task<HostTcpConnection> ConnectTcpAsync(
        string host,
        int port,
        TcpConnectOptions? options = null,
        CancellationToken cancellationToken = default)
        // 默认实现只为**源兼容**:第三方的测试替身在 SDK 加了这个能力之后仍然能编译。
        => throw new NotSupportedException("This host does not implement outbound connections.");
}
