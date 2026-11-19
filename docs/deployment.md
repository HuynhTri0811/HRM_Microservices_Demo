# Triển khai và chạy

## Yêu cầu

- Docker và Docker Compose (để chạy toàn bộ hệ thống).
- .NET SDK 8 trở lên (để build, chạy local hoặc chạy test). Các project nhắm tới `net8.0`. Nếu máy chỉ có runtime mới hơn (ví dụ .NET 10), đặt `DOTNET_ROLL_FORWARD=Major` khi chạy.
- Các port trống trên máy: 8080–8084, 5433, 5672, 15672, 27017, 9090, 3000, 16686, 4317, 4318. Xem [configuration.md](configuration.md#port).

## Chạy bằng Docker Compose (khuyến nghị)

```bash
docker compose up -d --build
```

Compose build 5 image (Gateway + 4 service) từ Dockerfile của từng thư mục, với build context là thư mục gốc repo. Dockerfile chạy `COPY . .` nên `Protos/nhansu.proto` có sẵn khi build. Compose cũng chạy kèm PostgreSQL, MongoDB, RabbitMQ, Prometheus, Grafana, Jaeger.

| URL | Nội dung |
|---|---|
| http://localhost:8080/ | Web UI kiểm thử (HRM Test Console) |
| http://localhost:8080/swagger | Swagger tổng hợp |
| http://localhost:15672 | RabbitMQ Management (`guest` / `guest`) |
| http://localhost:16686 | Jaeger |
| http://localhost:9090 | Prometheus |
| http://localhost:3000 | Grafana (`admin` / `admin`) |

Kiểm tra nhanh sau khi khởi động:

```bash
docker compose ps
```

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/swagger/nhansu/v1/swagger.json
```

Lệnh thứ hai trả `200` nghĩa là Gateway đã nói chuyện được với NhanSu. Sau đó mở web UI và bấm *Chạy toàn bộ* để kiểm tra mọi luồng ([testing.md](testing.md#web-ui-kiểm-thử-hrm-test-console)).

Dừng hệ thống:

```bash
docker compose down
```

Dừng và xoá luôn dữ liệu (volume `postgres-data`, `mongodb-data`):

```bash
docker compose down -v
```

### Database và migration

- **Tạo database:** `init-db.sql` được mount vào container PostgreSQL và tạo sẵn 4 database khi volume còn trống. Ngay cả khi thiếu, EF Core `Migrate()` cũng tự tạo database.
- **Áp migration:** mỗi service gọi `app.MigrateDatabase()` khi khởi động, nên không cần chạy `dotnet ef database update` bằng tay.
- **Dữ liệu seed duy nhất** là 2 trạng thái nhân viên (migration `SeedTinhTrangNhanVien`). Tài khoản đăng nhập phải tự tạo qua `POST /api/nhansu/Auth/register` (hoặc nút *Đăng ký* trên web UI).

### Thứ tự khởi động

`depends_on` trong `docker-compose.yml` chỉ đợi container **bắt đầu chạy**, không đợi dịch vụ bên trong sẵn sàng. Nếu một service khởi động trước khi PostgreSQL nhận kết nối, `Migrate()` sẽ lỗi và container dừng; `restart: always` sẽ khởi động lại cho tới khi thành công. Với RabbitMQ, MassTransit tự kết nối lại, nên service vẫn phục vụ HTTP trong lúc chờ broker.

## Chạy local bằng `dotnet run`

Phù hợp khi cần debug một service.

1. Bật hạ tầng bằng Compose:

   ```bash
   docker compose up -d postgres mongodb rabbitmq jaeger
   ```

2. Ghi đè cấu hình để trỏ tới `localhost`, vì `appsettings.json` dùng tên host trong Docker. Ví dụ cho NhanSu:

   ```bash
   export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=DbNhanSu;Username=postgres;Password=postgres"
   export RabbitMQ__Host=localhost
   export Otlp__Endpoint=http://localhost:4317
   export Serilog__WriteTo__1__Args__databaseUrl="mongodb://mongo:mongo@localhost:27017/HrmLogsDb?authSource=admin"
   ```

   Các service gọi NhanSu cần thêm `NhanSuGrpcUrl=http://localhost:9081`. Danh sách khoá đầy đủ ở [configuration.md](configuration.md).

3. Chạy service:

   ```bash
   dotnet run --project QuanLyNhanSuMicroservice
   ```

4. Nếu chạy cả Gateway ở local: cập nhật `ReverseProxy:Clusters:*:Destinations:destination1:Address` cho khớp với port local của từng service. NhanSu dùng 8081 (theo `Kestrel:Endpoints`); các service khác dùng port trong `Properties/launchSettings.json`.

Có thể trộn hai cách: chạy hầu hết bằng Compose, chỉ dừng một container (`docker compose stop quyetdinh-api`) rồi chạy service đó bằng `dotnet run` để debug. Khi đó Gateway trong Docker không gọi được tới máy host, trừ khi bạn đổi địa chỉ cluster thành `http://host.docker.internal:<port>`.

## Tạo migration mới

```bash
dotnet ef migrations add <TenMigration> --project QuyetDinhService
```

Migration được tạo trong thư mục `Migrations/` của project và áp tự động ở lần khởi động tiếp theo. Cần công cụ `dotnet-ef`:

```bash
dotnet tool install --global dotnet-ef
```

## Build từng image

```bash
docker build -f QuanLyNhanSuMicroservice/Dockerfile -t nhansu-api .
```

Phải chạy từ thư mục gốc repo, vì Dockerfile cần cả `Protos/`.
