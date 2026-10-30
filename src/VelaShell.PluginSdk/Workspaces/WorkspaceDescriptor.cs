using VelaShell.PluginSdk.Protocols;

namespace VelaShell.PluginSdk.Workspaces;

/// <summary>工作台连接类型的能力位。宿主据此决定要不要弹登录框、走证书信任流程、渲染隧道一节。</summary>
[Flags]
public enum WorkspaceFeatures
{
    /// <summary>无额外能力。</summary>
    None = 0,

    /// <summary>允许不填凭据直接连接;宿主据此决定要不要弹登录框。</summary>
    AnonymousAccess = 1 << 0,

    /// <summary>
    /// 端点为 TLS,可能使用自签证书:宿主据此在
    /// <see cref="ProtocolCertificateTrustException" /> 时走"提示 → 记指纹 → 重连"流程
    /// (与 FTPS / S3 共用同一个对话框与同一组文案)。
    /// </summary>
    CertificateTrust = 1 << 1,

    /// <summary>
    /// 端点可能藏在内网,支持经 SSH 隧道抵达。宿主据此在连接对话框里追加"经 SSH 隧道"一节
    /// (跳板会话下拉 + 目标地址),并在打开会话前**代为建好本地转发** ——
    /// 插件收到的 <see cref="WorkspaceConnectRequest.Host" /> 已是本地端点,
    /// 它因此一行 SSH 代码都不用写、一次凭据都不用见。
    /// </summary>
    SshTunnel = 1 << 2,

    /// <summary>
    /// 这个连接类型**没有协议级凭据**:宿主在连接配置页里收起用户名与口令两栏。
    /// <para>
    /// 给"凭据不在协议这一层"的形态用 —— 典型是本地文件型数据库(SQLite / DuckDB):
    /// 它就是一个文件,填了用户名也没有任何地方会用到。摆着两个填了不起作用的框,
    /// 只会让用户以为填上就能连。
    /// </para>
    /// <para>
    /// 与 <see cref="Protocols.ProtocolFeatures.NoCredentials" /> 同义;
    /// 同样隐含 <see cref="AnonymousAccess" /> 的判定 —— 没有凭据这回事,
    /// 自然不能拿"用户名没填"把连接按钮堵死。
    /// </para>
    /// </summary>
    NoCredentials = 1 << 3,

    /// <summary>
    /// 这个连接类型**没有网络端点**:宿主在连接配置页里收起"端口"那一栏。
    /// <para>
    /// 给本地文件型形态用 —— 典型是 SQLite / DuckDB:它就是磁盘上的一个文件,
    /// 端口填什么都不会被拼进连接串。摆着一栏还留着上一个变体残值(比如 PostgreSQL 的 55432)的端口框,
    /// 只会让用户以为它有意义。
    /// </para>
    /// <para>
    /// <b>只收端口,不收主机</b>:文件型方言恰恰要靠"主机"那一栏装文件路径
    /// (配上 <see cref="WorkspaceVariant.HostLabel" /> 改标成"数据库文件")。
    /// 两栏一起收的话,用户就没有地方填文件了。
    /// </para>
    /// <para>
    /// 这一位不影响端口的**取值**:连接类型描述符仍必须给出 1–65535 的
    /// <see cref="WorkspaceDescriptor.DefaultPort" />,保存/连接按钮的"端口在合法区间"那条判定
    /// 也照旧成立 —— 收起一栏不该顺手把按钮堵死。
    /// </para>
    /// </summary>
    NoEndpoint = 1 << 4
}

