using VelaShell.PluginSdk.RemoteFs;

namespace VelaShell.PluginSdk.Testing;

/// <summary>
/// <see cref="IRemoteFsApi" /> 的内存实现:每会话一棵以 <c>/</c> 分隔的路径树。
/// 语义与真实实现对齐:Stat 对不存在路径返回 <see langword="null" />;
/// ReadAllBytes 超限抛 <see cref="InvalidOperationException" />。
/// </summary>
public sealed class FakeRemoteFs : IRemoteFsApi
{
    private readonly Dictionary<string, Dictionary<string, byte[]>> _files = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, HashSet<string>> _dirs = [with(StringComparer.Ordinal)];

    /// <summary>显式模拟宿主是否具备独立传输能力;默认否,不退化为内存整文件上传。</summary>
    public bool SupportsTransferOperations { get; set; }

    /// <summary>显式模拟宿主是否具备独立下载能力;默认否,不退化为同步下载。</summary>
    public bool SupportsDownloadOperations { get; set; }

    /// <summary>
    /// 显式模拟宿主是否实现条件撤回(<see cref="IRemoteFsApi.WithdrawPendingTransferAsync" />);默认否,
    /// 不退化成无条件取消。可用版本:2.1.4。
    /// </summary>
    public bool SupportsTransferWithdraw { get; set; }

    /// <summary>独立上传启动脚本;测试自己控制任务生命周期与快照。</summary>
    public Func<string, string, string, RemoteUploadOptions, CancellationToken, Task<RemoteTransferSnapshot>>? StartUploadHandler { get; set; }

    /// <summary>独立下载启动脚本;测试自己控制任务生命周期与快照。</summary>
    public Func<string, string, string, RemoteDownloadOptions, CancellationToken, Task<RemoteTransferSnapshot>>? StartDownloadHandler { get; set; }

    /// <summary>读取传输快照脚本。</summary>
    public Func<string, CancellationToken, Task<RemoteTransferSnapshot>>? GetTransferHandler { get; set; }

    /// <inheritdoc />
    public Task<RemoteTransferSnapshot> StartDownloadAsync(string sessionId, string remotePath, string localPath,
        RemoteDownloadOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OperationId);
        if (options.Timeout is { } timeout && timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        DownloadsStarted.Add((sessionId, remotePath, localPath, options));
        return SupportsDownloadOperations && StartDownloadHandler is { } handler
            ? handler(sessionId, remotePath, localPath, options, cancellationToken)
            : Task.FromException<RemoteTransferSnapshot>(new NotSupportedException("No download start handler configured."));
    }

    /// <summary>等待进度脚本;等待令牌不是传输的生命周期令牌。</summary>
    public Func<string, long, TimeSpan, CancellationToken, Task<RemoteTransferSnapshot>>? WaitTransferHandler { get; set; }

    /// <summary>显式取消脚本;没有脚本时不假装已确认停止。</summary>
    public Func<string, long, CancellationToken, Task<RemoteTransferSnapshot>>? CancelTransferHandler { get; set; }

    /// <summary>显式恢复脚本;没有脚本时不从头重传。</summary>
    public Func<string, RemoteTransferResumeOptions, CancellationToken, Task<RemoteTransferSnapshot>>? ResumeTransferHandler { get; set; }

    /// <summary>
    /// 条件撤回脚本。「批准与撤回先到者赢」由测试自己模拟:替身不编造结论,没有脚本时明确不支持,
    /// 也不改走 <see cref="CancelTransferHandler" />。可用版本:2.1.4。
    /// </summary>
    public Func<string, long, CancellationToken, Task<RemoteTransferWithdrawResult>>? WithdrawTransferHandler { get; set; }

    /// <summary>已请求的独立上传;不读取本地文件内容。</summary>
    public List<(string SessionId, string LocalPath, string RemotePath, RemoteUploadOptions Options)> UploadsStarted { get; } = [];

