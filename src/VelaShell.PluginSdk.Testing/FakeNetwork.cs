using System.Net;
using VelaShell.PluginSdk.Network;

namespace VelaShell.PluginSdk.Testing;

/// <summary>
/// <see cref="INetworkApi" /> 的测试替身:优先走 <see cref="Handler" />,
/// 其次按顺序吐出 <see cref="Responses" /> 队列;都没有时抛 <see cref="InvalidOperationException" />。
/// <para>
/// 与 <see cref="FakeRemoteTunnel" /> 一样,刻意**不**在没有脚本时返回空流:
/// 空流在被测代码看来是「对端立刻断开」,报出来的会是和被测逻辑无关的 I/O 错误,
/// 而不是「你忘了给这条连接写应答」。
/// </para>
/// <para>
/// 用 <see cref="ViaProxy" /> 模拟宿主配置了代理的情形,验证插件在经代理时关掉了
/// 会绕过代理的旁路(例如 UDP);<see cref="Failure" /> 模拟代理或目标不可达。
/// </para>
/// </summary>
public sealed class FakeNetwork : INetworkApi
{
    private int _active;

    /// <summary>脚本化应答:(host, port) → 双工流。</summary>
    public Func<string, int, Stream>? Handler { get; set; }

    /// <summary>顺序应答队列(无 <see cref="Handler" /> 时使用)。</summary>
    public Queue<Stream> Responses { get; } = new();

    /// <summary>设置后每次连接都抛出它(先记录到 <see cref="Connected" />),例如 <see cref="NetworkProxyException" />。</summary>
    public Exception? Failure { get; set; }

    /// <summary>返回的连接是否标记为经代理建立。默认 false(直连)。</summary>
    public bool ViaProxy { get; set; }

    /// <summary>直连时报告的对端地址;为 null 时用 <c>127.0.0.1:port</c>。</summary>
    public IPEndPoint? RemoteEndPoint { get; set; }

    /// <summary>全部连接请求记录(含失败的)。</summary>
    public List<(string Host, int Port, TcpConnectOptions Options)> Connected { get; } = [];

    /// <summary>当前已建立、尚未释放的连接条数。</summary>
    public int ActiveConnections => Volatile.Read(ref _active);

    /// <inheritdoc />
    public Task<HostTcpConnection> ConnectTcpAsync(string host, int port, TcpConnectOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Connected.Add((host, port, options ?? new TcpConnectOptions()));
        if (Failure is { } failure)
        {
            return Task.FromException<HostTcpConnection>(failure);
        }

        Stream inner = Handler?.Invoke(host, port)
                       ?? (Responses.Count > 0
                           ? Responses.Dequeue()
                           : throw new InvalidOperationException(
                               $"FakeNetwork has no scripted response for '{host}:{port}'. Set Handler or enqueue Responses."));
        Interlocked.Increment(ref _active);
        var tracked = new TrackedStream(inner, () => Interlocked.Decrement(ref _active));
        IPEndPoint endPoint = RemoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, port);
        return Task.FromResult(new HostTcpConnection(tracked, ViaProxy, endPoint));
    }

    private sealed class TrackedStream(Stream inner, Action onDisposed) : DelegatingStream(inner)
    {
        private int _disposed;

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { base.Dispose(disposing); }
                finally { onDisposed(); }
                return;
            }
            base.Dispose(disposing);
        }
    }
}
