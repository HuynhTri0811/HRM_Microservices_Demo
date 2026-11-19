using System.Globalization;
using Grpc.Core;
using HRM.Contracts.NhanSu;
using QuyetDinhService.QuyetDinhService.Application.Services.DTO;

namespace QuyetDinhService.QuyetDinhService.Application.Services
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

        public async Task<bool> UpdateLuongAsync(Guid nhanVienId, decimal luongMoi, string token)
        {
            try
            {
                var reply = await _grpcClient.UpdateLuongAsync(new UpdateLuongRequest
                {
                    NhanVienId = nhanVienId.ToString(),
                    LuongCoBan = luongMoi.ToString(CultureInfo.InvariantCulture)
                }, BuildHeaders(token));

                return reply.Success;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC UpdateLuong tới NhanSu thất bại. Status: {Status}, Detail: {Detail}", ex.StatusCode, ex.Status.Detail);
                return false;
            }
        }

        public async Task<bool> UpdateBoNhiemAsync(Guid nhanVienId, Guid chucVuMoi, decimal phuCapMoi, string token)
        {
            try
            {
                var reply = await _grpcClient.UpdateBoNhiemAsync(new UpdateBoNhiemRequest
                {
                    NhanVienId = nhanVienId.ToString(),
                    ChucVuMoi = chucVuMoi.ToString(),
                    PhuCapMoi = phuCapMoi.ToString(CultureInfo.InvariantCulture)
                }, BuildHeaders(token));

                return reply.Success;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC UpdateBoNhiem tới NhanSu thất bại. Status: {Status}, Detail: {Detail}", ex.StatusCode, ex.Status.Detail);
                return false;
            }
        }

        public async Task<NhanVienServiceClientDto?> GetNhanVienByIdAsync(Guid nhanVienId, string token)
        {
            try
            {
                var reply = await _grpcClient.GetNhanVienByIdAsync(
                    new GetNhanVienByIdRequest { Id = nhanVienId.ToString() },
                    BuildHeaders(token));

                return new NhanVienServiceClientDto
                {
                    Id = Guid.Parse(reply.Id),
                    MaNhanVien = reply.MaNhanVien,
                    TenNhanVien = reply.TenNhanVien,
                    NgaySinh = reply.NgaySinh?.ToDateTime() ?? default,
                    GioiTinh = reply.GioiTinh,
                    Email = reply.Email,
                    LuongCoBan = ParseDecimal(reply.LuongCoBan),
                    PhuCap = ParseDecimal(reply.PhuCap)
                };
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

        public async Task<ChucVuServiceClientDto?> GetChucVuByIdAsync(Guid chucVuId, string token)
        {
            try
            {
                var reply = await _grpcClient.GetChucVuByIdAsync(
                    new GetChucVuByIdRequest { Id = chucVuId.ToString() },
                    BuildHeaders(token));

                return new ChucVuServiceClientDto
                {
                    Id = Guid.Parse(reply.Id),
                    MaChucVu = reply.MaChucVu,
                    TenChucVu = reply.TenChucVu,
                    PhuCap = ParseDecimal(reply.PhuCap)
                };
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC GetChucVuById tới NhanSu thất bại. Status: {Status}, Detail: {Detail}", ex.StatusCode, ex.Status.Detail);
                return null;
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
