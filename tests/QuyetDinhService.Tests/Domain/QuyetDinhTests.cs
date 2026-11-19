using QuyetDinhService.Domain.Entities;
using QuyetDinhService.Domain.Exceptions;

namespace QuyetDinhService.Tests.Domain;

public class QuyetDinhBoNhiemTests
{
    private static readonly DateTime Ngay = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_HopLe()
    {
        var qd = QuyetDinhBoNhiem.Create("QD-01", Ngay, "Bổ nhiệm", Ngay.AddDays(1));

        Assert.Equal("QD-01", qd.SoQuyetDinh);
        Assert.Equal(Ngay.AddDays(1), qd.NgayHieuLuc);
    }

    [Fact]
    public void Create_ThieuSoVaNoiDung_GomLoi()
    {
        var ex = Assert.Throws<BadRequestException>(() => QuyetDinhBoNhiem.Create("", Ngay, " ", Ngay));

        Assert.Equal(new[] { "SoQuyetDinh", "NoiDung" }, ex.Errors!.Keys.ToArray());
    }

    [Fact]
    public void BoNhiem_HopLe_GanChucVuVaPhuCap()
    {
        var qd = QuyetDinhBoNhiem.Create("QD-01", Ngay, "Bổ nhiệm", Ngay);
        var nv = Guid.NewGuid(); var cu = Guid.NewGuid(); var moi = Guid.NewGuid();

        qd.BoNhiem(nv, cu, moi, 1_000_000m, 5_000_000.5m, "Thăng chức");

        Assert.Equal(nv, qd.MaNhanVien);
        Assert.Equal(cu, qd.ChucVuCu);
        Assert.Equal(moi, qd.ChucVuMoi);
        Assert.Equal(1_000_000m, qd.PhuCapCu);
        Assert.Equal(5_000_000.5m, qd.PhuCapMoi);
        Assert.Equal("Thăng chức", qd.LyDo);
    }

    [Fact]
    public void BoNhiem_DuLieuSai_GomTatCaLoi()
    {
        var qd = QuyetDinhBoNhiem.Create("QD-01", Ngay, "Bổ nhiệm", Ngay);

        var ex = Assert.Throws<BadRequestException>(() => qd.BoNhiem(Guid.Empty, Guid.Empty, Guid.NewGuid(), -1, -1, ""));

        Assert.Equal(new[] { "MaNhanVien", "ChucVu", "PhuCapCu", "PhuCapMoi" }, ex.Errors!.Keys.ToArray());
    }
}

public class QuyetDinhNangLuongTests
{
    private static readonly DateTime Ngay = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CapNhatLuongCoBan_HopLe()
    {
        var qd = QuyetDinhNangLuong.Create("QD-NL-01", Ngay, "Nâng lương", Ngay);
        var nv = Guid.NewGuid();

        qd.CapNhatLuongCoBan(nv, 10_000_000m, 15_000_000.75m);

        Assert.Equal(nv, qd.MaNhanVien);
        Assert.Equal(10_000_000m, qd.LuongCoBanCu);
        Assert.Equal(15_000_000.75m, qd.LuongCoBanMoi);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void CapNhatLuongCoBan_LuongAm_BaoLoi(int cu, int moi)
    {
        var qd = QuyetDinhNangLuong.Create("QD-NL-01", Ngay, "Nâng lương", Ngay);

        var ex = Assert.Throws<BadRequestException>(() => qd.CapNhatLuongCoBan(Guid.NewGuid(), cu, moi));
        Assert.True(ex.Errors!.ContainsKey("LuongCoBan"));
    }

    [Fact]
    public void CapNhatLuongCoBan_ThieuNhanVien_BaoLoi()
    {
        var qd = QuyetDinhNangLuong.Create("QD-NL-01", Ngay, "Nâng lương", Ngay);

        var ex = Assert.Throws<BadRequestException>(() => qd.CapNhatLuongCoBan(Guid.Empty, 1, 2));
        Assert.True(ex.Errors!.ContainsKey("MaNhanVien"));
    }
}
