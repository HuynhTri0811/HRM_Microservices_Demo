using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;

namespace QuanLyNhanSu.Tests.Domain;

public class ChucVuTests
{
    [Fact]
    public void Create_HopLe()
    {
        var cv = ChucVu.Create("TP", "Trưởng phòng", 5_000_000.5m);

        Assert.Equal("TP", cv.MaChucVu);
        Assert.Equal("Trưởng phòng", cv.TenChucVu);
        Assert.Equal(5_000_000.5m, cv.PhuCap);
    }

    [Theory]
    [InlineData("", "Tên")]
    [InlineData("TP", " ")]
    public void Create_ThieuMaHoacTen_BaoLoi(string ma, string ten)
    {
        var ex = Assert.Throws<BadRequestException>(() => ChucVu.Create(ma, ten, 0));
        Assert.Equal("REQUEST_VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public void Create_PhuCapAm_BaoLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() => ChucVu.Create("TP", "Trưởng phòng", -1));
        Assert.Equal("INVALID_MONEY", ex.Code);
    }

    [Fact]
    public void SetPhuCap_Am_BaoLoi()
    {
        Assert.Throws<BadRequestException>(() => TestData.ChucVu().SetPhuCap(-0.01m));
    }

    [Fact]
    public void Update_DoiMaVaTen()
    {
        var cv = TestData.ChucVu();

        cv.Update("PP", "Phó phòng");

        Assert.Equal("PP", cv.MaChucVu);
        Assert.Equal("Phó phòng", cv.TenChucVu);
    }
}

public class PhongBanTests
{
    [Fact]
    public void Create_ThieuCaHaiTruong_GomLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() => PhongBan.Create("", ""));

        Assert.Equal(new[] { "MaQuanLy", "TenPhongBan" }, ex.Errors!.Keys.ToArray());
    }

    [Fact]
    public void CapNhat_HopLe_DoiThongTin()
    {
        var pb = TestData.PhongBan();

        pb.CapNhat("PB02", "Kinh doanh");

        Assert.Equal("PB02", pb.MaQuanLy);
        Assert.Equal("Kinh doanh", pb.TenPhongBan);
    }

    [Fact]
    public void Delete_DanhDauXoaMem()
    {
        var pb = TestData.PhongBan();

        pb.Delete();

        Assert.True(pb.IsDeleted);
        Assert.NotNull(pb.DeletedAt);
    }
}

public class TinhTrangNhanVienTests
{
    [Fact]
    public void IdMacDinh_KhacNhauVaKhongRong()
    {
        Assert.NotEqual(Guid.Empty, TinhTrangNhanVien.DangLamViecId);
        Assert.NotEqual(TinhTrangNhanVien.DangLamViecId, TinhTrangNhanVien.DaNghiViecId);
    }

    [Theory]
    [InlineData("", "Tên")]
    [InlineData("MA", "")]
    public void Create_ThieuThongTin_BaoLoi(string ma, string ten)
    {
        Assert.ThrowsAny<Exception>(() => TinhTrangNhanVien.Create(ma, ten, false));
    }
}