    /// <summary>已请求的独立下载;不读取远端文件内容。</summary>
    public List<(string SessionId, string RemotePath, string LocalPath, RemoteDownloadOptions Options)> DownloadsStarted { get; } = [];

    /// <summary>已请求的传输查询。</summary>
    public List<string> TransfersRead { get; } = [];

    /// <summary>已请求的进度等待。</summary>
    public List<(string TransferId, long AfterSequence, TimeSpan WaitTimeout)> TransfersWaited { get; } = [];

    /// <summary>已请求的显式取消。</summary>
    public List<(string TransferId, long ExpectedGeneration)> TransfersCancelled { get; } = [];

    /// <summary>已请求的显式恢复。</summary>
    public List<(string TransferId, RemoteTransferResumeOptions Options)> TransfersResumed { get; } = [];

    /// <summary>已请求的条件撤回(请求过,不代表撤下了;结论看脚本的返回)。</summary>
    public List<(string TransferId, long ExpectedGeneration)> TransfersWithdrawn { get; } = [];

    /// <inheritdoc />
    public Task<RemoteTransferSnapshot> StartUploadAsync(string sessionId, string localPath, string remotePath,
        RemoteUploadOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OperationId);
        if (options.Timeout is { } timeout && timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        UploadsStarted.Add((sessionId, localPath, remotePath, options));
        return SupportsTransferOperations && StartUploadHandler is { } handler
            ? handler(sessionId, localPath, remotePath, options, cancellationToken)
            : Task.FromException<RemoteTransferSnapshot>(new NotSupportedException("No transfer start handler configured."));
    }

    /// <inheritdoc />
    public Task<RemoteTransferSnapshot> GetTransferAsync(string transferId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TransfersRead.Add(transferId);
        return (SupportsTransferOperations || SupportsDownloadOperations) && GetTransferHandler is { } handler
            ? handler(transferId, cancellationToken)
            : Task.FromException<RemoteTransferSnapshot>(new NotSupportedException("No transfer query handler configured."));
    }

    /// <inheritdoc />
    public Task<RemoteTransferSnapshot> WaitTransferAsync(string transferId, long afterSequence, TimeSpan waitTimeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (afterSequence < 0) throw new ArgumentOutOfRangeException(nameof(afterSequence));
        if (waitTimeout < TimeSpan.Zero || waitTimeout > TimeSpan.FromSeconds(IRemoteFsApi.MaxTransferWaitSeconds))
            throw new ArgumentOutOfRangeException(nameof(waitTimeout));
        TransfersWaited.Add((transferId, afterSequence, waitTimeout));
        return (SupportsTransferOperations || SupportsDownloadOperations) && WaitTransferHandler is { } handler
            ? handler(transferId, afterSequence, waitTimeout, cancellationToken)
            : Task.FromException<RemoteTransferSnapshot>(new NotSupportedException("No transfer wait handler configured."));
    }

