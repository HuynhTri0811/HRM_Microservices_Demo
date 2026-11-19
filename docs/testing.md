# Kiểm thử

Dự án có hai lớp kiểm thử:

| Lớp | Chạy ở đâu | Kiểm tra gì |
|---|---|---|
| Unit test (xUnit) | `tests/`, không cần hạ tầng | Rule domain, handler, ánh xạ gRPC, consumer, middleware, resilience của Gateway |
| Web UI kiểm thử | Trình duyệt, cần cả hệ thống đang chạy | Luồng end-to-end qua Gateway: REST → gRPC → RabbitMQ → database thật |

## Unit test

### Chạy

```bash
dotnet test QuanLyNhanSuSystem.slnx
```

```bash
dotnet test tests/TinhLuongService.Tests --filter "FullyQualifiedName~KyTinhLuongHandlersDbTests"
```

Nếu máy chỉ có runtime .NET mới hơn 8:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test QuanLyNhanSuSystem.slnx
```

### Các project

| Project | Số test | Nội dung |
|---|---|---|
| `tests/QuanLyNhanSu.Tests` | 71 | Domain (nhân viên, chức vụ, phòng ban, trạng thái); `CreateNhanVienHandler`; đăng ký/đăng nhập (xác thực JWT bằng đúng tham số các service dùng); `NhanSuGrpcService`; consumer RabbitMQ; `ExceptionHandlingMiddleware`; hợp đồng event |
| `tests/QuyetDinhService.Tests` | 35 | Domain quyết định; handler bổ nhiệm (Publish trước SaveChanges) và nâng lương; gRPC client; hợp đồng event |
| `tests/ChamCongService.Tests` | 29 | Domain bảng công, loại chấm công, phiếu nghỉ; handler tạo phiếu nghỉ theo hình thức; gRPC client |
| `tests/TinhLuongService.Tests` | 23 | Domain kỳ lương; handler tính lương/khoá/mở/sửa kỳ trên SQLite in-memory; gRPC client |
| `tests/ApiGateway.Tests` | 21 | Method nào được retry, lỗi nào là lỗi hạ tầng, POST 500 giữ nguyên body, breaker theo từng cluster |

### Cách tổ chức

- **Cấu hình chung** ở `tests/Directory.Build.props`: `net8.0`, xUnit 2.9, Moq 4.20, coverlet; `using Xunit` và `using Moq` được khai báo toàn cục.
- **Mỗi project test** tham chiếu đúng một project service.
  - NhanSu bật `InternalsVisibleTo("QuanLyNhanSu.Tests")` để test dùng được factory `internal` như `TinhTrangNhanVien.Create`.
- **Mock gRPC client:** client sinh từ `nhansu.proto` có method `virtual`, nên Moq mock trực tiếp được. `tests/Shared/GrpcTestCalls.cs` (link vào 3 project client) tạo `AsyncUnaryCall` thành công hoặc lỗi và đọc header `Authorization` trong metadata.
- **Gọi trực tiếp gRPC server:** `tests/QuanLyNhanSu.Tests/Grpc/TestServerCallContext.cs` là `ServerCallContext` tối thiểu để gọi method của `NhanSuGrpcService`.
- **EF Core thật:** TinhLuong dùng `Microsoft.EntityFrameworkCore.Sqlite` in-memory, mỗi bước tạo một `DbContext` mới (giống DI scoped), để kiểm tra change tracking và concurrency token.
- **Hợp đồng event:** `EventContractTests` ở NhanSu và QuyetDinh cùng khẳng định namespace, tên và thuộc tính của 3 event bổ nhiệm.
- **Tên test** viết tiếng Việt không dấu theo mẫu `HanhDong_DieuKien_KetQua`, ví dụ `TinhLuong_UpdatedAtCu_BaoLoiConcurrency`.

### Test hồi quy cho các lỗi đã sửa

| Lỗi | Test |
|---|---|
| Không tạo được nhân viên trên DB mới (thiếu trạng thái) | `CreateNhanVienHandlerTests.HopLe_LuuNhanVienVoiTrangThaiDangLamViec` |
| Nhân viên đang làm việc không thôi việc được | `NhanVienTests.SetNhanVienThoiViec_NhanVienDangLamViec_ChuyenSangThoiViec` |
| Tính lương trả 409 | `KyTinhLuongHandlersDbTests.TinhLuong_CoNhanVien_InsertDongMoiThayVi409` và các test cùng class |
| Event bổ nhiệm không vào Outbox | `QuyetDinhBoNhiemHandlersTests.Create_PublishTruocSaveChanges_DeOutboxLuuEvent` |
| Event sai namespace | `EventContractTests` (2 project) |
| Gateway đổi 500 thành 502, breaker dùng chung | `GatewayResilienceTests.Post500_KhongRetryVaTraNguyenBodyLoi`, `MoiClusterCoBreakerRieng` |

### Lưu ý về JWT trong test

NhanSu đang dùng `System.IdentityModel.Tokens.Jwt` 7.1.2 cùng với `Microsoft.IdentityModel.Tokens` 8.14.0. Hai phiên bản lệch này làm `JwtSecurityTokenHandler.ReadJwtToken` đọc thiếu claim, dù token thật vẫn đúng. Vì vậy test xác thực token bằng `JsonWebTokenHandler` với cùng `TokenValidationParameters` như các service.

## Web UI kiểm thử (HRM Test Console)

Gateway phục vụ trang **http://localhost:8080/** (file `ApiGateway/wwwroot/index.html`, HTML/JS thuần, không cần build). Trang gọi API qua chính Gateway nên không vướng CORS.

### Kịch bản tự động

Bấm **▶ Chạy toàn bộ**. Trang tự tạo một tài khoản test riêng và dữ liệu có hậu tố ngẫu nhiên, sau đó chạy 12 bước. Mỗi bước gắn nhãn kênh đang kiểm tra:

| # | Bước | Kênh |
|---|---|---|
| 1 | Gateway lấy được Swagger của cả 4 service | REST |
| 2 | Đăng ký + đăng nhập tài khoản test | REST |
| 3 | Tạo phòng ban | REST |
| 4 | Tạo 2 chức vụ (phụ cấp 1.000.000 và 5.000.000,5) | REST |
| 5 | Tạo nhân viên | REST |
| 6 | Quyết định nâng lương → lương nhân viên thành 15.000.000,75 | REST + gRPC `UpdateLuong` |
| 7 | Danh sách quyết định nâng lương có thông tin nhân viên | gRPC `GetNhanVienById` |
| 8 | Quyết định bổ nhiệm lấy đúng phụ cấp chức vụ mới | REST + gRPC `GetChucVuById` |
| 9 | Event bổ nhiệm làm NhanSu đổi chức vụ (chờ tối đa 15 giây) | RabbitMQ |
| 10 | Tạo loại chấm công + phiếu nghỉ | REST + gRPC từ ChamCong |
| 11 | Tạo và chốt bảng công tháng | REST (optimistic concurrency) |
| 12 | Tạo kỳ và tính lương | REST + gRPC `GetAllNhanVien` |

- Bước nào lỗi sẽ hiện mã HTTP, thông báo lỗi và gợi ý nguyên nhân (ví dụ với 409, 502).
- Nếu bật *Chạy tiếp khi một bước lỗi*, các bước không phụ thuộc vẫn được chạy; bước thiếu dữ liệu đầu vào được đánh dấu *bỏ qua*.

### Thao tác tay

- Đăng nhập ở góc phải; nút *Đăng ký* tạo tài khoản role `Admin`. Token được lưu trong `localStorage`.
- Các tab **Nhân sự, Quyết định, Chấm công, Tính lương**:
  - xem bảng dữ liệu;
  - tạo mới bằng form, chọn nhân viên/chức vụ/phòng ban/loại chấm công từ dropdown thay vì gõ Guid;
  - xoá;
  - thao tác riêng: sửa lương, chốt/mở chốt bảng công, khoá/mở/tính lương kỳ. Các thao tác này tự gửi `updatedAt` của dòng.
- **Gọi API tự do**: chọn method, service, đường dẫn và body JSON.
- **Nhật ký request**: method, URL, status, thời gian, body request/response và `X-Correlation-Id`. Dùng id này để tìm log trong MongoDB hoặc trace trên Jaeger.
- Ô **Gateway** ở đầu trang cho phép trỏ sang Gateway khác, ví dụ khi mở file HTML trực tiếp hoặc chạy Gateway ở port khác.

## Gợi ý kiểm tra thủ công khác

- **Swagger tổng hợp:** http://localhost:8080/swagger. Bấm *Authorize* và nhập `Bearer <token>`.
- **RabbitMQ Management** (http://localhost:15672): queue `nhansu-quyetdinh-bonhiem-*` phải có 1 consumer.
- **Outbox của QuyetDinh:** sau khi tạo quyết định bổ nhiệm, bảng `OutboxMessage` trong `DbQuyetDinh` chỉ chứa message trong thời gian ngắn rồi được gửi đi.
- **Jaeger:** chọn service `ApiGateway` để thấy một request đi qua Gateway → service → gRPC → NhanSu.
