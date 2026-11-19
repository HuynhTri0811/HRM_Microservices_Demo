using MassTransit;
using Microsoft.AspNetCore.Http;
using QuyetDinhService.Application.Exceptions;
using QuyetDinhService.Domain.Entities;
using QuyetDinhService.QuyetDinhService.Application.Commands;
using QuyetDinhService.QuyetDinhService.Application.Events;
using QuyetDinhService.QuyetDinhService.Application.Handlers.QuyetDinhBoNhiemHandle;
using QuyetDinhService.QuyetDinhService.Application.Services;
using QuyetDinhService.QuyetDinhService.Application.Services.DTO;
using QuyetDinhService.QuyetDinhService.Domain.Repositories;

namespace QuyetDinhService.Tests.Handlers;

public class QuyetDinhBoNhiemHandlersTests
{
    private static readonly DateTime Ngay = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IQuyetDinhBoNhiemRepository> _repo = new();
    private readonly Mock<INhanSuServiceClient> _nhanSu = new();
    private readonly Mock<IPublishEndpoint> _publish = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly List<string> _calls = new();

    private readonly Guid _nhanVienId = Guid.NewGuid();
    private readonly Guid _chucVuCu = Guid.NewGuid();
    private readonly Guid _chucVuMoi = Guid.NewGuid();

    public QuyetDinhBoNhiemHandlersTests()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer token-goc";
        _http.SetupGet(h => h.HttpContext).Returns(ctx);

        _nhanSu.Setup(c => c.GetNhanVienByIdAsync(_nhanVienId, It.IsAny<string>()))
            .ReturnsAsync(new NhanVienServiceClientDto { Id = _nhanVienId, PhuCap = 1_000_000m });
        _nhanSu.Setup(c => c.GetChucVuByIdAsync(_chucVuMoi, It.IsAny<string>()))
            .ReturnsAsync(new ChucVuServiceClientDto { Id = _chucVuMoi, PhuCap = 5_000_000.5m });

