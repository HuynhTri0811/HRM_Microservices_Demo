# Service Quyết định (QuyetDinh)

| | |
|---|---|
| Thư mục | `QuyetDinhService/` |
| Database | `DbQuyetDinh` (gồm cả bảng Outbox của MassTransit) |
| Port | 8084 |
| Qua Gateway | `/api/quyetdinh/...` |
| Phụ thuộc | NhanSu (gRPC), PostgreSQL, RabbitMQ (publish qua Outbox) |

Quản lý hai loại quyết định nhân sự. Mỗi loại cập nhật dữ liệu bên NhanSu theo một cách khác nhau:

| Quyết định | Tác động lên NhanSu | Cách gửi |
|---|---|---|
| Bổ nhiệm | Đổi chức vụ (và phụ cấp theo chức vụ) | **Bất đồng bộ**: event RabbitMQ qua Transactional Outbox |
| Nâng lương | Đổi lương cơ bản | **Đồng bộ**: gRPC `UpdateLuong` sau khi lưu |

## Mô hình dữ liệu

`QuyetDinh` (abstract) gồm `SoQuyetDinh`, `NgayQuyetDinh`, `NoiDung`, `NgayHieuLuc`, `GhiChu`. Hai lớp con kế thừa:

| Entity | Trường riêng |
|---|---|
| `QuyetDinhBoNhiem` | `MaNhanVien`, `ChucVuCu`, `ChucVuMoi`, `PhuCapCu`, `PhuCapMoi`, `LyDo` |
| `QuyetDinhNangLuong` | `MaNhanVien`, `LuongCoBanCu`, `LuongCoBanMoi` |

`MaNhanVien`, `ChucVuCu`, `ChucVuMoi` là Id (Guid) bên NhanSu. Service này không lưu bản sao thông tin nhân viên.

## Rule nghiệp vụ

- **Mọi quyết định:** số quyết định và nội dung bắt buộc. Lỗi được gom theo trường (`REQUEST_VALIDATION_ERROR`).
- **Bổ nhiệm:**
  - nhân viên, chức vụ cũ và chức vụ mới bắt buộc; phụ cấp ≥ 0;
  - khi **tạo**, `PhuCapCu` lấy từ phụ cấp hiện tại của nhân viên (gRPC `GetNhanVienById`), `PhuCapMoi` lấy từ phụ cấp của chức vụ mới (gRPC `GetChucVuById`). Không tìm thấy nhân viên hoặc chức vụ → 404;
  - khi **sửa**, chỉ được đổi chức vụ mới; `PhuCapMoi` được lấy lại theo chức vụ mới. Nhân viên và chức vụ cũ giữ nguyên.
- **Nâng lương:**
  - nhân viên bắt buộc; lương cũ và lương mới ≥ 0;
  - `LuongCoBanCu` do **client gửi lên**, service không tự đọc từ NhanSu. Web UI tự điền sẵn từ lương hiện tại của nhân viên;
  - khi **sửa**, chỉ đổi lương mới, rồi gửi lương mới sang NhanSu;
  - khi **xoá**, gửi `LuongCoBanCu` sang NhanSu để khôi phục lương cũ.

## Luồng ghi sang NhanSu

### Bổ nhiệm: event qua Outbox

```csharp
// QuyetDinhBoNhiemCommandHandlers.cs (rút gọn)
await publishEndpoint.Publish(new QuyetDinhBoNhiemCreatedEvent { ... }); // chỉ thêm OutboxMessage vào DbContext
await repository.AddAsync(entity);
await repository.SaveChangesAsync();                                     // ghi quyết định + message trong một transaction
```

