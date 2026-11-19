using System.Net;
using ApiGateway.Extensions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace ApiGateway.Tests;

public class GatewayResilienceTests
{
    /// <summary>Handler giả trả lần lượt các response cho trước và đếm số lần được gọi.</summary>
    private sealed class FakeBackend(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var next = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return Task.FromResult(next());
        }
    }

    private static HttpResponseMessage Status(HttpStatusCode code, string body = "") =>
        new(code) { Content = new StringContent(body) };

    private static ResiliencePipeline<HttpResponseMessage> NewPipeline()
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        GatewayResilience.Configure(builder);
        return builder.Build();
    }

    private static Task<HttpResponseMessage> Send(ResiliencePipeline<HttpResponseMessage> pipeline, FakeBackend backend, HttpMethod method) =>
        new HttpMessageInvoker(new PollyDelegatingHandler(pipeline, backend))
            .SendAsync(new HttpRequestMessage(method, "http://service/api/x"), CancellationToken.None);

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("TRACE", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    [InlineData("DELETE", false)]
    public void IsRetryableMethod_ChiMethodAnToan(string method, bool expected)
    {
        Assert.Equal(expected, GatewayResilience.IsRetryableMethod(new HttpMethod(method)));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.OK, false)]
    public void IsInfrastructureFailure_Chi502_503_504(HttpStatusCode code, bool expected)
    {
        Assert.Equal(expected, GatewayResilience.IsInfrastructureFailure(code));
    }

    [Fact]
    public async Task Post500_KhongRetryVaTraNguyenBodyLoi()
    {
        // Hồi quy: trước đây POST lỗi 500 bị retry => "Stream was already consumed" => client nhận 502 rỗng.
        var backend = new FakeBackend(() => Status(HttpStatusCode.InternalServerError, "{\"code\":\"INTERNAL_SERVER_ERROR\"}"));

        var res = await Send(NewPipeline(), backend, HttpMethod.Post);

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Equal("{\"code\":\"INTERNAL_SERVER_ERROR\"}", await res.Content.ReadAsStringAsync());
        Assert.Equal(1, backend.Calls);
    }

    [Fact]
    public async Task Post503_KhongRetry()
    {
        var backend = new FakeBackend(() => Status(HttpStatusCode.ServiceUnavailable));

        var res = await Send(NewPipeline(), backend, HttpMethod.Post);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Equal(1, backend.Calls);
    }

    [Fact]
    public async Task Get500_KhongRetryVi500LaLoiNghiepVu()
    {
        var backend = new FakeBackend(() => Status(HttpStatusCode.InternalServerError));

        await Send(NewPipeline(), backend, HttpMethod.Get);

        Assert.Equal(1, backend.Calls);
    }

    [Fact]
    public async Task Get503RoiThanhCong_RetryVaTraKetQuaThanhCong()
    {
        var backend = new FakeBackend(() => Status(HttpStatusCode.ServiceUnavailable), () => Status(HttpStatusCode.OK, "ok"));

        var res = await Send(NewPipeline(), backend, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(2, backend.Calls);
    }

    [Fact]
    public async Task LoiHaTangLienTuc_MoCircuitBreaker()
    {
        var pipeline = NewPipeline();
        var backend = new FakeBackend(() => Status(HttpStatusCode.ServiceUnavailable));

        // POST không retry => mỗi lần gửi là đúng một mẫu cho breaker (MinimumThroughput = 8, FailureRatio = 0.5)
        for (var i = 0; i < 8; i++)
            await Send(pipeline, backend, HttpMethod.Post);

        await Assert.ThrowsAsync<BrokenCircuitException>(() => Send(pipeline, backend, HttpMethod.Post));
        Assert.Equal(8, backend.Calls);
    }

    [Fact]
    public async Task Loi500LienTuc_KhongMoCircuitBreaker()
    {
        // 500 là lỗi do service trả về (kể cả lỗi nghiệp vụ như sai mật khẩu) => không được làm chặn cả service.
        var pipeline = NewPipeline();
        var backend = new FakeBackend(() => Status(HttpStatusCode.InternalServerError));

        for (var i = 0; i < 12; i++)
            Assert.Equal(HttpStatusCode.InternalServerError, (await Send(pipeline, backend, HttpMethod.Post)).StatusCode);
    }

    [Fact]
    public async Task MoiClusterCoBreakerRieng()
    {
        // Hồi quy: trước đây cả 4 service dùng chung một pipeline => một service lỗi làm chặn mọi service.
        var registry = new ResiliencePipelineRegistry<string>();
        var nhanSu = registry.GetOrAddPipeline<HttpResponseMessage>("yarp:nhansu-cluster", GatewayResilience.Configure);
        var chamCong = registry.GetOrAddPipeline<HttpResponseMessage>("yarp:chamcong-cluster", GatewayResilience.Configure);
        var hong = new FakeBackend(() => Status(HttpStatusCode.ServiceUnavailable));
        var khoe = new FakeBackend(() => Status(HttpStatusCode.OK));

        for (var i = 0; i < 8; i++)
            await Send(nhanSu, hong, HttpMethod.Post);

        await Assert.ThrowsAsync<BrokenCircuitException>(() => Send(nhanSu, hong, HttpMethod.Post));
        Assert.Equal(HttpStatusCode.OK, (await Send(chamCong, khoe, HttpMethod.Post)).StatusCode);
        Assert.Same(nhanSu, registry.GetOrAddPipeline<HttpResponseMessage>("yarp:nhansu-cluster", GatewayResilience.Configure));
    }
}
