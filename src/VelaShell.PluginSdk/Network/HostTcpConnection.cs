using System.Net;

namespace VelaShell.PluginSdk.Network;

/// <summary>
/// 宿主替插件建立的一条 TCP 连接(<see cref="INetworkApi.ConnectTcpAsync" /> 的结果)。
/// 释放本对象即关闭连接。可用版本:2.2.0。
/// </summary>
/// <param name="stream">已连通的双工字节流;所有权随本对象转移。</param>
/// <param name="viaProxy">是否经代理建立。</param>
/// <param name="remoteEndPoint">直连时对端的实际地址;经代理时为 <see langword="null" />。</param>
public sealed class HostTcpConnection(Stream stream, bool viaProxy, IPEndPoint? remoteEndPoint)
    : IDisposable, IAsyncDisposable
{
    /// <summary>已连通的双工字节流。不要单独释放它,释放本对象。</summary>
    public Stream Stream { get; } = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// 是否经代理建立。经代理时只有这条 TCP 流到得了目标:协议若还会**另开**到同一目标的连接
    /// (例如 RDP 的 UDP 多传输),必须在为 true 时关掉,否则那条连接会绕过代理、
    /// 或者连到代理服务器的地址上。
    /// </summary>
    public bool ViaProxy { get; } = viaProxy;

    /// <summary>
    /// 直连时对端的实际地址(已解析的 IP 与端口);经代理时为 <see langword="null" />,
    /// 因为此时本机 socket 的对端是代理而不是目标。
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; } = viaProxy ? null : remoteEndPoint;

    /// <inheritdoc />
    public void Dispose() => Stream.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}
