namespace VelaShell.PluginSdk.Secrets;

/// <summary>
/// 机密存储能力:插件私有的加密键值(API token、口令等)。
/// 与 <see cref="Storage.IPluginStorage" /> 的区别:值**加密落盘**(宿主的机密保护器,
/// Windows 上为 DPAPI 包裹的本地密钥),且隔离模式下机密只存在宿主侧、
/// 不落插件进程的数据目录。命名空间按插件隔离,互不可见。
/// 适合少量短字符串;不要塞大 payload。
/// <para>
/// <b>主密码</b>:用户可以给 VelaShell 设主密码。设了而此刻没解锁时,宿主读不出已存的值;
/// 无论宿主新旧,都<b>不会</b>因此对一个已存的机密返回 <see langword="null" />,
/// <see cref="GetAsync" /> 返回 <see langword="null" /> 只表示它真的不存在。锁着时具体怎么失败见
/// <see cref="SupportsLockedState" />。
/// </para>
/// </summary>
public interface ISecretsApi
{
    /// <summary>
    /// 宿主是否在机密存储锁着时明确抛 <see cref="SecretsLockedException" />;旧宿主默认 <see langword="false" />。
    /// <list type="bullet">
    ///   <item><see langword="true" />:用户设了主密码而此刻没解锁时,<see cref="GetAsync" /> /
    ///   <see cref="SetAsync" /> 不弹解锁框、直接抛 <see cref="SecretsLockedException" />;
    ///   <see cref="DeleteAsync" /> 照常可用。</item>
    ///   <item><see langword="false" />:旧宿主,不会抛这个异常。锁着时它可能等待用户交互式解锁
    ///   (用户拒绝时可能抛 <see cref="PluginPermissionDeniedException" />),别的语义不作保证 ——
    ///   但同样不会对一个已存的机密返回 <see langword="null" />。</item>
    /// </list>
    /// <para>可用版本:TBD(发版时替换)。</para>
    /// </summary>
    bool SupportsLockedState => false;

    /// <summary>读取机密;不存在返回 <see langword="null" />。</summary>
    /// <remarks>
    /// 机密存储锁着时<b>不</b>返回 <see langword="null" /> —— 那会让插件以为机密不存在、让用户重填,
    /// 解锁后旧值就被覆盖了。见 <see cref="SupportsLockedState" />。
    /// </remarks>
    /// <exception cref="SecretsLockedException">
    /// 宿主宣告了 <see cref="SupportsLockedState" />,而机密存储此刻锁着(用户设了主密码、还没解锁)。
    /// </exception>
    Task<string?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>写入机密(加密后持久化)。</summary>
    /// <exception cref="SecretsLockedException">
    /// 宿主宣告了 <see cref="SupportsLockedState" />,而机密存储此刻锁着;什么都没写。
    /// </exception>
    Task SetAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除机密;返回此前是否存在。不需要解密:宣告了 <see cref="SupportsLockedState" /> 的宿主
    /// 在机密存储锁着时照常删除,不抛 <see cref="SecretsLockedException" />。
    /// </summary>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
