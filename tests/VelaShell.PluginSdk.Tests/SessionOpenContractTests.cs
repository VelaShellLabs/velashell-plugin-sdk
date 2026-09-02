using VelaShell.PluginSdk.Rpc;
using VelaShell.PluginSdk.Sessions;
using VelaShell.PluginSdk.Testing;

namespace VelaShell.PluginSdk.Tests;

/// <summary>
/// "插件请求宿主打开一条已保存会话"这条能力的契约面。
/// </summary>
/// <remarks>
/// 这是一次实打实的权限扩张(在这之前插件只能用用户手动连上的机器),所以三条闸门
/// —— 只能开已保存的、宿主可以拒绝、只能关自己开的 —— 都在这里钉住。
/// 这些断言守的是<b>语义</b>而不是某个实现:宿主与测试替身都得是这个行为,
/// 插件作者才敢照着写代码。
/// </remarks>
[TestClass]
[TestCategory("Plugins")]
public class SessionOpenContractTests
{
    private static SessionOpenOptions Reason(string reason = "unit test") => new(reason);

    [TestMethod]
    public async Task OpenAsync_ConnectsASavedSessionAndReportsIt()
    {
        var sessions = new FakeSessions();
        SavedSessionInfo saved = sessions.AddSaved(name: "prod-1", host: "10.0.0.1", username: "root");

        SessionInfo opened = await sessions.OpenAsync(saved.SavedSessionId, Reason());

        Assert.AreEqual("10.0.0.1", opened.Host);
        Assert.AreEqual("root", opened.Username);
        Assert.AreEqual(SessionState.Connected, opened.State);
        // 打开之后它就该出现在普通的会话枚举里 —— 插件后续拿它当远程执行的第一参数
        Assert.IsNotNull(await sessions.GetAsync(opened.SessionId));
    }

    /// <summary>已保存列表里没有的 id 一律不认 —— 插件不能凭主机名凭空发起连接。</summary>
    [TestMethod]
    public async Task OpenAsync_RefusesAnUnknownSavedSession()
        => await Assert.ThrowsExactlyAsync<PluginSessionNotFoundException>(
            () => new FakeSessions().OpenAsync("no-such-config", Reason()));

    /// <summary>用户点了"不"就是不。插件应体面退回去,而不是换个姿势再试。</summary>
    [TestMethod]
    public async Task OpenAsync_SurfacesTheUsersRefusal()
    {
        var sessions = new FakeSessions { DenyOpen = true };
        SavedSessionInfo saved = sessions.AddSaved();

        await Assert.ThrowsExactlyAsync<PluginPermissionDeniedException>(
            () => sessions.OpenAsync(saved.SavedSessionId, Reason()));
        Assert.AreEqual(0, sessions.Sessions.Count);
    }

    /// <summary>"不让你连"和"让你连了但没连上"是两回事,类型上就要分开。</summary>
    [TestMethod]
    public async Task OpenAsync_ReportsAConnectFailureSeparatelyFromARefusal()
    {
        var sessions = new FakeSessions { OpenFailure = "connection refused" };
        SavedSessionInfo saved = sessions.AddSaved();

        PluginSessionOpenException error = await Assert.ThrowsExactlyAsync<PluginSessionOpenException>(
            () => sessions.OpenAsync(saved.SavedSessionId, Reason()));

        Assert.AreEqual(saved.SavedSessionId, error.SavedSessionId);
    }

    /// <summary>那句理由是显示给用户的,所以它必须真的传到宿主手上。</summary>
    [TestMethod]
    public async Task OpenAsync_CarriesTheReasonThroughToTheHost()
    {
        var sessions = new FakeSessions();
        SavedSessionInfo saved = sessions.AddSaved();

        await sessions.OpenAsync(saved.SavedSessionId, Reason("AI assistant: check nginx logs for Ann"));

        Assert.AreEqual("AI assistant: check nginx logs for Ann", sessions.LastOpenReason);
    }

    [TestMethod]
    public async Task OpenAsync_ReusesAnAlreadyConnectedSessionByDefault()
    {
        var sessions = new FakeSessions();
        SavedSessionInfo saved = sessions.AddSaved(host: "10.0.0.1", username: "root");
        SessionInfo existing = sessions.AddConnected(host: "10.0.0.1", username: "root");

        SessionInfo opened = await sessions.OpenAsync(saved.SavedSessionId, Reason());

        Assert.AreEqual(existing.SessionId, opened.SessionId);
        Assert.AreEqual(1, sessions.Sessions.Count);
    }

