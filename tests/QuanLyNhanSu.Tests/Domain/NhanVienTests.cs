using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;

namespace QuanLyNhanSu.Tests.Domain;

public class NhanVienTests
{
    [Fact]
    public void Create_HopLe_GanDayDuThongTin()
    {
        var phongBan = TestData.PhongBan();

        var nv = NhanVien.Create("NV001", "Nguyễn Văn A", TestData.NgaySinh, "Nam", "a@example.test", phongBan);

        Assert.Equal("NV001", nv.MaNhanVien);
        Assert.Equal("Nguyễn Văn A", nv.TenNhanVien);
        Assert.Equal("a@example.test", nv.Email);
        Assert.Same(phongBan, nv.PhongBan);
        Assert.NotEqual(Guid.Empty, nv.Id);
        Assert.Equal(0m, nv.LuongCoBan);
    }

    [Fact]
    public void Create_ThieuNhieuTruong_GomLoiTheoTungTruong()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            NhanVien.Create("", " ", TestData.NgaySinh, "", "", TestData.PhongBan()));

        Assert.Equal("REQUEST_VALIDATION_ERROR", ex.Code);
        Assert.NotNull(ex.Errors);
        Assert.Equal(new[] { "MaNhanVien", "TenNhanVien", "GioiTinh", "Email" }, ex.Errors!.Keys.ToArray());
    }

    [Fact]
    public void Create_NgaySinhTrongTuongLai_BaoLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            NhanVien.Create("NV001", "A", DateTime.Now.AddDays(1), "Nam", "a@example.test", TestData.PhongBan()));

        Assert.Equal("INVALID_DATE", ex.Code);
    }

    [Fact]
    public void Create_EmailKhongCoA_BaoLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            NhanVien.Create("NV001", "A", TestData.NgaySinh, "Nam", "khong-hop-le", TestData.PhongBan()));

        Assert.Equal("INVALID_EMAIL", ex.Code);
    }

    [Fact]
    public void Create_ThieuPhongBan_BaoLoi()
    {
        Assert.Throws<BadRequestException>(() =>
            NhanVien.Create("NV001", "A", TestData.NgaySinh, "Nam", "a@example.test", null!));
    }

    [Fact]
    public void UpdatePosition_GanChucVuVaPhuCapTheoChucVu()
    {
        var nv = TestData.NhanVien();
        var chucVu = TestData.ChucVu(phuCap: 2_500_000.5m);

        nv.UpdatePosition(chucVu);

        Assert.Same(chucVu, nv.ChucVu);
        Assert.Equal(2_500_000.5m, nv.PhuCap);
    }

    [Fact]
    public void UpdatePosition_Null_BaoLoi()
    {
        Assert.Throws<BadRequestException>(() => TestData.NhanVien().UpdatePosition(null!));
    }

    [Fact]
    public void CapNhatLuong_GiuDuSoLe()
    {
        var nv = TestData.NhanVien();

        nv.CapNhatLuong(15_000_000.75m);

        Assert.Equal(15_000_000.75m, nv.LuongCoBan);
    }

    [Fact]
    public void CapNhatLuong_Am_BaoLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() => TestData.NhanVien().CapNhatLuong(-1));
        Assert.Equal("INVALID_MONEY", ex.Code);
    }

    [Fact]
    public void TongLuong_BangLuongCoBanCongPhuCap()
    {
        var nv = TestData.NhanVien();
        nv.UpdatePosition(TestData.ChucVu(phuCap: 1_000_000m));
        nv.CapNhatLuong(9_000_000m);

        Assert.Equal(10_000_000m, nv.TongLuong);
    }

    [Fact]
    public void SetNhanVienLamViec_HopLe_GanTrangThaiVaNgayVao()
    {
        var nv = TestData.NhanVien();
        var tinhTrang = TestData.DangLamViec();
        var ngayVao = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        nv.SetNhanVienLamViec(tinhTrang, ngayVao);

        Assert.Same(tinhTrang, nv.TinhTrangNhanVien);
        Assert.Equal(ngayVao, nv.NgayVaoCongTy);
    }

    [Fact]
    public void SetNhanVienLamViec_TrangThaiKhongConCongTac_BaoLoi()
    {
        Assert.Throws<BadRequestException>(() =>
            TestData.NhanVien().SetNhanVienLamViec(TestData.DaNghiViec(), new DateTime(2020, 1, 1)));
    }

    [Fact]
    public void SetNhanVienLamViec_NgayVaoTruocNgaySinh_BaoLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            TestData.NhanVien().SetNhanVienLamViec(TestData.DangLamViec(), TestData.NgaySinh.AddDays(-1)));
        Assert.Equal("INVALID_DATE", ex.Code);
    }

    [Fact]
    public void SetNhanVienThoiViec_NhanVienDangLamViec_ChuyenSangThoiViec()
    {
        // Hồi quy: trước đây hàm kiểm tra trạng thái HIỆN TẠI thay vì trạng thái mới,
        // nên nhân viên đang làm việc không bao giờ cho thôi việc được.
        var nv = TestData.NhanVien();
        nv.SetNhanVienLamViec(TestData.DangLamViec(), new DateTime(2020, 1, 1));
        var daNghi = TestData.DaNghiViec();
        var ngayNghi = new DateTime(2024, 6, 30);

        nv.SetNhanVienThoiViec(daNghi, ngayNghi, "Chuyển công tác");

        Assert.Same(daNghi, nv.TinhTrangNhanVien);
        Assert.Equal(ngayNghi, nv.NgayThoiViec);
        Assert.Equal("Chuyển công tác", nv.LiDoThoiViec);
    }

    [Fact]
    public void SetNhanVienThoiViec_TrangThaiMoiVanConCongTac_BaoLoi()
    {
        var nv = TestData.NhanVien();
        nv.SetNhanVienLamViec(TestData.DangLamViec(), new DateTime(2020, 1, 1));

        Assert.Throws<BadRequestException>(() =>
            nv.SetNhanVienThoiViec(TestData.DangLamViec(), new DateTime(2024, 6, 30), "Lý do"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetNhanVienThoiViec_ThieuLyDo_BaoLoi(string lyDo)
    {
        var nv = TestData.NhanVien();
        nv.SetNhanVienLamViec(TestData.DangLamViec(), new DateTime(2020, 1, 1));

        Assert.Throws<BadRequestException>(() =>
            nv.SetNhanVienThoiViec(TestData.DaNghiViec(), new DateTime(2024, 6, 30), lyDo));
    }

    [Fact]
    public void SetNhanVienThoiViec_NgayNghiTruocNgayVao_BaoLoi()
    {
        var nv = TestData.NhanVien();
        nv.SetNhanVienLamViec(TestData.DangLamViec(), new DateTime(2020, 1, 1));

        var ex = Assert.Throws<BadRequestException>(() =>
            nv.SetNhanVienThoiViec(TestData.DaNghiViec(), new DateTime(2019, 12, 31), "Lý do"));
        Assert.Equal("INVALID_DATE", ex.Code);
    }
}
