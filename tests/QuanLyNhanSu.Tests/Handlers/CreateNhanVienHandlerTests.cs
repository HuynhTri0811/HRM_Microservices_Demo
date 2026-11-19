using MassTransit;
using QuanLyNhanSuMicroservice.Core.Repositories;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.NhanSu;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Events;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Handlers.NhanVienHandler;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Repositories;

namespace QuanLyNhanSu.Tests.Handlers;

public class CreateNhanVienHandlerTests
{
    private readonly Mock<INhanVienRepository> _nhanVienRepo = new();
    private readonly Mock<IPhongBanRepository> _phongBanRepo = new();
    private readonly Mock<IChucVuRepository> _chucVuRepo = new();
    private readonly Mock<ITinhTrangNhanVienRepository> _tinhTrangRepo = new();
    private readonly Mock<IPublishEndpoint> _publish = new();

    private readonly PhongBan _phongBan = TestData.PhongBan();
    private readonly ChucVu _chucVu = TestData.ChucVu(phuCap: 1_200_000m);
    private readonly TinhTrangNhanVien _dangLamViec = TestData.DangLamViec();
    private NhanVien? _added;

    public CreateNhanVienHandlerTests()
    {
        _nhanVienRepo.Setup(r => r.IsEmployeeCodeUniqueAsync(It.IsAny<string>())).ReturnsAsync(true);
        _nhanVienRepo.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>())).ReturnsAsync(true);
        _nhanVienRepo.Setup(r => r.AddAsync(It.IsAny<NhanVien>())).Callback<NhanVien>(nv => _added = nv).Returns(Task.CompletedTask);
        _phongBanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(_phongBan);
        _chucVuRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(_chucVu);
        _tinhTrangRepo.Setup(r => r.GetByIdAsync(TinhTrangNhanVien.DangLamViecId)).ReturnsAsync(_dangLamViec);
    }

    private CreateNhanVienHandler Handler() =>
        new(_nhanVienRepo.Object, _phongBanRepo.Object, _chucVuRepo.Object, _tinhTrangRepo.Object, _publish.Object);

    private static CreateNhanVienDto Request(DateTime? ngayVao = null) =>
        new("NV001", "Nguyễn Văn A", TestData.NgaySinh, "Nam", "a@example.test", Guid.NewGuid(), Guid.NewGuid(), ngayVao);

    [Fact]
    public async Task HopLe_LuuNhanVienVoiTrangThaiDangLamViec()
    {
        var id = await Handler().Handle(Request(), CancellationToken.None);

        Assert.NotNull(_added);
        Assert.Equal(id, _added!.Id);
        // Hồi quy: trước đây không gán trạng thái => lỗi khoá ngoại khi lưu trên DB mới
        Assert.Same(_dangLamViec, _added.TinhTrangNhanVien);
        Assert.Same(_chucVu, _added.ChucVu);
        Assert.Equal(1_200_000m, _added.PhuCap);
        _nhanVienRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task KhongTruyenNgayVao_DungNgayHomNayDangUtc()
    {
        await Handler().Handle(Request(), CancellationToken.None);

        Assert.Equal(DateTime.Now.Date, _added!.NgayVaoCongTy.Date);
        Assert.Equal(DateTimeKind.Utc, _added.NgayVaoCongTy.Kind);
        Assert.Equal(TimeSpan.Zero, _added.NgayVaoCongTy.TimeOfDay);
    }

    [Fact]
    public async Task CoNgayVao_ChiGiuPhanNgay()
    {
        await Handler().Handle(Request(new DateTime(2021, 3, 15, 14, 30, 0)), CancellationToken.None);

        Assert.Equal(new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc), _added!.NgayVaoCongTy);
    }

    [Fact]
    public async Task HopLe_PhatEventNhanVienCreated()
    {
        await Handler().Handle(Request(), CancellationToken.None);

        _publish.Verify(p => p.Publish(
            It.Is<NhanVienCreatedEvent>(e => e.MaNhanVien == "NV001" && e.Email == "a@example.test" && e.PhuCap == 1_200_000m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrungMaNhanVien_BaoIsExist()
    {
        _nhanVienRepo.Setup(r => r.IsEmployeeCodeUniqueAsync("NV001")).ReturnsAsync(false);

        await Assert.ThrowsAsync<IsExistException>(() => Handler().Handle(Request(), CancellationToken.None));
        _nhanVienRepo.Verify(r => r.AddAsync(It.IsAny<NhanVien>()), Times.Never);
    }

    [Fact]
    public async Task TrungEmail_BaoIsExist()
    {
        _nhanVienRepo.Setup(r => r.IsEmailUniqueAsync("a@example.test")).ReturnsAsync(false);

        await Assert.ThrowsAsync<IsExistException>(() => Handler().Handle(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task KhongCoPhongBan_BaoNotFound()
    {
        _phongBanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((PhongBan?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task KhongCoChucVu_BaoNotFound()
    {
        _chucVuRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ChucVu?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task ChuaSeedTrangThai_BaoNotFoundVaKhongLuu()
    {
        _tinhTrangRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((TinhTrangNhanVien?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(Request(), CancellationToken.None));
        _nhanVienRepo.Verify(r => r.AddAsync(It.IsAny<NhanVien>()), Times.Never);
        _publish.Verify(p => p.Publish(It.IsAny<NhanVienCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NgayVaoTruocNgaySinh_BaoBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            Handler().Handle(Request(TestData.NgaySinh.AddYears(-1)), CancellationToken.None));
    }
}
