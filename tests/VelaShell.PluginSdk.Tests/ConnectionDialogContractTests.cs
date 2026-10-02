using VelaShell.PluginSdk.Protocols;
using VelaShell.PluginSdk.Workspaces;

namespace VelaShell.PluginSdk.Tests;

/// <summary>
/// 连接对话框的两样新契约面:字段版式(分节 / 宽度 / 画法 / 右侧栏 / 主机列表)与工作台的连接检查。
/// </summary>
/// <remarks>
/// 守的是**兼容性**与**语义**:版式属性的默认值必须等于"老样子"(一列铺开、默认控件、主表单),
/// 否则一个没碰这些属性的老插件换新 SDK 重编之后,连接对话框会悄悄变样;
/// 枚举只能往后加,已有的值不能挪 —— 插件里编进去的是整数。
/// </remarks>
[TestClass]
[TestCategory("Plugins")]
public class ConnectionDialogContractTests
{
    [TestMethod]
    public void LayoutDefaults_MeanTheOldSingleColumnForm()
    {
        var field = new ProtocolSettingField { Key = "k", Label = "K" };

        Assert.IsNull(field.Section);
        Assert.AreEqual(ProtocolFieldWidth.Full, field.Width);
        Assert.AreEqual(ProtocolSettingPresentation.Default, field.Presentation);
        Assert.AreEqual(ProtocolFieldPlacement.Form, field.Placement);
        Assert.AreEqual(ProtocolTone.Neutral, new ProtocolSettingChoice("v", "V").Tone);
    }

    [TestMethod]
    public void SettingKinds_OnlyGrowAtTheEnd()
    {
        // 插件编译进去的是整数:已有的值一个都不能挪。
        Assert.AreEqual(0, (int)ProtocolSettingKind.Text);
        Assert.AreEqual(4, (int)ProtocolSettingKind.Choice);
        Assert.AreEqual(5, (int)ProtocolSettingKind.DynamicChoice);
        Assert.AreEqual(6, (int)ProtocolSettingKind.SshSession);
        Assert.AreEqual(7, (int)ProtocolSettingKind.HostList);
    }

    [TestMethod]
    public void HostSections_CannotCollideWithAPluginHeading()
    {
        string[] sections = [ProtocolSettingSection.Basic, ProtocolSettingSection.Target, ProtocolSettingSection.Authentication];

        Assert.AreEqual(sections.Length, sections.Distinct(StringComparer.Ordinal).Count());
        // 插件自己的节标题是本地化文案;宿主的节用一个文案里不会出现的前缀,两者不可能撞上。
        Assert.IsTrue(sections.All(static s => s.StartsWith("vela:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PreviewText_IsTheSpansConcatenated()
    {
        var preview = new WorkspaceConnectionPreview
        {
            Title = "连接字符串",
            Spans =
            [
                new("mongodb://", WorkspacePreviewRole.Scheme),
                new("ops", WorkspacePreviewRole.User),
                new(":****@", WorkspacePreviewRole.Secret),
                new("10.0.0.1:27017", WorkspacePreviewRole.Host),
                new("/shop", WorkspacePreviewRole.Path)
            ]
        };

        Assert.AreEqual("mongodb://ops:****@10.0.0.1:27017/shop", preview.Text);
        Assert.AreEqual(WorkspacePreviewRole.Plain, new WorkspacePreviewSpan("?").Role);
    }

    [TestMethod]
    public void Report_NamesTheFirstFailedStep()
    {
        var report = new WorkspaceProbeReport
        {
            Succeeded = false,
            Steps =
            [
                new("tunnel", "SSH 隧道", WorkspaceProbeState.Passed, ElapsedMs: 12),
                new("tcp", "TCP 连接", WorkspaceProbeState.Failed, "connection refused"),
                new("auth", "认证", WorkspaceProbeState.Skipped)
            ]
        };

        Assert.AreEqual("tcp", report.FirstFailure?.Key);
        Assert.IsNull(new WorkspaceProbeReport { Succeeded = true }.FirstFailure);
        Assert.AreEqual(0, new WorkspaceProbeReport { Succeeded = true }.Endpoints.Count);
    }

    [TestMethod]
    public async Task Inspector_ReportsProgressByKeyAndCanBeCalledWithoutIt()
    {
        var inspector = new StubInspector();
        var seen = new List<WorkspaceProbeStep>();
        var request = new WorkspaceConnectRequest { SessionId = "s", Host = "h", Port = 1 };

        WorkspaceProbeReport report = await inspector.ProbeAsync(request, new SyncProgress(seen.Add));
        WorkspaceProbeReport quiet = await inspector.ProbeAsync(request);

        CollectionAssert.AreEqual(new[] { WorkspaceProbeState.Running, WorkspaceProbeState.Passed }, seen.Select(static s => s.State).ToArray());
        Assert.IsTrue(seen.All(static s => s.Key == "tcp"), "a step's updates share its key so the host can update the row in place");
        Assert.IsTrue(report.Succeeded);
        Assert.IsTrue(quiet.Succeeded);
        Assert.IsNull(inspector.Preview(request with { Host = "" }));
    }

    private sealed class StubInspector : IWorkspaceConnectionInspector
    {
        public WorkspaceConnectionPreview? Preview(WorkspaceConnectRequest draft) =>
            draft.Host.Length == 0 ? null : new WorkspaceConnectionPreview { Title = "t", Spans = [new(draft.Host)] };

        public Task<WorkspaceProbeReport> ProbeAsync(
            WorkspaceConnectRequest request,
            IProgress<WorkspaceProbeStep>? progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new("tcp", "TCP", WorkspaceProbeState.Running));
            var done = new WorkspaceProbeStep("tcp", "TCP", WorkspaceProbeState.Passed, ElapsedMs: 1);
            progress?.Report(done);
            return Task.FromResult(new WorkspaceProbeReport { Succeeded = true, Steps = [done] });
        }
    }

    /// <summary>同步回调的进度(<see cref="Progress{T}" /> 会把回调投到线程池,断言就得等)。</summary>
    private sealed class SyncProgress(Action<WorkspaceProbeStep> report) : IProgress<WorkspaceProbeStep>
    {
        public void Report(WorkspaceProbeStep value) => report(value);
    }
}
