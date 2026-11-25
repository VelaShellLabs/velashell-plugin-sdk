using System.IO.Compression;
using System.Security.Cryptography;
using VelaShell.PluginSdk.Manifest;
using VelaShell.PluginSdk.Packaging;

namespace VelaShell.PluginSdk.Packer;

/// <summary>
/// .vpx 打包器。<c>VelaShell.PluginSdk.Build</c> 把本工程的构建产物收进包的 <c>tools/</c>,
/// 它的 targets 用 <c>dotnet exec</c> 调这里的 <c>validate</c> 与 <c>pack</c>。
/// </summary>
/// <remarks>
/// 命令面**刻意只有三条**,而且只服务于一个调用方(那套 targets)——
/// 面向人的完整工具是 <c>vela-plugin</c>(VelaShell/velashell-plugin-cli),
/// 它有商店、开发内环、签名与体检。这里多加一条命令,就多一份要与那边对齐的东西。
///
/// 真正的逻辑一行都不在这里:容器格式在 <see cref="VpxContainer" />,清单规则在
/// <see cref="PluginManifestReader" />,两者都在 VelaShell.PluginSdk —— 也就是**同一个包**
/// 发给插件工程编译用的那份契约。"打包器与契约同版本"因此是构建出来的事实,不是约定。
/// </remarks>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                return Usage();
            }
            string[] rest = args[1..];
            return args[0] switch
            {
                "validate" => Validate(rest),
                "pack" => Pack(rest),
                "info" => Info(rest),
                "help" or "--help" or "-h" => Usage(),
                _ => throw new PackerException($"Unknown command '{args[0]}'. Try validate | pack | info.")
            };
        }
        catch (PackerException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        // 清单/容器两类异常带的是"哪一条规则没过",原样透出比包一层更有用。
        catch (Exception ex) when (ex is PluginManifestException or VpxFormatException
                                      or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Usage()
    {
        Console.WriteLine("""
            vela .vpx packer - invoked by the VelaShell.PluginSdk.Build MSBuild targets.

              validate <dir>                     check plugin.json and the entry assembly
              pack <dir> [options]               pack a plugin output directory into .vpx
                  -o, --output <dir|file>        destination (default: next to <dir>)
                      --no-mask                  do not mask the payload
                  -k, --key <key.pem>            sign with this ECDSA P-256 private key
              info <package.vpx>                 print the container header and manifest

            For everyday use install the full tool: dotnet tool install -g VelaShell.Plugin.Cli
            """);
        return 0;
    }

    /// <summary>校验清单与入口程序集(目录或 plugin.json 路径均可)。</summary>
    private static int Validate(string[] args)
    {
        Options options = Options.Parse(args);
        string target = Path.GetFullPath(options.Positional.FirstOrDefault() ?? ".");
        string directory = Directory.Exists(target) ? target : Path.GetDirectoryName(target)!;
        PluginManifest manifest = LoadManifest(directory);
        RequireEntry(directory, manifest);

        Console.WriteLine($"OK  {manifest.Id} v{manifest.Version} ({manifest.DisplayName})");
        Console.WriteLine($"    entry      {manifest.Entry}");
        Console.WriteLine($"    hostMode   {manifest.HostMode}");
        Console.WriteLine($"    apiLevel   {manifest.ApiLevel} (this SDK: {VelaPluginApi.Level})");
        Console.WriteLine($"    author     {manifest.Author ?? manifest.Publisher ?? "(not set)"}");
        if (manifest.ApiLevel > VelaPluginApi.Level)
        {
            Warn($"apiLevel {manifest.ApiLevel} is newer than this SDK ({VelaPluginApi.Level}); "
                 + "hosts built on this SDK will refuse to load the plugin.");
        }
        if (manifest.Author is null && manifest.Publisher is null)
        {
            Warn("neither \"author\" nor \"publisher\" is set - the plugin manager page will show no author.");
        }
        return 0;
    }

    /// <summary>把插件产物目录打成 .vpx。</summary>
    private static int Pack(string[] args)
    {
        Options options = Options.Parse(args);
        string source = Path.GetFullPath(options.Positional.FirstOrDefault() ?? ".");
        PluginManifest manifest = LoadManifest(source);
        RequireEntry(source, manifest);

        string fileName = $"{manifest.Id}-{manifest.Version}{VpxContainer.FileExtension}";
        string output = options.Get("--output") is { } requested
            // 目录 → 用约定文件名;否则当成完整路径。MSBuild 的 PackVpx 传的就是目录。
            ? Path.GetFullPath(Directory.Exists(requested)
                               || !requested.EndsWith(VpxContainer.FileExtension, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(requested, fileName)
                : requested)
            : Path.GetFullPath(Path.Combine(source, "..", fileName));

        using ECDsa? key = LoadPrivateKey(options.Get("--key"));
        VpxContainer.Pack(source, output, new()
        {
            Mask = !options.Has("--no-mask"),
            SigningKey = key
        });

        VpxPackageInfo info = VpxContainer.ReadInfo(output);
        Console.WriteLine($"Packed {manifest.Id} v{manifest.Version}");
        Console.WriteLine($"  -> {output}");
        Console.WriteLine($"     payload {info.PayloadLength} bytes, sha256 {info.PayloadSha256}");
        Console.WriteLine($"     {(info.Signature is null ? "unsigned" : "signed by " + VpxContainer.PublicKeyFingerprint(info.Signature.PublicKey))}");
        return 0;
    }

    /// <summary>
    /// 打印容器头、签名状态与包里的清单。targets 用不到它 —— 它在这里是给 CI 的端到端冒烟
    /// 做"把刚打出的包读回来"那一步用的(见 scripts/Invoke-Smoke.ps1),顺带也方便排障。
    /// </summary>
    private static int Info(string[] args)
    {
        Options options = Options.Parse(args);
        string package = Path.GetFullPath(options.Positional.FirstOrDefault()
                                          ?? throw new PackerException("Missing package path. Usage: info <package.vpx>"));
        if (!File.Exists(package))
        {
            throw new PackerException($"Package not found: {package}");
        }

        VpxPackageInfo info = VpxContainer.ReadInfo(package);
        Console.WriteLine(Path.GetFileName(package));
        Console.WriteLine($"  format     v{info.FormatVersion}");
        Console.WriteLine($"  flags      {info.Flags}");
        Console.WriteLine($"  payload    {info.PayloadLength} bytes");
        Console.WriteLine($"  sha256     {info.PayloadSha256}");
        VpxSignatureState signature = VpxContainer.VerifySignature(info);
        Console.WriteLine($"  signature  {(signature == VpxSignatureState.Trusted ? "Valid" : signature.ToString())}"
                          + (info.Signature is { } block ? $" ({VpxContainer.PublicKeyFingerprint(block.PublicKey)})" : ""));

        // 光有摘要看不出这是哪个插件,把清单从载荷里读出来。
        using Stream payload = VpxContainer.OpenPayload(package);
        using ZipArchive archive = new(payload, ZipArchiveMode.Read);
        if (archive.GetEntry(PluginManifestReader.FileName) is { } entry)
        {
            using StreamReader reader = new(entry.Open());
            PluginManifest manifest = PluginManifestReader.Parse(reader.ReadToEnd());
            Console.WriteLine($"  plugin     {manifest.Id} v{manifest.Version} ({manifest.DisplayName})");
            Console.WriteLine($"  author     {manifest.Author ?? manifest.Publisher ?? "(not set)"}");
        }
        return 0;
    }

    // ---- helpers ----------------------------------------------------------

    private static PluginManifest LoadManifest(string directory)
    {
        string manifestPath = Path.Combine(directory, PluginManifestReader.FileName);
        if (!File.Exists(manifestPath))
        {
            throw new PackerException($"No {PluginManifestReader.FileName} in '{directory}'. "
                                      + "Point the command at the plugin's build output directory.");
        }
        return PluginManifestReader.Load(manifestPath);
    }

    private static void RequireEntry(string directory, PluginManifest manifest)
    {
        if (!File.Exists(Path.Combine(directory, manifest.Entry)))
        {
            throw new PackerException($"Entry assembly '{manifest.Entry}' is missing from '{directory}'. "
                                      + "Build the plugin project first.");
        }
    }

    private static ECDsa? LoadPrivateKey(string? path)
    {
        if (path is null)
        {
            return null;
        }
        string full = Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            throw new PackerException($"Key file not found: {full}");
        }
        ECDsa key = ECDsa.Create();
        try
        {
            key.ImportFromPem(File.ReadAllText(full));
        }
        catch (ArgumentException ex)
        {
            key.Dispose();
            throw new PackerException($"'{full}' is not a PEM private key: {ex.Message}");
        }
        return key;
    }

    private static void Warn(string message) => Console.WriteLine($"warning: {message}");
}

/// <summary>参数已经用尽了可诊断的信息,剩下的只是把话说给人听。</summary>
internal sealed class PackerException(string message) : Exception(message);

/// <summary>
/// 极简参数解析。调用方只有那套 targets,形状是固定的:一个位置参数 + 三个选项。
/// 不引第三方命令行库 —— 本工程要被原样复制进别人的 tools/,依赖越少越好。
/// </summary>
internal sealed class Options
{
    private readonly Dictionary<string, string?> _options = [with(StringComparer.Ordinal)];

    public List<string> Positional { get; } = [];

    public static Options Parse(string[] args)
    {
        Options parsed = new();
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (token.Length == 0 || token[0] != '-')
            {
                parsed.Positional.Add(token);
                continue;
            }
            string? name = token switch
            {
                "--output" or "-o" => "--output",
                "--key" or "-k" => "--key",
                _ => null
            };
            if (name is null)
            {
                // 开关(目前只有 --no-mask)。不认识的也收下:多一个未知开关不该让构建失败,
                // 而漏掉一个带值选项会让下一个 token 被当成位置参数,那才是会出错的方向。
                parsed._options[token] = null;
                continue;
            }
            if (i + 1 >= args.Length)
            {
                throw new PackerException($"'{token}' needs a value.");
            }
            parsed._options[name] = args[++i];
        }
        return parsed;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.TryGetValue(name, out string? value) ? value : null;
}
