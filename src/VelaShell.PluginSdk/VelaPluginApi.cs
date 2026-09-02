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
    /// <para>
    /// apiLevel 只在**破坏性**变更时才动,所以它管不住"只增不改"的那一半:
    /// SDK 1.1 给 <c>ExecResult</c> 加了标准错误与退出码、给远程执行加了流式形态,
    /// apiLevel 仍然是 1。一个用了这些新面的插件装到只带 1.0 的老宿主上,清单校验会放行,
    /// 然后在**运行期**炸出一个 <see cref="MissingMethodException" /> —— 那正是 apiLevel
    /// 当初要消灭的那种"看不懂的绑定异常",只是它太粗,拦不住这一档。
    /// </para>
    /// <para>
    /// 所以清单上多了一个 <c>minSdkVersion</c>:用到新面的插件声明它,老宿主在**发现期**
    /// 就干净地标 Incompatible 并说清该升级什么。
    /// </para>
    /// <para>
    /// <b>1.2</b> 加了 <see cref="RemoteTunnel.IRemoteTunnelApi" />:到远端 unix socket /
    /// TCP 端点的**裸字节双工流**。远程执行的两种形态都是文本(整段 UTF-8 解码,或按
    /// <c>\n</c> 切行回调),而 Docker Engine API 的分块传输、tar 归档流与 exec 的多路复用帧
    /// 全是二进制 —— 用文本模型承载它们不是"慢一点",是数据静默损坏。同样只增不改,
    /// apiLevel 仍是 1,靠 <c>minSdkVersion: "1.2.0"</c> 在发现期拦住老宿主。
    /// </para>
    /// <para>
    /// <b>1.3</b> 加了 <see cref="TerminalView.ITerminalViewApi" />:把宿主那套 VT 解析、
    /// 屏幕缓冲、选区、IME 与键盘编码整体出借给插件,插件拿到一个可嵌进自己界面的终端控件。
    /// 在这之前,插件想要一个真终端只有两条路 —— 自己再写一个仿真器(ANSI 不是"处理一下
    /// 转义序列"那么回事),或者退化成"一条命令一份输出"的行式控制台(<c>top</c>、<c>vim</c>、
    /// <c>less</c> 一概不能用)。同样只增不改,apiLevel 仍是 1,
    /// 靠 <c>minSdkVersion: "1.3.0"</c> 在发现期拦住老宿主。
    /// </para>
    /// <para>
    /// <b>1.3.1</b> 给工作区描述符加了**变体**:<see cref="Workspaces.WorkspaceVariant" />
    /// 与 <see cref="Workspaces.WorkspaceDescriptor.VariantKey" /> /
    /// <see cref="Workspaces.WorkspaceDescriptor.Variants" />,让同一个插件按某个设置字段
    /// 切换连接框形态(各变体自带默认端口、字段标签与能力位);配套的
    /// <see cref="Workspaces.WorkspaceFeatures.NoCredentials" /> 与
    /// <see cref="Workspaces.WorkspaceFeatures.NoEndpoint" /> 用于本地文件型数据库那种
    /// 既没有主机端口也没有账号密码的形态 —— 在这之前它们只能把无关字段留在界面上晾着。
    /// 仍是只增不改,apiLevel 仍是 1,靠 <c>minSdkVersion: "1.3.1"</c> 在发现期拦住老宿主。
    /// </para>
    /// <para>
    /// <b>1.4</b> 加了 <see cref="Hosting.HostRegistry" />:宿主每次启动把安装路径与版本写进
    /// <c>~/.velashell/host.json</c>,<c>vela-plugin</c> 据此生成 IDE 启动配置、核对兼容性。
    /// 这一档是**工具链**面而不是插件运行时面 —— 插件代码不会调用它,因此**不需要**为它声明
    /// <c>minSdkVersion: "1.4.0"</c>(声明了反而会把插件挡在老宿主之外,而它在老宿主上跑得好好的)。
    /// </para>
    /// <para>
    /// <b>2.0</b> —— 本系列**第一次**动 <see cref="Level" />(1 → 2)。
    /// 上面 1.1 ~ 1.5 那几档都是"只增不改",这一档不是:主版本跳变把
    /// <c>AssemblyVersion</c> 从 <c>1.0.0.0</c> 带到 <c>2.0.0.0</c>
    /// (见 <c>src/Directory.Build.props</c> 的 <c>$(VelaSdkMajor).0.0.0</c>),
    /// 于是**已编译的插件必须重新编译**才能被装载 —— 这就是代际的含义,
    /// 也正是 <c>apiLevel</c> 存在的理由:老宿主在发现期按代际干净拒载,
    /// 而不是等到装载时抛一个"找不到 VelaShell.PluginSdk 2.0.0.0"的绑定异常。
    /// </para>
    /// <para>
    /// 插件作者要做的两件事:① 用 2.x 的 SDK 重新编译;
    /// ② 把 <c>plugin.json</c> 的 <c>apiLevel</c> 改成 <c>2</c> —— 不改也能在 2.x 宿主上跑
    /// (宿主接受不高于自身的代际),但**装到 1.x 宿主上时**你要的是发现期那句
    /// "需要更新 VelaShell",而不是一个程序集绑定异常。
    /// </para>
    /// <para>
    /// 内容上这一档带的是 <see cref="Theming.IHostThemeApi" />(<c>IPluginContext.Theme</c>):
    /// 主题身份 + 整套已解析的 <c>Vela*</c> 配色 + 覆盖全部换肤情形的变更信号。
    /// 在这之前插件能看到的主题信息只有 <see cref="IHostInfo.Theme" /> 那三个值
    /// (<c>dark</c>/<c>light</c>/<c>system</c>)—— 宿主后来长出十来套具名主题,
    /// 它们全被收敛成自己的明暗名,于是插件既认不出是哪一套、也认不出强调色,
    /// 而且在 VelaDark 与 Tokyo Night 之间换肤时那个值**根本不变**:
    /// 任何按它判断"要不要重取颜色"的代码都会整整漏掉一次换肤。
    /// </para>
    /// <para>
    /// 顺带一处源码级不兼容:<see cref="IPluginContext" /> 多了 <c>Theme</c> 成员。
    /// 插件是**消费**这个接口的,不受影响;自己写了 <c>IPluginContext</c> 实现的
    /// (多半是测试替身)要补上这个成员 —— 或者改用
    /// <c>VelaShell.PluginSdk.Testing</c> 的 <c>TestPluginContext</c>,它已经带好了。
    /// </para>
    /// <para>
    /// <b>TBD</b>(发版时替换为实际版本号)给会话能力加了三个方法:
    /// <see cref="Sessions.ISessionsApi.ListSavedAsync" />(已保存的连接配置,含此刻没连着的)、
    /// <see cref="Sessions.ISessionsApi.OpenAsync" />(请求宿主打开其中一条)与
    /// <see cref="Sessions.ISessionsApi.CloseAsync" />(只能关自己打开的那些)。
    /// 在这之前插件只能操作用户<b>已经手动连上</b>的机器,于是所有"无人值守"的用法都塌了半边:
    /// IM 桥接里同事在群里问一句"生产机磁盘满了吗",而值班的人昨晚关掉了那个标签页,
    /// 机器人只能回"你先去连一台";外部 agent 经 MCP 调进来时更是如此 —— 它根本不在这台机器前面。
    /// </para>
    /// <para>
    /// 权限扩张的闸门写在契约里,而不是留给实现去自觉:插件<b>只能打开已保存的配置</b>
    /// (连哪些机器由用户先在宿主里定下来)、凭据一个字节都不经过插件、宿主可以拒绝且
    /// 拒绝是契约的一部分(<see cref="PluginPermissionDeniedException" />)。
    /// <see cref="Sessions.SessionOpenOptions.Reason" /> 是给那个确认框用的,会原样显示给用户。
    /// 连不上单独给了 <see cref="PluginSessionOpenException" /> —— "不让你连"与"没连上"
    /// 对插件而言处置完全不同,塞进同一个类型里就只能靠读消息文本去猜。
    /// </para>
    /// <para>
    /// 仍是只增不改,<see cref="Level" /> 不动;用到这一档的插件靠
    /// <c>minSdkVersion: "TBD"</c> 在发现期拦住老宿主。
    /// 自己写了 <c>ISessionsApi</c> 实现的(多半是测试替身)要补上这三个成员 ——
    /// <c>VelaShell.PluginSdk.Testing</c> 的 <c>FakeSessions</c> 已经带好了,
    /// 还附带 <c>DenyOpen</c> / <c>OpenFailure</c> / <c>LastOpenReason</c> 三个钩子,
    /// 把"用户点了不"与"连不上"这两条路也能测到。
    /// </para>
    /// </summary>
    public const string SdkVersion = "2.0.2";
}
