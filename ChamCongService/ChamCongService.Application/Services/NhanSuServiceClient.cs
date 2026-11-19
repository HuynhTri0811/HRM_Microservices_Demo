using ChamCongService.Application.Services.DTO;
using Grpc.Core;
using HRM.Contracts.NhanSu;

namespace ChamCongService.Application.Services
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

        public async Task<NhanVienServiceClientDto?> GetNhanVienByIdAsync(Guid nhanVienId, string token)
        {
            try
            {
                var reply = await _grpcClient.GetNhanVienByIdAsync(
                    new GetNhanVienByIdRequest { Id = nhanVienId.ToString() },
                    BuildHeaders(token));

                return new NhanVienServiceClientDto(
                    Guid.Parse(reply.Id),
                    reply.MaNhanVien,
                    reply.TenNhanVien,
                    reply.Email);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC GetNhanVienById tới NhanSu thất bại. Status: {Status}, Detail: {Detail}", ex.StatusCode, ex.Status.Detail);
                return null;
            }
        }

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
