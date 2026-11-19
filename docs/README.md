# Tài liệu HRM Microservices Demo

Bộ tài liệu chi tiết cho hệ thống Quản lý Nhân sự (HRM) chạy trên .NET 8 theo kiến trúc microservices. Trang tổng quan và hướng dẫn chạy nhanh nằm ở [README gốc](../README.md).

## Đọc theo thứ tự

Nếu mới làm quen với dự án, đọc theo thứ tự sau:

1. [Kiến trúc tổng quan](architecture.md): các service, ranh giới dữ liệu, cấu trúc Clean Architecture/CQRS trong mỗi service.
2. [Giao tiếp giữa các service](communication.md): REST qua Gateway, gRPC tới NhanSu, event RabbitMQ với Outbox, cách token được chuyển tiếp.
3. [Các vấn đề xuyên suốt](cross-cutting.md): xác thực JWT, định dạng lỗi, optimistic concurrency, xoá mềm, audit, logging/metrics/tracing.
4. Tài liệu từng service (entity, rule nghiệp vụ, API, event):
   - [Nhân sự (NhanSu)](services/nhansu.md)
   - [Quyết định (QuyetDinh)](services/quyetdinh.md)
   - [Chấm công (ChamCong)](services/chamcong.md)
   - [Tính lương (TinhLuong)](services/tinhluong.md)
   - [API Gateway](services/api-gateway.md)

## Vận hành và phát triển

| Tài liệu | Nội dung |
|---|---|
| [Cấu hình](configuration.md) | Mọi khoá cấu hình, biến môi trường, port của từng service |
| [Triển khai và chạy](deployment.md) | Docker Compose, chạy local bằng `dotnet run`, migration |
| [Kiểm thử](testing.md) | Unit test (xUnit) và web UI kiểm thử HRM Test Console |
| [Hướng dẫn phát triển](development.md) | Thêm endpoint, RPC, event, migration; quy ước code |
| [Xử lý sự cố và giới hạn đã biết](troubleshooting.md) | Lỗi thường gặp, cách chẩn đoán, các giới hạn hiện tại của code |

## Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| Nhân viên (`NhanVien`) | Hồ sơ nhân sự: mã, họ tên, phòng ban, chức vụ, lương cơ bản, phụ cấp, trạng thái làm việc |
| Chức vụ (`ChucVu`) | Vị trí công việc, kèm **phụ cấp** áp cho nhân viên giữ chức vụ đó |
| Phòng ban (`PhongBan`) | Đơn vị tổ chức (mã quản lý + tên) |
| Văn bằng (`VanBang`) | Bằng cấp/chứng chỉ của nhân viên |
| Quyết định bổ nhiệm | Văn bản đổi chức vụ của nhân viên (kéo theo đổi phụ cấp) |
| Quyết định nâng lương | Văn bản đổi lương cơ bản của nhân viên |
| Phiếu đăng ký nghỉ | Đơn xin nghỉ theo giờ, theo buổi hoặc theo ngày |
| Bảng chấm công tháng | Kỳ chấm công của một tháng, có thể chốt/mở chốt |
| Kỳ tính lương | Kỳ trả lương; tính lương tạo một dòng `NhanVienTinhLuong` cho mỗi nhân viên |
| Chốt / khoá | Đánh dấu dữ liệu đã chốt để không sửa thêm |
