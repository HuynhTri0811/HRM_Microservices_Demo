using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TinhLuongService.Application.Command;
using TinhLuongService.Application.Handlers;
using TinhLuongService.Application.Services.DTO;
using TinhLuongService.Application.Services.Interface;
using TinhLuongService.Data;
using TinhLuongService.Domain.Entities;
using TinhLuongService.Infrastructure.Repositories;
using DomainTinhLuongService = TinhLuongService.Domain.Service.TinhLuongService;

namespace TinhLuongService.Tests.Handlers;

/// <summary>
/// Chạy handler với DbContext + repository thật trên SQLite in-memory (mỗi "request" một DbContext riêng,
/// như DI scoped trong ASP.NET Core) để kiểm tra hành vi change tracking và optimistic concurrency của EF.
/// </summary>
public sealed class KyTinhLuongHandlersDbTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Mock<INhanSuServiceClient> _nhanSu = new();
    private readonly Mock<IHttpContextAccessor> _http = new();

    public KyTinhLuongHandlersDbTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private TinhLuongDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TinhLuongDbContext>().UseSqlite(_connection).Options, _http.Object);

    private async Task<(Guid Id, DateTime UpdatedAt)> SeedKy()
    {
        await using var db = NewContext();
        var ky = KyTinhLuong.Create("2026-11", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc));
        db.KyTinhLuongs.Add(ky);
        await db.SaveChangesAsync();
        return (ky.Id, ky.UpdatedAt);
    }

    private async Task<DateTime> CurrentUpdatedAt(Guid id)
    {
        await using var db = NewContext();
        return (await db.KyTinhLuongs.AsNoTracking().SingleAsync(k => k.Id == id)).UpdatedAt;
    }

    private void NhanSuTra(params NhanVienServiceClientDto[] nhanViens) =>
        _nhanSu.Setup(c => c.GetAllNhanVienAsync(It.IsAny<string>())).ReturnsAsync(nhanViens);

    private static NhanVienServiceClientDto NV(decimal luong) => new(Guid.NewGuid(), "NV", "Tên", "x@example.test", luong);

    private async Task<bool> TinhLuong(Guid id, DateTime updatedAt)
    {
        await using var db = NewContext();
        var handler = new TinhLuongHandler(
            new DomainTinhLuongService(),
            new KyTinhLuongRepository(db),
            new NhanVienTinhLuongRepository(db),
            _nhanSu.Object,
            _http.Object,
            NullLogger<TinhLuongHandler>.Instance);
        return await handler.Handle(new TinhLuongCommand(id, updatedAt), CancellationToken.None);
    }

    private async Task<List<NhanVienTinhLuong>> Rows(Guid kyId, bool includeDeleted = false)
    {
        await using var db = NewContext();
        var q = includeDeleted ? db.NhanVienTinhLuongs.IgnoreQueryFilters() : db.NhanVienTinhLuongs;
        return await q.AsNoTracking().Where(x => x.KyTinhLuongId == kyId).ToListAsync();
    }

    [Fact]
    public async Task TinhLuong_CoNhanVien_InsertDongMoiThayVi409()
    {
        // Hồi quy: trước đây Update() cả graph khiến các dòng mới bị UPDATE (0 dòng) => DbUpdateConcurrencyException => 409.
        var (id, updatedAt) = await SeedKy();
        var a = NV(15_000_000.75m); var b = NV(9_000_000m);
        NhanSuTra(a, b);

        Assert.True(await TinhLuong(id, updatedAt));

        var rows = await Rows(id);
        Assert.Equal(2, rows.Count);
        Assert.Equal(15_000_000.75m, rows.Single(r => r.NhanVienId == a.Id).LuongCoBan);
        Assert.Equal(9_000_000m, rows.Single(r => r.NhanVienId == b.Id).LuongThucLanh);
    }

    [Fact]
    public async Task TinhLuong_BoQuaNhanVienChuaCoLuong()
    {
        var (id, updatedAt) = await SeedKy();
        var coLuong = NV(10_000_000m);
        NhanSuTra(coLuong, NV(0m));

        Assert.True(await TinhLuong(id, updatedAt));

        var row = Assert.Single(await Rows(id));
        Assert.Equal(coLuong.Id, row.NhanVienId);
    }

    [Fact]
    public async Task TinhLuongLan2_ThayKetQuaCuKhongBiTrung()
    {
        var (id, updatedAt) = await SeedKy();
        NhanSuTra(NV(1_000_000m), NV(2_000_000m));
        await TinhLuong(id, updatedAt);

        var nvMoi = NV(3_000_000m);
        NhanSuTra(nvMoi);
        Assert.True(await TinhLuong(id, await CurrentUpdatedAt(id)));

        var active = await Rows(id);
        Assert.Equal(nvMoi.Id, Assert.Single(active).NhanVienId);
        var all = await Rows(id, includeDeleted: true);
        Assert.Equal(3, all.Count);
        Assert.Equal(2, all.Count(r => r.IsDeleted));
    }

    [Fact]
    public async Task TinhLuong_UpdatedAtCu_BaoLoiConcurrency()
    {
        var (id, updatedAt) = await SeedKy();
        NhanSuTra(NV(1_000_000m));
        await TinhLuong(id, updatedAt); // làm UpdatedAt trong DB đổi

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => TinhLuong(id, updatedAt));
    }

    [Fact]
    public async Task TinhLuong_KyKhongTonTai_TraFalse()
    {
        NhanSuTra(NV(1m));

        Assert.False(await TinhLuong(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public async Task MoRoiKhoaKy_SauKhiDaTinhLuong_KhongBaoLoi()
    {
        // Khoá/mở kỳ dùng chung UpdateAsync của repository: kỳ đã có dòng lương vẫn phải cập nhật được.
        var (id, updatedAt) = await SeedKy();
        NhanSuTra(NV(1_000_000m));
        await TinhLuong(id, updatedAt);

        await using (var db = NewContext())
            Assert.True(await new MoKyTinhLuongHandler(new KyTinhLuongRepository(db)).Handle(new MoKyTinhLuongCommand(id, await CurrentUpdatedAt(id)), CancellationToken.None));
        await using (var db = NewContext())
            Assert.True(await new KhoaKyTinhLuongHandler(new KyTinhLuongRepository(db)).Handle(new KhoaKyTinhLuongCommand(id, await CurrentUpdatedAt(id)), CancellationToken.None));

        await using var check = NewContext();
        Assert.True((await check.KyTinhLuongs.SingleAsync(k => k.Id == id)).ChotTinhLuong);
        Assert.Single(await Rows(id));
    }

    [Fact]
    public async Task UpdateKy_SauKhiMo_LuuThongTinMoi()
    {
        var (id, _) = await SeedKy();
        await using (var db = NewContext())
            await new MoKyTinhLuongHandler(new KyTinhLuongRepository(db)).Handle(new MoKyTinhLuongCommand(id, await CurrentUpdatedAt(id)), CancellationToken.None);

        await using (var db = NewContext())
        {
            var ok = await new UpdateKyTinhLuongHandler(new KyTinhLuongRepository(db)).Handle(
                new UpdateKyTinhLuongCommand(id, "2026-12", new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), await CurrentUpdatedAt(id)),
                CancellationToken.None);
            Assert.True(ok);
        }

        await using var check = NewContext();
        Assert.Equal("2026-12", (await check.KyTinhLuongs.SingleAsync(k => k.Id == id)).MaKy);
    }
}
