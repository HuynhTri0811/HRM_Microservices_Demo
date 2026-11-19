using TinhLuongService.Domain.Entities;
using DomainTinhLuongService = TinhLuongService.Domain.Service.TinhLuongService;

namespace TinhLuongService.Tests.Domain;

public class KyTinhLuongTests
{
    private static readonly DateTime BatDau = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime KetThuc = new(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_HopLe_MacDinhDaKhoa()
    {
        var ky = KyTinhLuong.Create("2026-11", BatDau, KetThuc);

        Assert.Equal("2026-11", ky.MaKy);
        Assert.True(ky.ChotTinhLuong);
        Assert.Empty(ky.NhanVienTinhLuongs!);
    }

    [Fact]
    public void Create_NgayBatDauSauNgayKetThuc_BaoLoi()
    {
        Assert.ThrowsAny<Exception>(() => KyTinhLuong.Create("2026-11", KetThuc, BatDau));
    }

    [Fact]
    public void Create_ThieuMaKy_BaoLoi()
    {
        Assert.ThrowsAny<Exception>(() => KyTinhLuong.Create("", BatDau, KetThuc));
    }

    [Fact]
    public void UpdateInfo_KyDangKhoa_BaoLoi()
    {
        var ky = KyTinhLuong.Create("2026-11", BatDau, KetThuc);

        var ex = Assert.ThrowsAny<Exception>(() => ky.UpdateInfo("2026-12", BatDau, KetThuc));
        Assert.Contains("đã chốt", ex.Message);
    }

    [Fact]
    public void UpdateInfo_SauKhiMoKy_DoiThongTin()
    {
        var ky = KyTinhLuong.Create("2026-11", BatDau, KetThuc);
        ky.MoKy();

        ky.UpdateInfo("2026-12", BatDau.AddMonths(1), KetThuc.AddMonths(1));

        Assert.Equal("2026-12", ky.MaKy);
        Assert.False(ky.ChotTinhLuong);
    }

    [Fact]
    public void KhoaVaMoKy_DoiTrangThai()
    {
        var ky = KyTinhLuong.Create("2026-11", BatDau, KetThuc);

        ky.MoKy();
        Assert.False(ky.ChotTinhLuong);
        ky.KhoaKy();
        Assert.True(ky.ChotTinhLuong);
    }
}

public class NhanVienTinhLuongTests
{
    [Fact]
    public void Create_HopLe()
    {
        var nv = Guid.NewGuid(); var ky = Guid.NewGuid();

        var row = NhanVienTinhLuong.Create(nv, ky, 15_000_000.75m, 0, 0, 0, 15_000_000.75m, 0, 15_000_000.75m);

        Assert.Equal(nv, row.NhanVienId);
        Assert.Equal(ky, row.KyTinhLuongId);
        Assert.Equal(15_000_000.75m, row.LuongThucLanh);
        Assert.NotEqual(Guid.Empty, row.Id);
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 0)]
    [InlineData(false, false, -1)]
    public void Create_DuLieuSai_BaoArgumentException(bool nhanVienRong, bool kyRong, int luong)
    {
        Assert.Throws<ArgumentException>(() => NhanVienTinhLuong.Create(
            nhanVienRong ? Guid.Empty : Guid.NewGuid(), kyRong ? Guid.Empty : Guid.NewGuid(), luong, 0, 0, 0, luong, 0, luong));
    }
}

public class TinhLuongDomainServiceTests
{
    [Fact]
    public void TinhLuong_GanCacDongVaoKy()
    {
        var ky = KyTinhLuong.Create("2026-11", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        var rows = new[]
        {
            NhanVienTinhLuong.Create(Guid.NewGuid(), ky.Id, 1, 0, 0, 0, 1, 0, 1),
            NhanVienTinhLuong.Create(Guid.NewGuid(), ky.Id, 2, 0, 0, 0, 2, 0, 2),
        };

        new DomainTinhLuongService().TinhLuong(ky, rows);

        Assert.Equal(rows, ky.NhanVienTinhLuongs);
    }

    [Fact]
    public void TinhLuong_ThamSoNull_BaoLoi()
    {
        var svc = new DomainTinhLuongService();
        Assert.Throws<ArgumentNullException>(() => svc.TinhLuong(null!, new List<NhanVienTinhLuong>()));
        Assert.Throws<ArgumentNullException>(() => svc.TinhLuong(KyTinhLuong.Create("K", DateTime.UtcNow, DateTime.UtcNow), null!));
    }
}
