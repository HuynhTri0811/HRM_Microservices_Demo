# Cấu hình

Cấu hình đọc theo thứ tự chuẩn của ASP.NET Core: `appsettings.json` → `appsettings.{Environment}.json` → biến môi trường. Biến môi trường dùng `__` thay cho `:` (ví dụ `RabbitMQ__Host`). Giá trị mặc định trong repo dùng tên host trong mạng Docker (`postgres`, `rabbitmq`, `mongodb`, `nhansu-api`...).

## Port

| Thành phần | Port trong container | Port publish ra host (`docker-compose.yml`) | Port khi `dotnet run` |
|---|---|---|---|
| API Gateway | 8080 | 8080 | 43722 (https 43721) |
| NhanSu – REST | 8081 | 8081 | 8081 (`Kestrel:Endpoints`) |
| NhanSu – gRPC | 9081 | không publish (chỉ dùng nội bộ) | 9081 (`Kestrel:Endpoints`) |
| TinhLuong | 8082 | 8082 | 5091 |
| ChamCong | 8083 | 8083 | 5196 |
| QuyetDinh | 8084 | 8084 | 60418 |
| PostgreSQL | 5432 | 5433 | — |
| MongoDB | 27017 | 27017 | — |
| RabbitMQ (AMQP / UI) | 5672 / 15672 | 5672 / 15672 | — |
| Prometheus | 9090 | 9090 | — |
| Grafana | 3000 | 3000 | — |
| Jaeger (UI / OTLP gRPC / OTLP HTTP) | 16686 / 4317 / 4318 | như trong container | — |

Các service (trừ NhanSu) lấy port container từ `ASPNETCORE_URLS` trong `Dockerfile`. NhanSu khai báo `Kestrel:Endpoints`, nên port của nó luôn theo `appsettings.json` và bỏ qua `ASPNETCORE_URLS` lẫn `applicationUrl` trong `launchSettings.json`.

## Khoá cấu hình theo service

### Dùng chung cho cả 4 service nghiệp vụ

| Khoá | Ví dụ / mặc định | Ý nghĩa |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `Host=postgres;Port=5432;Database=DbNhanSu;Username=postgres;Password=postgres` | Database của service (`DbNhanSu`, `DbQuyetDinh`, `DbChamCong`, `DbTinhLuong`) |
| `Jwt:Key` | `SuperSecretKeyForJwtAuthenticationWhichIsVeryLong!123` | Khoá ký JWT; **phải giống nhau** ở mọi service |
| `Jwt:Issuer` / `Jwt:Audience` | `QuanLyNhanSuMicroservice` | Phải giống nhau ở mọi service |
| `Serilog:WriteTo:1:Args:databaseUrl` | `mongodb://mongo:mongo@mongodb:27017/HrmLogsDb?authSource=admin` | MongoDB nhận log |
| `Serilog:WriteTo:1:Args:collectionName` | `NhanSuLogs`... | Collection log của service |
| `Otlp:Endpoint` | `http://jaeger:4317` (mặc định trong code: `http://localhost:4317`) | Nơi gửi trace |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Swagger chỉ bật khi là `Development` |

### Theo từng service

| Khoá | NhanSu | QuyetDinh | ChamCong | TinhLuong | Ý nghĩa |
|---|:-:|:-:|:-:|:-:|---|
| `RabbitMQ:Host` / `Username` / `Password` | ✓ | ✓ | ✓ | | Broker MassTransit (mặc định `localhost` / `guest` / `guest`; luôn dùng port 5672) |
| `NhanSuGrpcUrl` | | ✓ | ✓ | ✓ | Địa chỉ gRPC của NhanSu, mặc định `http://nhansu-api:9081` |
| `Kestrel:Endpoints:Http:Url` | ✓ | | | | REST, HTTP/1.1 (`http://+:8081`) |
| `Kestrel:Endpoints:Grpc:Url` | ✓ | | | | gRPC, HTTP/2 (`http://+:9081`) |

Bảng lưu lịch sử migration được đặt tên riêng cho từng service: `__NhanSuMigrationsHistory`, `__QuyetDinhMigrationsHistory`, `__ChamCongMigrationsHistory`, `__TinhLuongMigrationsHistory`.

### API Gateway

| Khoá | Mặc định trong `appsettings.json` | Giá trị trong `docker-compose.yml` |
|---|---|---|
| `ReverseProxy:Clusters:nhansu-cluster:Destinations:destination1:Address` | `http://localhost:8081` | `http://nhansu-api:8081` |
| `ReverseProxy:Clusters:quyetdinh-cluster:Destinations:destination1:Address` | `http://localhost:8084` | `http://quyetdinh-api:8084` |
| `ReverseProxy:Clusters:chamcong-cluster:Destinations:destination1:Address` | `http://localhost:8083` | `http://chamcong-api:8083` |
| `ReverseProxy:Clusters:tinhluong-cluster:Destinations:destination1:Address` | `http://localhost:8082` | `http://tinhluong-api:8082` |

Gateway dùng chính các địa chỉ này để lấy `/swagger/v1/swagger.json` của từng service khi gộp Swagger. Các route (`ReverseProxy:Routes`) được mô tả ở [communication.md](communication.md#rest-qua-gateway).

## Ghi đè khi chạy ngoài Docker

Ví dụ chạy QuyetDinh trên máy, trỏ tới hạ tầng chạy bằng Docker Compose:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=DbQuyetDinh;Username=postgres;Password=postgres"
export RabbitMQ__Host=localhost
export NhanSuGrpcUrl=http://localhost:9081
export Otlp__Endpoint=http://localhost:4317
export Serilog__WriteTo__1__Args__databaseUrl="mongodb://mongo:mongo@localhost:27017/HrmLogsDb?authSource=admin"
dotnet run --project QuyetDinhService
```

Hướng dẫn đầy đủ ở [deployment.md](deployment.md#chạy-local-bằng-dotnet-run).
