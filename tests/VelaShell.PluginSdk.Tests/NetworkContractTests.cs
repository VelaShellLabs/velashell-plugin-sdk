using System.Net;
using VelaShell.PluginSdk.Network;
using VelaShell.PluginSdk.Testing;

namespace VelaShell.PluginSdk.Tests;

/// <summary>
/// 出站网络能力(<see cref="INetworkApi" />):插件的裸 TCP 由宿主按代理设置拨号。
/// 契约要点是 —— 经代理时不暴露对端地址(那是代理不是目标)、连接对象拥有流、
/// 代理失败是可区分的 <see cref="IOException" />、没实现的旧替身仍能编译并明确报不支持。
/// </summary>
[TestClass]
public sealed class NetworkContractTests
{
    /// <summary>没覆写任何成员的替身:靠接口默认实现编译通过。</summary>
    private sealed class LegacyNetwork : INetworkApi;

    [TestMethod]
    public async Task DefaultImplementation_ReportsNotSupported()
    {
        INetworkApi legacy = new LegacyNetwork();
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => legacy.ConnectTcpAsync("example.com", 23));
    }

    [TestMethod]
    public void Options_Defaults()
    {
        var options = new TcpConnectOptions();
        Assert.AreEqual(TimeSpan.FromSeconds(15), options.ConnectTimeout);
        Assert.IsTrue(options.NoDelay, "交互式协议默认要关 Nagle");
        Assert.AreEqual(TimeSpan.Zero, options.KeepAliveIdle, "keep-alive 默认不开");
        Assert.AreEqual(TimeSpan.FromMinutes(2), TcpConnectOptions.MaxConnectTimeout);
    }

    [TestMethod]
    public void Connection_HidesTheProxyEndpointWhenProxied()
    {
        var proxyPeer = new IPEndPoint(IPAddress.Parse("192.0.2.7"), 7890);
        using var proxied = new HostTcpConnection(new MemoryStream(), viaProxy: true, proxyPeer);
        Assert.IsTrue(proxied.ViaProxy);
        Assert.IsNull(proxied.RemoteEndPoint, "经代理时 socket 的对端是代理,不能当成目标交给插件");

        var target = new IPEndPoint(IPAddress.Parse("198.51.100.3"), 3389);
        using var direct = new HostTcpConnection(new MemoryStream(), viaProxy: false, target);
        Assert.IsFalse(direct.ViaProxy);
        Assert.AreEqual(target, direct.RemoteEndPoint);
    }

    [TestMethod]
    public async Task Connection_OwnsTheStream()
    {
        var stream = new MemoryStream();
        var connection = new HostTcpConnection(stream, viaProxy: false, null);
        await connection.DisposeAsync();
        Assert.IsFalse(stream.CanRead, "释放连接即关闭流");
        Assert.ThrowsExactly<ArgumentNullException>(() => new HostTcpConnection(null!, false, null));
    }

    [TestMethod]
    public void ProxyException_IsAnIOException()
    {
        var ex = new NetworkProxyException("proxy refused");
        Assert.IsInstanceOfType<IOException>(ex, "catch (IOException) 的旧代码照样接得住");
        Assert.IsTrue(typeof(NetworkProxyException).IsSealed);
        var inner = new IOException("handshake");
        Assert.AreSame(inner, new NetworkProxyException("proxy refused", inner).InnerException);
    }

    [TestMethod]
    public async Task Fake_RecordsRequestsAndTracksDisposal()
    {
        var ctx = new TestPluginContext();
        ctx.FakeNetwork.Responses.Enqueue(new MemoryStream([1, 2, 3]));
        var options = new TcpConnectOptions { KeepAliveIdle = TimeSpan.FromSeconds(30) };

        HostTcpConnection connection = await ctx.Network.ConnectTcpAsync("db.internal", 6379, options);

        Assert.AreEqual(1, ctx.FakeNetwork.ActiveConnections);
        Assert.AreEqual(("db.internal", 6379, options), ctx.FakeNetwork.Connected.Single());
        Assert.IsFalse(connection.ViaProxy);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 6379), connection.RemoteEndPoint);
        Assert.AreEqual(1, connection.Stream.ReadByte());

        connection.Dispose();
        connection.Dispose();
        Assert.AreEqual(0, ctx.FakeNetwork.ActiveConnections);
    }

    [TestMethod]
    public async Task Fake_SimulatesProxyAndFailure()
    {
        var fake = new FakeNetwork { ViaProxy = true, Handler = (_, _) => new MemoryStream() };
        await using (HostTcpConnection proxied = await fake.ConnectTcpAsync("rdp.example", 3389))
        {
            Assert.IsTrue(proxied.ViaProxy);
            Assert.IsNull(proxied.RemoteEndPoint);
        }

        fake.Failure = new NetworkProxyException("bad proxy");
        await Assert.ThrowsExactlyAsync<NetworkProxyException>(() => fake.ConnectTcpAsync("rdp.example", 3389));
        Assert.HasCount(2, fake.Connected);
        Assert.AreEqual(0, fake.ActiveConnections);
    }

    [TestMethod]
    public async Task Fake_WithoutScript_FailsLoudly()
    {
        var fake = new FakeNetwork();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fake.ConnectTcpAsync("h", 1));
    }
}
