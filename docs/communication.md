# Giao tiếp giữa các service

Hệ thống có ba kiểu giao tiếp:

| Kiểu | Ai gọi ai | Dùng cho |
|---|---|---|
| REST qua API Gateway | Client → Gateway → service | Mọi API công khai |
| gRPC | QuyetDinh, ChamCong, TinhLuong → NhanSu | Đọc nhân viên/chức vụ, cập nhật lương |
| RabbitMQ (MassTransit) | QuyetDinh → NhanSu | Đồng bộ chức vụ khi có quyết định bổ nhiệm |

## REST qua Gateway

Gateway (YARP) ánh xạ prefix theo cấu hình `ReverseProxy` trong `ApiGateway/appsettings.json`:

| Đường dẫn ở Gateway | Service đích | Đường dẫn ở service |
|---|---|---|
| `/api/nhansu/{**rest}` | `nhansu-cluster` (`nhansu-api:8081`) | `/api/{rest}` |
| `/api/quyetdinh/{**rest}` | `quyetdinh-cluster` (`quyetdinh-api:8084`) | `/api/{rest}` |
| `/api/chamcong/{**rest}` | `chamcong-cluster` (`chamcong-api:8083`) | `/api/{rest}` |
| `/api/tinhluong/{**rest}` | `tinhluong-cluster` (`tinhluong-api:8082`) | `/api/{rest}` |

Ví dụ: `GET http://localhost:8080/api/nhansu/NhanVien` → `GET http://nhansu-api:8081/api/NhanVien`.

Gateway thêm header `X-Correlation-Id` vào request và response (nếu client chưa gửi). Cơ chế retry và circuit breaker được mô tả ở [services/api-gateway.md](services/api-gateway.md).

## gRPC tới NhanSu

### Contract

File [`Protos/nhansu.proto`](../Protos/nhansu.proto) là contract duy nhất, package `nhansu.v1`, service `NhanSuGrpc`, C# namespace `HRM.Contracts.NhanSu`. Mỗi project tự sinh code từ file này khi build (`Grpc.Tools`):

```xml
<!-- QuanLyNhanSuMicroservice.csproj -->
<Protobuf Include="..\Protos\nhansu.proto" GrpcServices="Server" Link="Protos\nhansu.proto" />
<!-- QuyetDinhService.csproj, ChamCongService.csproj, TinhLuongService.csproj -->
<Protobuf Include="..\Protos\nhansu.proto" GrpcServices="Client" Link="Protos\nhansu.proto" />
```

| RPC | Request | Response | Ai dùng |
|---|---|---|---|
| `GetNhanVienById` | `id` | `NhanVienReply` | ChamCong (kiểm tra nhân viên khi tạo phiếu nghỉ), QuyetDinh (phụ cấp cũ; hiển thị nhân viên trong danh sách quyết định) |
| `GetAllNhanVien` | (trống) | `NhanVienListReply` | TinhLuong (tính lương) |
| `GetChucVuById` | `id` | `ChucVuReply` | QuyetDinh (phụ cấp chức vụ mới) |
| `UpdateLuong` | `nhan_vien_id`, `luong_co_ban` | `UpdateReply` | QuyetDinh (tạo/sửa/xoá quyết định nâng lương) |
| `UpdateBoNhiem` | `nhan_vien_id`, `chuc_vu_moi`, `phu_cap_moi` | `UpdateReply` | Dự phòng; luồng bổ nhiệm hiện đi qua RabbitMQ |

Quy ước kiểu dữ liệu:

- `Guid` truyền dưới dạng `string`.
- `decimal` (lương, phụ cấp) truyền dưới dạng `string` theo `CultureInfo.InvariantCulture` (ví dụ `"15000000.75"`) để không mất độ chính xác.
- Ngày truyền bằng `google.protobuf.Timestamp` (UTC). `DateTimeKind.Unspecified` được coi là UTC.

### Server (NhanSu)

[`QuanLyNhanVien.API/Grpc/NhanSuGrpcService.cs`](../QuanLyNhanSuMicroservice/QuanLyNhanVien.API/Grpc/NhanSuGrpcService.cs) có `[Authorize]` và gọi lại đúng các MediatR handler mà REST controller dùng, nên logic không bị lặp.

Kestrel của NhanSu mở hai endpoint (`Kestrel:Endpoints` trong `appsettings.json`):
- **8081**: HTTP/1.1, cho REST, Swagger, `/metrics` và Gateway.
- **9081**: HTTP/2 cleartext, cho gRPC.

Hai endpoint phải tách riêng vì khi không có TLS, Kestrel không phục vụ được cả HTTP/1.1 và HTTP/2 trên cùng một port.

Ánh xạ lỗi:

