using Microsoft.AspNetCore.Http;
using QuyetDinhService.Domain.Entities;
using QuyetDinhService.Domain.Exceptions;
using QuyetDinhService.QuyetDinhService.Application.Commands;
using QuyetDinhService.QuyetDinhService.Application.Handlers.QuyetDinhNangLuongHandle;
using QuyetDinhService.QuyetDinhService.Application.Services;
using QuyetDinhService.QuyetDinhService.Domain.Repositories;

namespace QuyetDinhService.Tests.Handlers;

public class CreateQuyetDinhNangLuongHandlerTests
{
    private static readonly DateTime Ngay = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IQuyetDinhNangLuongRepositoris> _repo = new();
    private readonly Mock<INhanSuServiceClient> _nhanSu = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private QuyetDinhNangLuong? _saved;

    public CreateQuyetDinhNangLuongHandlerTests()
    {
        _repo.Setup(r => r.AddAsync(It.IsAny<QuyetDinhNangLuong>())).Callback<QuyetDinhNangLuong>(q => _saved = q).Returns(Task.CompletedTask);
        _nhanSu.Setup(c => c.UpdateLuongAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>())).ReturnsAsync(true);
    }

    private CreateQuyetDinhNangLuongCommandHandler Handler() => new(_repo.Object, _nhanSu.Object, _http.Object);

    [Fact]
    public async Task HopLe_LuuQuyetDinhVaCapNhatLuongBenNhanSu()
    {
        var nv = Guid.NewGuid();

        var id = await Handler().Handle(new CreateQuyetDinhNangLuongCommand("QD-NL-01", Ngay, "Nâng lương", Ngay, "ghi chú", nv, 10_000_000m, 15_000_000.75m), CancellationToken.None);

        Assert.Equal(id, _saved!.Id);
        Assert.Equal(15_000_000.75m, _saved.LuongCoBanMoi);
        Assert.Equal("ghi chú", _saved.GhiChu);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        // Không có HttpContext => token rỗng
        _nhanSu.Verify(c => c.UpdateLuongAsync(nv, 15_000_000.75m, ""), Times.Once);
    }

    [Fact]
    public async Task LuongAm_BaoLoiVaKhongGoiNhanSu()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            Handler().Handle(new CreateQuyetDinhNangLuongCommand("QD-NL-01", Ngay, "Nâng lương", Ngay, null, Guid.NewGuid(), 0, -5), CancellationToken.None));

        _repo.Verify(r => r.AddAsync(It.IsAny<QuyetDinhNangLuong>()), Times.Never);
        _nhanSu.Verify(c => c.UpdateLuongAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }
}