Thứ tự **Publish trước, SaveChanges sau** là bắt buộc. Xem [communication.md](../communication.md#transactional-outbox-quyetdinh).

| Thao tác | Event | NhanSu làm gì |
|---|---|---|
| Tạo | `QuyetDinhBoNhiemCreatedEvent` | Đặt chức vụ mới |
| Sửa | `QuyetDinhBoNhiemUpdatedEvent` | Đặt chức vụ mới |
| Xoá | `QuyetDinhBoNhiemDeletedEvent` | Đưa về chức vụ cũ |

### Nâng lương: gRPC đồng bộ

Handler lưu quyết định trước, sau đó gọi `UpdateLuongAsync`. Nếu NhanSu lỗi hoặc không truy cập được, client trả `false`. Handler **không** rollback và API vẫn trả thành công; lỗi chỉ được ghi log. Khi đó lương bên NhanSu có thể lệch với quyết định. Xem [troubleshooting.md](../troubleshooting.md#giới-hạn-đã-biết).

## API (REST)

Qua Gateway: thay `/api` bằng `/api/quyetdinh`. Mọi endpoint cần JWT.

### QuyetDinhBoNhiem

| Method | Route | Body | Kết quả |
|---|---|---|---|
| GET | `/api/QuyetDinhBoNhiem` | | Mảng `QuyetDinhBoNhiemDto` |
| GET | `/api/QuyetDinhBoNhiem/{id}` | | `QuyetDinhBoNhiemDto`, hoặc 404 |
| POST | `/api/QuyetDinhBoNhiem` | `{ soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc, ghiChu?, maNhanVien, chucVuCu, chucVuMoi, lyDo }` | Id quyết định |
| PUT | `/api/QuyetDinhBoNhiem/{id}` | `{ id, soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc, ghiChu?, chucVuMoi, lyDo, updatedAt }` | `{ message }`; không tìm thấy → 404 |
| DELETE | `/api/QuyetDinhBoNhiem/{id}` | | `{ message }`; không tìm thấy → 404 |

`QuyetDinhBoNhiemDto`: `id, soQuyetDinh, ngayHieuLuc, noiDung, ghiChu, nhanVien, chucVuCu, chucVuMoi, phuCapCu, phuCapMoi, lyDo, updatedAt`.

### QuyetDinhNangLuong

| Method | Route | Body | Kết quả |
|---|---|---|---|
| GET | `/api/QuyetDinhNangLuong` | | Mảng `QuyetDinhNangLuongDto` |
| GET | `/api/QuyetDinhNangLuong/{id}` | | `QuyetDinhNangLuongDto`, hoặc 404 |
| POST | `/api/QuyetDinhNangLuong` | `{ soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc, ghiChu?, maNhanVien, luongCoBanCu, luongCoBanMoi }` | Id quyết định |
| PUT | `/api/QuyetDinhNangLuong/{id}` | `{ id, soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc, ghiChu?, luongCoBanMoi, updatedAt }` | `{ message }`; không tìm thấy → 404 |
| DELETE | `/api/QuyetDinhNangLuong/{id}` | | `{ message }`; không tìm thấy → 404 |

`QuyetDinhNangLuongDto`: `id, soQuyetDinh, ngayHieuLuc, noiDung, ghiChu, nhanVien, luongCu, luongMoi, updatedAt`.

Trong cả hai DTO, `nhanVien` (`{ id, maNhanVien, tenNhanVien, ngaySinh, gioiTinh, email, luongCoBan, phuCap }`) được lấy **trực tiếp từ NhanSu qua gRPC** mỗi lần đọc. Danh sách gọi gRPC một lần cho mỗi quyết định. Nếu nhân viên không còn tồn tại, `nhanVien` là `null`.

`updatedAt` dùng cho optimistic concurrency. Nếu không khớp, service hiện trả **500**, chưa phải 409. Xem [cross-cutting.md](../cross-cutting.md#optimistic-concurrency).

## Migration

| Migration | Nội dung |
|---|---|
| `AddQuyetDinhNangLuong`, `AddQuyetDinh` | Bảng quyết định nâng lương và bổ nhiệm |
| `AddMassTransitOutbox` | Bảng `InboxState`, `OutboxMessage`, `OutboxState` cho MassTransit |

## Mã nguồn chính

| Nội dung | File |
|---|---|
| Entity | `QuyetDinhService.Domain/Entity/*.cs` |
| Handler bổ nhiệm (event) | `QuyetDinhService.Application/Handlers/QuyetDinhBoNhiemHandle/QuyetDinhBoNhiemCommandHandlers.cs` |
| Handler nâng lương (gRPC) | `QuyetDinhService.Application/Handlers/QuyetDinhNangLuongHandle/*.cs` |
| gRPC client | `QuyetDinhService.Application/Services/NhanSuServiceClient.cs` |
| Event (contract với NhanSu) | `QuyetDinhService.Application/Events/*.cs` |
| Cấu hình Outbox | `Extensions/MessagingExtensions.cs` |
