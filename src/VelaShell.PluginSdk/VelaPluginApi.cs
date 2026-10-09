namespace VelaShell.PluginSdk;

/// <summary>
/// 插件 API 的版本常量。apiLevel 是整数代际:宿主对同一 apiLevel 承诺只增不改不删
/// (接口方法、DTO 字段、清单 schema);破坏性变更才会提升 apiLevel。
/// 插件在 <c>plugin.json</c> 的 <c>apiLevel</c> 字段声明其编译目标代际,
/// 宿主拒绝加载高于自身代际的插件。
/// </summary>
public static class VelaPluginApi
{
    /// <summary>
    /// 当前 SDK 的 apiLevel 代际。
    /// <para>
    /// 纪律是「SDK 主版本 == apiLevel」,由 <c>scripts/Set-Version.ps1</c> 在发版前硬核对。
    /// 代际是插件与宿主之间的**装载闸**:宿主拒载 <c>apiLevel</c> 高于自身的插件,
    /// 在**发现期**给出可读原因,而不是等装载时抛一个看不懂的程序集绑定异常。
    /// </para>
    /// <para>
    /// <b>2</b>(SDK 2.0):见 <see cref="SdkVersion" /> 的 2.0 一段。
    /// </para>
    /// </summary>
    public const int Level = 2;

    /// <summary>
    /// 当前 SDK 的语义版本(<c>主.次.修订</c>)。
    /// </summary>
    public const string SdkVersion = "2.0.9";
}