    [TestMethod]
    public async Task OpenAsync_CanBeToldNotToReuse()
    {
        var sessions = new FakeSessions();
        SavedSessionInfo saved = sessions.AddSaved(host: "10.0.0.1", username: "root");
        SessionInfo existing = sessions.AddConnected(host: "10.0.0.1", username: "root");

        SessionInfo opened = await sessions.OpenAsync(saved.SavedSessionId, new SessionOpenOptions("test", false));

        Assert.AreNotEqual(existing.SessionId, opened.SessionId);
        Assert.AreEqual(2, sessions.Sessions.Count);
    }

    [TestMethod]
    public async Task CloseAsync_ClosesWhatThePluginOpened()
    {
        var sessions = new FakeSessions();
        SavedSessionInfo saved = sessions.AddSaved();
        SessionInfo opened = await sessions.OpenAsync(saved.SavedSessionId, Reason());

        await sessions.CloseAsync(opened.SessionId);

        Assert.IsNull(await sessions.GetAsync(opened.SessionId));
    }

    /// <summary>
    /// <b>这条是安全用例。</b>一个能挂断用户正在用的终端的接口不该存在 ——
    /// 插件只能关自己打开的那些。
    /// </summary>
    [TestMethod]
    public async Task CloseAsync_RefusesSessionsTheUserOpened()
    {
        var sessions = new FakeSessions();
        SessionInfo mine = sessions.AddConnected();

        await Assert.ThrowsExactlyAsync<PluginPermissionDeniedException>(() => sessions.CloseAsync(mine.SessionId));
        Assert.IsNotNull(await sessions.GetAsync(mine.SessionId));
    }

    /// <summary>用户先手动关掉了不算错:幂等,直接返回。</summary>
    [TestMethod]
    public async Task CloseAsync_IsIdempotentWhenTheSessionIsAlreadyGone()
        => await new FakeSessions().CloseAsync("gone");

    [TestMethod]
    public async Task ListSavedAsync_ReportsConfigsThatAreNotConnected()
    {
        var sessions = new FakeSessions();
        sessions.AddSaved(name: "prod-1", group: "生产");

        IReadOnlyList<SavedSessionInfo> saved = await sessions.ListSavedAsync();

        Assert.AreEqual(1, saved.Count);
        Assert.AreEqual("prod-1", saved[0].Name);
        Assert.AreEqual("生产", saved[0].Group);
        // 已保存 ≠ 已连接:列表里有它,会话枚举里没有
        Assert.AreEqual(0, (await sessions.ListAsync()).Count);
    }

    /// <summary>
    /// 跨进程时异常要能原样还原成同一个类型。
    /// </summary>
    /// <remarks>
    /// <see cref="PluginSessionOpenException" /> 是 <see cref="InvalidOperationException" /> 的子类,
    /// 而 <c>FromException</c> 是按 switch 的书写顺序匹配的 —— 排到基类后面就永远命中不到,
    /// 隔离进程里的插件收到的会是一个笼统的 <c>invalid-op</c>,"连不上"与"状态不对"再也分不开。
    /// </remarks>
    [TestMethod]
    public void RpcErrorCodes_KeepSessionOpenFailuresDistinct()
    {
        string code = RpcErrorCodes.FromException(new PluginSessionOpenException("cfg", "connection refused"));

        Assert.AreEqual(RpcErrorCodes.SessionOpenFailed, code);
        Assert.IsInstanceOfType<PluginSessionOpenException>(RpcErrorCodes.ToException(code, "connection refused"));
    }

    /// <summary>另外两种会话相关的异常不能被新加的这一档抢走。</summary>
    [TestMethod]
    public void RpcErrorCodes_StillMapTheOlderSessionErrors()
    {
        Assert.AreEqual(RpcErrorCodes.SessionNotFound,
            RpcErrorCodes.FromException(new PluginSessionNotFoundException("s1")));
        Assert.AreEqual(RpcErrorCodes.PermissionDenied,
            RpcErrorCodes.FromException(new PluginPermissionDeniedException("no")));
        Assert.AreEqual(RpcErrorCodes.InvalidOperation,
            RpcErrorCodes.FromException(new InvalidOperationException("plain")));
    }
}