/// <summary>
/// 连接类型的一种**变体**:同一个页签下,按某个设置字段的取值切换连接框的形态。
/// <para>
/// <b>它解决的是一类真问题</b>:一个插件想用**一个页签**承载一族相近的连接类型
/// (数据库插件的五种方言、消息队列的几种协议),但端口、"主机"那一栏的含义、
/// 要不要凭据,这些**是随着具体那一种变的**,而它们又都长在描述符上、
/// 一个页签只有一份。没有变体的话,用户选了 PostgreSQL,端口框里还写着 MySQL 的 3306。
/// </para>
/// <para>
/// <b>只覆盖需要覆盖的</b>:每一项都是可空的,<see langword="null" /> 表示沿用
/// <see cref="WorkspaceDescriptor" /> 上的值。字段的显隐不在这里 ——
/// 那是 <see cref="ProtocolSettingField.VisibleWhen" /> 的事,两者用的是同一个键。
/// </para>
/// </summary>
public sealed record WorkspaceVariant
{
    /// <summary>
    /// 触发本变体的取值,与 <see cref="WorkspaceDescriptor.VariantKey" /> 指向的那个字段比较
    /// (序数比较,大小写敏感)。
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// 类型选择卡片上的图标;<see langword="null" /> 回落到 <see cref="WorkspaceDescriptor.Icon" />。
    /// <para>
    /// 卡片与会话标签页共用 <see cref="PluginIcon" /> 这一种形态。卡片上的**名字**不在变体上重复声明:
    /// 宿主取 <see cref="WorkspaceDescriptor.VariantKey" /> 那个字段的
    /// <see cref="ProtocolSettingField.Choices" /> 里 <c>Value</c> 与 <see cref="Value" /> 相同那一项的 <c>Label</c>
    /// —— 同一个名字只写一处,改文案不会两边分家。
    /// </para>
    /// <para>
    /// 出不出卡片不是 SDK 开关,条件由宿主决定(现行:<see cref="WorkspaceDescriptor.VariantKey" /> 指向的字段是
    /// <see cref="ProtocolSettingKind.Choice" />、不隐藏(<see cref="ProtocolSettingField.IsHidden" /> 为
    /// <see langword="false" />)、不可手输(<see cref="ProtocolSettingField.AllowsCustomValue" /> 为
    /// <see langword="false" />)、至少两个候选项)。
    /// </para>
    /// <para>可用版本:2.1.5。</para>
    /// </summary>
    public PluginIcon? Icon { get; init; }

    /// <summary>
    /// 这一变体的默认端口。
    /// <para>
    /// 宿主沿用与切换页签**同一套**判定:只有在用户没手填过端口时才跟随,
    /// 已经改成 13306 的不会被切回 3306。
    /// </para>
    /// </summary>
    public int? DefaultPort { get; init; }

    /// <summary>"主机"输入框的标签改写(例如 SQLite 上改成"数据库文件")。</summary>
    public string? HostLabel { get; init; }

    /// <summary>"主机"输入框的占位提示改写。</summary>
    public string? HostPlaceholder { get; init; }

    /// <summary>"用户名"输入框的标签改写。</summary>
    public string? UsernameLabel { get; init; }

    /// <summary>"密码"输入框的标签改写。</summary>
    public string? PasswordLabel { get; init; }

    /// <summary>
    /// 这一变体的能力位;<see langword="null" /> 表示沿用描述符上的。
    /// <para><b>是整体替换而不是按位合并</b> —— 合并的话就没法表达"这一种反而不需要凭据"。</para>
    /// </summary>
    public WorkspaceFeatures? Features { get; init; }
}

/// <summary>
/// 一种由插件**全权渲染**的会话文档类型(Redis、MySQL、Kafka…)。
/// 注册后它在连接配置页里与 SSH/SFTP/FTP 平起平坐:用户建的会话由宿主路由到插件的
/// <see cref="IWorkspaceProvider" />,而连接对话框、凭据加密落盘、登录弹窗、云同步、
/// 会话树与最近连接**全部零改动复用**。
/// <para>
/// 与 <see cref="ProtocolDescriptor" /> 的分工:协议类型长得像文件系统(宿主打开双栏浏览器),
/// 工作台类型不是(宿主向插件索取一个 Avalonia 控件挂成停靠文档)。
/// 二者共用同一套**声明式表单**(<see cref="ProtocolSettingField" />)与同一族连接异常。
/// </para>
/// </summary>
public sealed record WorkspaceDescriptor
{
    /// <summary>
    /// 连接类型 id:必须等于插件 id,或以 <c>&lt;插件id&gt;.</c> 为前缀(宿主强制,防插件间冒名)。
    /// 该 id 会落进用户的会话配置,**发布后不可更改**,否则老配置认不出自己的连接类型。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>连接配置页上的页签名称(如 <c>Redis</c>)。</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 会话标签页上的图标;留空则宿主画一个通用的插件图标。
    /// 形态与取舍见 <see cref="PluginIcon" />(标签页图标三处共用同一个类型)。
    /// <para>可用版本:2.0.4。</para>
    /// </summary>
    public PluginIcon? Icon { get; init; }