| Tình huống | Status gRPC | Phía client xử lý |
|---|---|---|
| Không tìm thấy nhân viên/chức vụ | `NOT_FOUND` | Trả `null` |
| `id` không phải Guid, số không hợp lệ | `INVALID_ARGUMENT` | Log lỗi, trả `null`/`false` |
| Handler cập nhật ném exception (ví dụ "Không tìm thấy nhân viên") | `FAILED_PRECONDITION` + thông điệp | Log lỗi, trả `false` |
| Thiếu/sai token | `UNAUTHENTICATED` | Log lỗi, trả `null`/`false` |

### Client (QuyetDinh, ChamCong, TinhLuong)

Mỗi service có `NhanSuServiceClient` implement `INhanSuServiceClient`. Interface này giúp handler không phụ thuộc vào transport. Client được đăng ký trong `Extensions/DatabaseExtensions.cs`:

```csharp
builder.Services.AddGrpcClient<NhanSuGrpc.NhanSuGrpcClient>(options =>
{
    options.Address = new Uri(builder.Configuration["NhanSuGrpcUrl"] ?? "http://nhansu-api:9081");
});
builder.Services.AddScoped<INhanSuServiceClient, NhanSuServiceClient>();
```

Client lấy header `Authorization` của request gốc (qua `IHttpContextAccessor`) và gửi kèm trong metadata gRPC. Tiền tố được chuẩn hoá về `Bearer <token>`, nên NhanSu xác thực cùng một token mà client cuối đã gửi tới Gateway.

## RabbitMQ (MassTransit)

### Event

| Event | Publisher | Queue / consumer ở NhanSu | Tác dụng |
|---|---|---|---|
| `QuyetDinhBoNhiemCreatedEvent` | QuyetDinh (tạo quyết định) | `nhansu-quyetdinh-bonhiem-created` / `QuyetDinhBoNhiemCreatedConsumer` | Đặt nhân viên sang chức vụ mới |
| `QuyetDinhBoNhiemUpdatedEvent` | QuyetDinh (sửa quyết định) | `nhansu-quyetdinh-bonhiem-updated` / `QuyetDinhBoNhiemUpdatedConsumer` | Đặt nhân viên sang chức vụ mới |
| `QuyetDinhBoNhiemDeletedEvent` | QuyetDinh (xoá quyết định) | `nhansu-quyetdinh-bonhiem-deleted` / `QuyetDinhBoNhiemDeletedConsumer` | Đưa nhân viên về chức vụ cũ |
| `NhanVienCreatedEvent` | NhanSu (tạo nhân viên) | chưa có consumer | — |
| `PhieuDangKyNghiCreatedEvent` | ChamCong (tạo phiếu nghỉ) | chưa có consumer | — |

Consumer ở NhanSu gửi `UpdateNhanVienBoNhiemDTO` qua MediatR. Handler lấy chức vụ từ database rồi gọi `NhanVien.UpdatePosition`, nên phụ cấp của nhân viên luôn là **phụ cấp hiện tại của chức vụ**. Các trường `PhuCapMoi`/`PhuCapCu` trong event chỉ để ghi nhận trên quyết định, NhanSu không dùng chúng.

### Khớp kiểu message

MassTransit khớp message theo **tên đầy đủ của type** (namespace + tên class). Không có thư viện contract dùng chung: class event được **sao chép** sang NhanSu và phải giữ **đúng** namespace `QuyetDinhService.QuyetDinhService.Application.Events`, cùng tên và cùng thuộc tính. Hai test `EventContractTests` (ở `tests/QuanLyNhanSu.Tests` và `tests/QuyetDinhService.Tests`) giữ ràng buộc này. Khi đổi event, phải sửa cả hai phía cùng lúc.

### Transactional Outbox (QuyetDinh)

QuyetDinh dùng MassTransit EF Core Outbox (`Extensions/MessagingExtensions.cs`):

```csharp
x.AddEntityFrameworkOutbox<QuyetDinhDbContext>(o =>
{
    o.UsePostgres();
    o.UseBusOutbox();
});
```

Với `UseBusOutbox`, lệnh `Publish(...)` **chỉ thêm** một `OutboxMessage` vào `QuyetDinhDbContext`. Message chỉ được ghi xuống database khi `SaveChanges` chạy, trong cùng transaction với dữ liệu quyết định. Sau đó một tiến trình nền đọc bảng outbox và gửi lên RabbitMQ.

> **Thứ tự bắt buộc: `Publish` trước, `SaveChangesAsync` sau.** Nếu làm ngược lại, message không bao giờ được lưu và event không tới NhanSu. Ba handler trong `QuyetDinhBoNhiemCommandHandlers.cs` đã theo thứ tự này, và có unit test kiểm tra.

Bảng outbox (`InboxState`, `OutboxMessage`, `OutboxState`) được tạo bởi migration `AddMassTransitOutbox` của QuyetDinh.

ChamCong và NhanSu publish trực tiếp, không qua outbox. Nếu broker lỗi đúng lúc đó, event `PhieuDangKyNghiCreatedEvent` và `NhanVienCreatedEvent` có thể bị mất.
