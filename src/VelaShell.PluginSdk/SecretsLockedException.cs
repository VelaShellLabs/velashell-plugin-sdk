using VelaShell.PluginSdk.Secrets;

namespace VelaShell.PluginSdk;

/// <summary>
/// 机密存储此刻<b>锁着</b>:用户给 VelaShell 设了主密码,而这会儿还没解锁 —— 宿主此刻既读不出已存的机密,
/// 也不接受写入。
/// <para>
/// 只有宣告了 <see cref="ISecretsApi.SupportsLockedState" /> 的宿主才抛它:锁着时
/// <see cref="ISecretsApi.GetAsync" /> / <see cref="ISecretsApi.SetAsync" /> <b>不弹解锁框</b>,直接抛本异常,
/// 什么都没读、什么都没写。<see cref="ISecretsApi.DeleteAsync" /> 不需要解密,锁着时照常可用,不抛它。
/// 没宣告的旧宿主不会抛它。
/// </para>
/// <para>
/// <b>插件不得把它当成「机密不存在」。</b>机密还在,只是此刻读不出来 —— 据此让用户重填、再
/// <see cref="ISecretsApi.SetAsync" /> 一个新值,解锁之后就是拿新值覆盖了用户原来存的那一个。
/// 正确的处置是提示用户先在 VelaShell 里解锁(状态栏的锁形入口),再重试这次操作。
/// 不要紧接着自动重试(用户没解锁,结果只会一样),也不要把要存的值改存到
/// <see cref="Storage.IPluginStorage" /> 之类不加密的地方去「先凑合」。
/// </para>
/// <para>
/// 与 <see cref="PluginPermissionDeniedException" /> 刻意分开:那一个是用户说了不,重试没有意义;
/// 这一个是用户还没解锁,解锁之后同一个请求就能成功。刻意派生自 <see cref="InvalidOperationException" />
/// (「状态不允许」),只认基类的旧 <c>catch</c> 照样接得住。
/// </para>
/// <para>隔离进程经错误码 <c>secrets-locked</c> 还原成本类型。可用版本:TBD(发版时替换)。</para>
/// </summary>
public sealed class SecretsLockedException : InvalidOperationException
{
    /// <summary>用说明构造。</summary>
    public SecretsLockedException(string message)
        : base(message)
    {
    }

    /// <summary>用说明与内层异常构造(内层异常不跨进程)。</summary>
    public SecretsLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
