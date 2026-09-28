using VelaShell.PluginSdk.RemoteFs;

namespace VelaShell.PluginSdk.Protocols;

/// <summary>
/// 由 <see cref="IProtocolFileSystem" /> <b>可选</b>兼实现的接口:把一条顺序读的流写成远端文件。
/// <para>
/// 存在的理由:<see cref="IProtocolFileSystem.UploadFileAsync" /> 只收本地路径,
/// 而宿主有些传输的源根本不是本地文件 —— 典型的是双栏文件标签里「两台远端之间」的中转:
/// 源端 <see cref="IProtocolFileSystem.OpenReadAsync" /> 读出来的流直接喂给目标端,字节只在内存里过一下,
/// 不落本地磁盘。没有这一面,宿主只能先整份下载到临时文件再上传,耗时是两段之和,还要占一份磁盘。
/// </para>
/// <para>
/// 不实现它完全合法:宿主问不到这一面,就把这个协议当作「不支持从流上传」,
/// 相关入口(跨会话中转)对它不开放,其余功能不受影响。
/// </para>
/// <para>
/// 纪律:
/// <list type="bullet">
///   <item><b>流归调用方</b>:读完即可,不要释放它。</item>
///   <item>流是<b>顺序</b>的,可能不可 Seek(源往往是另一条网络连接);不要依赖 <c>Length</c> 与 <c>Position</c>,
///   长度由 <see cref="UploadStreamAsync" /> 的 <c>length</c> 参数给出。</item>
///   <item>语义是覆盖写;不做续传(源端没有「已传的那半截」可供核实)。</item>
///   <item>与 <see cref="IProtocolFileSystem" /> 的其余方法一样可能被并发调用,且在后台线程上调用。</item>
/// </list>
/// </para>
/// <example>
/// <code>
/// internal sealed class S3FileSystem : IProtocolFileSystem, IProtocolStreamUpload
/// {
///     public Task UploadStreamAsync(string sessionId, Stream source, string path, long length,
///         IProgress&lt;RemoteTransferProgress&gt;? progress = null, CancellationToken cancellationToken = default) =>
///         PutObjectAsync(sessionId, path, source, length, progress, cancellationToken);
/// }
/// </code>
/// </example>
/// </summary>
public interface IProtocolStreamUpload
{
    /// <summary>把 <paramref name="source" /> 的全部内容写成远端文件 <paramref name="path" />(覆盖)。</summary>
    /// <param name="sessionId">会话标识(即 <see cref="IProtocolFileSystem.ConnectAsync" /> 收到的那个)。</param>
    /// <param name="source">顺序读的源流;归调用方所有,实现不要释放它。</param>
    /// <param name="path">目标路径。</param>
    /// <param name="length">源的字节数,供进度与需要预先声明长度的后端(如对象存储的单次 PUT)使用;未知时为 0。</param>
    /// <param name="progress">进度回报;宿主侧已节流,可以放心高频上报。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="PluginSessionNotFoundException">未知的 <paramref name="sessionId" />。</exception>
    Task UploadStreamAsync(
        string sessionId,
        Stream source,
        string path,
        long length,
        IProgress<RemoteTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