    /// <inheritdoc />
    public Task<RemoteTransferSnapshot> CancelTransferAsync(string transferId, long expectedGeneration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedGeneration <= 0) throw new ArgumentOutOfRangeException(nameof(expectedGeneration));
        TransfersCancelled.Add((transferId, expectedGeneration));
        return (SupportsTransferOperations || SupportsDownloadOperations) && CancelTransferHandler is { } handler
            ? handler(transferId, expectedGeneration, cancellationToken)
            : Task.FromException<RemoteTransferSnapshot>(new NotSupportedException("No transfer cancellation handler configured."));
    }

    /// <inheritdoc />
    public Task<RemoteTransferWithdrawResult> WithdrawPendingTransferAsync(string transferId, long expectedGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedGeneration <= 0) throw new ArgumentOutOfRangeException(nameof(expectedGeneration));
        TransfersWithdrawn.Add((transferId, expectedGeneration));
        return SupportsTransferWithdraw && WithdrawTransferHandler is { } handler
            ? handler(transferId, expectedGeneration, cancellationToken)
            : Task.FromException<RemoteTransferWithdrawResult>(new NotSupportedException("No transfer withdrawal handler configured."));
    }

    /// <inheritdoc />
    public Task<RemoteTransferSnapshot> ResumeTransferAsync(string transferId, RemoteTransferResumeOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OperationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetSessionId);
        if (options.ExpectedGeneration <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        TransfersResumed.Add((transferId, options));
        return SupportsTransferOperations && ResumeTransferHandler is { } handler
            ? handler(transferId, options, cancellationToken)
            : Task.FromException<RemoteTransferSnapshot>(new NotSupportedException("No transfer resume handler configured."));
    }

    /// <summary>预置一个远端文件(自动创建父目录)。</summary>
    public void AddFile(string sessionId, string path, byte[] content)
    {
        FilesOf(sessionId)[Normalize(path)] = content;
        EnsureParents(sessionId, path);
    }

    /// <summary>预置一个远端目录。</summary>
    public void AddDirectory(string sessionId, string path)
    {
        DirsOf(sessionId).Add(Normalize(path));
        EnsureParents(sessionId, path);
    }

    /// <summary>读取当前文件内容(测试断言用);不存在时返回 <see langword="null" />。</summary>
    public byte[]? GetFile(string sessionId, string path)
        => FilesOf(sessionId).GetValueOrDefault(Normalize(path));

    private Dictionary<string, byte[]> FilesOf(string sessionId)
        => _files.TryGetValue(sessionId, out Dictionary<string, byte[]>? files) ? files : _files[sessionId] = [with(StringComparer.Ordinal)];

    private HashSet<string> DirsOf(string sessionId)
        => _dirs.TryGetValue(sessionId, out HashSet<string>? dirs) ? dirs : _dirs[sessionId] = [with(StringComparer.Ordinal)];

    private static string Normalize(string path)
    {
        string normalized = path.Replace('\\', '/').TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized.StartsWith('/') ? normalized : "/" + normalized;
    }

    private void EnsureParents(string sessionId, string path)
    {
        string current = Normalize(path);
        HashSet<string> dirs = DirsOf(sessionId);
        while (current.LastIndexOf('/') is > 0 and var slash)
        {
            current = current[..slash];
            dirs.Add(current);
        }
        dirs.Add("/");
    }

    private RemoteFileEntry? StatCore(string sessionId, string path)
    {
        string normalized = Normalize(path);
        string name = normalized == "/" ? "/" : normalized[(normalized.LastIndexOf('/') + 1)..];
        if (FilesOf(sessionId).TryGetValue(normalized, out byte[]? content))
        {
            return new(name, normalized, false, content.Length, DateTimeOffset.UtcNow, "rw-r--r--", "tester", "tester");
        }
        return DirsOf(sessionId).Contains(normalized)
            ? new(name, normalized, true, 0, DateTimeOffset.UtcNow, "rwxr-xr-x", "tester", "tester")
            : null;
    }

    /// <summary>
    /// <see cref="ListDirectoryAsync" /> 至今被调用了多少次。
    /// 列目录在真实环境里是一次 SFTP 往返,"敲一个字符列一次"就是卡顿的来源,
    /// 因此凡是靠缓存/防抖压请求数的功能(如 AI 面板的 <c>@</c> 补全)都拿它来钉行为。
    /// </summary>
    public int ListDirectoryCalls { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<RemoteFileEntry>> ListDirectoryAsync(string sessionId, string path,
        CancellationToken cancellationToken = default)
    {
        ListDirectoryCalls++;
        string normalized = Normalize(path);
        if (StatCore(sessionId, normalized) is not { IsDirectory: true })
        {
            return Task.FromException<IReadOnlyList<RemoteFileEntry>>(
                new DirectoryNotFoundException($"Remote directory not found: {path}"));
        }
        string prefix = normalized == "/" ? "/" : normalized + "/";
        IEnumerable<string> children = FilesOf(sessionId).Keys.Concat(DirsOf(sessionId))
            .Where(p => p.Length > prefix.Length && p.StartsWith(prefix, StringComparison.Ordinal)
                        && !p[prefix.Length..].Contains('/'));
        IReadOnlyList<RemoteFileEntry> entries = [.. children.Distinct().Order(StringComparer.Ordinal).Select(p => StatCore(sessionId, p)!)];
        return Task.FromResult(entries);
    }

    /// <inheritdoc />
    public Task<RemoteFileEntry?> StatAsync(string sessionId, string path, CancellationToken cancellationToken = default)
        => Task.FromResult(StatCore(sessionId, path));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string sessionId, string path, CancellationToken cancellationToken = default)
        => Task.FromResult(StatCore(sessionId, path) is not null);

    /// <inheritdoc />
    public Task<string> GetWorkingDirectoryAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult("/home/tester");

    /// <inheritdoc />
    public async Task DownloadFileAsync(string sessionId, string remotePath, string localPath,
        IProgress<RemoteTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        byte[] content = await ReadAllBytesAsync(sessionId, remotePath, int.MaxValue, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(localPath, content, cancellationToken).ConfigureAwait(false);
        progress?.Report(new(content.Length, content.Length));
    }

    /// <inheritdoc />
    public async Task UploadFileAsync(string sessionId, string localPath, string remotePath,
        IProgress<RemoteTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        byte[] content = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
        AddFile(sessionId, remotePath, content);
        progress?.Report(new(content.Length, content.Length));
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default)
    {
        byte[] content = await ReadAllBytesAsync(sessionId, remotePath, int.MaxValue, cancellationToken).ConfigureAwait(false);
        return new MemoryStream(content, writable: false);
    }

    /// <inheritdoc />
    public Task<byte[]> ReadAllBytesAsync(string sessionId, string remotePath, int maxBytes = 16 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        if (FilesOf(sessionId).TryGetValue(Normalize(remotePath), out byte[]? content))
        {
            return content.Length > maxBytes
                ? Task.FromException<byte[]>(new InvalidOperationException(
                    $"Remote file '{remotePath}' is {content.Length} bytes (limit {maxBytes})."))
                : Task.FromResult(content);
        }
        return Task.FromException<byte[]>(new FileNotFoundException($"Remote file not found: {remotePath}"));
    }

    /// <inheritdoc />
    public Task WriteAllBytesAsync(string sessionId, string remotePath, ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        AddFile(sessionId, remotePath, content.ToArray());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default)
    {
        string normalized = Normalize(remotePath);
        string prefix = normalized + "/";
        FilesOf(sessionId).Remove(normalized);
        FilesOf(sessionId).Keys.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)).ToList()
            .ForEach(p => FilesOf(sessionId).Remove(p));
        DirsOf(sessionId).RemoveWhere(p => p == normalized || p.StartsWith(prefix, StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateDirectoryAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default)
    {
        if (StatCore(sessionId, remotePath) is not null)
        {
            return Task.FromException(new IOException($"Remote path already exists: {remotePath}"));
        }
        AddDirectory(sessionId, remotePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureDirectoryAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default)
    {
        AddDirectory(sessionId, remotePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RenameAsync(string sessionId, string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        string from = Normalize(oldPath);
        string to = Normalize(newPath);
        Dictionary<string, byte[]> files = FilesOf(sessionId);
        if (files.Remove(from, out byte[]? content))
        {
            AddFile(sessionId, to, content);
            return Task.CompletedTask;
        }
        if (DirsOf(sessionId).Contains(from))
        {
            string prefix = from + "/";
            foreach (string path in files.Keys.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                files.Remove(path, out byte[]? moved);
                AddFile(sessionId, to + path[from.Length..], moved!);
            }
            DirsOf(sessionId).RemoveWhere(p => p == from || p.StartsWith(prefix, StringComparison.Ordinal));
            AddDirectory(sessionId, to);
            return Task.CompletedTask;
        }
        return Task.FromException(new FileNotFoundException($"Remote path not found: {oldPath}"));
    }
}
