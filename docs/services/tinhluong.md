# Service Tính lương (TinhLuong)

| | |
|---|---|
| Thư mục | `TinhLuongService/` |
| Database | `DbTinhLuong` |
| Port | 8082 |
| Qua Gateway | `/api/tinhluong/...` |
| Phụ thuộc | NhanSu (gRPC), PostgreSQL. Không dùng RabbitMQ |

Quản lý kỳ tính lương và kết quả lương của từng nhân viên trong kỳ.

## Mô hình dữ liệu

| Entity | Trường |
|---|---|
| `KyTinhLuong` | `MaKy`, `NgayBatDau`, `NgayKetThuc`, `ChotTinhLuong` (đã khoá), `NhanVienTinhLuongs` |
| `NhanVienTinhLuong` | `NhanVienId`, `KyTinhLuongId`, `LuongCoBan`, `PhuCap`, `Thuong`, `Phat`, `TongLuong`, `Thue`, `LuongThucLanh` |

## Rule nghiệp vụ

- **Kỳ tính lương:**
  - mã kỳ bắt buộc; ngày bắt đầu ≤ ngày kết thúc;
  - **kỳ mới tạo đang ở trạng thái khoá** (`ChotTinhLuong = true`). Phải **mở** kỳ trước khi sửa thông tin kỳ (`UpdateInfo` báo "Kỳ đã chốt không thể sửa").
- **Dòng lương nhân viên:** nhân viên và kỳ bắt buộc; lương cơ bản > 0.
- **Tính lương** (`PATCH /{id}/tinhluong`):
  1. Lấy toàn bộ nhân viên từ NhanSu qua gRPC `GetAllNhanVien` (dùng token của người gọi).
  2. Bỏ qua nhân viên có lương cơ bản ≤ 0, ghi cảnh báo vào log.
  3. Xoá mềm các dòng lương cũ của kỳ, nên tính lại nhiều lần không tạo dòng trùng.
  4. Tạo một `NhanVienTinhLuong` cho mỗi nhân viên. Công thức hiện tại rất đơn giản: `TongLuong = LuongThucLanh = LuongCoBan`; `PhuCap`, `Thuong`, `Phat`, `Thue` đều bằng 0.
  5. Lưu với kiểm tra concurrency theo `updatedAt`.

  Tính lương hiện **không kiểm tra** kỳ đang khoá hay mở.

## Concurrency

Mọi thao tác ghi (`PUT`, khoá, mở, tính lương) nhận `updatedAt` mà client đã đọc. Nếu bản ghi đã đổi, service trả **409 Conflict** (`ConcurrencyExceptionFilter`):

```json
{
  "title": "Database Concurrency Conflict",
  "status": 409,
  "detail": "Dữ liệu đã bị thay đổi bởi người dùng khác kể từ khi bạn tải trang. Vui lòng tải lại trang và thực hiện lại thao tác."
}
```

Lưu ý khi sửa code: `KyTinhLuongRepository.UpdateAsync` chỉ đánh dấu chính kỳ lương là `Modified` khi entity đã được tracked. Không gọi `context.Update(entity)` trên cả graph, vì các dòng `NhanVienTinhLuong` mới (Id đã sinh sẵn trong constructor) sẽ bị coi là bản ghi cũ và sinh `UPDATE` thay vì `INSERT`, dẫn tới 409. Handler tính lương cũng thêm các dòng mới một cách tường minh qua repository. Hành vi này có test hồi quy chạy trên SQLite in-memory (`tests/TinhLuongService.Tests/Handlers/KyTinhLuongHandlersDbTests.cs`).

## API (REST)

Qua Gateway: thay `/api` bằng `/api/tinhluong`. Mọi endpoint cần JWT.

| Method | Route | Body / query | Kết quả |
|---|---|---|---|
| GET | `/api/KyTinhLuong` | | Mảng `{ id, maKy, ngayBatDau, ngayKetThuc, chotTinhLuong, updatedAt }` |
| GET | `/api/KyTinhLuong/{id}` | | Một kỳ, hoặc 404 |
| POST | `/api/KyTinhLuong` | `{ maKy, ngayBatDau, ngayKetThuc }` | Id |
| PUT | `/api/KyTinhLuong/{id}` | `{ id, maKy, ngayBatDau, ngayKetThuc, updatedAt }` | `{ message }` (kỳ phải đang mở) |
| DELETE | `/api/KyTinhLuong/{id}` | | `{ message }` |
| PATCH | `/api/KyTinhLuong/{id}/khoa?updatedAt=...` | | `{ message }` |
| PATCH | `/api/KyTinhLuong/{id}/mo?updatedAt=...` | | `{ message }` |
| PATCH | `/api/KyTinhLuong/{id}/tinhluong?updatedAt=...` | | `{ "message": "Đã tính lương" }` |

Hiện chưa có API đọc các dòng `NhanVienTinhLuong` của một kỳ. DTO của kỳ không kèm kết quả lương; muốn xem thì truy vấn trực tiếp database `DbTinhLuong`, bảng `NhanVienTinhLuongs`.

## Migration

| Migration | Nội dung |
|---|---|
| `UpdateKyTinhLuong` | Bảng `KyTinhLuongs`, `NhanVienTinhLuongs` |

## Mã nguồn chính

| Nội dung | File |
|---|---|
| Entity | `TinhLuongService.Domain/Entities/*.cs` |
| Domain service | `TinhLuongService.Domain/Service/TinhLuongService.cs` |
| Handler | `TInhLuongService.Application/Handlers/KyTinhLuongHandlers.cs` |
| Repository | `TInhLuongService.Infrastructure/Repositories/*.cs` |
| gRPC client | `TInhLuongService.Application/Services/NhanSuServiceClient.cs` |
| Filter 409 | `TinhLuongService.API/Filters/ConcurrencyExceptionFilter.cs` |
