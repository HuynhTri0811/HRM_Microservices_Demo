using Grpc.Core;

namespace QuanLyNhanSu.Tests.Grpc;

/// <summary>ServerCallContext tối thiểu để gọi trực tiếp các method của gRPC service trong unit test.</summary>
internal sealed class TestServerCallContext(CancellationToken cancellationToken = default) : ServerCallContext
{
    private readonly Metadata _requestHeaders = new();
    private readonly Metadata _responseTrailers = new();

    protected override string MethodCore => "/nhansu.v1.NhanSuGrpc/Test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "ipv4:127.0.0.1:0";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => _requestHeaders;
    protected override CancellationToken CancellationTokenCore => cancellationToken;
    protected override Metadata ResponseTrailersCore => _responseTrailers;
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
