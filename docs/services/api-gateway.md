# API Gateway

| | |
|---|---|
| Thư mục | `ApiGateway/` |
| Port | 8080 |
| Công nghệ | YARP 2.3, Polly v8 |

Gateway là điểm vào duy nhất cho client. Nó làm bốn việc:

1. **Reverse proxy**: chuyển `/api/{service}/**` tới service tương ứng. Bảng route ở [communication.md](../communication.md#rest-qua-gateway).
2. **Resilience**: retry, circuit breaker và timeout cho request đi tới service (xem bên dưới).
3. **Gộp Swagger**: http://localhost:8080/swagger hiển thị Swagger của cả 4 service.
4. **Web UI kiểm thử**: http://localhost:8080/ phục vụ `wwwroot/index.html` (HRM Test Console). Xem [testing.md](../testing.md#web-ui-kiểm-thử-hrm-test-console).

## Thứ tự middleware

```
UseDefaultFiles / UseStaticFiles   → web UI
UseRouting
UseObservability                   → /metrics
GatewayLoggingMiddleware           → X-Correlation-Id + log
UseGatewaySwagger / MapSwaggerRoutes
MapReverseProxy                    → YARP + Polly
```

## Resilience (Polly)

Cấu hình trong `Extensions/YarpExtensions.cs`. Mỗi cluster (service) có **một pipeline riêng**, lấy từ `ResiliencePipelineRegistry` theo khoá `yarp:{ClusterId}`. Vì vậy circuit breaker của từng service độc lập với nhau.

| Tầng | Cấu hình | Áp dụng khi |
|---|---|---|
| Retry | 3 lần, exponential backoff 1s + jitter | Chỉ với method an toàn (GET, HEAD, OPTIONS, TRACE), **và** khi lỗi hạ tầng: `HttpRequestException`, 502, 503, 504 |
| Circuit breaker | Mở khi ≥ 50% lỗi trong 10s (tối thiểu 8 request), ngắt 30s | Lỗi hạ tầng: `HttpRequestException`, timeout, 502, 503, 504 |
| Timeout | 5s mỗi lần thử | Mọi request |

Lý do thiết kế:

- **Không retry POST/PUT/PATCH/DELETE.** YARP stream body từ client nên body chỉ đọc được một lần; retry sẽ gây lỗi "Stream was already consumed" và client nhận 502. Ngoài ra, POST không idempotent nên retry có thể tạo bản ghi trùng.
- **500 không tính là lỗi hạ tầng.** 500 là lỗi do service trả về (kể cả lỗi nghiệp vụ), nên Gateway trả nguyên 500 cùng body JSON cho client, không retry và không làm mở breaker.
- **Khi breaker đang mở**, request tới service đó bị chặn ngay (client nhận lỗi 5xx từ Gateway) cho tới khi hết 30s.

Unit test: `tests/ApiGateway.Tests/GatewayResilienceTests.cs`.

## Gộp Swagger

`MapSwaggerRoutes` mở `GET /swagger/{service}/v1/swagger.json` cho `nhansu`, `chamcong`, `quyetdinh`, `tinhluong`. Endpoint này:

1. lấy `/swagger/v1/swagger.json` của service, theo địa chỉ cluster trong `ReverseProxy:Clusters`;
2. đổi mọi path `/api/...` thành `/api/{service}/...`;
3. xoá trường `servers`, để Swagger UI gửi request tương đối tới chính Gateway.

Swagger của service chỉ có khi service chạy ở môi trường `Development`. Nếu không lấy được, Gateway trả `500` kèm thông báo "Không thể kết nối lấy tài liệu Swagger từ ...". Web UI dùng chính các endpoint này để hiển thị đèn trạng thái của từng service.

## Logging

`GatewayLoggingMiddleware` ghi mỗi request vào MongoDB collection `HttpTrafficLogs`: `Information` khi 2xx/3xx, `Warning` khi 4xx, `Error` khi 5xx. Middleware cũng gắn `X-Correlation-Id` (giữ nguyên nếu client gửi lên) vào request gửi tới service và vào response trả về client.
