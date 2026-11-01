using System.Text.Json;
using VelaShell.PluginSdk.Rpc;
using VelaShell.PluginSdk.Secrets;
using VelaShell.PluginSdk.Testing;

namespace VelaShell.PluginSdk.Tests;

/// <summary>
/// 机密存储「已锁定」(用户设了主密码、此刻没解锁):宣告了能力位的宿主直接抛 <see cref="SecretsLockedException" />,
/// 插件必须能把它与「机密不存在」(null)、「用户拒绝」与笼统的「状态不对」分开 —— 否则插件会让用户重填,
/// 解锁后旧值就被覆盖了。进程内是同一个类型,隔离进程经错误码还原成同一个类型;旧宿主如实不宣告。
/// </summary>
[TestClass]
public sealed class SecretsLockedContractTests
{
    /// <summary>只实现三个必需方法的旧宿主。</summary>
    private sealed class LegacySecrets : ISecretsApi
    {
        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<string?>("stored");
        public Task SetAsync(string name, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    [TestMethod]
    public void Type_IsAnInvalidOperation_DistinctFromDenial()
    {
        var locked = new SecretsLockedException("locked");
        Assert.IsInstanceOfType<InvalidOperationException>(locked, "旧插件 catch (InvalidOperationException) 照样接得住");
        Assert.IsNotInstanceOfType<PluginPermissionDeniedException>(locked, "「还没解锁」不是「用户说了不」");
        Assert.IsTrue(typeof(SecretsLockedException).IsSealed);
        Assert.AreEqual("locked", locked.Message);
        var inner = new IOException("vault file busy");
        Assert.AreSame(inner, new SecretsLockedException("locked", inner).InnerException);
    }

    [TestMethod]
    public void ErrorCodes_RoundTrip()
    {
        Assert.AreEqual("secrets-locked", RpcErrorCodes.SecretsLocked, "线上码是两端共享的协议常量,不能悄悄改名");
        string code = RpcErrorCodes.FromException(new SecretsLockedException("unlock VelaShell first"));
        Assert.AreEqual(RpcErrorCodes.SecretsLocked, code);
        Exception restored = RpcErrorCodes.ToException(code, "unlock VelaShell first");
        Assert.IsInstanceOfType<SecretsLockedException>(restored);
        Assert.AreEqual("unlock VelaShell first", restored.Message);

        string[] codes = [.. typeof(RpcErrorCodes).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!)];
        Assert.HasCount(codes.Length, codes.Distinct(StringComparer.Ordinal), "错误码撞了,隔离插件会还原成别的类型");
    }

    [TestMethod]
    public void ErrorCodes_MatchBeforeInvalidOperation_AndDoNotSwallowNeighbours()
    {
        Assert.AreNotEqual(RpcErrorCodes.InvalidOperation, RpcErrorCodes.FromException(new SecretsLockedException("x")),
            "子类必须排在 InvalidOperationException 之前");
        Assert.AreEqual(RpcErrorCodes.InvalidOperation, RpcErrorCodes.FromException(new InvalidOperationException("plain")));
        Assert.AreEqual(RpcErrorCodes.PermissionDenied, RpcErrorCodes.FromException(new PluginPermissionDeniedException("no")));
        Assert.IsNotInstanceOfType<SecretsLockedException>(RpcErrorCodes.ToException(RpcErrorCodes.InvalidOperation, "x"));
        Assert.IsNotInstanceOfType<SecretsLockedException>(RpcErrorCodes.ToException(RpcErrorCodes.PermissionDenied, "x"));
        Assert.IsNotInstanceOfType<SecretsLockedException>(RpcErrorCodes.ToException("secrets-lockedx", "x"));
        Assert.IsNotInstanceOfType<SecretsLockedException>(RpcErrorCodes.ToException(null, "x"));
    }

    [TestMethod]
    public void LegacyHost_DoesNotAnnounceLockedState()
    {
        ISecretsApi legacy = new LegacySecrets();
        Assert.IsFalse(legacy.SupportsLockedState, "只实现三个方法的旧宿主不会抛 SecretsLockedException");
    }

    [TestMethod]
    public void Handshake_MissingFieldMeansLegacyHost_AndStaysBinaryCompatible()
    {
        HandshakeResponse legacy = JsonSerializer.Deserialize<HandshakeResponse>(
            """{"ApiLevel":2,"HostVersion":"1","Locale":"en","Theme":"dark","SupportsNotifications":true,"SupportsTransferWithdraw":true}""")!;
        Assert.IsFalse(legacy.SupportsSecretsLocked, "旧宿主不宣告就是不支持,不能按别的能力位推断");
        Assert.IsTrue(legacy.SupportsNotifications);
        HandshakeResponse modern = legacy with { SupportsSecretsLocked = true };
        Assert.AreEqual(modern, JsonSerializer.Deserialize<HandshakeResponse>(JsonSerializer.Serialize(modern)));
        Assert.HasCount(6, typeof(HandshakeResponse).GetMethod("Deconstruct")!.GetParameters());
    }

