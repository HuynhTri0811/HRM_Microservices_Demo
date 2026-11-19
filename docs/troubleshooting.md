# Xử lý sự cố và giới hạn đã biết

## Công cụ chẩn đoán

| Muốn biết | Cách xem |
|---|---|
| Container nào lỗi | `docker compose ps`, `docker compose logs <service> --tail 100` |
| Một request đi đâu | Lấy `X-Correlation-Id` từ response (hoặc từ nhật ký request của web UI), tìm trong MongoDB `HrmLogsDb` hoặc trên Jaeger |
| Event RabbitMQ | http://localhost:15672 → *Queues*: số consumer và số message của `nhansu-quyetdinh-bonhiem-*` |
| Outbox của QuyetDinh | Bảng `OutboxMessage` trong `DbQuyetDinh` |
| Service còn sống không | `GET http://localhost:808x/metrics`, hoặc đèn trạng thái trên web UI |

## Lỗi thường gặp

### `docker compose up` báo port đã được dùng

Compose publish nhiều port cố định (5433, 3000, 5672, 15672, 27017, 8080–8084...). Hãy tắt chương trình đang chiếm port, hoặc đổi vế trái của mục `ports` trong `docker-compose.yml` (ví dụ `"15433:5432"`).

Riêng 5672: nếu máy đang có RabbitMQ khác (hoặc một ssh tunnel) ở `localhost:5672`, service chạy bằng `dotnet run` với `RabbitMQ:Host=localhost` sẽ kết nối **nhầm** vào broker đó, vì code luôn dùng port 5672.

### 401 Unauthorized

- Thiếu header hoặc header sai dạng. Header phải là `Authorization: Bearer <token>`; trên Swagger cũng phải nhập cả chữ `Bearer`.
- Token đã hết hạn (2 giờ). Đăng nhập lại.
- Các service có `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` khác nhau. Chúng phải giống hệt nhau.
- Đăng nhập sai cũng trả 401 `{ "message": "Tên đăng nhập hoặc mật khẩu không đúng." }`.

### 502 hoặc 503 khi gọi qua Gateway

- Service đích chưa chạy hoặc đang khởi động lại. Kiểm tra `docker compose ps` và log của service đó.
- Circuit breaker của service đó đang mở: nhiều lỗi hạ tầng liên tiếp (không kết nối được, 502/503/504) làm Gateway chặn service đó 30 giây. Lỗi 500 do service trả về **không** làm mở breaker.
- Nếu service trả **500** thì Gateway chuyển nguyên 500 cùng JSON lỗi. Xem trường `message` và log của service.

### Swagger tổng hợp báo "Không thể kết nối lấy tài liệu Swagger từ ..."

Service chưa chạy, hoặc không chạy ở môi trường `Development` (Swagger chỉ bật khi `ASPNETCORE_ENVIRONMENT=Development`), hoặc địa chỉ cluster trong `ReverseProxy:Clusters` của Gateway sai.

### Lời gọi gRPC tới NhanSu thất bại (danh sách quyết định có `nhanVien: null`, tạo phiếu nghỉ báo "Nhân viên không tồn tại")

- `NhanSuGrpcUrl` phải trỏ tới **port gRPC 9081**, không phải 8081. Port 8081 chỉ nói HTTP/1.1, nên gọi gRPC vào đó sẽ lỗi giao thức.
- NhanSu phải đang chạy và mở `Kestrel:Endpoints:Grpc`.
- Log phía client có dòng `gRPC ... tới NhanSu thất bại. Status: ...`. Ví dụ `Unauthenticated` nghĩa là token không được chuyển tiếp hoặc không hợp lệ.

### Quyết định bổ nhiệm đã lưu nhưng chức vụ nhân viên không đổi

1. Kiểm tra queue `nhansu-quyetdinh-bonhiem-created` trên RabbitMQ có **1 consumer** chưa. Nếu chưa có, NhanSu chưa kết nối được broker.
2. Kiểm tra bảng `OutboxMessage` của `DbQuyetDinh`: message bị kẹt lại nghĩa là QuyetDinh chưa kết nối được broker.
3. Nếu có message tới broker nhưng nằm trong exchange khác tên (trên RabbitMQ, tên exchange có dạng `<namespace>:<TênEvent>`): class event hai bên lệch namespace. Chạy `EventContractTests`.
4. Xem log NhanSu: consumer có thể lỗi khi chức vụ mới không tồn tại.

### Không tạo được nhân viên: "Chưa có danh mục tình trạng nhân viên 'Đang làm việc'"

