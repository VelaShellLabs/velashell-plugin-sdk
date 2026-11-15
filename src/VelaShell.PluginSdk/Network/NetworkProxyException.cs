namespace VelaShell.PluginSdk.Network;

/// <summary>
/// 经宿主代理建立连接失败:用户显式配置的代理不成立(缺主机或端口、凭据超出协议长度),
/// 或代理拒绝 / 握手 / 认证失败。<see cref="Exception.Message" /> 已由宿主本地化,可直接展示。
/// <para>
/// 与目标不可达区分开,插件可以据此提示用户去检查「设置 → 网络代理」而不是目标地址。
/// 宿主在这种情况下**不会**静默改为直连。可用版本:TBD(发版时替换)。
/// </para>
/// </summary>
public sealed class NetworkProxyException : IOException
{
    /// <summary>创建实例。</summary>
    public NetworkProxyException(string message) : base(message)
    {
    }

    /// <summary>创建实例,并保留内层异常。</summary>
    public NetworkProxyException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
