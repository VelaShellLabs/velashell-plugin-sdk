namespace VelaShell.PluginSdk;

/// <summary>
/// 已保存的会话存在、宿主也放行了,但连不上(网络不通、认证失败、主机指纹对不上……)。
/// </summary>
/// <remarks>
/// 与 <see cref="PluginPermissionDeniedException" /> 刻意分开:那一个是"不让你连",
/// 这一个是"让你连了,没连上"。前者重试没有意义(用户已经说了不),
/// 后者换个时间再来可能就好了 —— 插件对这两种情况的处置完全不同,
/// 塞进同一个异常类型里就只能靠读消息文本去猜。
/// <para>
/// <b>不要把失败原因原样转发给不受信的一方。</b>认证失败的细节、指纹不符的具体值,
/// 对排障有用,对一个能读到机器人回帖的群聊则是多余的暴露面。
/// </para>
/// </remarks>
public sealed class PluginSessionOpenException : InvalidOperationException
{
    /// <summary>用已保存会话 id 与原因构造。</summary>
    public PluginSessionOpenException(string savedSessionId, string message)
        : base(message)
        => SavedSessionId = savedSessionId;

    /// <summary>用已保存会话 id、原因与内层异常构造。</summary>
    public PluginSessionOpenException(string savedSessionId, string message, Exception innerException)
        : base(message, innerException)
        => SavedSessionId = savedSessionId;

    /// <summary>没能打开的那条已保存配置的 id;跨进程还原时可能为空串。</summary>
    public string SavedSessionId { get; }
}
