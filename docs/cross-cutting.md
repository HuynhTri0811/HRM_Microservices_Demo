# Các vấn đề xuyên suốt

## Xác thực (JWT)

- **NhanSu cấp token.** `POST /api/nhansu/Auth/login` trả `{ "token": "..." }`. Token ký HMAC-SHA256, hạn 2 giờ, gồm các claim:
  - `nameid`: Id tài khoản;
  - `unique_name`: tên đăng nhập;
  - `role`: vai trò;
  - `iss` / `aud`.
- **Mọi service xác thực cùng một khoá.** Cả 4 service đọc `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` (mặc định `QuanLyNhanSuMicroservice`) và bật `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` (`Extensions/AuthenticationExtensions.cs`). Vì vậy token do NhanSu cấp dùng được ở mọi service.
- **Mọi controller có `[Authorize]`**, trừ `AuthController`. gRPC service của NhanSu cũng có `[Authorize]`.
- **Chưa phân quyền theo role.** Claim `role` có trong token, nhưng chưa endpoint nào dùng `[Authorize(Roles = ...)]`.
- **Đăng ký mở cho mọi người.** `POST /api/nhansu/Auth/register` không cần đăng nhập và cho chọn role tuỳ ý. Mật khẩu được hash bằng BCrypt.
- **Token được chuyển tiếp** từ request gốc sang lời gọi gRPC (xem [communication.md](communication.md#client-quyetdinh-chamcong-tinhluong)).
- **Swagger** của từng service có nút *Authorize*. Scheme khai báo kiểu `ApiKey`, nên phải nhập **cả tiền tố**: `Bearer <token>`.

> ⚠️ `Jwt:Key`, mật khẩu PostgreSQL/RabbitMQ/MongoDB trong `appsettings.json` và `docker-compose.yml` chỉ dùng cho demo. Khi triển khai thật, hãy đưa chúng ra biến môi trường hoặc secret store.

## Định dạng lỗi

Mỗi service có `ExceptionHandlingMiddleware` (thư mục `Application/Exception(s)`). Middleware bắt mọi exception và trả JSON camelCase:

```json
{
  "timestamp": "2026-11-19T02:09:26Z",
  "status": 404,
  "error": "Not Found",
  "message": "Không tìm thấy nhân viên trong hệ thống nhân sự",
  "code": "NOT_FOUND",
  "errors": { "Email": ["Email không hợp lệ"] }
}
```

| Exception | HTTP | `error` | `code` mặc định |
|---|---|---|---|
| `BadRequestException` (Domain) | 400 | Bad Request | `BAD_REQUEST`, hoặc mã truyền vào (`REQUEST_VALIDATION_ERROR`, `INVALID_DATE`, `INVALID_EMAIL`, `INVALID_MONEY`...) |
| `NotFoundException` | 404 | Not Found | `NOT_FOUND` |
| `IsExistException` | 400 | Is Exist | `IS_EXIST` |
| Mọi exception khác | 500 | Internal Server Error | `INTERNAL_SERVER_ERROR` |

Trường `errors` chỉ có khi exception mang danh sách lỗi theo từng trường; nếu không có thì trường này bị ẩn.

Một số trường hợp đặc biệt:

- **TinhLuong** có thêm `ConcurrencyExceptionFilter`: `DbUpdateConcurrencyException` → **409** với body `ProblemDetails`.
- **`AuthController` tự bắt exception:** đăng nhập sai trả **401** `{ "message": ... }`; đăng ký trùng tên trả **400** `{ "message": ... }`.
- **Một số chỗ còn ném `Exception` thường, nên trả 500 thay vì 400/404:**
  - validate trong domain của ChamCong và TinhLuong;
  - "Nhân viên không tồn tại" khi tạo phiếu nghỉ;
  - `update-luong` / `update-chuc-vu` của NhanSu khi không tìm thấy nhân viên hoặc chức vụ.

  Xem [troubleshooting.md](troubleshooting.md#giới-hạn-đã-biết).
- **Controller tự trả lỗi** trong vài trường hợp:
  - `404` khi handler trả `false`/`null`;
  - `400` (chuỗi thông báo, không phải JSON) khi Id trên URL khác Id trong body. Áp dụng cho các API `PUT` của ChamCong, TinhLuong, QuyetDinh, vì body của các API này chứa cả `id`.

## Optimistic concurrency

Các entity của **QuyetDinh, ChamCong, TinhLuong** cấu hình `UpdatedAt` làm concurrency token (`IsConcurrencyToken()` trong `DbContext`).

Luồng sửa dữ liệu:

1. Client đọc bản ghi và lấy `updatedAt` (DTO của các service này đều trả trường này).
2. Client gửi `updatedAt` đó kèm lệnh sửa: trong body với `PUT`, hoặc query `?updatedAt=` với các `PATCH` chốt/khoá/mở/tính lương.
3. Repository đặt `OriginalValue` của `UpdatedAt` bằng giá trị client gửi. EF sinh `UPDATE ... WHERE "Id" = @id AND "UpdatedAt" = @updatedAt`.
4. Nếu bản ghi đã bị người khác sửa thì không có dòng nào khớp, và EF ném `DbUpdateConcurrencyException`.
   - TinhLuong trả **409**.
   - ChamCong và QuyetDinh hiện trả **500**, vì chưa có filter chuyển lỗi này thành 409.

`UpdatedAt` được làm tròn tới mili-giây (`ObjectBase`), nên giá trị đi qua JSON rồi quay lại vẫn khớp. Khi đặt vào query string, nhớ URL-encode (ví dụ dấu `+` của múi giờ).

NhanSu **không** dùng concurrency token: lần sửa sau ghi đè lần sửa trước.

## Xoá mềm và audit

- Mọi entity implement `IDelete` và `IAuditable` qua `ObjectBase`.
- **`SoftInterceptor`** (`Common/SoftInterceptor.cs`) chạy trước mỗi lần `SaveChanges`:
  - entity ở trạng thái `Deleted` được đổi thành `Modified`, rồi gọi `Delete(user)` để đặt `IsDeleted = true`, `DeletedAt`, `DeletedBy`;
  - entity `Modified` được gọi `Update(user)` / `UpdateBase(user)` để cập nhật `UpdatedAt`, `UpdatedBy`.
- **`DbContext.SaveChangesAsync`** đặt người tạo cho entity mới. Người dùng lấy từ claim `Name`, nếu không có thì `NameIdentifier`, nếu không có nữa thì `"System"`.
- **Global query filter** `!IsDeleted` tự ẩn bản ghi đã xoá trong mọi truy vấn. Muốn thấy cả bản ghi đã xoá thì dùng `IgnoreQueryFilters()`.

## Logging

- **Serilog** ghi ra Console và MongoDB `HrmLogsDb`, đọc cấu hình từ mục `Serilog` trong `appsettings.json`. Mỗi service ghi vào một collection riêng:

  | Service | Collection |
  |---|---|
  | API Gateway | `HttpTrafficLogs` |
  | NhanSu | `NhanSuLogs` |
  | QuyetDinh | `QuyetDinhLogs` |
  | ChamCong | `ChamCongLogs` |
  | TinhLuong | `TinhLuongLogs` |

- **Gateway** (`GatewayLoggingMiddleware`):
  - sinh hoặc giữ nguyên `X-Correlation-Id`, đặt vào `LogContext` dưới tên `TraceId`, cùng với `ClientIp` và `UserAgent`;
  - ghi mỗi request ở mức `Information`; `Warning` với 4xx; `Error` với 5xx.
- **Service** (`ResponseLoggingFilter`) ghi dữ liệu trả về của mỗi action.
- **Khi khởi động**, mỗi service ping MongoDB (timeout 3 giây). Nếu MongoDB không chạy, service vẫn chạy, chỉ ghi cảnh báo và mất log lưu vào MongoDB.

## Metrics và tracing

- **Metrics**: `prometheus-net` (`UseHttpMetrics` + `MapMetrics`) mở `GET /metrics` ở mỗi service và Gateway. Prometheus lấy số liệu theo [`prometheus.yml`](../prometheus.yml) (15 giây một lần). Trên Grafana (http://localhost:3000), thêm data source `http://prometheus:9090`.
- **Tracing**: OpenTelemetry gồm instrumentation ASP.NET Core, HttpClient (bao cả lời gọi gRPC) và source `MassTransit`, gửi trace qua OTLP tới `Otlp:Endpoint` (Jaeger, cổng 4317). Mỗi service xuất hiện trên Jaeger (http://localhost:16686) với tên: `ApiGateway`, `QuanLyNhanSuMicroservice`, `QuyetDinhService`, `ChamCongService`, `TinhLuongService`.
