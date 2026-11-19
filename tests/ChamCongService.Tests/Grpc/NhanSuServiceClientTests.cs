using ChamCongService.Application.Services;
using Grpc.Core;
using HRM.Contracts.NhanSu;
using HRM.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChamCongService.Tests.Grpc;

public class NhanSuServiceClientTests
{
    private readonly Mock<NhanSuGrpc.NhanSuGrpcClient> _grpc = new();
    private Metadata? _headers;

    private NhanSuServiceClient Client() => new(_grpc.Object, NullLogger<NhanSuServiceClient>.Instance);

    private void Setup(AsyncUnaryCall<NhanVienReply> call) =>
        _grpc.Setup(c => c.GetNhanVienByIdAsync(It.IsAny<GetNhanVienByIdRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GetNhanVienByIdRequest, Metadata, DateTime?, CancellationToken>((_, h, _, _) => _headers = h)
            .Returns(call);

    [Fact]
    public async Task GetNhanVienById_MapTenNhanVienSangHoTen()
    {
        var id = Guid.NewGuid();
        Setup(GrpcTestCalls.Success(new NhanVienReply { Id = id.ToString(), MaNhanVien = "NV001", TenNhanVien = "Nguyễn Văn A", Email = "a@example.test" }));

        var nv = await Client().GetNhanVienByIdAsync(id, "Bearer abc");

        Assert.Equal(id, nv!.Id);
        Assert.Equal("NV001", nv.MaNhanVien);
        Assert.Equal("Nguyễn Văn A", nv.HoTen);
        Assert.Equal("a@example.test", nv.Email);
        Assert.Equal("Bearer abc", GrpcTestCalls.Authorization(_headers));
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.Unavailable)]
    public async Task GetNhanVienById_LoiGrpc_TraNull(StatusCode code)
    {
        Setup(GrpcTestCalls.Failure<NhanVienReply>(code));

        Assert.Null(await Client().GetNhanVienByIdAsync(Guid.NewGuid(), "t"));
    }
}
