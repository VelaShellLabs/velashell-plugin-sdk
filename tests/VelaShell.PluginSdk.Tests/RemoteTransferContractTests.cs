using System.Text.Json;
using VelaShell.PluginSdk.RemoteFs;
using VelaShell.PluginSdk.Rpc;
using VelaShell.PluginSdk.Testing;

namespace VelaShell.PluginSdk.Tests;

[TestClass]
public sealed class RemoteTransferContractTests
{
    private static RemoteTransferSnapshot Snapshot(RemoteTransferState state = RemoteTransferState.Running) => new()
    {
        TransferId = "transfer", OperationId = "start-operation", SessionId = "session",
        LocalPath = "local.bin", RemotePath = "/remote.bin", State = state,
        Generation = 1, Sequence = 1, TotalBytes = 8L * 1024 * 1024 * 1024,
        TransferredBytes = 4L * 1024 * 1024 * 1024
    };

    private static RemoteTransferResumeOptions ResumeOptions(string operationId = "resume") => new()
    {
        OperationId = operationId, ExpectedGeneration = 1, TargetSessionId = "reconnected-session"
    };

    private sealed class LegacyFs : IRemoteFsApi
    {
        public int UploadCalls { get; private set; }
        public Task UploadFileAsync(string sessionId, string localPath, string remotePath,
            IProgress<RemoteTransferProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<RemoteFileEntry>> ListDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RemoteFileEntry?> StatAsync(string sessionId, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string sessionId, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetWorkingDirectoryAsync(string sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DownloadFileAsync(string sessionId, string remotePath, string localPath, IProgress<RemoteTransferProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ReadAllBytesAsync(string sessionId, string remotePath, int maxBytes = 16 * 1024 * 1024, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task WriteAllBytesAsync(string sessionId, string remotePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateDirectoryAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureDirectoryAsync(string sessionId, string remotePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RenameAsync(string sessionId, string oldPath, string newPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [TestMethod]
    public void LegacyHost_NewMethodsExplicitlyFailWithoutStartingSynchronousUpload()
    {
        var legacy = new LegacyFs();
        IRemoteFsApi fs = legacy;
        Assert.IsFalse(fs.SupportsTransferOperations);
        Assert.ThrowsExactly<NotSupportedException>(() => fs.StartUploadAsync("s", "local", "/remote", new() { OperationId = "op" }));
        Assert.ThrowsExactly<NotSupportedException>(() => fs.StartDownloadAsync("s", "/remote", "local", new() { OperationId = "op" }));
        Assert.ThrowsExactly<NotSupportedException>(() => fs.GetTransferAsync("t"));
        Assert.ThrowsExactly<NotSupportedException>(() => fs.WaitTransferAsync("t", 0, TimeSpan.Zero));
        Assert.ThrowsExactly<NotSupportedException>(() => fs.CancelTransferAsync("t", 1));
        Assert.ThrowsExactly<NotSupportedException>(() => fs.ResumeTransferAsync("t", ResumeOptions()));
        Assert.AreEqual(0, legacy.UploadCalls);
    }

    [TestMethod]
    public async Task Fake_RequiresExplicitCapabilityAndHandlers_DoesNotReadLocalFile()
    {
        var fake = new FakeRemoteFs();
        var options = new RemoteUploadOptions { OperationId = "operation" };
        var downloadOptions = new RemoteDownloadOptions { OperationId = "download-operation" };
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.StartDownloadAsync("s", "/remote", "missing.bin", downloadOptions));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.StartUploadAsync("s", "missing.bin", "/remote", options));
        fake.SupportsTransferOperations = true;
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.StartUploadAsync("s", "missing.bin", "/remote", options));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.GetTransferAsync("t"));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.WaitTransferAsync("t", 0, TimeSpan.Zero));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.CancelTransferAsync("t", 1));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fake.ResumeTransferAsync("t", ResumeOptions()));
        Assert.IsNull(fake.GetFile("s", "/remote"));
    }

    [TestMethod]
    public async Task Fake_ForwardsOptionsIdsAndRequestTokens()
    {
        using var cts = new CancellationTokenSource();
        var options = new RemoteUploadOptions { OperationId = "start", Overwrite = true, Timeout = TimeSpan.FromHours(10), ApprovalContext = RemoteTransferApprovalContext.ExternalMcp };
        RemoteTransferSnapshot expected = Snapshot();
        var downloadOptions = new RemoteDownloadOptions { OperationId = "download", Overwrite = true, Timeout = TimeSpan.FromHours(10), ApprovalContext = RemoteTransferApprovalContext.ExternalMcp };
        var fake = new FakeRemoteFs
        {
            SupportsTransferOperations = true,
            SupportsDownloadOperations = true,
            StartDownloadHandler = (session, remote, local, actualOptions, token) =>
            {
                Assert.AreEqual("s", session);
                Assert.AreEqual("/remote", remote);
                Assert.AreEqual("missing.bin", local);
                Assert.AreSame(downloadOptions, actualOptions);
                Assert.AreEqual(cts.Token, token);
                return Task.FromResult(expected with { Direction = RemoteTransferDirection.Download });
            },
            StartUploadHandler = (session, local, remote, actualOptions, token) =>
            {
                Assert.AreEqual("s", session);
                Assert.AreEqual("missing.bin", local);
                Assert.AreEqual("/remote", remote);
                Assert.AreSame(options, actualOptions);
                Assert.AreEqual(cts.Token, token);
                return Task.FromResult(expected);
            },
            GetTransferHandler = (id, token) =>
            {
                Assert.AreEqual("transfer", id);
                Assert.AreEqual(cts.Token, token);
                return Task.FromResult(expected);
            },
            WaitTransferHandler = (id, sequence, wait, token) =>
            {
                Assert.AreEqual("transfer", id);
                Assert.AreEqual(1L, sequence);
                Assert.AreEqual(TimeSpan.FromSeconds(30), wait);
                Assert.AreEqual(cts.Token, token);
                return Task.FromResult(expected);
            },
            CancelTransferHandler = (id, generation, token) =>
            {
                Assert.AreEqual("transfer", id);
                Assert.AreEqual(1L, generation);
                Assert.AreEqual(cts.Token, token);
                return Task.FromResult(expected with { State = RemoteTransferState.CancelRequested, CancellationRequested = true });
            },
            ResumeTransferHandler = (id, actualOptions, token) =>
            {
                Assert.AreEqual("transfer", id);
                Assert.AreEqual(ResumeOptions(), actualOptions);
                Assert.AreEqual(cts.Token, token);
                return Task.FromResult(expected);
            }
        };
        RemoteTransferSnapshot downloading = await fake.StartDownloadAsync("s", "/remote", "missing.bin", downloadOptions, cts.Token);
        Assert.AreEqual(RemoteTransferDirection.Download, downloading.Direction);
        Assert.AreSame(expected, await fake.StartUploadAsync("s", "missing.bin", "/remote", options, cts.Token));
        Assert.AreSame(expected, await fake.GetTransferAsync("transfer", cts.Token));
        Assert.AreSame(expected, await fake.WaitTransferAsync("transfer", 1, TimeSpan.FromSeconds(30), cts.Token));
        RemoteTransferSnapshot cancelling = await fake.CancelTransferAsync("transfer", 1, cts.Token);
        Assert.IsFalse(cancelling.IsTerminal);
        Assert.IsFalse(cancelling.RemoteStopConfirmed);
        Assert.AreSame(expected, await fake.ResumeTransferAsync("transfer", ResumeOptions(), cts.Token));
        Assert.HasCount(1, fake.UploadsStarted);
        Assert.HasCount(1, fake.DownloadsStarted);
        Assert.AreEqual(("s", "/remote", "missing.bin", downloadOptions), fake.DownloadsStarted[0]);
        Assert.HasCount(1, fake.TransfersRead);
        Assert.HasCount(1, fake.TransfersWaited);
        Assert.HasCount(1, fake.TransfersCancelled);
        Assert.AreEqual(("transfer", 1L), fake.TransfersCancelled[0]);
        Assert.HasCount(1, fake.TransfersResumed);
        Assert.AreEqual(("transfer", ResumeOptions()), fake.TransfersResumed[0]);
    }

    [TestMethod]
    public async Task WaitExpiryOrCancellation_DoesNotInvokeTransferCancellation()
    {
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RemoteTransferSnapshot running = Snapshot();
        var fake = new FakeRemoteFs { SupportsDownloadOperations = true };
        fake.GetTransferHandler = (_, _) => Task.FromResult(running);
        fake.WaitTransferHandler = (_, _, _, _) => Task.FromResult(running);
        fake.CancelTransferHandler = (_, _, _) => Task.FromResult(running);
        Assert.AreSame(running, await fake.GetTransferAsync("transfer"));
        Assert.AreSame(running, await fake.CancelTransferAsync("transfer", 1));
        int cancelCount = fake.TransfersCancelled.Count;
        Assert.AreSame(running, await fake.WaitTransferAsync("transfer", 1, TimeSpan.Zero));
        using var cts = new CancellationTokenSource();
        fake.WaitTransferHandler = async (_, _, _, token) =>
        {
            waiting.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return running;
        };
        Task<RemoteTransferSnapshot> wait = fake.WaitTransferAsync("transfer", 1, TimeSpan.FromSeconds(30), cts.Token);
        await waiting.Task;
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => wait);
        Assert.AreEqual(cancelCount, fake.TransfersCancelled.Count);
    }

