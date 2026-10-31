using VelaShell.PluginSdk.Clipboard;
using VelaShell.PluginSdk.Secrets;

namespace VelaShell.PluginSdk.Testing;

/// <summary>
/// <see cref="ISecretsApi" /> 的内存替身(不加密——测试无此必要)。
/// 置 <see cref="IsLocked" /> 模拟用户设了主密码而此刻没解锁。
/// </summary>
public sealed class FakeSecrets : ISecretsApi
{
    /// <summary>当前机密表;测试可直接预置/断言。锁着时它原样保留,读写都碰不到它。</summary>
    public Dictionary<string, string> Values { get; } = [with(StringComparer.Ordinal)];

    /// <summary>
    /// 是否模拟一个宣告了锁定态的宿主(锁着时抛 <see cref="SecretsLockedException" />),默认是。
    /// 置 false 模拟旧宿主:锁着时不抛这个异常,而按「用户拒绝了解锁提示」抛
    /// <see cref="PluginPermissionDeniedException" />。可用版本:TBD(发版时替换)。
    /// </summary>
    public bool SupportsLockedState { get; set; } = true;

    /// <summary>
    /// 是否模拟机密存储锁着(用户设了主密码、此刻没解锁),默认否。锁着时 <see cref="GetAsync" /> /
    /// <see cref="SetAsync" /> 抛 <see cref="SecretsLockedException" />(<see cref="SupportsLockedState" /> 为 false 时
    /// 改抛 <see cref="PluginPermissionDeniedException" />),<b>绝不</b>对已存的机密返回 null,也不写入;
    /// <see cref="DeleteAsync" /> 照常。置回 false 即解锁,原来的值都还在。可用版本:TBD(发版时替换)。
    /// </summary>
    public bool IsLocked { get; set; }

    /// <inheritdoc />
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
        => IsLocked
            ? Task.FromException<string?>(LockedError())
            : Task.FromResult(Values.GetValueOrDefault(name));

    /// <inheritdoc />
    public Task SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        if (IsLocked)
        {
            return Task.FromException(LockedError());
        }
        Values[name] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>不需要解密:<see cref="IsLocked" /> 时照常删除。</remarks>
    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(Values.Remove(name));

    private Exception LockedError() => SupportsLockedState
        ? new SecretsLockedException("FakeSecrets is simulating a locked secret store (master password not unlocked).")
        : new PluginPermissionDeniedException("FakeSecrets is simulating a host without locked-state support where the user declined to unlock.");
}

/// <summary><see cref="IClipboardApi" /> 的内存替身。</summary>
public sealed class FakeClipboard : IClipboardApi
{
    /// <summary>当前剪贴板文本;测试可直接预置/断言。</summary>
    public string? Text { get; set; }

    /// <inheritdoc />
    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult(Text);

    /// <inheritdoc />
    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Text = text;
        return Task.CompletedTask;
    }
}
