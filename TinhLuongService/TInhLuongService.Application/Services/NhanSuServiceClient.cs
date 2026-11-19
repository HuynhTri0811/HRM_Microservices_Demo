using System.Globalization;
using Grpc.Core;
using HRM.Contracts.NhanSu;
using Microsoft.Extensions.Logging;
using TinhLuongService.Application.Services.DTO;
using TinhLuongService.Application.Services.Interface;

namespace TinhLuongService.Application.Services
{
    /// <summary>
    /// Gọi sang NhanSu service qua gRPC (Protos/nhansu.proto).
    /// </summary>
    public class NhanSuServiceClient : INhanSuServiceClient
    {
        private readonly NhanSuGrpc.NhanSuGrpcClient _grpcClient;
        private readonly ILogger<NhanSuServiceClient> _logger;

        public NhanSuServiceClient(NhanSuGrpc.NhanSuGrpcClient grpcClient, ILogger<NhanSuServiceClient> logger)
        {
            _grpcClient = grpcClient;
            _logger = logger;
        }

        public async Task<IEnumerable<NhanVienServiceClientDto>> GetAllNhanVienAsync(string token)
        {
            try
            {
                var reply = await _grpcClient.GetAllNhanVienAsync(new GetAllNhanVienRequest(), BuildHeaders(token));

                return reply.Items.Select(nv => new NhanVienServiceClientDto(
                    Guid.Parse(nv.Id),
                    nv.MaNhanVien,
                    nv.TenNhanVien,
                    nv.Email,
                    ParseDecimal(nv.LuongCoBan))).ToList();
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC GetAllNhanVien tới NhanSu thất bại. Status: {Status}, Detail: {Detail}", ex.StatusCode, ex.Status.Detail);
                return Enumerable.Empty<NhanVienServiceClientDto>();
            }
        }

        private static decimal ParseDecimal(string value) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0m;

        private static Metadata BuildHeaders(string token)
        {
            var headers = new Metadata();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var actualToken = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? token[7..] : token;
                headers.Add("Authorization", $"Bearer {actualToken}");
            }
            return headers;
        }
    }
}
