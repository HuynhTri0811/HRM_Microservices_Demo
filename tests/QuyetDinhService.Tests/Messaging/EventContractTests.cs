using QuyetDinhService.QuyetDinhService.Application.Events;

namespace QuyetDinhService.Tests.Messaging;

/// <summary>
/// Hợp đồng event mà NhanSu đang consume (bản sao class nằm trong QuanLyNhanSuMicroservice).
/// Đổi namespace, tên hoặc thuộc tính ở đây mà không đổi bên NhanSu sẽ làm event không tới consumer.
/// Phía NhanSu có test tương ứng (QuanLyNhanSu.Tests/Messaging/EventContractTests).
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
    public void EventDuocPublish_GiuDungTenVaThuocTinh(Type type, string[] properties)
    {
        Assert.Equal($"QuyetDinhService.QuyetDinhService.Application.Events.{type.Name}", type.FullName);
        Assert.Equal(properties, type.GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }
}
