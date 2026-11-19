using MassTransit;
using MediatR;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.NhanSu;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Consumers;
using QuyetDinhService.QuyetDinhService.Application.Events;

namespace QuanLyNhanSu.Tests.Messaging;

public class QuyetDinhBoNhiemConsumerTests
{
    private readonly Mock<IMediator> _mediator = new();

    public QuyetDinhBoNhiemConsumerTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<UpdateNhanVienBoNhiemDTO>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        return ctx.Object;
    }

    [Fact]
    public async Task Created_CapNhatChucVuMoiVaPhuCapMoi()
    {
        var evt = new QuyetDinhBoNhiemCreatedEvent
        {
            MaNhanVien = Guid.NewGuid(), ChucVuCu = Guid.NewGuid(), ChucVuMoi = Guid.NewGuid(), PhuCapCu = 1m, PhuCapMoi = 2.5m
        };

        await new QuyetDinhBoNhiemCreatedConsumer(_mediator.Object).Consume(Context(evt));

        _mediator.Verify(m => m.Send(new UpdateNhanVienBoNhiemDTO(evt.MaNhanVien, evt.ChucVuMoi, evt.PhuCapMoi), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Updated_CapNhatChucVuMoiVaPhuCapMoi()
    {
        var evt = new QuyetDinhBoNhiemUpdatedEvent
        {
            MaNhanVien = Guid.NewGuid(), ChucVuCu = Guid.NewGuid(), ChucVuMoi = Guid.NewGuid(), PhuCapCu = 1m, PhuCapMoi = 3m
        };

        await new QuyetDinhBoNhiemUpdatedConsumer(_mediator.Object).Consume(Context(evt));

        _mediator.Verify(m => m.Send(new UpdateNhanVienBoNhiemDTO(evt.MaNhanVien, evt.ChucVuMoi, evt.PhuCapMoi), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deleted_KhoiPhucChucVuCuVaPhuCapCu()
    {
        var evt = new QuyetDinhBoNhiemDeletedEvent { MaNhanVien = Guid.NewGuid(), ChucVuCu = Guid.NewGuid(), PhuCapCu = 1_000_000m };

        await new QuyetDinhBoNhiemDeletedConsumer(_mediator.Object).Consume(Context(evt));

        _mediator.Verify(m => m.Send(new UpdateNhanVienBoNhiemDTO(evt.MaNhanVien, evt.ChucVuCu, evt.PhuCapCu), It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// MassTransit khớp message theo tên đầy đủ của type. Các class event phía NhanSu là bản sao của
/// QuyetDinhService, nên phải giữ đúng namespace + tên + thuộc tính, nếu không event sẽ không bao giờ tới consumer.
/// Phía QuyetDinhService có test tương ứng (QuyetDinhService.Tests/Messaging/EventContractTests).
/// </summary>
public class EventContractTests
{
    public static TheoryData<Type, string[]> Events => new()
    {
        { typeof(QuyetDinhBoNhiemCreatedEvent), new[] { "ChucVuCu", "ChucVuMoi", "Id", "MaNhanVien", "NgayQuyetDinh", "PhuCapCu", "PhuCapMoi", "SoQuyetDinh" } },
        { typeof(QuyetDinhBoNhiemUpdatedEvent), new[] { "ChucVuCu", "ChucVuMoi", "Id", "MaNhanVien", "NgayQuyetDinh", "PhuCapCu", "PhuCapMoi", "SoQuyetDinh" } },
        { typeof(QuyetDinhBoNhiemDeletedEvent), new[] { "ChucVuCu", "MaNhanVien", "PhuCapCu" } },
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void EventPhiaNhanSu_KhopTenVaThuocTinhVoiQuyetDinhService(Type type, string[] properties)
    {
        Assert.Equal($"QuyetDinhService.QuyetDinhService.Application.Events.{type.Name}", type.FullName);
        Assert.Equal(properties, type.GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }
}