        // Ghi lại thứ tự gọi để kiểm tra Outbox: Publish phải chạy TRƯỚC SaveChanges
        _repo.Setup(r => r.AddAsync(It.IsAny<QuyetDinhBoNhiem>())).Callback(() => _calls.Add("add")).Returns(Task.CompletedTask);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<QuyetDinhBoNhiem>(), It.IsAny<DateTime>())).Callback(() => _calls.Add("update")).Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).Callback(() => _calls.Add("delete")).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Callback(() => _calls.Add("save")).Returns(Task.CompletedTask);
        _publish.Setup(p => p.Publish(It.IsAny<QuyetDinhBoNhiemCreatedEvent>(), It.IsAny<CancellationToken>())).Callback(() => _calls.Add("publish")).Returns(Task.CompletedTask);
        _publish.Setup(p => p.Publish(It.IsAny<QuyetDinhBoNhiemUpdatedEvent>(), It.IsAny<CancellationToken>())).Callback(() => _calls.Add("publish")).Returns(Task.CompletedTask);
        _publish.Setup(p => p.Publish(It.IsAny<QuyetDinhBoNhiemDeletedEvent>(), It.IsAny<CancellationToken>())).Callback(() => _calls.Add("publish")).Returns(Task.CompletedTask);
    }

    private CreateQuyetDinhBoNhiemCommand CreateCommand() =>
        new("QD-BN-01", Ngay, "Bổ nhiệm", Ngay, null, _nhanVienId, _chucVuCu, _chucVuMoi, "Thăng chức");

    private QuyetDinhBoNhiem ExistingEntity()
    {
        var qd = QuyetDinhBoNhiem.Create("QD-BN-01", Ngay, "Bổ nhiệm", Ngay);
        qd.BoNhiem(_nhanVienId, _chucVuCu, _chucVuMoi, 1_000_000m, 3_000_000m, "Cũ");
        return qd;
    }

    [Fact]
    public async Task Create_LayPhuCapTuNhanSuVaPhatEvent()
    {
        var handler = new CreateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        var id = await handler.Handle(CreateCommand(), CancellationToken.None);

        _publish.Verify(p => p.Publish(It.Is<QuyetDinhBoNhiemCreatedEvent>(e =>
            e.Id == id && e.MaNhanVien == _nhanVienId && e.ChucVuCu == _chucVuCu && e.ChucVuMoi == _chucVuMoi
            && e.PhuCapCu == 1_000_000m && e.PhuCapMoi == 5_000_000.5m && e.SoQuyetDinh == "QD-BN-01"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ChuyenTiepTokenCuaRequestGoc()
    {
        var handler = new CreateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        _nhanSu.Verify(c => c.GetNhanVienByIdAsync(_nhanVienId, "Bearer token-goc"), Times.Once);
        _nhanSu.Verify(c => c.GetChucVuByIdAsync(_chucVuMoi, "Bearer token-goc"), Times.Once);
    }

    [Fact]
    public async Task Create_PublishTruocSaveChanges_DeOutboxLuuEvent()
    {
        // Hồi quy: với MassTransit EF Outbox, Publish chỉ thêm OutboxMessage vào DbContext;
        // nếu SaveChanges chạy trước thì event không bao giờ được lưu và không tới NhanSu.
        var handler = new CreateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.True(_calls.IndexOf("publish") < _calls.LastIndexOf("save"), string.Join(" → ", _calls));
    }

    [Fact]
    public async Task Create_KhongCoNhanVien_BaoNotFoundVaKhongLuu()
    {
        _nhanSu.Setup(c => c.GetNhanVienByIdAsync(_nhanVienId, It.IsAny<string>())).ReturnsAsync((NhanVienServiceClientDto?)null);
        var handler = new CreateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(CreateCommand(), CancellationToken.None));
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Create_KhongCoChucVuMoi_BaoNotFound()
    {
        _nhanSu.Setup(c => c.GetChucVuByIdAsync(_chucVuMoi, It.IsAny<string>())).ReturnsAsync((ChucVuServiceClientDto?)null);
        var handler = new CreateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(CreateCommand(), CancellationToken.None));
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Update_PhatEventVoiPhuCapMoiVaPublishTruocSave()
    {
        var entity = ExistingEntity();
        _repo.Setup(r => r.GetByIdAsync(entity.Id)).ReturnsAsync(entity);
        var updatedAt = DateTime.UtcNow;
        var handler = new UpdateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        var ok = await handler.Handle(new UpdateQuyetDinhBoNhiemCommand(entity.Id, "QD-BN-02", Ngay, "Sửa", Ngay, "ghi chú", _chucVuMoi, "Lý do mới", updatedAt), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(5_000_000.5m, entity.PhuCapMoi);
        Assert.Equal("QD-BN-02", entity.SoQuyetDinh);
        _repo.Verify(r => r.UpdateAsync(entity, updatedAt), Times.Once);
        _publish.Verify(p => p.Publish(It.Is<QuyetDinhBoNhiemUpdatedEvent>(e => e.PhuCapMoi == 5_000_000.5m && e.ChucVuMoi == _chucVuMoi), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_calls.IndexOf("publish") < _calls.LastIndexOf("save"), string.Join(" → ", _calls));
    }

    [Fact]
    public async Task Update_KhongTonTai_TraFalse()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((QuyetDinhBoNhiem?)null);
        var handler = new UpdateQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        var ok = await handler.Handle(new UpdateQuyetDinhBoNhiemCommand(Guid.NewGuid(), "x", Ngay, "x", Ngay, null, _chucVuMoi, "x", DateTime.UtcNow), CancellationToken.None);

        Assert.False(ok);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Delete_PhatEventKhoiPhucChucVuCu()
    {
        var entity = ExistingEntity();
        _repo.Setup(r => r.GetByIdAsync(entity.Id)).ReturnsAsync(entity);
        var handler = new DeleteQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        var ok = await handler.Handle(new DeleteQuyetDinhBoNhiemCommand(entity.Id), CancellationToken.None);

        Assert.True(ok);
        _publish.Verify(p => p.Publish(It.Is<QuyetDinhBoNhiemDeletedEvent>(e =>
            e.MaNhanVien == _nhanVienId && e.ChucVuCu == _chucVuCu && e.PhuCapCu == 1_000_000m), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_calls.IndexOf("publish") < _calls.LastIndexOf("save"), string.Join(" → ", _calls));
    }

    [Fact]
    public async Task Delete_KhongTonTai_BaoNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((QuyetDinhBoNhiem?)null);
        var handler = new DeleteQuyetDinhBoNhiemCommandHandler(_repo.Object, _nhanSu.Object, _http.Object, _publish.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteQuyetDinhBoNhiemCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
