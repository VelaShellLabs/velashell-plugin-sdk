namespace VelaShell.PluginSdk.Protocols;

/// <summary>
/// 由协议实现(<see cref="IProtocolTerminal" /> 或 <see cref="IProtocolFileSystem" />)**可选**兼实现的接口:
/// 为 <see cref="ProtocolSettingKind.DynamicChoice" /> 字段现场提供候选项。
/// <para>
/// 存在的理由:<see cref="ProtocolSettingField.Choices" /> 在 <c>Register</c> 那一刻就定死了,
/// 而插件的注册只发生一次(惰性激活)。对候选项会变的字段,那份快照从第二次打开对话框起就是错的。
/// 串口是最直白的例子 —— USB 转串口适配器是热插拔设备,用户很可能是**先打开连接对话框、
/// 才想起去插线**的。
/// </para>
/// <para>
/// 调用时机:宿主渲染连接表单时对每个 <see cref="ProtocolSettingKind.DynamicChoice" /> 字段调一次,
/// 用户按下下拉旁的刷新按钮时再调一次。**在后台线程调用**,不要碰界面。
/// </para>
/// <para>
/// "主机"那一栏(<see cref="ProtocolDescriptor.HostKind" /> 取
/// <see cref="ProtocolSettingKind.DynamicChoice" /> 时)走同一条路,字段键是
/// <see cref="ProtocolDescriptor.HostFieldKey" />。
/// </para>
/// <para>
/// 纪律:这是画界面路径上的一次同步等待,必须**快**且**不抛**。
/// 枚举失败请返回空表(宿主退回 <see cref="ProtocolSettingField.Choices" /> 那份静态兜底列表),
/// 别把一次列不出设备变成一个连表单都打不开的错误 —— 用户此时还没按连接,
/// 而手输一个端口名本来就该是允许的(见 <see cref="ProtocolSettingField.AllowsCustomValue" />)。
/// 抛出的异常宿主会吞掉并记进插件日志,按空表处理。
/// </para>
/// <example>
/// <code>
/// internal sealed class SerialTerminal : IProtocolTerminal, IProtocolChoiceSource
/// {
///     public Task&lt;IReadOnlyList&lt;ProtocolSettingChoice&gt;&gt; GetChoicesAsync(
///         string fieldKey, CancellationToken cancellationToken = default) =>
///         Task.FromResult&lt;IReadOnlyList&lt;ProtocolSettingChoice&gt;&gt;(
///             fieldKey == ProtocolDescriptor.HostFieldKey ? EnumeratePorts() : []);
/// }
/// </code>
/// </example>
/// </summary>
public interface IProtocolChoiceSource
{
    /// <summary>
    /// 取某个动态下拉字段当前的候选项。
    /// </summary>
    /// <param name="fieldKey">字段键(<see cref="ProtocolSettingField.Key" />);
    /// 不认识的键返回空表 —— 宿主对每个动态字段都会问一遍,包括将来新增的那些。</param>
    /// <param name="cancellationToken">取消令牌(用户已经切走了页签)。</param>
    /// <returns>候选项;空表示"这次没列出任何东西",宿主退回静态兜底列表。</returns>
    Task<IReadOnlyList<ProtocolSettingChoice>> GetChoicesAsync(
        string fieldKey,
        CancellationToken cancellationToken = default);
}