    [TestMethod]
    public async Task CancelledRequests_AreNotDispatched()
    {
        var fake = new FakeRemoteFs { SupportsTransferOperations = true, SupportsDownloadOperations = true };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fake.StartDownloadAsync("s", "/remote", "local", new() { OperationId = "download-op" }, cts.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fake.StartUploadAsync("s", "local", "/remote", new() { OperationId = "op" }, cts.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fake.GetTransferAsync("t", cts.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fake.WaitTransferAsync("t", 0, TimeSpan.Zero, cts.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fake.CancelTransferAsync("t", 1, cts.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fake.ResumeTransferAsync("t", ResumeOptions("op"), cts.Token));
        Assert.AreEqual(0, fake.UploadsStarted.Count + fake.DownloadsStarted.Count + fake.TransfersRead.Count + fake.TransfersWaited.Count
            + fake.TransfersCancelled.Count + fake.TransfersResumed.Count);
    }

    [TestMethod]
    public void InvalidWaitAndOperationArguments_AreNotDispatched()
    {
        var fake = new FakeRemoteFs();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.WaitTransferAsync("t", -1, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.WaitTransferAsync("t", 0, TimeSpan.FromSeconds(-1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.WaitTransferAsync("t", 0, TimeSpan.FromSeconds(31)));
        Assert.ThrowsExactly<ArgumentException>(() => fake.StartUploadAsync("s", "local", "/remote", new() { OperationId = " " }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.StartUploadAsync("s", "local", "/remote", new() { OperationId = "op", Timeout = TimeSpan.Zero }));
        Assert.ThrowsExactly<ArgumentException>(() => fake.StartDownloadAsync("s", "/remote", "local", new() { OperationId = " " }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.StartDownloadAsync("s", "/remote", "local", new() { OperationId = "op", Timeout = TimeSpan.Zero }));
        Assert.ThrowsExactly<ArgumentException>(() => fake.ResumeTransferAsync("t", ResumeOptions(" ")));
        Assert.ThrowsExactly<ArgumentException>(() => fake.ResumeTransferAsync("t", ResumeOptions() with { TargetSessionId = " " }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.ResumeTransferAsync("t", ResumeOptions() with { ExpectedGeneration = 0 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fake.CancelTransferAsync("t", 0));
        Assert.AreEqual(0, fake.UploadsStarted.Count + fake.DownloadsStarted.Count + fake.TransfersWaited.Count + fake.TransfersResumed.Count);
    }

    [TestMethod]
    public void Snapshot_DefaultsAreConservative_AndFullByteCountDoesNotImplySuccess()
    {
        RemoteTransferSnapshot unknown = JsonSerializer.Deserialize<RemoteTransferSnapshot>(
            """{"TransferId":"t","OperationId":"op","SessionId":"s","LocalPath":"local","RemotePath":"/remote"}""")!;
        Assert.AreEqual(RemoteTransferState.Unknown, unknown.State);
        Assert.AreEqual(RemoteTransferDirection.Upload, unknown.Direction);
        Assert.AreEqual(RemoteTransferPhase.Unknown, unknown.Phase);
        Assert.AreEqual(-1L, unknown.TotalBytes);
        Assert.IsTrue(unknown.IsTerminal);
        Assert.IsFalse(unknown.IsSuccess);
        Assert.IsFalse(unknown.CanResume);
        RemoteTransferSnapshot full = Snapshot() with { TransferredBytes = 8L * 1024 * 1024 * 1024 };
        Assert.IsFalse(full.IsSuccess);
        Assert.IsFalse(full.IsTerminal);
        Assert.IsTrue((full with { State = RemoteTransferState.Succeeded }).IsSuccess);
        Assert.IsFalse((unknown with { Direction = RemoteTransferDirection.Download, Resumable = true,
            RemoteStopConfirmed = true, DestinationCommitConfirmedAbsent = true }).CanResume);
        Assert.IsFalse((full with { State = RemoteTransferState.Succeeded, Resumable = true, RemoteStopConfirmed = true }).CanResume);
        Assert.IsFalse((unknown with { Resumable = true }).CanResume);
        Assert.IsFalse((unknown with { Resumable = true, RemoteStopConfirmed = true }).CanResume,
            "确认停止仍不代表提交结果已消歧");
        Assert.IsTrue((unknown with { Resumable = true, RemoteStopConfirmed = true, DestinationCommitConfirmedAbsent = true }).CanResume);
        Assert.IsFalse((full with { Resumable = true, RemoteStopConfirmed = true }).CanResume);
    }

    [TestMethod]
    public void RpcDtos_RoundTripLargeCounters_AndKeepLegacySignatures()
    {
        RemoteTransferSnapshot snapshot = Snapshot() with
        {
            Sequence = long.MaxValue - 1, CheckpointOffset = 4L * 1024 * 1024 * 1024,
            SpeedBytesPerSecond = 100000000, EstimatedTimeRemaining = TimeSpan.FromHours(2)
        };
        RoundTrip(new FsTransferStartUploadRequest("s", "local", "/remote", new() { OperationId = "op" }));
        RoundTrip(new FsTransferStartDownloadRequest("s", "/remote", "local", new() { OperationId = "download" }));
        RoundTrip(new FsTransferRef("t"));
        RoundTrip(new FsTransferWaitRequest("t", snapshot.Sequence, 30));
        RoundTrip(new FsTransferResumeRequest("t", ResumeOptions()));
        RoundTrip(new FsTransferCancelRequest("t", 1));
        RoundTrip(new FsTransferProgressNotification(snapshot));
        HandshakeResponse legacy = JsonSerializer.Deserialize<HandshakeResponse>(
            """{"ApiLevel":2,"HostVersion":"1","Locale":"en","Theme":"dark"}""")!;
        Assert.IsFalse(legacy.SupportsTransferOperations);
        Assert.IsFalse(legacy.SupportsDownloadOperations);
        RoundTrip(legacy with { SupportsTransferOperations = true, SupportsDownloadOperations = true });
        Assert.IsNotNull(typeof(FsTransferRequest).GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string)]));
        Assert.HasCount(4, typeof(FsTransferRequest).GetMethod("Deconstruct")!.GetParameters());
        Assert.HasCount(3, typeof(FsProgressNotification).GetMethod("Deconstruct")!.GetParameters());
        Assert.HasCount(6, typeof(HandshakeResponse).GetMethod("Deconstruct")!.GetParameters());
        string[] methods = [PluginRpc.FsTransferStartUpload, PluginRpc.FsTransferStartDownload, PluginRpc.FsTransferGet, PluginRpc.FsTransferWait,
            PluginRpc.FsTransferCancel, PluginRpc.FsTransferResume, PluginRpc.FsTransferProgress];
        Assert.HasCount(7, methods.Distinct(StringComparer.Ordinal));
        Assert.IsTrue(methods.All(m => m.StartsWith("fs/transfer/", StringComparison.Ordinal)));
    }

    private static void RoundTrip<T>(T value) => Assert.AreEqual(value, JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)));

    [TestMethod]
    public void TransferApprovalContext_RoundTripsAndLegacyRequestsStillRequireConfirmation()
    {
        const string legacy = """{"OperationId":"op","Overwrite":true}""";
        Assert.AreEqual(RemoteTransferApprovalContext.Default, JsonSerializer.Deserialize<RemoteUploadOptions>(legacy)!.ApprovalContext);
        Assert.AreEqual(RemoteTransferApprovalContext.Default, JsonSerializer.Deserialize<RemoteDownloadOptions>(legacy)!.ApprovalContext);
        foreach (RemoteTransferApprovalContext context in new[] { RemoteTransferApprovalContext.ExternalMcp, (RemoteTransferApprovalContext)99 })
        {
            RoundTrip(new FsTransferStartUploadRequest("s", "local", "/remote",
                new() { OperationId = "upload", ApprovalContext = context }));
            RoundTrip(new FsTransferStartDownloadRequest("s", "/remote", "local",
                new() { OperationId = "download", ApprovalContext = context }));
        }
    }
}
