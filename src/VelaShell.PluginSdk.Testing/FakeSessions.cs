using VelaShell.PluginSdk.Sessions;

namespace VelaShell.PluginSdk.Testing;

/// <summary><see cref="ISessionsApi" /> 的测试替身:测试直接摆放会话列表。</summary>
public sealed class FakeSessions : ISessionsApi
{
    /// <summary>当前会话列表;测试可直接增删。</summary>
    public List<SessionInfo> Sessions { get; } = [];

    /// <summary>已保存的连接配置;测试可直接增删。</summary>
    public List<SavedSessionInfo> Saved { get; } = [];

    /// <summary>经 <see cref="OpenAsync" /> 打开的那些会话 id(<see cref="CloseAsync" /> 只认这些)。</summary>
    public HashSet<string> OpenedByPlugin { get; } = [];

    /// <summary>最近一次 <see cref="OpenAsync" /> 收到的理由。</summary>
    /// <remarks>
    /// 单独留一个字段是有意的:那句理由是显示给用户的确认框看的,
    /// 「插件到底把什么话交了上去」正是这条能力最该被断言的一点 ——
    /// 一个传了"插件需要连接"的实现,功能上完全正常,却把确认框变成了盲点按钮。
    /// </remarks>
    public string? LastOpenReason { get; private set; }

    /// <summary>置 true 后 <see cref="OpenAsync" /> 一律抛 <see cref="PluginPermissionDeniedException" />(模拟用户点了"不")。</summary>
    public bool DenyOpen { get; set; }

    /// <summary>非空时 <see cref="OpenAsync" /> 抛 <see cref="PluginSessionOpenException" />(模拟连不上)。</summary>
    public string? OpenFailure { get; set; }

    /// <summary>便捷构造一条已连接会话并加入列表。</summary>
    public SessionInfo AddConnected(string host = "test-host", int port = 22, string username = "tester")
    {
        var session = new SessionInfo(Guid.NewGuid().ToString(), host, port, username,
            SessionState.Connected, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Sessions.Add(session);
        return session;
    }

    /// <summary>便捷构造一条已保存配置(此刻并没有连着)并加入列表。</summary>
    public SavedSessionInfo AddSaved(string name = "test-saved", string host = "test-host", int port = 22,
        string username = "tester", string? group = null)
    {
        var saved = new SavedSessionInfo(Guid.NewGuid().ToString(), name, host, port, username, group);
        Saved.Add(saved);
        return saved;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionInfo>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SessionInfo>>([.. Sessions]);

    /// <inheritdoc />
    public Task<SessionInfo?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Sessions.Find(s => s.SessionId == sessionId));

    /// <inheritdoc />
    public Task<IReadOnlyList<SavedSessionInfo>> ListSavedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SavedSessionInfo>>([.. Saved]);

    /// <inheritdoc />
    public Task<SessionInfo> OpenAsync(string savedSessionId, SessionOpenOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        // 先记下理由再做任何判断:拒绝路径上的那句理由同样值得断言
        LastOpenReason = options.Reason;
        if (Saved.Find(s => s.SavedSessionId == savedSessionId) is not { } saved)
        {
            throw new PluginSessionNotFoundException(savedSessionId);
        }
        if (DenyOpen)
        {
            throw new PluginPermissionDeniedException($"The user refused to open '{saved.Name}'.");
        }
        if (OpenFailure is { Length: > 0 } failure)
        {
            throw new PluginSessionOpenException(savedSessionId, failure);
        }
        if (options.ReuseConnected
            && Sessions.Find(s => s.State == SessionState.Connected
                                  && s.Host == saved.Host
                                  && s.Port == saved.Port
                                  && s.Username == saved.Username) is { } existing)
        {
            return Task.FromResult(existing);
        }
        SessionInfo opened = AddConnected(saved.Host, saved.Port, saved.Username);
        OpenedByPlugin.Add(opened.SessionId);
        return Task.FromResult(opened);
    }

    /// <inheritdoc />
    public Task CloseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (Sessions.Find(s => s.SessionId == sessionId) is null)
        {
            return Task.CompletedTask; // 已经没了,幂等
        }
        if (!OpenedByPlugin.Contains(sessionId))
        {
            throw new PluginPermissionDeniedException(
                $"Session '{sessionId}' was not opened by this plugin, so it cannot close it.");
        }
        Sessions.RemoveAll(s => s.SessionId == sessionId);
        OpenedByPlugin.Remove(sessionId);
        return Task.CompletedTask;
    }
}
