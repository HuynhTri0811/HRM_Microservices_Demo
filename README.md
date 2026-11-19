# HRM Microservices Demo

Hệ thống Quản lý Nhân sự (HRM) xây dựng theo kiến trúc **Microservices** trên **.NET 8**, gồm 4 service nghiệp vụ đứng sau một **API Gateway (YARP)**, giao tiếp với nhau qua **gRPC** và **RabbitMQ (MassTransit)**, kèm đầy đủ stack quan sát hệ thống (logging, metrics, tracing).

---

## Mục lục

- [Kiến trúc tổng quan](#kiến-trúc-tổng-quan)
- [Danh sách service](#danh-sách-service)
- [Công nghệ sử dụng](#công-nghệ-sử-dụng)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Chạy dự án](#chạy-dự-án)
- [Xác thực (JWT)](#xác-thực-jwt)
- [API Endpoints](#api-endpoints)
- [Giao tiếp giữa các service](#giao-tiếp-giữa-các-service)
- [Xử lý lỗi & Concurrency](#xử-lý-lỗi--concurrency)
- [Observability](#observability)
- [Database & Migration](#database--migration)

---

## Kiến trúc tổng quan

```
                          ┌──────────────────────────┐
           Client ──────► │  API Gateway (YARP) :8080│  Polly: Retry + Circuit Breaker + Timeout
                          └────────────┬─────────────┘  Swagger tổng hợp tại /swagger
         ┌──────────────┬──────────────┼──────────────┬───────────────┐
         ▼              ▼              ▼              ▼               │
   ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐         │
   │  NhanSu   │  │ TinhLuong │  │ ChamCong  │  │ QuyetDinh │         │
   │:8081 REST │  │   :8082   │  │   :8083   │  │   :8084   │         │
   │:9081 gRPC │  │           │  │           │  │           │         │
   └─────┬─────┘  └─────┬─────┘  └─────┬─────┘  └─────┬─────┘         │
         │   ◄── gRPC ──┴──────────────┴──────────────┘               │
         │                                            │               │
         │ ◄────────── RabbitMQ (MassTransit) ────────┘ (Outbox)      │
         ▼                                                            │
   PostgreSQL (mỗi service 1 database)    MongoDB (log)   Prometheus/Grafana/Jaeger
```

- **Database per service**: mỗi service có database PostgreSQL riêng (`DbNhanSu`, `DbTinhLuong`, `DbChamCong`, `DbQuyetDinh`).
- **NhanSu** là service trung tâm — các service khác gọi **gRPC** sang (port 9081) để lấy/cập nhật thông tin nhân viên, chức vụ. Không còn service nào gọi REST nội bộ.
- **QuyetDinh** phát event bổ nhiệm qua RabbitMQ (dùng **Transactional Outbox**), **NhanSu** consume để cập nhật chức vụ & phụ cấp của nhân viên.

## Danh sách service

| Service | Thư mục | Port | Database | Chức năng |
|---|---|---|---|---|
| **API Gateway** | `ApiGateway/` | 8080 | — | Reverse proxy (YARP), Polly resilience, gộp Swagger |
| **Nhân Sự** | `QuanLyNhanSuMicroservice/` | 8081 (REST), 9081 (gRPC) | `DbNhanSu` | Tài khoản/đăng nhập, Nhân viên, Phòng ban, Chức vụ, Văn bằng; gRPC server cho các service khác |
| **Tính Lương** | `TinhLuongService/` | 8082 | `DbTinhLuong` | Kỳ tính lương: khoá/mở kỳ, tính lương |
| **Chấm Công** | `ChamCongService/` | 8083 | `DbChamCong` | Loại chấm công, Phiếu đăng ký nghỉ, Bảng chấm công tháng (chốt/mở chốt) |
| **Quyết Định** | `QuyetDinhService/` | 8084 | `DbQuyetDinh` | Quyết định bổ nhiệm, Quyết định nâng lương |

Hạ tầng đi kèm (trong `docker-compose.yml`):

| Thành phần | Port | Ghi chú |
|---|---|---|
| PostgreSQL 15 | 5433 → 5432 | user/pass: `postgres`/`postgres`, tạo DB qua `init-db.sql` |
| MongoDB 7 | 27017 | user/pass: `mongo`/`mongo`, lưu log Serilog (`HrmLogsDb`) |
| RabbitMQ | 5672, 15672 (UI) | user/pass: `guest`/`guest` |
| Prometheus | 9090 | cấu hình trong `prometheus.yml` |
| Grafana | 3000 | admin/`admin` |
| Jaeger | 16686 (UI), 4317/4318 (OTLP) | distributed tracing |

## Công nghệ sử dụng

- **.NET 8 / ASP.NET Core Web API**
- **Clean Architecture + CQRS** với **MediatR** (tách lớp `API` / `Application` / `Domain` / `Infrastructure` trong từng service)
- **Entity Framework Core 8 + Npgsql** (PostgreSQL), soft delete qua `SoftInterceptor`, auto-migrate khi khởi động
- **gRPC** (Grpc.AspNetCore / Grpc.Net.ClientFactory, contract chung `Protos/nhansu.proto`)
- **MassTransit 8 + RabbitMQ**, **EF Core Outbox** (QuyetDinhService)
- **YARP** reverse proxy + **Polly** (retry, circuit breaker, timeout)
- **JWT Bearer** authentication, mật khẩu hash bằng **BCrypt**
- **Serilog** → Console + MongoDB
- **prometheus-net** (endpoint `/metrics`), **OpenTelemetry** → Jaeger
- **Swagger / Swashbuckle**
- **Scrutor** (đăng ký DI tự động – NhanSu)

## Cấu trúc thư mục

```
HRM_Microservices_Demo/
├── Protos/
│   └── nhansu.proto                # Contract gRPC của NhanSu (server + client dùng chung)
├── ApiGateway/                     # YARP gateway
│   └── wwwroot/index.html          #   Web UI kiểm thử (http://localhost:8080/)
├── QuanLyNhanSuMicroservice/       # Service Nhân sự
│   ├── QuanLyNhanVien.API/         #   Controllers, Filters, Grpc (NhanSuGrpcService)
│   ├── QuanLyNhanVien.Application/ #   Commands/DTO, Queries, Handlers, Consumers, Events, Exception
│   ├── QuanLyNhanVien.Domain/      #   Entities, Repository interfaces, Domain exceptions
│   ├── QuanLyNhanVien.Infrastructure/ # DbContext, Repositories
│   ├── Extensions/                 #   Cấu hình Swagger, DB, Auth, Messaging, Logging, Observability
│   └── Migrations/
├── TinhLuongService/               # Service Tính lương (cùng cấu trúc)
├── ChamCongService/                # Service Chấm công (cùng cấu trúc)
├── QuyetDinhService/               # Service Quyết định (cùng cấu trúc)
├── docker-compose.yml
├── init-db.sql                     # Tạo 4 database PostgreSQL
├── prometheus.yml                  # Scrape config cho 5 service
└── QuanLyNhanSuSystem.slnx         # Solution
```

## Chạy dự án

### Cách 1 — Docker Compose (khuyến nghị)

```bash
docker compose up -d --build
```

Sau khi khởi động:

| URL | Mô tả |
|---|---|
| http://localhost:8080/ | **HRM Test Console** – web UI kiểm thử (xem [bên dưới](#web-ui-kiểm-thử)) |
| http://localhost:8080/swagger | Swagger tổng hợp của tất cả service qua Gateway |
| http://localhost:15672 | RabbitMQ Management |
| http://localhost:16686 | Jaeger UI |
| http://localhost:9090 | Prometheus |
| http://localhost:3000 | Grafana |

Mỗi service tự chạy migration khi khởi động, nên không cần `dotnet ef database update` thủ công.

### Cách 2 — Chạy local bằng `dotnet run`

1. Chỉ bật hạ tầng:
   ```bash
   docker compose up -d postgres mongodb rabbitmq jaeger
   ```
2. File `appsettings.json` đang trỏ tới hostname trong Docker (`postgres`, `rabbitmq`, `mongodb`, `nhansu-api`). Khi chạy ngoài Docker cần override, ví dụ:
   - `ConnectionStrings:DefaultConnection` → `Host=localhost;Port=5433;...`
   - `RabbitMQ:Host` → `localhost`
   - `NhanSuGrpcUrl` (ChamCong, QuyetDinh, TinhLuong) → địa chỉ gRPC của NhanSu, ví dụ `http://localhost:9081`
   - `Serilog:WriteTo:1:Args:databaseUrl` → `mongodb://mongo:mongo@localhost:27017/HrmLogsDb?authSource=admin`
3. Chạy từng service:
   ```bash
   dotnet run --project QuanLyNhanSuMicroservice
   ```
   Lưu ý:
   - NhanSu luôn lắng nghe theo `Kestrel:Endpoints` trong `appsettings.json` (8081 REST/HTTP1, 9081 gRPC/HTTP2), bỏ qua `applicationUrl` trong `launchSettings.json`.
   - Các service còn lại dùng port trong `Properties/launchSettings.json` (khác port Docker 8082–8084). Nếu chạy Gateway local thì cập nhật lại `ReverseProxy:Clusters:*:Destinations:destination1:Address` trong `ApiGateway/appsettings.json` cho khớp.

## Web UI kiểm thử

API Gateway phục vụ sẵn một trang kiểm thử tại **http://localhost:8080/** (file [`ApiGateway/wwwroot/index.html`](ApiGateway/wwwroot/index.html), HTML/JS thuần, không cần build hay CDN). Trang gọi API qua chính Gateway nên không vướng CORS.

- **Kịch bản tự động**: bấm *Chạy toàn bộ* để tạo dữ liệu mới (hậu tố ngẫu nhiên, tài khoản test riêng) và đi qua toàn bộ luồng; mỗi bước ghi rõ kênh được kiểm tra (REST, gRPC tới NhanSu, RabbitMQ) và kết quả đạt/lỗi/bỏ qua:
  1. Gateway thấy cả 4 service (qua Swagger)
  2. Đăng ký + đăng nhập
  3. Tạo phòng ban, 2 chức vụ, nhân viên
  4. QĐ nâng lương → lương nhân viên được cập nhật (kiểm tra giữ đủ số lẻ)
  5. Danh sách QĐ kèm thông tin nhân viên
  6. QĐ bổ nhiệm lấy đúng phụ cấp chức vụ mới
  7. Event bổ nhiệm → NhanSu đổi chức vụ nhân viên (chờ tối đa 15 giây)
  8. Chấm công: loại chấm công + phiếu nghỉ; tạo và chốt bảng công tháng
  9. Tạo kỳ và tính lương
- **Tab theo service** (Nhân sự, Quyết định, Chấm công, Tính lương): xem bảng dữ liệu, tạo mới bằng form (chọn nhân viên/chức vụ/phòng ban từ dropdown thay vì gõ Guid), xoá, và các thao tác riêng như sửa lương, chốt/mở chốt bảng công, khoá/mở/tính lương kỳ.
- **Gọi API tự do**: chọn method, service, đường dẫn và body JSON.
- **Nhật ký request**: method, URL, status, thời gian, body request/response và `X-Correlation-Id` (dùng để tìm log trong MongoDB/trace trong Jaeger).

Đăng nhập ở góc phải (nút *Đăng ký* tạo tài khoản role `Admin`). Token và tab đang mở được nhớ trong `localStorage` của trình duyệt. Ô *Gateway* cho phép trỏ tới Gateway khác (ví dụ khi mở file HTML trực tiếp).

## Xác thực (JWT)

Tất cả controller đều có `[Authorize]`, trừ `AuthController`.

1. Đăng ký tài khoản:
   ```http
   POST /api/nhansu/Auth/register
   { "username": "admin", "password": "123456", "role": "Admin" }
   ```
2. Đăng nhập lấy token (hạn 2 giờ):
   ```http
   POST /api/nhansu/Auth/login
   { "username": "admin", "password": "123456" }
   ```
3. Gửi kèm header `Authorization: Bearer <token>` cho các request khác.

Các service dùng chung cấu hình `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` nên token do NhanSu cấp hợp lệ ở mọi service. Khi một service gọi gRPC sang NhanSu, token của request gốc được forward qua metadata `Authorization` (gRPC service của NhanSu cũng có `[Authorize]`).

> ⚠️ `Jwt:Key` và các mật khẩu trong repo chỉ dùng cho môi trường demo/dev.

## API Endpoints

Qua Gateway, prefix `/api/{service}/...` được chuyển thành `/api/...` ở service đích:

| Gateway | Service đích |
|---|---|
| `/api/nhansu/**` | NhanSu `:8081/api/**` |
| `/api/tinhluong/**` | TinhLuong `:8082/api/**` |
| `/api/chamcong/**` | ChamCong `:8083/api/**` |
| `/api/quyetdinh/**` | QuyetDinh `:8084/api/**` |

### Nhân Sự (`/api/nhansu`)

| Method | Route | Mô tả |
|---|---|---|
| POST | `/Auth/register` | Đăng ký tài khoản |
| POST | `/Auth/login` | Đăng nhập, trả JWT |
| GET / POST | `/NhanVien` | Danh sách / tạo nhân viên |
| GET / PUT / DELETE | `/NhanVien/{id}` | Chi tiết / cập nhật / xoá |
| PATCH | `/NhanVien/{id}/update-luong` | Cập nhật lương cơ bản |
| PATCH | `/NhanVien/{id}/update-chuc-vu` | Cập nhật chức vụ |
| GET / POST | `/PhongBan` | Danh sách / tạo phòng ban |
| PUT / DELETE | `/PhongBan/{id}` | Cập nhật / xoá |
| GET / POST | `/ChucVu` | Danh sách / tạo chức vụ |
| GET / PUT / DELETE | `/ChucVu/{id}` | Chi tiết / cập nhật / xoá |
| GET / POST | `/VanBang` | Danh sách / tạo văn bằng |
| GET / PUT / DELETE | `/VanBang/{id}` | Chi tiết / cập nhật / xoá |
| GET | `/VanBang/nhanvien/{nhanVienId}` | Văn bằng của một nhân viên |

### Tính Lương (`/api/tinhluong`)

| Method | Route | Mô tả |
|---|---|---|
| GET / POST | `/KyTinhLuong` | Danh sách / tạo kỳ tính lương |
| GET / PUT / DELETE | `/KyTinhLuong/{id}` | Chi tiết / cập nhật / xoá |
| PATCH | `/KyTinhLuong/{id}/khoa?updatedAt=...` | Khoá kỳ |
| PATCH | `/KyTinhLuong/{id}/mo?updatedAt=...` | Mở kỳ |
| PATCH | `/KyTinhLuong/{id}/tinhluong?updatedAt=...` | Tính lương (lấy danh sách nhân viên từ NhanSu) |

### Chấm Công (`/api/chamcong`)

| Method | Route | Mô tả |
|---|---|---|
| GET / POST | `/LoaiChamCong` | Danh sách / tạo loại chấm công |
| GET / PUT / DELETE | `/LoaiChamCong/{id}` | Chi tiết / cập nhật / xoá |
| GET / POST | `/PhieuDangKyNghi` | Danh sách / tạo phiếu nghỉ |
| GET / PUT / DELETE | `/PhieuDangKyNghi/{id}` | Chi tiết / cập nhật / xoá |
| GET / POST | `/BangChamCongTheoThang` | Danh sách / tạo bảng công tháng |
| GET / PUT / DELETE | `/BangChamCongTheoThang/{id}` | Chi tiết / cập nhật / xoá |
| PATCH | `/BangChamCongTheoThang/{id}/chot?updatedAt=...` | Chốt bảng công |
| PATCH | `/BangChamCongTheoThang/{id}/mo-chot?updatedAt=...` | Mở chốt bảng công |

### Quyết Định (`/api/quyetdinh`)

| Method | Route | Mô tả |
|---|---|---|
| GET / POST | `/QuyetDinhBoNhiem` | Danh sách / tạo quyết định bổ nhiệm |
| GET / PUT / DELETE | `/QuyetDinhBoNhiem/{id}` | Chi tiết / cập nhật / xoá |
| GET / POST | `/QuyetDinhNangLuong` | Danh sách / tạo quyết định nâng lương |
| GET / PUT / DELETE | `/QuyetDinhNangLuong/{id}` | Chi tiết / cập nhật / xoá |

Chi tiết request/response xem trên Swagger: http://localhost:8080/swagger.

## Giao tiếp giữa các service

### Đồng bộ — gRPC (`INhanSuServiceClient`)

Contract nằm ở [`Protos/nhansu.proto`](Protos/nhansu.proto) (service `nhansu.v1.NhanSuGrpc`). Mỗi project tự sinh code bằng `Grpc.Tools` từ cùng file này: NhanSu sinh **server** (`QuanLyNhanVien.API/Grpc/NhanSuGrpcService.cs`), các service còn lại sinh **client** và bọc trong `NhanSuServiceClient` (giữ nguyên interface `INhanSuServiceClient`, nên handler không phụ thuộc vào transport).

| Service gọi | RPC | Mục đích |
|---|---|---|
| ChamCong | `GetNhanVienById` | Kiểm tra nhân viên khi tạo phiếu nghỉ |
| TinhLuong | `GetAllNhanVien` | Lấy danh sách nhân viên để tính lương |
| QuyetDinh | `GetNhanVienById`, `GetChucVuById` | Lấy phụ cấp cũ/mới khi bổ nhiệm; hiển thị thông tin nhân viên trong danh sách quyết định |
| QuyetDinh | `UpdateLuong` | Cập nhật lương khi tạo/sửa/xoá quyết định nâng lương |
| QuyetDinh | `UpdateBoNhiem` | Cập nhật chức vụ + phụ cấp (hiện luồng bổ nhiệm dùng RabbitMQ, RPC này để dự phòng) |

Chi tiết kỹ thuật:
- NhanSu mở 2 endpoint Kestrel: **8081** (HTTP/1.1 – REST, Swagger, `/metrics`, Gateway) và **9081** (HTTP/2 cleartext – gRPC). Port 9081 chỉ dùng trong mạng nội bộ Docker, không publish ra host.
- Client đăng ký bằng `AddGrpcClient<NhanSuGrpc.NhanSuGrpcClient>` với địa chỉ `NhanSuGrpcUrl` (mặc định `http://nhansu-api:9081`).
- `Guid` và `decimal` được truyền dạng `string` (InvariantCulture) để không mất độ chính xác tiền lương; ngày dùng `google.protobuf.Timestamp`.
- Lỗi: `NOT_FOUND` → client trả `null` (handler ném `NotFoundException` → HTTP 404); `INVALID_ARGUMENT` khi Id/số không hợp lệ; lỗi nghiệp vụ khi cập nhật → `FAILED_PRECONDITION`.

Thêm RPC mới: sửa `Protos/nhansu.proto` → implement trong `NhanSuGrpcService` → gọi từ `NhanSuServiceClient` của service cần dùng. Code được sinh lại tự động khi build.

### Bất đồng bộ — RabbitMQ (MassTransit)

| Event | Publisher | Consumer (queue) | Tác dụng |
|---|---|---|---|
| `QuyetDinhBoNhiemCreatedEvent` | QuyetDinh | NhanSu (`nhansu-quyetdinh-bonhiem-created`) | Cập nhật chức vụ & phụ cấp mới |
| `QuyetDinhBoNhiemUpdatedEvent` | QuyetDinh | NhanSu (`nhansu-quyetdinh-bonhiem-updated`) | Cập nhật lại chức vụ & phụ cấp mới |
| `QuyetDinhBoNhiemDeletedEvent` | QuyetDinh | NhanSu (`nhansu-quyetdinh-bonhiem-deleted`) | Khôi phục chức vụ & phụ cấp cũ |
| `NhanVienCreatedEvent` | NhanSu | _(chưa có consumer)_ | — |
| `PhieuDangKyNghiCreatedEvent` | ChamCong | _(chưa có consumer)_ | — |

QuyetDinhService dùng **MassTransit EF Core Outbox** (PostgreSQL): event được ghi vào bảng outbox cùng transaction với dữ liệu nghiệp vụ, sau đó mới đẩy lên RabbitMQ — tránh mất event khi broker lỗi.

> Lưu ý: event được định nghĩa trùng lặp ở cả service publish và consume, và phải **giữ cùng namespace** (`QuyetDinhService.QuyetDinhService.Application.Events`) để MassTransit khớp message type.

## Xử lý lỗi & Concurrency

Mỗi service có `ExceptionHandlingMiddleware` trả lỗi theo format thống nhất:

```json
{
  "timestamp": "2026-06-02T12:00:00Z",
  "status": 404,
  "error": "Not Found",
  "message": "Không tìm thấy nhân viên trong hệ thống nhân sự",
  "code": "NOT_FOUND",
  "errors": { }
}
```

| Exception | HTTP status |
|---|---|
| `BadRequestException` (Domain) | 400 |
| `IsExistException` | 400 |
| `NotFoundException` | 404 |
| Khác | 500 |

**Optimistic concurrency**: các thao tác cập nhật/khoá/chốt nhận tham số `updatedAt` (giá trị `UpdatedAt` lúc client đọc dữ liệu). Nếu bản ghi đã bị người khác thay đổi, EF Core ném `DbUpdateConcurrencyException`; TinhLuongService chuyển lỗi này thành **409 Conflict** qua `ConcurrencyExceptionFilter`.

## Observability

- **Logging**: Serilog ghi ra Console và MongoDB `HrmLogsDb`, mỗi service một collection (`NhanSuLogs`, `TinhLuongLogs`, `ChamCongLogs`, `QuyetDinhLogs`, `HttpTrafficLogs` cho Gateway). `ResponseLoggingFilter` / `GatewayLoggingMiddleware` log request/response.
- **Metrics**: mỗi service expose `/metrics` (prometheus-net), Prometheus scrape theo `prometheus.yml`, xem dashboard trên Grafana (thêm data source `http://prometheus:9090`).
- **Tracing**: OpenTelemetry (ASP.NET Core + HttpClient) export OTLP tới Jaeger (`Otlp:Endpoint`), theo dõi được một request đi từ Gateway qua các service.
- **Resilience (Gateway)**: Polly pipeline cho YARP — retry 3 lần (exponential backoff + jitter) với lỗi 500/502/503, circuit breaker (tỷ lệ lỗi ≥ 50% trong 10s, tối thiểu 8 request, ngắt 30s), timeout 5s.

## Database & Migration

Migration nằm trong thư mục `Migrations/` của từng service và được áp dụng tự động lúc khởi động (`app.MigrateDatabase()`).

Tạo migration mới (ví dụ cho QuyetDinhService):

```bash
dotnet ef migrations add <TenMigration> --project QuyetDinhService
```
