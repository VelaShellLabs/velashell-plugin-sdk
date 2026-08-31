namespace VelaShell.PluginSdk.Theming;

/// <summary>
/// 当前**生效**的宿主主题。
/// <para>
/// 与 <see cref="IHostInfo.Theme" /> 的区别是它已经解析过了:宿主的主题设置里有一个
/// “跟随系统”的伪值,<c>IHostInfo.Theme</c> 会把那个字面量原样交出来(插件因此不知道
/// 此刻究竟是明是暗),而这里的 <see cref="Id" /> / <see cref="IsDark" /> 永远是落地之后
/// 的那一套;想知道用户选的是不是“跟随系统”,看 <see cref="FollowsSystem" />。
/// </para>
/// </summary>
/// <param name="Id">
/// 主题 id(<c>dark</c>、<c>light</c>、<c>tokyo-night</c>、<c>sakura</c>…)。
/// 已解析,**永远不是** <c>system</c>。
/// </param>
/// <param name="Name">显示名(<c>Tokyo Night</c>、<c>VelaDark</c>…)。品牌名,不本地化。</param>
/// <param name="IsDark">明暗基底。与宿主的 Avalonia <c>ThemeVariant</c> 一致。</param>
/// <param name="FollowsSystem">用户选的是“跟随系统”(此时 <see cref="Id" /> 是系统当前明暗对应的那套默认主题)。</param>
/// <param name="Accent">当前生效的强调色 <c>#RRGGBB</c>,**含**用户在设置里的自定义覆盖(与 <c>VelaAccent</c> 令牌同值)。</param>
public sealed record HostThemeInfo(
    string Id,
    string Name,
    bool IsDark,
    bool FollowsSystem,
    string Accent)
{
    /// <summary>明暗名(<c>dark</c> / <c>light</c>)。与 <see cref="IHostInfo.Theme" /> 的取值域一致,但永不为 <c>system</c>。</summary>
    public string Variant => IsDark ? "dark" : "light";
}

/// <summary>
/// 主题能力:主题身份 + 整套已解析的 <c>Vela*</c> 配色 + “生效配色变了”的信号。
/// <para>
/// <b>什么时候用它、什么时候不用</b>:插件的 Avalonia 界面取色应当一律走
/// <c>{DynamicResource VelaXxx}</c> —— 那是自动跟随的,进程内与隔离模式都一样,不需要本接口。
/// 本接口是给 <b>DynamicResource 到不了的地方</b>准备的:
/// </para>
/// <list type="bullet">
///   <item>要的是 <c>Color</c> 而不是 <c>Brush</c>(语法高亮定义、自绘、导出图片);</item>
///   <item>在代码里一次性取值的地方(转换器、控件模板之外的计算),需要一个“该重取了”的信号;</item>
///   <item>要按主题身份而不是按颜色做决策(给某套主题换一张插画、换一组图标)。</item>
/// </list>
/// <para>
/// <b>关于 <see cref="Changed" /></b>:它覆盖**全部三种**会让界面配色改变的情况 ——
/// 换具名主题、“跟随系统”时系统明暗翻转、用户改强调色。
/// <see cref="Events.IHostEvents.ThemeChanged" /> 只覆盖第一种,而且它的参数只有
/// <c>dark</c>/<c>light</c>/<c>system</c> 三个值 —— 在 VelaDark 与 Tokyo Night 之间切换时
/// 那个参数**不会变**(两套都是 dark),按参数判断“变没变”的插件会漏掉整整一次换肤。
/// 要刷新颜色,认这个事件,别认那个参数。
/// </para>
/// </summary>
public interface IHostThemeApi
{
    /// <summary>当前生效的主题。每次 <see cref="Changed" /> 之前已更新。</summary>
    HostThemeInfo Current { get; }

    /// <summary>
    /// 当前主题下全部已解析的颜色令牌:键是令牌名(<c>VelaAccent</c>、<c>VelaBgTerminal</c>…),
    /// 值是 <c>#AARRGGBB</c>。快照语义 —— 每次 <see cref="Changed" /> 之前整体换新,
    /// 拿到的实例不会被就地改写。
    /// </summary>
    IReadOnlyDictionary<string, string> Colors { get; }

    /// <summary>取一个颜色令牌的 <c>#AARRGGBB</c>;令牌不存在时返回 <see langword="null" />。</summary>
    /// <param name="token">令牌名,如 <c>VelaAccent</c>。</param>
    string? GetColor(string token);

    /// <summary>
    /// 生效配色变化:换主题、“跟随系统”下系统明暗翻转、用户改强调色,三者都会触发。
    /// 触发时 <see cref="Current" /> 与 <see cref="Colors" /> 已经是新值。
    /// <para>
    /// 事件在非 UI 线程触发,处理器必须快速返回且不得抛出(异常由宿主捕获并记入插件日志)。
    /// 要动界面请自行封送到 UI 线程。插件停用时订阅由宿主自动拆除。
    /// </para>
    /// </summary>
    event Action<HostThemeInfo>? Changed;
}