Database chưa có migration `SeedTinhTrangNhanVien`. Thường do chạy image cũ; build lại bằng `docker compose up -d --build`.

### `relation "..." does not exist`

Model EF có bảng nhưng chưa có migration tạo bảng đó, hoặc container đang chạy image cũ. Tạo migration ([development.md](development.md#thêm-migration)) hoặc build lại image.

### 409 Conflict (TinhLuong) hoặc 500 kèm `DbUpdateConcurrencyException` (QuyetDinh, ChamCong)

`updatedAt` gửi lên không còn khớp database vì bản ghi đã bị sửa sau lần đọc. Hãy đọc lại bản ghi để lấy `updatedAt` mới. Khi đặt vào query string, nhớ URL-encode.

### Service không ghi log vào MongoDB

Khi khởi động, service ping MongoDB trong 3 giây; nếu thất bại chỉ ghi cảnh báo và vẫn chạy. Kiểm tra container `mongodb` và khoá `Serilog:WriteTo:1:Args:databaseUrl`.

### `dotnet test` / `dotnet run` báo thiếu framework `Microsoft.NETCore.App 8.0`

Cài .NET 8 runtime, hoặc cho phép chạy bằng runtime mới hơn:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test QuanLyNhanSuSystem.slnx
```

## Giới hạn đã biết

Đây là các hành vi hiện tại của code, đã được kiểm chứng, chưa được xử lý.

**Nghiệp vụ**

- **Quyết định nâng lương không có cơ chế bù trừ.** QuyetDinh lưu quyết định rồi gọi gRPC `UpdateLuong`. Nếu NhanSu lỗi, API vẫn trả thành công và lương hai bên lệch nhau; lỗi chỉ được ghi log. Luồng bổ nhiệm thì dùng Outbox, không bị vấn đề này.
- **`LuongCoBanCu` của quyết định nâng lương do client gửi**, không được đối chiếu với lương thật bên NhanSu.
- **Đổi phụ cấp của chức vụ không cập nhật phụ cấp của nhân viên đang giữ chức vụ.** Phụ cấp chỉ được tính lại khi gán lại chức vụ.
- **Tính lương rất đơn giản:** `TongLuong = LuongThucLanh = LuongCoBan`; chưa tính phụ cấp, thưởng, phạt, thuế, ngày công từ ChamCong. Tính lương cũng không kiểm tra kỳ đang khoá hay mở. Chưa có API xem kết quả lương của kỳ.
- **Kỳ tính lương mới tạo ở trạng thái khoá**, phải mở trước khi sửa.
- **`PUT /api/PhieuDangKyNghi/{id}` chưa sửa được trường nào** (chỉ chạm `UpdatedAt`).
- **`PUT /api/NhanVien/{id}` không đổi được mã nhân viên** (trường `maNhanVien` trong body bị bỏ qua).
- **`POST /api/PhongBan` không trả Id**; muốn lấy Id phải gọi `GET`.
- **`PUT`/`DELETE` quyết định nâng lương trả thông báo "…quyết định bổ nhiệm"** (thông điệp bị sao chép nhầm).

**Kỹ thuật**

- **Lỗi concurrency:** chỉ TinhLuong trả 409; QuyetDinh và ChamCong trả 500 khi `updatedAt` không khớp. NhanSu không kiểm tra concurrency.
- **Một số lỗi validate trả 500:** domain của ChamCong/TinhLuong, "Nhân viên không tồn tại" khi tạo phiếu nghỉ, và `update-luong` / `update-chuc-vu` của NhanSu khi không tìm thấy dữ liệu đều đang ném `Exception` thường.
- **N+1 gRPC:** danh sách quyết định gọi gRPC `GetNhanVienById` một lần cho **mỗi** quyết định.
- **Một số event chưa có consumer và không qua Outbox:** `NhanVienCreatedEvent` (NhanSu) và `PhieuDangKyNghiCreatedEvent` (ChamCong).
- **Event được sao chép thủ công** giữa QuyetDinh và NhanSu, không có thư viện contract dùng chung. Ràng buộc chỉ được giữ bằng `EventContractTests`.
- **Bảo mật:** chưa phân quyền theo role; `register` mở cho mọi người và cho tự chọn role; JWT key và mật khẩu hạ tầng nằm trong repo.
- **Phiên bản IdentityModel lệch nhau:** `System.IdentityModel.Tokens.Jwt` 7.1.2 chạy cùng `Microsoft.IdentityModel.Tokens` 8.14.0. Token phát hành vẫn đúng, nhưng nên cho hai package cùng phiên bản.
- **Không có CI.** Test chưa chạy tự động trên pull request.
