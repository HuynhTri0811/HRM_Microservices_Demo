using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChamCongService.Domain.Exceptions;

namespace ChamCongService.Application.Exceptions
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context); // Chạy request bình thường
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex); // Nếu có lỗi thì nhảy vào đây
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var statusCode = HttpStatusCode.InternalServerError;
            var errorTitle = "Internal Server Error";
            var errorCode = "INTERNAL_SERVER_ERROR";
            IDictionary<string, string[]>? validationErrors = null;

            switch (exception)
            {
                case BadRequestException badRequestEx:
                    statusCode = HttpStatusCode.BadRequest;
                    errorTitle = "Bad Request";
                    errorCode = badRequestEx.Code;
                    validationErrors = badRequestEx.Errors; // Lấy thuộc tính errors ra
                    break;
                case NotFoundException notFoundEx:
                    statusCode = HttpStatusCode.NotFound;
                    errorTitle = "Not Found";
                    errorCode = notFoundEx.Code;
                    validationErrors = notFoundEx.Errors; // Lấy thuộc tính errors ra
                    break;
                case IsExistException isExistEx:
                    statusCode = HttpStatusCode.BadRequest;
                    errorTitle = "Is Exist";
                    errorCode = isExistEx.Code;
                    validationErrors = isExistEx.Errors; // Lấy thuộc tính errors ra
                    break;
                // Thêm các exception khác nếu có...
            }

            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                status = (int)statusCode,
                error = errorTitle,
                message = exception.Message,
                code = errorCode,
                errors = validationErrors // Sẽ là null hoặc danh sách chi tiết lỗi
            };

            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Tự động ẩn field "errors" nếu nó bằng null
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }
    }
}
