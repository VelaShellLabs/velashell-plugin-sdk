using System.Text.Json;
using System.Text.Json.Serialization;

namespace VelaShell.PluginSdk.Rpc;

/// <summary>
/// 线上一帧的载荷:请求(req)/应答(res)/通知(evt)三型。
/// 帧格式:4 字节小端长度前缀 + UTF-8 JSON 正文(见 <see cref="RpcConnection" />)。
/// 刻意不用完整 JSON-RPC 2.0 + StreamJsonRpc:本仓库零依赖纪律优先,
/// 双进程同库同版,无跨语言协商需求(决策注记见主仓库 docs/plugins/05-ipc-protocol.md §实现注记)。
/// </summary>
public sealed record RpcMessage
{
    /// <summary>消息类型:"req" / "res" / "evt"。</summary>
    [JsonPropertyName("t")]
    public required string Type { get; init; }

    /// <summary>请求 id;应答携带同 id。通知无 id。</summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    /// <summary>方法名(req/evt)。</summary>
    [JsonPropertyName("m")]
    public string? Method { get; init; }

    /// <summary>参数或返回值(JSON 原文)。</summary>
    [JsonPropertyName("p")]
    public JsonElement? Payload { get; init; }

    /// <summary>错误码(res 且失败时),见 <see cref="RpcErrorCodes" />。</summary>
    [JsonPropertyName("c")]
    public string? ErrorCode { get; init; }

    /// <summary>错误消息(res 且失败时)。宿主内部细节不下发,只给安全的 message。</summary>
    [JsonPropertyName("e")]
    public string? Error { get; init; }
}

/// <summary>统一错误码空间与异常映射。</summary>
public static class RpcErrorCodes
{
    /// <summary>能力调用引用的会话不存在或未连接。</summary>
    public const string SessionNotFound = "session-not-found";

    /// <summary>操作超时。</summary>
    public const string Timeout = "timeout";

    /// <summary>无效操作(能力不可用、状态不允许)。</summary>
    public const string InvalidOperation = "invalid-op";

    /// <summary>参数非法。</summary>
    public const string BadArguments = "bad-args";

    /// <summary>握手失败(令牌/版本不符)。</summary>
    public const string HandshakeRejected = "handshake-rejected";

    /// <summary>用户拒绝了敏感能力请求。</summary>
    public const string PermissionDenied = "permission-denied";

    /// <summary>已保存的会话打不开(网络、认证、指纹等)。</summary>
    public const string SessionOpenFailed = "session-open-failed";

    /// <summary>宿主明确不支持该专用能力,不得静默降级。</summary>
    public const string NotSupported = "not-supported";

    /// <summary>操作已取消,不是执行超时。</summary>
    public const string Cancelled = "cancelled";

    /// <summary>
    /// 传输在登记任务之前就被宿主明确拒绝(<see cref="PluginTransferRejectedException" />)。
    /// 线上带原因后缀:<c>transfer-rejected:source-in-use</c>;不带后缀或后缀不认识时还原成
    /// <see cref="RemoteFs.RemoteTransferRejectReason.Unknown" />,类型照样是「启动前被拒」。可用版本:2.1.3。
    /// </summary>
    public const string TransferRejected = "transfer-rejected";

    /// <summary>
    /// 机密存储锁着:用户设了主密码而此刻没解锁(<see cref="SecretsLockedException" />)。
    /// 只有握手宣告了 <see cref="HandshakeResponse.SupportsSecretsLocked" /> 的宿主才应答它。可用版本:TBD(发版时替换)。
    /// </summary>
    public const string SecretsLocked = "secrets-locked";

    /// <summary>未分类错误。</summary>
    public const string Unknown = "error";

    /// <summary>把异常折叠为线上错误码(细节不跨进程)。</summary>
    public static string FromException(Exception exception) => exception switch
    {
        PluginPermissionDeniedException => PermissionDenied,
        PluginSessionNotFoundException => SessionNotFound,
        // 必须排在 InvalidOperationException 之前:它是后者的子类,顺序反了就永远命中不到
        PluginSessionOpenException => SessionOpenFailed,
        // 同上(InvalidOperationException 的子类)
        PluginTransferRejectedException rejected => TransferRejectedCode(rejected.Reason),
        // 同上(InvalidOperationException 的子类);折成 invalid-op 的话,隔离插件就分不出「锁着」与「状态不对」
        SecretsLockedException => SecretsLocked,
        TimeoutException => Timeout,
        OperationCanceledException => Cancelled,
        NotSupportedException => NotSupported,
        ArgumentException => BadArguments,
        InvalidOperationException => InvalidOperation,
        _ => Unknown
    };

    /// <summary>把线上错误码还原为类型化异常(SDK 侧)。</summary>
    public static Exception ToException(string? code, string message) => code switch
    {
        PermissionDenied => new PluginPermissionDeniedException(message),
        SessionNotFound => new PluginSessionNotFoundException("", message),
        SessionOpenFailed => new PluginSessionOpenException("", message),
        Timeout => new TimeoutException(message),
        Cancelled => new OperationCanceledException(message),
        NotSupported => new NotSupportedException(message),
        BadArguments => new ArgumentException(message),
        InvalidOperation => new InvalidOperationException(message),
        TransferRejected => new PluginTransferRejectedException(RemoteFs.RemoteTransferRejectReason.Unknown, message),
        SecretsLocked => new SecretsLockedException(message),
        _ when code?.StartsWith(TransferRejected + ":", StringComparison.Ordinal) == true
            => new PluginTransferRejectedException(ParseRejectReason(code[(TransferRejected.Length + 1)..]), message),
        _ => new RpcRemoteException(code ?? Unknown, message)
    };

    /// <summary>「启动前被拒」的线上码:原因按短横线名接在 <c>:</c> 后面,未知原因不带后缀。</summary>
    private static string TransferRejectedCode(RemoteFs.RemoteTransferRejectReason reason) =>
        RejectReasonToken(reason) is { } token ? $"{TransferRejected}:{token}" : TransferRejected;

    private static string? RejectReasonToken(RemoteFs.RemoteTransferRejectReason reason) => reason switch
    {
        RemoteFs.RemoteTransferRejectReason.SourceNotFound => "source-not-found",
        RemoteFs.RemoteTransferRejectReason.SourceInUse => "source-in-use",
        RemoteFs.RemoteTransferRejectReason.QuotaExceeded => "quota-exceeded",
        RemoteFs.RemoteTransferRejectReason.InsufficientSpace => "insufficient-space",
        _ => null
    };

    /// <summary>线上名 → 原因;不认识的(更新的对端加了新原因)一律 Unknown,不报错。</summary>
    private static RemoteFs.RemoteTransferRejectReason ParseRejectReason(string token) => token switch
    {
        "source-not-found" => RemoteFs.RemoteTransferRejectReason.SourceNotFound,
        "source-in-use" => RemoteFs.RemoteTransferRejectReason.SourceInUse,
        "quota-exceeded" => RemoteFs.RemoteTransferRejectReason.QuotaExceeded,
        "insufficient-space" => RemoteFs.RemoteTransferRejectReason.InsufficientSpace,
        _ => RemoteFs.RemoteTransferRejectReason.Unknown
    };
}

/// <summary>对端处理请求时报告的错误(未映射到具体异常类型时的兜底)。</summary>
public sealed class RpcRemoteException(string code, string message) : Exception(message)
{
    /// <summary>线上错误码。</summary>
    public string Code { get; } = code;
}

/// <summary>连接已断开(进程退出、管道关闭);所有未决请求以此失败。</summary>
public sealed class RpcDisconnectedException(string message) : Exception(message);
