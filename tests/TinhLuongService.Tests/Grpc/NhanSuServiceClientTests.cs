using Grpc.Core;
using HRM.Contracts.NhanSu;
using HRM.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using TinhLuongService.Application.Services;

namespace TinhLuongService.Tests.Grpc;

public class NhanSuServiceClientTests
{
    private readonly Mock<NhanSuGrpc.NhanSuGrpcClient> _grpc = new();
    private Metadata? _headers;

    private NhanSuServiceClient Client() => new(_grpc.Object, NullLogger<NhanSuServiceClient>.Instance);

    private void Setup(AsyncUnaryCall<NhanVienListReply> call) =>
        _grpc.Setup(c => c.GetAllNhanVienAsync(It.IsAny<GetAllNhanVienRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GetAllNhanVienRequest, Metadata, DateTime?, CancellationToken>((_, h, _, _) => _headers = h)
            .Returns(call);

    [Fact]
    public async Task GetAllNhanVien_ParseLuongVaChuyenTiepToken()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var reply = new NhanVienListReply();
        reply.Items.Add(new NhanVienReply { Id = a.ToString(), MaNhanVien = "A", TenNhanVien = "Nhân viên A", Email = "a@x.test", LuongCoBan = "15000000.75" });
        reply.Items.Add(new NhanVienReply { Id = b.ToString(), MaNhanVien = "B", TenNhanVien = "Nhân viên B", Email = "b@x.test", LuongCoBan = "0" });
        Setup(GrpcTestCalls.Success(reply));

        var list = (await Client().GetAllNhanVienAsync("abc")).ToList();

        Assert.Equal(new[] { a, b }, list.Select(x => x.Id));
        Assert.Equal(15_000_000.75m, list[0].LuongCoBan);
        Assert.Equal(0m, list[1].LuongCoBan);
        Assert.Equal("Bearer abc", GrpcTestCalls.Authorization(_headers));
    }

    [Fact]
    public async Task GetAllNhanVien_LuongKhongParseDuoc_ThanhKhong()
    {
        var reply = new NhanVienListReply();
        reply.Items.Add(new NhanVienReply { Id = Guid.NewGuid().ToString(), LuongCoBan = "khong-phai-so" });
        Setup(GrpcTestCalls.Success(reply));

        Assert.Equal(0m, Assert.Single(await Client().GetAllNhanVienAsync("t")).LuongCoBan);
    }

    [Fact]
    public async Task GetAllNhanVien_LoiGrpc_TraDanhSachRong()
    {
        Setup(GrpcTestCalls.Failure<NhanVienListReply>(StatusCode.Unavailable));

        Assert.Empty(await Client().GetAllNhanVienAsync("t"));
    }
}