    [TestMethod]
    public async Task Fake_DefaultsToANewHostThatIsUnlocked()
    {
        var fake = new FakeSecrets();
        ISecretsApi api = fake;
        Assert.IsTrue(api.SupportsLockedState, "替身默认模拟新宿主,且经接口读到的是替身自己的值");
        Assert.IsFalse(fake.IsLocked);
        await api.SetAsync("token", "v1");
        Assert.AreEqual("v1", await api.GetAsync("token"));
        Assert.IsNull(await api.GetAsync("missing"), "没锁时不存在照旧是 null");
    }

    [TestMethod]
    public async Task Fake_Locked_GetAndSetThrow_DeleteStillWorks_ValuesSurviveUnlock()
    {
        var fake = new FakeSecrets();
        fake.Values["token"] = "original";
        fake.Values["stale"] = "old";
        fake.IsLocked = true;
        ISecretsApi api = fake;

        await Assert.ThrowsExactlyAsync<SecretsLockedException>(() => api.GetAsync("token"), "锁着绝不返回 null");
        await Assert.ThrowsExactlyAsync<SecretsLockedException>(() => api.GetAsync("missing"), "锁着时连「不存在」也说不出来");
        await Assert.ThrowsExactlyAsync<SecretsLockedException>(() => api.SetAsync("token", "re-entered"));
        Assert.AreEqual("original", fake.Values["token"], "锁着时的写入不能覆盖旧值");
        Assert.IsFalse(fake.Values.ContainsKey("missing"));

        Assert.IsTrue(await api.DeleteAsync("stale"), "删除不需要解密,锁着照常");
        Assert.IsFalse(await api.DeleteAsync("stale"));

        fake.IsLocked = false;
        Assert.AreEqual("original", await api.GetAsync("token"), "解锁后原来的值还在");
        Assert.IsNull(await api.GetAsync("stale"));
        await api.SetAsync("token", "rotated");
        Assert.AreEqual("rotated", await api.GetAsync("token"));
    }

    [TestMethod]
    public async Task Fake_LegacyHostLocked_NeverThrowsTheNewType_AndNeverReturnsNull()
    {
        var fake = new FakeSecrets { SupportsLockedState = false, IsLocked = true };
        fake.Values["token"] = "original";
        ISecretsApi api = fake;

        Assert.IsFalse(api.SupportsLockedState);
        await Assert.ThrowsExactlyAsync<PluginPermissionDeniedException>(() => api.GetAsync("token"),
            "旧宿主不抛 SecretsLockedException,但也不能对已存的机密返回 null");
        await Assert.ThrowsExactlyAsync<PluginPermissionDeniedException>(() => api.SetAsync("token", "re-entered"));
        Assert.AreEqual("original", fake.Values["token"]);
        Assert.IsTrue(await api.DeleteAsync("token"));
    }

    [TestMethod]
    public async Task IsolatedGetAndSet_RestoreTheLockedType_DeleteStillWorks()
    {
        await using RpcTestPipes pipes = await RpcTestPipes.ConnectAsync();
        await using var host = new RpcConnection(pipes.Server);
        await using var plugin = new RpcConnection(pipes.Client);
        host.SetRequestHandler((method, payload, _) => method switch
        {
            PluginRpc.SecretsGet or PluginRpc.SecretsSet => throw new SecretsLockedException("The secret store is locked."),
            PluginRpc.SecretsDelete => Task.FromResult<object?>(true),
            _ => throw new InvalidOperationException(method)
        });
        host.Start();
        plugin.Start();

        SecretsLockedException locked = await Assert.ThrowsExactlyAsync<SecretsLockedException>(() =>
            plugin.RequestAsync<string>(PluginRpc.SecretsGet, new SecretRef("token"), TimeSpan.FromSeconds(5)));
        Assert.AreEqual("The secret store is locked.", locked.Message);

        await Assert.ThrowsExactlyAsync<SecretsLockedException>(() =>
            plugin.RequestAsync<object>(PluginRpc.SecretsSet, new SecretSetRequest("token", "v"), TimeSpan.FromSeconds(5)));

        Assert.IsTrue(await plugin.RequestAsync<bool>(PluginRpc.SecretsDelete, new SecretRef("token"), TimeSpan.FromSeconds(5)));
    }
}
