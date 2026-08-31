using VelaShell.PluginSdk.Events;

namespace VelaShell.PluginSdk;

/// <summary>宿主环境信息。<see cref="Locale" /> 与 <see cref="Theme" /> 为实时值,变更事件见 <see cref="IHostEvents" />。</summary>
public interface IHostInfo
{
    // 主题在这里只有粗粒度的一档(dark/light/system)。要主题身份、整套配色或可靠的
    // 换肤信号,用 IPluginContext.Theme(Theming.IHostThemeApi)—— 见 Theme 属性的说明。

    /// <summary>宿主应用版本(如 <c>0.0.1-dev</c>)。</summary>
    string AppVersion { get; }

    /// <summary>宿主支持的最高插件 apiLevel。</summary>
    int ApiLevel { get; }

    /// <summary>当前 UI 语言代码(如 <c>zh-CN</c>、<c>en</c>)。</summary>
    string Locale { get; }

    /// <summary>
    /// 当前主题的**明暗名**:<c>dark</c>、<c>light</c>,或 <c>system</c>(用户选了“跟随系统”)。
    /// <para>
    /// 只有这三个值 —— 宿主内置十来套具名主题(VelaDark、Tokyo Night、Nord、Sakura…),
    /// 它们在这里一律被收敛成自己的明暗名。因此:
    /// </para>
    /// <list type="bullet">
    ///   <item>本属性**认不出**是哪一套主题,也认不出强调色;</item>
    ///   <item>在 VelaDark 与 Tokyo Night 之间换肤时它**不会变化**(两套都是 dark);</item>
    ///   <item>为 <c>system</c> 时它**没有**告诉你此刻是明是暗。</item>
    /// </list>
    /// <para>
    /// 需要上面任何一条,用 <see cref="IPluginContext.Theme" />
    /// (<see cref="Theming.IHostThemeApi" />):那里的 <c>Current</c> 是已解析的主题身份,
    /// <c>Colors</c> 是整套已解析的 <c>Vela*</c> 配色,<c>Changed</c> 覆盖全部三种换肤情形。
    /// 本属性保留只为兼容,新代码不该依赖它做取色决策。
    /// </para>
    /// </summary>
    string Theme { get; }
}
