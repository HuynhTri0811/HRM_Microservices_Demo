using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Timeout;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace ApiGateway.Extensions
{
    public static class YarpExtensions
    {
        public static void ConfigureYarp(this WebApplicationBuilder builder)
        {
            builder.Services.AddHttpClient();

            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            // Mỗi cluster (service) có một pipeline riêng => circuit breaker riêng.
            // Lỗi ở một service không làm chặn request tới các service khác.
            builder.Services.AddSingleton(new ResiliencePipelineRegistry<string>());
            builder.Services.AddSingleton<IForwarderHttpClientFactory, ResilientForwarderHttpClientFactory>();
        }

        public static void UseYarp(this WebApplication app)
        {
            app.MapReverseProxy();
        }
    }

    // ---------------------------------------------------------------------
    // CÁC LỚP HỖ TRỢ ĐĂNG KÝ RESILIENCE CHO YARP FORWARDER HTTP CLIENT
    // ---------------------------------------------------------------------
    public static class GatewayResilience
    {
        /// <summary>
        /// Đánh dấu request hiện tại có được phép retry hay không (chỉ method idempotent, không có body).
        /// </summary>
        public static readonly ResiliencePropertyKey<bool> CanRetryKey = new("Gateway.CanRetry");

        /// <summary>
        /// Chỉ retry các method an toàn: GET/HEAD/OPTIONS/TRACE.
        /// POST/PATCH không idempotent (retry có thể tạo bản ghi trùng); PUT/DELETE cũng không retry vì
        /// body request do YARP stream thẳng từ client chỉ đọc được một lần (retry => "Stream was already consumed").
        /// </summary>
        public static bool IsRetryableMethod(HttpMethod method) =>
            method == HttpMethod.Get
            || method == HttpMethod.Head
            || method == HttpMethod.Options
            || method == HttpMethod.Trace;

        /// <summary>
        /// Lỗi hạ tầng: không kết nối được / timeout / 502-503-504.
        /// 500 là lỗi do chính service trả về (kể cả lỗi nghiệp vụ) => trả nguyên cho client,
        /// không retry và không tính vào circuit breaker.
        /// </summary>
        public static bool IsInfrastructureFailure(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.ServiceUnavailable
            || statusCode == HttpStatusCode.GatewayTimeout;

        public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder)
        {
            builder
                .AddRetry(new()
                {
                    ShouldHandle = args =>
                    {
                        if (!args.Context.Properties.GetValue(CanRetryKey, false))
                            return PredicateResult.False();

                        return ValueTask.FromResult(args.Outcome switch
                        {
                            { Exception: HttpRequestException } => true,
                            { Result: { } r } => IsInfrastructureFailure(r.StatusCode),
                            _ => false
                        });
                    },
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new()
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(r => IsInfrastructureFailure(r.StatusCode)),

                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(10),

                    MinimumThroughput = 8,

                    BreakDuration = TimeSpan.FromSeconds(30)
                })
                .AddTimeout(TimeSpan.FromSeconds(5));
        }
    }

    public class ResilientForwarderHttpClientFactory : ForwarderHttpClientFactory
    {
        private readonly ResiliencePipelineRegistry<string> _registry;

        public ResilientForwarderHttpClientFactory(ResiliencePipelineRegistry<string> registry)
        {
            _registry = registry;
        }

        protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
        {
            var baseHandler = base.WrapHandler(context, handler);

            // Lấy (hoặc tạo) pipeline theo ClusterId; registry giữ nguyên trạng thái breaker
            // kể cả khi YARP tạo lại HttpClient do cấu hình cluster thay đổi.
            var pipeline = _registry.GetOrAddPipeline<HttpResponseMessage>(
                $"yarp:{context.ClusterId}",
                GatewayResilience.Configure);

            return new PollyDelegatingHandler(pipeline, baseHandler);
        }
    }

    public class PollyDelegatingHandler : DelegatingHandler
    {
        private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

        public PollyDelegatingHandler(ResiliencePipeline<HttpResponseMessage> pipeline, HttpMessageHandler innerHandler)
        {
            _pipeline = pipeline;
            InnerHandler = innerHandler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var context = ResilienceContextPool.Shared.Get(cancellationToken);
            context.Properties.Set(GatewayResilience.CanRetryKey, GatewayResilience.IsRetryableMethod(request.Method));
            try
            {
                return await _pipeline.ExecuteAsync(
                    static async (ctx, state) => await state.Handler.BaseSend(state.Request, ctx.CancellationToken),
                    context,
                    (Handler: this, Request: request));
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }

        private Task<HttpResponseMessage> BaseSend(HttpRequestMessage request, CancellationToken cancellationToken)
            => base.SendAsync(request, cancellationToken);
    }
}
