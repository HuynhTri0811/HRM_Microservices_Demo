using Grpc.Core;

namespace HRM.Tests.Shared;

/// <summary>Tạo AsyncUnaryCall giả để mock gRPC client sinh ra từ Protos/nhansu.proto.</summary>
internal static class GrpcTestCalls
{
    public static AsyncUnaryCall<T> Success<T>(T response) => new(
        Task.FromResult(response),
        Task.FromResult(new Metadata()),
        () => Status.DefaultSuccess,
        () => new Metadata(),
        () => { });

    public static AsyncUnaryCall<T> Failure<T>(StatusCode code, string detail = "lỗi giả lập")
    {
        var status = new Status(code, detail);
        return new AsyncUnaryCall<T>(
            Task.FromException<T>(new RpcException(status)),
            Task.FromResult(new Metadata()),
            () => status,
            () => new Metadata(),
            () => { });
    }

    /// <summary>Giá trị header Authorization trong metadata (null nếu không có).</summary>
    public static string? Authorization(Metadata? headers) =>
        headers?.FirstOrDefault(h => string.Equals(h.Key, "authorization", StringComparison.OrdinalIgnoreCase))?.Value;
}
