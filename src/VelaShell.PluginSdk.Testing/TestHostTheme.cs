using VelaShell.PluginSdk.Theming;

namespace VelaShell.PluginSdk.Testing;

/// <summary>
/// <see cref="IHostThemeApi" /> 的测试替身:用 <see cref="Set" /> 换一套主题(身份 + 配色),
/// 它会像宿主那样先整体换掉快照、再触发 <see cref="Changed" />。
/// </summary>
public sealed class TestHostTheme : IHostThemeApi
{
    /// <summary>默认身份:VelaDark。</summary>
    public static HostThemeInfo DefaultInfo { get; } = new("dark", "VelaDark", true, false, "#BD93F9");

    private HostThemeInfo _current = DefaultInfo;
    private IReadOnlyDictionary<string, string> _colors =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VelaAccent"] = "#FFBD93F9",
            ["VelaBgTerminal"] = "#FF282A36",
            ["VelaTextPrimary"] = "#FFF8F8F2",
        };

    /// <inheritdoc />
    public HostThemeInfo Current => _current;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Colors => _colors;

    /// <inheritdoc />
    public event Action<HostThemeInfo>? Changed;

    /// <inheritdoc />
    public string? GetColor(string token) =>
        token is not null && _colors.TryGetValue(token, out string? value) ? value : null;

    /// <summary>
    /// 换一套主题:先整体替换身份与配色快照,再触发 <see cref="Changed" /> ——
    /// 与宿主的次序一致,处理器里读到的一定是新值。
    /// </summary>
    /// <param name="info">新的主题身份。</param>
    /// <param name="colors">新的配色快照;为 null 时保留原快照(只换身份)。</param>
    public void Set(HostThemeInfo info, IReadOnlyDictionary<string, string>? colors = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        _current = info;
        if (colors is not null)
        {
            _colors = new Dictionary<string, string>(colors, StringComparer.Ordinal);
        }
        Changed?.Invoke(info);
    }

    /// <summary>只改一个颜色令牌并触发 <see cref="Changed" />(模拟用户改强调色那一类变化)。</summary>
    public void SetColor(string token, string argb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var next = new Dictionary<string, string>(_colors, StringComparer.Ordinal) { [token] = argb };
        _colors = next;
        Changed?.Invoke(_current);
    }
}
