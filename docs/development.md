# Hướng dẫn phát triển

## Quy ước

- **Ngôn ngữ:** tên miền nghiệp vụ dùng tiếng Việt không dấu (`NhanVien`, `QuyetDinhBoNhiem`, `LuongCoBan`...). Thông báo lỗi trả cho người dùng viết tiếng Việt có dấu.
- **Entity:**
  - kế thừa `ObjectBase`; constructor `private`;
  - tạo qua `static Create(...)` có validate; đổi trạng thái qua method có validate; property để `private set`;
  - lỗi nghiệp vụ ném `BadRequestException` (kèm `code` và, nếu có, danh sách lỗi theo trường) để trả 400. Không ném `Exception` thường vì sẽ thành 500.
- **Handler:**
  - mỗi command/query là một `record : IRequest<T>`;
  - handler dùng primary constructor để nhận dependency;
  - không tìm thấy dữ liệu: ném `NotFoundException` (404), hoặc trả `false`/`null` để controller tự trả 404;
  - trùng dữ liệu: ném `IsExistException`.
- **Controller** mỏng: chỉ gọi `mediator.Send(...)` và chọn status code.
- **Optimistic concurrency** (QuyetDinh, ChamCong, TinhLuong): lệnh sửa nhận `UpdatedAt` từ client và truyền vào `repository.UpdateAsync(entity, updatedAt)`.
- **Thời gian:** cột `timestamp with time zone` của Npgsql chỉ nhận `DateTimeKind.Utc`. Chuyển sang UTC trước khi lưu (ví dụ `DateTime.SpecifyKind(value.Date, DateTimeKind.Utc)`).

## Thêm một API mới

Ví dụ: thêm `GET /api/NhanVien/phongban/{phongBanId}` ở NhanSu.

1. **Query:** thêm `record GetEmployeesByPhongBanQuery(Guid PhongBanId) : IRequest<IEnumerable<NhanVienDto>>` vào `QuanLyNhanSuMicroservice/QuanLyNhanVien.Application/Queries/NhanVienQuery.cs`.
2. **Repository:** nếu cần truy vấn mới, thêm method vào `INhanVienRepository` và `NhanVienRepository`.
3. **Handler:** tạo `GetEmployeesByPhongBanHandler` trong `QuanLyNhanVien.Application/Handlers/NhanVienHandler/`. MediatR tự đăng ký nhờ `AddMediatR(... RegisterServicesFromAssembly ...)`.
4. **Controller:** thêm action vào `NhanVienController`.
5. **Unit test:** thêm vào `tests/QuanLyNhanSu.Tests/Handlers/`.
6. **Tài liệu:** cập nhật bảng API trong `docs/services/nhansu.md`.

Gateway tự chuyển tiếp mọi đường dẫn dưới `/api/nhansu/`, nên không cần sửa Gateway.

## Thêm một RPC gRPC

1. Sửa [`Protos/nhansu.proto`](../Protos/nhansu.proto): thêm `rpc` và message. Giữ quy ước `Guid`/`decimal` là `string`. **Không đổi số thứ tự field** đã có.
2. Implement trong `QuanLyNhanSuMicroservice/QuanLyNhanVien.API/Grpc/NhanSuGrpcService.cs`: gọi MediatR, ném `RpcException` với status phù hợp (`NotFound`, `InvalidArgument`, `FailedPrecondition`).
3. Ở service gọi tới: thêm method vào `INhanSuServiceClient` và `NhanSuServiceClient`. Gửi header bằng `BuildHeaders(token)`, bắt `RpcException`.
4. Build lại: code client/server được sinh tự động từ file `.proto`.
5. Thêm test cho server (`NhanSuGrpcServiceTests`) và client (`NhanSuServiceClientTests`).

## Thêm một event RabbitMQ

1. Tạo class event ở service **publish**, trong thư mục `Application/Events`.
2. Publish trong handler:
   - nếu service dùng Outbox (QuyetDinh): gọi `Publish` **trước** `SaveChangesAsync`;
   - nếu không dùng Outbox: publish sau khi lưu thành công.
3. Ở service **consume**:
   - sao chép class event, **giữ nguyên namespace và tên** của bên publish;
   - viết consumer `IConsumer<TEvent>`;
   - đăng ký consumer và `ReceiveEndpoint("<service>-<event>", ...)` trong `Extensions/MessagingExtensions.cs`.
4. Thêm event vào `EventContractTests` ở **cả hai** project test, để lần đổi sau làm vỡ test thay vì làm mất message.

## Thêm migration

```bash
dotnet ef migrations add <TenMigration> --project <ThuMucService>
```

- Kiểm tra file migration sinh ra chỉ chứa thay đổi mong muốn. Migration được áp tự động khi service khởi động.
- Dữ liệu danh mục cố định thì seed bằng `HasData(...)` trong `OnModelCreating`, với Id cố định. Xem cách seed `TinhTrangNhanVien` trong `NhanSuDbContext`.
- **Đừng quên migration khi thêm entity/DbSet mới.** ChamCong từng thiếu migration cho `BangChamCongTheoThang`, nên API lỗi `relation ... does not exist` trên DB mới.

## Thêm một service mới

1. Tạo project ASP.NET Core `net8.0` theo cấu trúc của các service hiện có (API / Application / Domain / Infrastructure / Extensions / Common).
2. Thêm vào `QuanLyNhanSuSystem.slnx`.
3. Thêm `Dockerfile` (build context là thư mục gốc) và service vào `docker-compose.yml`; thêm database vào `init-db.sql`.
4. Gateway: thêm route `/api/<ten>/{**remainder}` và cluster trong `ApiGateway/appsettings.json`, biến môi trường địa chỉ cluster trong compose, và một mục trong `MapSwaggerRoutes` / `UseGatewaySwagger` (`ApiGateway/Extensions/SwaggerExtensions.cs`).
5. Prometheus: thêm job vào `prometheus.yml`.
6. Tạo project test trong `tests/` và tài liệu trong `docs/services/`.

## Checklist trước khi tạo PR

- [ ] `dotnet build QuanLyNhanSuSystem.slnx` không lỗi.
- [ ] `dotnet test QuanLyNhanSuSystem.slnx` đạt hết.
- [ ] Có migration nếu đổi model EF.
- [ ] Đổi event thì đã sửa cả hai phía và `EventContractTests`.
- [ ] Đổi `.proto` thì vẫn tương thích ngược (không đổi số field).
- [ ] Chạy *Chạy toàn bộ* trên web UI với `docker compose up -d --build` nếu thay đổi chạm tới luồng giữa các service.
- [ ] Cập nhật tài liệu trong `docs/`.
