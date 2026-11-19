using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using HRM.Contracts.NhanSu;
using HRM.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using QuyetDinhService.QuyetDinhService.Application.Services;

namespace QuyetDinhService.Tests.Grpc;

public class NhanSuServiceClientTests
{
    private readonly Mock<NhanSuGrpc.NhanSuGrpcClient> _grpc = new();
    private Metadata? _headers;

    private NhanSuServiceClient Client() => new(_grpc.Object, NullLogger<NhanSuServiceClient>.Instance);

    private void SetupGetNhanVien(AsyncUnaryCall<NhanVienReply> call) =>
        _grpc.Setup(c => c.GetNhanVienByIdAsync(It.IsAny<GetNhanVienByIdRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GetNhanVienByIdRequest, Metadata, DateTime?, CancellationToken>((_, h, _, _) => _headers = h)
            .Returns(call);

    [Fact]
    public async Task GetNhanVienById_ParseDuLieuVaSoThapPhan()
    {
        var id = Guid.NewGuid();
        SetupGetNhanVien(GrpcTestCalls.Success(new NhanVienReply
        {
            Id = id.ToString(), MaNhanVien = "NV001", TenNhanVien = "Nguyễn Văn A", Email = "a@example.test", GioiTinh = "Nam",
            NgaySinh = Timestamp.FromDateTime(new DateTime(1995, 5, 10, 0, 0, 0, DateTimeKind.Utc)),
            LuongCoBan = "15000000.75", PhuCap = "1000000"
        }));

        var nv = await Client().GetNhanVienByIdAsync(id, "Bearer abc");

        Assert.NotNull(nv);
        Assert.Equal(id, nv!.Id);
        Assert.Equal("Nguyễn Văn A", nv.TenNhanVien);
        Assert.Equal(15_000_000.75m, nv.LuongCoBan);
        Assert.Equal(1_000_000m, nv.PhuCap);
        Assert.Equal(new DateTime(1995, 5, 10), nv.NgaySinh.Date);
    }

    [Theory]
    [InlineData("Bearer abc", "Bearer abc")]
    [InlineData("abc", "Bearer abc")]
    [InlineData("bearer abc", "Bearer abc")]
    public async Task ChuyenTiepToken_ChuanHoaTienToBearer(string input, string expected)
    {
        SetupGetNhanVien(GrpcTestCalls.Success(new NhanVienReply { Id = Guid.NewGuid().ToString(), LuongCoBan = "0", PhuCap = "0" }));

        await Client().GetNhanVienByIdAsync(Guid.NewGuid(), input);

        Assert.Equal(expected, GrpcTestCalls.Authorization(_headers));
    }

    [Fact]
    public async Task KhongCoToken_KhongGuiHeaderAuthorization()
    {
        SetupGetNhanVien(GrpcTestCalls.Success(new NhanVienReply { Id = Guid.NewGuid().ToString(), LuongCoBan = "0", PhuCap = "0" }));

        await Client().GetNhanVienByIdAsync(Guid.NewGuid(), "");

        Assert.Null(GrpcTestCalls.Authorization(_headers));
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.Unauthenticated)]
    public async Task GetNhanVienById_LoiGrpc_TraNull(StatusCode code)
    {
        SetupGetNhanVien(GrpcTestCalls.Failure<NhanVienReply>(code));

        Assert.Null(await Client().GetNhanVienByIdAsync(Guid.NewGuid(), "t"));
    }

    [Fact]
    public async Task GetChucVuById_ParsePhuCap()
    {
        var id = Guid.NewGuid();
        _grpc.Setup(c => c.GetChucVuByIdAsync(It.Is<GetChucVuByIdRequest>(r => r.Id == id.ToString()), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(GrpcTestCalls.Success(new ChucVuReply { Id = id.ToString(), MaChucVu = "TP", TenChucVu = "Trưởng phòng", PhuCap = "5000000.5" }));

        var cv = await Client().GetChucVuByIdAsync(id, "t");

        Assert.Equal("TP", cv!.MaChucVu);
        Assert.Equal(5_000_000.5m, cv.PhuCap);
    }

    [Fact]
    public async Task GetChucVuById_NotFound_TraNull()
    {
        _grpc.Setup(c => c.GetChucVuByIdAsync(It.IsAny<GetChucVuByIdRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(GrpcTestCalls.Failure<ChucVuReply>(StatusCode.NotFound));

        Assert.Null(await Client().GetChucVuByIdAsync(Guid.NewGuid(), "t"));
    }

    [Fact]
    public async Task UpdateLuong_GuiSoThapPhanDangInvariant()
    {
        UpdateLuongRequest? sent = null;
        _grpc.Setup(c => c.UpdateLuongAsync(It.IsAny<UpdateLuongRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateLuongRequest, Metadata, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
            .Returns(GrpcTestCalls.Success(new UpdateReply { Success = true }));
        var id = Guid.NewGuid();
        var old = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("vi-VN");
        try
        {
            Assert.True(await Client().UpdateLuongAsync(id, 15_000_000.75m, "t"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = old;
        }

        Assert.Equal(id.ToString(), sent!.NhanVienId);
        Assert.Equal("15000000.75", sent.LuongCoBan);
    }

    [Fact]
    public async Task UpdateLuong_LoiGrpc_TraFalse()
    {
        _grpc.Setup(c => c.UpdateLuongAsync(It.IsAny<UpdateLuongRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(GrpcTestCalls.Failure<UpdateReply>(StatusCode.FailedPrecondition, "Không tìm thấy nhân viên"));

        Assert.False(await Client().UpdateLuongAsync(Guid.NewGuid(), 1m, "t"));
    }

    [Fact]
    public async Task UpdateBoNhiem_GuiDuTruong()
    {
        UpdateBoNhiemRequest? sent = null;
        _grpc.Setup(c => c.UpdateBoNhiemAsync(It.IsAny<UpdateBoNhiemRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateBoNhiemRequest, Metadata, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
            .Returns(GrpcTestCalls.Success(new UpdateReply { Success = true }));
        var nv = Guid.NewGuid(); var cv = Guid.NewGuid();

        Assert.True(await Client().UpdateBoNhiemAsync(nv, cv, 2_500_000.5m, "t"));

        Assert.Equal(nv.ToString(), sent!.NhanVienId);
        Assert.Equal(cv.ToString(), sent.ChucVuMoi);
        Assert.Equal("2500000.5", sent.PhuCapMoi);
    }
}
