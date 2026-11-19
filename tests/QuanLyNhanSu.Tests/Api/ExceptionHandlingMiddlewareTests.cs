using System.Text.Json;
using Microsoft.AspNetCore.Http;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;

namespace QuanLyNhanSu.Tests.Api;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> Run(Exception? toThrow)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => toThrow is null ? Task.CompletedTask : throw toThrow);

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        var text = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        return (ctx.Response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement);
    }

    [Fact]
    public async Task KhongCoLoi_KhongDoiResponse()
    {
        var (status, body) = await Run(null);

        Assert.Equal(200, status);
        Assert.Equal(JsonValueKind.Undefined, body.ValueKind);
    }

    [Fact]
    public async Task BadRequest_Tra400KemDanhSachLoi()
    {
        var errors = new Dictionary<string, string[]> { ["Email"] = new[] { "Email không hợp lệ" } };

        var (status, body) = await Run(new BadRequestException("Dữ liệu không hợp lệ", errors, "REQUEST_VALIDATION_ERROR"));

        Assert.Equal(400, status);
        Assert.Equal("REQUEST_VALIDATION_ERROR", body.GetProperty("code").GetString());
        Assert.Equal("Dữ liệu không hợp lệ", body.GetProperty("message").GetString());
        Assert.Equal("Email không hợp lệ", body.GetProperty("errors").GetProperty("Email")[0].GetString());
    }

    [Fact]
    public async Task NotFound_Tra404()
    {
        var (status, body) = await Run(new NotFoundException("Không tìm thấy", "NOT_FOUND"));

        Assert.Equal(404, status);
        Assert.Equal("NOT_FOUND", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("errors", out _)); // null => bị ẩn
    }

    [Fact]
    public async Task IsExist_Tra400()
    {
        var (status, body) = await Run(new IsExistException("Đã tồn tại"));

        Assert.Equal(400, status);
        Assert.Equal("IS_EXIST", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoiKhac_Tra500()
    {
        var (status, body) = await Run(new InvalidOperationException("boom"));

        Assert.Equal(500, status);
        Assert.Equal("INTERNAL_SERVER_ERROR", body.GetProperty("code").GetString());
        Assert.Equal(500, body.GetProperty("status").GetInt32());
    }
}
