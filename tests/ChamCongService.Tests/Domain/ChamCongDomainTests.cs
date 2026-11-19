using ChamCongService.Domain.Entity;

namespace ChamCongService.Tests.Domain;

public class BangChamCongTheoThangTests
{
    private static readonly DateTime TuNgay = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DenNgay = new(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_HopLe_ChuaChot()
    {
        var bc = BangChamCongTheoThang.Create(11, 2026, TuNgay, DenNgay);

        Assert.Equal(11, bc.Thang);
        Assert.Equal(2026, bc.Nam);
        Assert.False(bc.IsChot);
    }

    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(11, 1999)]
    public void Create_ThangNamKhongHopLe_BaoLoi(int thang, int nam)
    {
        Assert.ThrowsAny<Exception>(() => BangChamCongTheoThang.Create(thang, nam, TuNgay, DenNgay));
    }

    [Fact]
    public void Create_TuNgayKhongNhoHonDenNgay_BaoLoi()
    {
        Assert.ThrowsAny<Exception>(() => BangChamCongTheoThang.Create(11, 2026, DenNgay, DenNgay));
    }

    [Fact]
    public void ChotRoiMoChot_DoiTrangThai()
    {
        var bc = BangChamCongTheoThang.Create(11, 2026, TuNgay, DenNgay);

        bc.ChotBangCong();
        Assert.True(bc.IsChot);

        bc.MoChotBangCong();
        Assert.False(bc.IsChot);
    }

    [Fact]
    public void CapNhat_DaChot_BaoLoi()
    {
        var bc = BangChamCongTheoThang.Create(11, 2026, TuNgay, DenNgay);
        bc.ChotBangCong();

        var ex = Assert.ThrowsAny<Exception>(() => bc.CapNhat(12, 2026, TuNgay, DenNgay));
        Assert.Contains("đã chốt", ex.Message);
    }

    [Fact]
    public void CapNhat_ChuaChot_DoiThongTin()
    {
        var bc = BangChamCongTheoThang.Create(11, 2026, TuNgay, DenNgay);

        bc.CapNhat(12, 2026, TuNgay.AddMonths(1), DenNgay.AddMonths(1));

        Assert.Equal(12, bc.Thang);
    }
}

public class LoaiChamCongTests
{
    [Fact]
    public void Create_HopLe()
    {
        var loai = LoaiChamCong.Create("NP", "Nghỉ phép", 1.5, HinhThucNghi.NghiTheoNgay);

        Assert.Equal("NP", loai.MaLoai);
        Assert.Equal(1.5, loai.HeSo);
        Assert.Equal(HinhThucNghi.NghiTheoNgay, loai.HinhThuc);
    }

    [Theory]
    [InlineData("", "Tên")]
    [InlineData("NP", " ")]
    public void Create_ThieuThongTin_BaoLoi(string ma, string ten)
    {
        Assert.ThrowsAny<Exception>(() => LoaiChamCong.Create(ma, ten, 1, HinhThucNghi.NghiTheoNgay));
    }
}

public class PhieuDangKyNghiTests
{
    private static readonly DateTime Ngay = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid NhanVien = Guid.NewGuid();
    private static readonly Guid Loai = Guid.NewGuid();

    [Fact]
    public void TheoGio_HopLe()
    {
        var p = PhieuDangKyNghiTheoGio.Create(Ngay, "Đi khám", NhanVien, Loai, TimeSpan.FromHours(8), TimeSpan.FromHours(10));

        Assert.Equal(TimeSpan.FromHours(8), p.TuGio);
        Assert.Equal(TimeSpan.FromHours(10), p.DenGio);
    }

    [Fact]
    public void TheoGio_GioBatDauSauGioKetThuc_BaoLoi()
    {
        Assert.ThrowsAny<Exception>(() =>
            PhieuDangKyNghiTheoGio.Create(Ngay, "Đi khám", NhanVien, Loai, TimeSpan.FromHours(10), TimeSpan.FromHours(8)));
    }

    [Fact]
    public void TheoBuoi_HopLe()
    {
        var p = PhieuDangKyNghiTheoBuoi.Create(Ngay, "Việc riêng", NhanVien, Loai, LoaiBuoi.Chieu);

        Assert.Equal(LoaiBuoi.Chieu, p.LoaiBuoi);
    }

    [Fact]
    public void TheoBuoi_GiaTriBuoiNgoaiEnum_BaoLoi()
    {
        Assert.ThrowsAny<Exception>(() => PhieuDangKyNghiTheoBuoi.Create(Ngay, "Việc riêng", NhanVien, Loai, (LoaiBuoi)99));
    }

    public static TheoryData<Func<PhieuDangKyNghi>> PhieuThieuDuLieu => new()
    {
        () => PhieuDangKyNghiTheoNgay.Create(Ngay, " ", NhanVien, Loai),
        () => PhieuDangKyNghiTheoNgay.Create(Ngay, "Lý do", Guid.Empty, Loai),
        () => PhieuDangKyNghiTheoNgay.Create(Ngay, "Lý do", NhanVien, Guid.Empty),
        () => PhieuDangKyNghiTheoNgay.Create(DateTime.MinValue, "Lý do", NhanVien, Loai),
        () => PhieuDangKyNghiTheoBuoi.Create(Ngay, "", NhanVien, Loai, LoaiBuoi.Sang),
        () => PhieuDangKyNghiTheoGio.Create(Ngay, "Lý do", Guid.Empty, Loai, TimeSpan.FromHours(8), TimeSpan.FromHours(9)),
    };

    [Theory]
    [MemberData(nameof(PhieuThieuDuLieu))]
    public void ThieuDuLieuBatBuoc_BaoLoi(Func<PhieuDangKyNghi> create)
    {
        Assert.ThrowsAny<Exception>(() => create());
    }
}
