using ChamCongService.Application.Commands;
using ChamCongService.Application.Events;
using ChamCongService.Application.Handlers;
using ChamCongService.Application.Services;
using ChamCongService.Application.Services.DTO;
using ChamCongService.Domain.Entity;
using ChamCongService.Domain.Repositories;
using MassTransit;
using Microsoft.AspNetCore.Http;

namespace ChamCongService.Tests.Handlers;

public class CreatePhieuDangKyNghiHandlerTests
{
    private static readonly DateTime Ngay = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPhieuDangKyNghiRepository> _repo = new();
    private readonly Mock<ILoaiChamCongRepository> _loaiRepo = new();
    private readonly Mock<INhanSuServiceClient> _nhanSu = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IPublishEndpoint> _publish = new();
    private readonly Guid _nhanVienId = Guid.NewGuid();
    private PhieuDangKyNghi? _saved;

    public CreatePhieuDangKyNghiHandlerTests()
    {
        _nhanSu.Setup(c => c.GetNhanVienByIdAsync(_nhanVienId, It.IsAny<string>()))
            .ReturnsAsync(new NhanVienServiceClientDto(_nhanVienId, "NV001", "Nguyễn Văn A", "a@example.test"));
        _repo.Setup(r => r.AddAsync(It.IsAny<PhieuDangKyNghi>())).Callback<PhieuDangKyNghi>(p => _saved = p).Returns(Task.CompletedTask);
    }

    private PhieuDangKyNghiHandlers Handler() => new(_repo.Object, _loaiRepo.Object, _nhanSu.Object, _http.Object, _publish.Object);

    private LoaiChamCong SeedLoai(HinhThucNghi hinhThuc)
    {
        var loai = LoaiChamCong.Create("L", "Loại", 1, hinhThuc);
        _loaiRepo.Setup(r => r.GetByIdAsync(loai.Id)).ReturnsAsync(loai);
        return loai;
    }

    [Fact]
    public async Task NghiTheoNgay_TaoDungLoaiPhieuVaPhatEvent()
    {
        var loai = SeedLoai(HinhThucNghi.NghiTheoNgay);

        var id = await Handler().Handle(new CreatePhieuDangKyNghiCommand(Ngay, "Nghỉ phép", _nhanVienId, loai.Id), CancellationToken.None);

        Assert.IsType<PhieuDangKyNghiTheoNgay>(_saved);
        Assert.Equal(id, _saved!.Id);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _publish.Verify(p => p.Publish(
            It.Is<PhieuDangKyNghiCreatedEvent>(e => e.Id == id && e.MaNhanVien == _nhanVienId && e.LoaiChamCongId == loai.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NghiTheoGio_GanGioTuRequest()
    {
        var loai = SeedLoai(HinhThucNghi.NghiTheoGio);

        await Handler().Handle(new CreatePhieuDangKyNghiCommand(Ngay, "Đi khám", _nhanVienId, loai.Id, TimeSpan.FromHours(8), TimeSpan.FromHours(10)), CancellationToken.None);

        var phieu = Assert.IsType<PhieuDangKyNghiTheoGio>(_saved);
        Assert.Equal(TimeSpan.FromHours(8), phieu.TuGio);
        Assert.Equal(TimeSpan.FromHours(10), phieu.DenGio);
    }

    [Fact]
    public async Task NghiTheoBuoi_KhongTruyenBuoi_MacDinhBuoiSang()
    {
        var loai = SeedLoai(HinhThucNghi.NghiTheoBuoi);

        await Handler().Handle(new CreatePhieuDangKyNghiCommand(Ngay, "Việc riêng", _nhanVienId, loai.Id), CancellationToken.None);

        Assert.Equal(LoaiBuoi.Sang, Assert.IsType<PhieuDangKyNghiTheoBuoi>(_saved).LoaiBuoi);
    }

    [Fact]
    public async Task NhanVienKhongTonTaiBenNhanSu_BaoLoiVaKhongLuu()
    {
        var loai = SeedLoai(HinhThucNghi.NghiTheoNgay);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(new CreatePhieuDangKyNghiCommand(Ngay, "Nghỉ", Guid.NewGuid(), loai.Id), CancellationToken.None));

        Assert.Contains("Nhân viên không tồn tại", ex.Message);
        Assert.Null(_saved);
    }

    [Fact]
    public async Task LoaiChamCongKhongTonTai_BaoLoi()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(new CreatePhieuDangKyNghiCommand(Ngay, "Nghỉ", _nhanVienId, Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("Loại chấm công không tồn tại", ex.Message);
        Assert.Null(_saved);
    }
}