    /// <summary>新建配置时的默认端口(必填,1–65535)。</summary>
    public int DefaultPort { get; init; }

    /// <summary>"主机"输入框的标签;留空即用宿主的"主机名/IP"。</summary>
    public string? HostLabel { get; init; }

    /// <summary>"主机"输入框的占位提示。</summary>
    public string? HostPlaceholder { get; init; }

    /// <summary>"用户名"输入框的标签;留空即用宿主的"用户名"。</summary>
    public string? UsernameLabel { get; init; }

    /// <summary>"密码"输入框的标签;留空即用宿主的"密码"。</summary>
    public string? PasswordLabel { get; init; }

    /// <summary>专属设置的表单声明(按顺序渲染);形态与语义见 <see cref="ProtocolSettingField" />。</summary>
    public IReadOnlyList<ProtocolSettingField> Fields { get; init; } = [];

    /// <summary>
    /// 连接对话框里介于「常规」与「高级」之间的自定义分页,按声明顺序渲染;字段用
    /// <see cref="ProtocolSettingField.Page" /> 引用其中一页的键。
    /// <para>
    /// 顺序即页签顺序。「常规」「高级」两页的文案由宿主出,不在此列表;
    /// <see cref="ProtocolSettingPage.Label" /> 由插件自行本地化(与字段的 <c>Label</c>、<c>Choices</c>
    /// 同一套做法:locale 变化时插件重新注册描述符)。
    /// </para>
    /// <para>
    /// <b>为什么不是枚举</b>:「数据库」「SSL」是领域页,宿主无法为任意插件预备一套封闭集合的本地化文案;
    /// 做成枚举的话,每加一页都要发一版 SDK。<b>为什么是键不是文案</b>:显隐、落盘、测试都按键比较,
    /// 文案换语言不影响。
    /// </para>
    /// <para>可用版本:2.1.5。</para>
    /// </summary>
    public IReadOnlyList<ProtocolSettingPage> Pages { get; init; } = [];

    /// <summary>能力位。</summary>
    public WorkspaceFeatures Features { get; init; } = WorkspaceFeatures.None;

    /// <summary>
    /// 用户确认信任服务器证书后,指纹写回的字段键(须是 <see cref="Fields" /> 里
    /// 一个 <see cref="ProtocolSettingField.IsHidden" /> 的字段)。仅在声明了
    /// <see cref="WorkspaceFeatures.CertificateTrust" /> 时有意义。
    /// </summary>
    public string? TrustedThumbprintSettingKey { get; init; }

    /// <summary>
    /// 由哪个设置字段选择<see cref="Variants">变体</see>;<see langword="null" /> 表示没有变体。
    /// <para>
    /// 通常就是表单第一栏那个"类型"下拉。它一般也是各字段
    /// <see cref="ProtocolSettingField.VisibleWhen" /> 依赖的那个键 ——
    /// 一个键同时管住"表单长什么样"与"连接框长什么样"。
    /// </para>
    /// </summary>
    public string? VariantKey { get; init; }

    /// <summary>
    /// 变体表。<see cref="VariantKey" /> 指向的字段取到哪个值,就套用哪一条;
    /// 一个都对不上时沿用描述符自身的值。
    /// </summary>
    public IReadOnlyList<WorkspaceVariant> Variants { get; init; } = [];

    /// <summary>
    /// 按当前表单取值挑出适用的变体;没有变体或对不上时返回 <see langword="null" />。
    /// </summary>
    /// <param name="lookup">按键取当前值;键不存在时返回 <see langword="null" />。</param>
    /// <returns>适用的变体。</returns>
    public WorkspaceVariant? ResolveVariant(Func<string, string?> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        if (VariantKey is not { Length: > 0 } key || Variants.Count == 0)
        {
            return null;
        }
        string current = lookup(key) ?? string.Empty;
        for (int i = 0; i < Variants.Count; i++)
        {
            if (string.Equals(Variants[i].Value, current, StringComparison.Ordinal))
            {
                return Variants[i];
            }
        }
        return null;
    }
}
