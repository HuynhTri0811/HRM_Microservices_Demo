using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using HRM.Contracts.NhanSu;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.ChucVu;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.NhanSu;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Queries;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.API.Grpc
{
    /// <summary>
    /// gRPC server cho các service khác (ChamCong, QuyetDinh, TinhLuong) truy vấn / cập nhật dữ liệu nhân sự.
    /// Tái sử dụng các MediatR handler giống như REST controller.
    /// </summary>
    [Authorize]
    public class NhanSuGrpcService(IMediator mediator, ILogger<NhanSuGrpcService> logger) : NhanSuGrpc.NhanSuGrpcBase
    {
        public override async Task<NhanVienReply> GetNhanVienById(GetNhanVienByIdRequest request, ServerCallContext context)
        {
            var id = ParseGuid(request.Id, nameof(request.Id));
            var nhanVien = await mediator.Send(new GetEmployeeByIdQuery(id), context.CancellationToken);
            if (nhanVien == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Không tìm thấy nhân viên {id}"));

            return ToReply(nhanVien);
        }

        public override async Task<NhanVienListReply> GetAllNhanVien(GetAllNhanVienRequest request, ServerCallContext context)
        {
            var nhanViens = await mediator.Send(new GetAllEmployeesQuery(), context.CancellationToken);
            var reply = new NhanVienListReply();
            reply.Items.AddRange(nhanViens.Select(ToReply));
            return reply;
        }

        public override async Task<ChucVuReply> GetChucVuById(GetChucVuByIdRequest request, ServerCallContext context)
        {
            var id = ParseGuid(request.Id, nameof(request.Id));
            var chucVu = await mediator.Send(new GetChucVuByIdQuery(id), context.CancellationToken);
            if (chucVu == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Không tìm thấy chức vụ {id}"));

            return ToReply(chucVu);
        }

        public override Task<UpdateReply> UpdateLuong(UpdateLuongRequest request, ServerCallContext context)
        {
            var id = ParseGuid(request.NhanVienId, nameof(request.NhanVienId));
            var luong = ParseDecimal(request.LuongCoBan, nameof(request.LuongCoBan));

            return ExecuteUpdate(() => mediator.Send(new UpdateNhanVienCapNhatLuongDTO(id, luong), context.CancellationToken));
        }

        public override Task<UpdateReply> UpdateBoNhiem(UpdateBoNhiemRequest request, ServerCallContext context)
        {
            var id = ParseGuid(request.NhanVienId, nameof(request.NhanVienId));
            var chucVuMoi = ParseGuid(request.ChucVuMoi, nameof(request.ChucVuMoi));
            var phuCapMoi = ParseDecimal(request.PhuCapMoi, nameof(request.PhuCapMoi));

            return ExecuteUpdate(() => mediator.Send(new UpdateNhanVienBoNhiemDTO(id, chucVuMoi, phuCapMoi), context.CancellationToken));
        }

        private async Task<UpdateReply> ExecuteUpdate(Func<Task<bool>> action)
        {
            try
            {
                return new UpdateReply { Success = await action() };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Các handler cập nhật đang throw Exception thường (vd: "Không tìm thấy nhân viên"),
                // chuyển thành status gRPC để client nhận được thông điệp lỗi rõ ràng.
                logger.LogWarning(ex, "gRPC cập nhật nhân sự thất bại");
                throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
            }
        }

        private static NhanVienReply ToReply(NhanVienDto nv) => new()
        {
            Id = nv.Id.ToString(),
            MaNhanVien = nv.MaNhanVien ?? string.Empty,
            TenNhanVien = nv.TenNhanVien ?? string.Empty,
            NgaySinh = ToTimestamp(nv.NgaySinh),
            GioiTinh = nv.GioiTinh ?? string.Empty,
            Email = nv.Email ?? string.Empty,
            LuongCoBan = nv.LuongCoBan.ToString(CultureInfo.InvariantCulture),
            PhuCap = nv.PhuCap.ToString(CultureInfo.InvariantCulture),
            PhongBanId = nv.PhongBan?.Id.ToString() ?? string.Empty,
            ChucVuId = nv.ChucVu?.Id.ToString() ?? string.Empty
        };

        private static ChucVuReply ToReply(ChucVuDto cv) => new()
        {
            Id = cv.Id.ToString(),
            MaChucVu = cv.MaChucVu ?? string.Empty,
            TenChucVu = cv.TenChucVu ?? string.Empty,
            PhuCap = cv.PhuCap.ToString(CultureInfo.InvariantCulture)
        };

        private static Timestamp ToTimestamp(DateTime value) => Timestamp.FromDateTime(value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        });

        private static Guid ParseGuid(string value, string field) =>
            Guid.TryParse(value, out var id)
                ? id
                : throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} không phải Guid hợp lệ"));

        private static decimal ParseDecimal(string value, string field) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} không phải số hợp lệ"));
    }
}
