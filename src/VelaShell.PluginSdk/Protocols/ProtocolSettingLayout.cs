namespace VelaShell.PluginSdk.Protocols;

/// <summary>
/// 字段在连接对话框里占一行的多少。宿主把相邻字段按声明顺序**流式**排进一行,放不下就换行。
/// <para>
/// 为什么要有它:一列到底的表单在字段多的连接类型上(MongoDB 的「副本集 / 默认库 / 读偏好」、
/// 「认证机制 / authSource」)把对话框拉得很长,而这些字段每一个都只需要很窄的一格。
/// 宽度只描述"占几分之几",不给像素 —— 对话框多宽、间距多少,仍然是宿主的事。
/// </para>
/// </summary>
public enum ProtocolFieldWidth
{
    /// <summary>独占一行(默认)。</summary>
    Full,

    /// <summary>半行:两个并排。</summary>
    Half,

    /// <summary>三分之一行:三个并排。</summary>
    Third
}

/// <summary>
/// 字段的画法。只改长相,**不改取值**:同一个字段换一种画法,落盘的值与
/// <see cref="ProtocolConnectRequest.Settings" /> 里收到的完全一样。宿主不认识的画法退回默认画法。
/// </summary>
public enum ProtocolSettingPresentation
{
    /// <summary>按 <see cref="ProtocolSettingField.Kind" /> 的默认控件画(文本框、下拉、复选框…)。</summary>
    Default,

    /// <summary>
    /// <see cref="ProtocolSettingKind.Choice" /> 画成分段按钮:几个候选一眼看全,点一下就切。
    /// 适合两到四个互斥形态(「主机列表 / SRV 记录 / 连接字符串」);候选多了宿主可以退回下拉。
    /// </summary>
    Segmented,

    /// <summary>
    /// <see cref="ProtocolSettingKind.Choice" /> 画成一排小标签,选中的那个按它的
    /// <see cref="ProtocolSettingChoice.Tone" /> 着色 —— 给"环境标记"这类带语气的取值用
    /// (生产 = 危险色,一眼就能看出这条连接要小心)。
    /// </summary>
    Chips,

    /// <summary>
    /// 一张带开关的卡片:标题 + 右侧开关 + 下方说明(<see cref="ProtocolSettingField.Hint" />)。
    /// 适用于 <see cref="ProtocolSettingKind.Boolean" />(开关就是取值),以及
    /// <see cref="ProtocolSettingKind.SshSession" />(开关表示"经不经跳板",开着时卡片里多一个会话下拉)。
    /// 两张卡片并排配 <see cref="ProtocolFieldWidth.Half" />。
    /// </summary>
    Card
}

/// <summary>字段放在对话框的哪一块。</summary>
public enum ProtocolFieldPlacement
{
    /// <summary>主表单(默认)。</summary>
    Form,

    /// <summary>
    /// 右侧栏:与连接检查(<see cref="Workspaces.IWorkspaceConnectionInspector" />)的连接串预览、
    /// 测试结果放在一起。给"打开之后怎么用"的策略类开关用(以只读模式打开、写前确认)——
    /// 它们不决定连不连得上,放在表单里会和连接参数混在一起。
    /// 宿主不画右侧栏时这些字段回到主表单末尾,取值不受影响。
    /// </summary>
    Aside
}

/// <summary>
/// 语气色。插件只说"这是什么语气",具体颜色由宿主映射到自己的语义令牌,换主题时跟着走 ——
/// 插件因此不需要、也不应该知道任何一个颜色值。
/// </summary>
public enum ProtocolTone
{
    /// <summary>中性(默认)。</summary>
    Neutral,

    /// <summary>强调色。</summary>
    Accent,

    /// <summary>信息。</summary>
    Info,

    /// <summary>成功 / 正常。</summary>
    Success,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>危险。</summary>
    Danger
}

/// <summary>
/// <see cref="ProtocolSettingField.Section" /> 里几个由宿主认识的节:字段声明成这些节时,
/// 并进宿主自己的那一节,排在宿主自己的字段**之后**;其余任何字符串都是插件自己的一节(标题即该字符串)。
/// </summary>
public static class ProtocolSettingSection
{
    /// <summary>
    /// 基本信息(宿主的显示名称 / 会话分组 / 标签那一节)。给"这条连接是什么"的标注用,如环境标记。
    /// 有字段声明在这一节时,宿主把这一节挪到表单最上面。
    /// </summary>
    public const string Basic = "vela:basic";

    /// <summary>
    /// 连接目标(宿主的主机 / 端口那一节)。连接类型的 <c>VariantKey</c> 字段声明在这一节时排在主机那一行**之上**
    /// —— 它决定主机那一行长什么样;其余字段排在主机那一行之后。
    /// </summary>
    public const string Target = "vela:target";

    /// <summary>身份验证(宿主的用户名 / 口令那一节)。认证机制、认证库之类放这里。</summary>
    public const string Authentication = "vela:auth";
}
