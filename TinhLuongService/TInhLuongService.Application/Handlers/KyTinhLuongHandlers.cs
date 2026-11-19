using MediatR;
using TinhLuongService.Domain.Entities;
using TinhLuongService.Domain.Service.Interface;
using TinhLuongService.Domain.Repositories;
using TinhLuongService.Application.Command;
using TinhLuongService.Application.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TinhLuongService.Application.Handlers
{
    public class CreateKyTinhLuongHandler(IKyTinhLuongRepositorie repository) : IRequestHandler<CreateKyTinhLuongCommand, Guid>
    {
        public async Task<Guid> Handle(CreateKyTinhLuongCommand request, CancellationToken cancellationToken)
        {
            var entity = KyTinhLuong.Create(request.MaKy, request.NgayBatDau, request.NgayKetThuc);
            await repository.AddAsync(entity);
            await repository.SaveChangesAsync();
            return entity.Id;
        }
    }

    public class UpdateKyTinhLuongHandler(IKyTinhLuongRepositorie repository) : IRequestHandler<UpdateKyTinhLuongCommand, bool>
    {
        public async Task<bool> Handle(UpdateKyTinhLuongCommand request, CancellationToken cancellationToken)
        {
            var entity = await repository.GetByIdAsync(request.Id);
            if (entity == null) return false;

            entity.UpdateInfo(request.MaKy, request.NgayBatDau, request.NgayKetThuc);
            await repository.UpdateAsync(entity, request.UpdatedAt);
            await repository.SaveChangesAsync();
            return true;
        }
    }

    public class DeleteKyTinhLuongHandler(IKyTinhLuongRepositorie repository) : IRequestHandler<DeleteKyTinhLuongCommand, bool>
    {
        public async Task<bool> Handle(DeleteKyTinhLuongCommand request, CancellationToken cancellationToken)
        {
            var entity = await repository.GetByIdAsync(request.Id);
            if (entity == null) return false;

            await repository.DeleteAsync(request.Id);
            await repository.SaveChangesAsync();
            return true;
        }
    }

    public class KhoaKyTinhLuongHandler(IKyTinhLuongRepositorie repository) : IRequestHandler<KhoaKyTinhLuongCommand, bool>
    {
        public async Task<bool> Handle(KhoaKyTinhLuongCommand request, CancellationToken cancellationToken)
        {
            var entity = await repository.GetByIdAsync(request.Id);
            if (entity == null) return false;

            entity.KhoaKy();
            await repository.UpdateAsync(entity,request.UpdatedAt);
            await repository.SaveChangesAsync();
            return true;
        }
    }

    public class MoKyTinhLuongHandler(IKyTinhLuongRepositorie repository) : IRequestHandler<MoKyTinhLuongCommand, bool>
    {
        public async Task<bool> Handle(MoKyTinhLuongCommand request, CancellationToken cancellationToken)
        {
            var entity = await repository.GetByIdAsync(request.Id);
            if (entity == null) return false;

            entity.MoKy();
            await repository.UpdateAsync(entity,request.UpdatedAt);
            await repository.SaveChangesAsync();
            return true;
        }
    }

    public class TinhLuongHandler(ITinhLuongService tinhLuongService
        , IKyTinhLuongRepositorie repository
        , INhanVienTinhLuongRepositorie nhanVienTinhLuongRepository
        , INhanSuServiceClient nhanSuServiceClient
        , IHttpContextAccessor httpContextAccessor
        , ILogger<TinhLuongHandler> logger) : IRequestHandler<TinhLuongCommand, bool>
    {
        public async Task<bool> Handle(TinhLuongCommand request, CancellationToken cancellationToken)
        {
            var entity = await repository.GetByIdAsync(request.Id);
            if (entity == null) return false;

            var token = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            var nhanViens = await nhanSuServiceClient.GetAllNhanVienAsync(token ?? "");

            // NhanVienTinhLuong.Create yêu cầu LuongCoBan > 0. Nhân viên chưa có lương (vd vừa tạo, lương = 0)
            // được bỏ qua kèm cảnh báo thay vì làm hỏng cả lần tính lương (trước đây ném ArgumentException -> 500).
            var nhanVienHopLe = nhanViens.Where(nv => nv.LuongCoBan > 0).ToList();
            foreach (var nv in nhanViens.Where(nv => nv.LuongCoBan <= 0))
            {
                logger.LogWarning("Bỏ qua nhân viên {MaNhanVien} ({NhanVienId}) khi tính lương kỳ {MaKy}: lương cơ bản = {LuongCoBan}",
                    nv.MaNhanVien, nv.Id, entity.MaKy, nv.LuongCoBan);
            }

            // Tính lại kỳ: xoá mềm kết quả cũ để không bị trùng dòng khi tính nhiều lần
            foreach (var cu in (entity.NhanVienTinhLuongs ?? new List<NhanVienTinhLuong>()).ToList())
            {
                await nhanVienTinhLuongRepository.DeleteAsync(cu.Id);
            }

            var nhanVienTinhLuongs = nhanVienHopLe.Select(nv => NhanVienTinhLuong.Create(
                nv.Id,
                entity.Id,
                nv.LuongCoBan,
                0, // PhuCap
                0, // Thuong
                0, // Phat
                nv.LuongCoBan, // TongLuong
                0, // Thue
                nv.LuongCoBan // LuongThucLanh
            )).ToList();

            // Thêm tường minh (state Added -> INSERT) trước khi gắn vào kỳ lương
            foreach (var nvtl in nhanVienTinhLuongs)
            {
                await nhanVienTinhLuongRepository.AddAsync(nvtl);
            }

            tinhLuongService.TinhLuong(entity, nhanVienTinhLuongs);
            await repository.UpdateAsync(entity, request.UpdatedAt);
            await repository.SaveChangesAsync();
            return true;
        }
    }
}
