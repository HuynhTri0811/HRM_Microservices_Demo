# HRM Microservices Demo

Hệ thống Quản lý Nhân sự (HRM) xây dựng theo kiến trúc **Microservices** trên **.NET 8**, gồm 4 service nghiệp vụ đứng sau một **API Gateway (YARP)**, giao tiếp với nhau qua **HTTP** và **RabbitMQ (MassTransit)**, kèm đầy đủ stack quan sát hệ thống (logging, metrics, tracing).

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
   │   :8081   │  │   :8082   │  │   :8083   │  │   :8084   │         │
   └─────┬─────┘  └─────┬─────┘  └─────┬─────┘  └─────┬─────┘         │
         │   ◄── HTTP ──┴──────────────┴──────────────┘               │
         │                                            │               │
         │ ◄────────── RabbitMQ (MassTransit) ────────┘ (Outbox)      │
         ▼                                                            │
   PostgreSQL (mỗi service 1 database)    MongoDB (log)   Prometheus/Grafana/Jaeger
```

- **Database per service**: mỗi service có database PostgreSQL riêng (`DbNhanSu`, `DbTinhLuong`, `DbChamCong`, `DbQuyetDinh`).
- **NhanSu** là service trung tâm — các service khác gọi HTTP sang để lấy thông tin nhân viên / chức vụ.
- **QuyetDinh** phát event bổ nhiệm qua RabbitMQ (dùng **Transactional Outbox**), **NhanSu** consume để cập nhật chức vụ & phụ cấp của nhân viên.

## Danh sách service

| Service | Thư mục | Port | Database | Chức năng |
|---|---|---|---|---|
| **API Gateway** | `ApiGateway/` | 8080 | — | Reverse proxy (YARP), Polly resilience, gộp Swagger |
| **Nhân Sự** | `QuanLyNhanSuMicroservice/` | 8081 | `DbNhanSu` | Tài khoản/đăng nhập, Nhân viên, Phòng ban, Chức vụ, Văn bằng |
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
├── ApiGateway/                     # YARP gateway
├── QuanLyNhanSuMicroservice/       # Service Nhân sự
│   ├── QuanLyNhanVien.API/         #   Controllers, Filters
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
   - `NhanSuApiUrl` → URL thực tế của service NhanSu
   - `Serilog:WriteTo:1:Args:databaseUrl` → `mongodb://mongo:mongo@localhost:27017/HrmLogsDb?authSource=admin`
3. Chạy từng service:
   ```bash
   dotnet run --project QuanLyNhanSuMicroservice
   ```
   Lưu ý: port trong `Properties/launchSettings.json` khác với port Docker (8081–8084). Nếu chạy Gateway local thì cập nhật lại `ReverseProxy:Clusters:*:Destinations:destination1:Address` trong `ApiGateway/appsettings.json` cho khớp.

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

Các service dùng chung cấu hình `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` nên token do NhanSu cấp hợp lệ ở mọi service. Token cũng được forward khi một service gọi HTTP sang NhanSu.

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

### Đồng bộ — HTTP (`INhanSuServiceClient`)

Các service gọi sang NhanSu qua `NhanSuApiUrl`, forward header `Authorization` của request gốc:

| Service gọi | Endpoint NhanSu | Mục đích |
|---|---|---|
| ChamCong | `GET /api/NhanVien/{id}` | Kiểm tra nhân viên khi tạo phiếu nghỉ |
| TinhLuong | `GET /api/NhanVien` | Lấy danh sách nhân viên để tính lương |
| QuyetDinh | `GET /api/NhanVien/{id}`, `GET /api/ChucVu/{id}` | Lấy phụ cấp cũ/mới khi bổ nhiệm |
| QuyetDinh | `PATCH /api/NhanVien/{id}/update-luong` | Cập nhật lương khi tạo/sửa/xoá quyết định nâng lương |

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
