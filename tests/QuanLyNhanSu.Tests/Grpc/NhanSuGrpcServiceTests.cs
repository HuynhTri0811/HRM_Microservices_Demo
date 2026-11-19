using System.Globalization;
using Grpc.Core;
using HRM.Contracts.NhanSu;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.API.Grpc;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.ChucVu;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.NhanSu;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.PhongBan;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.VanBang;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Queries;

namespace QuanLyNhanSu.Tests.Grpc;

public class NhanSuGrpcServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly TestServerCallContext _ctx = new();

    private NhanSuGrpcService Service() => new(_mediator.Object, NullLogger<NhanSuGrpcService>.Instance);

    private static NhanVienDto Dto(Guid? id = null, DateTime? ngaySinh = null) => new(
        id ?? Guid.NewGuid(), "NV001", "Nguyễn Văn A",
        ngaySinh ?? new DateTime(1995, 5, 10, 0, 0, 0, DateTimeKind.Utc),
        "Nam", "a@example.test", 15_000_000.75m, 1_000_000m,
        new PhongBanDto(Guid.NewGuid(), "PB01", "Kỹ thuật"),
        new ChucVuDto(Guid.NewGuid(), "NV", "Nhân viên", 1_000_000m),
        new List<VanBangDto>());

    [Fact]
    public async Task GetNhanVienById_MapDayDuVaGiuSoLeTheoInvariantCulture()
    {
        var dto = Dto();
        _mediator.Setup(m => m.Send(It.Is<GetEmployeeByIdQuery>(q => q.Id == dto.Id), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("vi-VN"); // vi-VN dùng dấu phẩy thập phân
        try
        {
            var reply = await Service().GetNhanVienById(new GetNhanVienByIdRequest { Id = dto.Id.ToString() }, _ctx);

            Assert.Equal(dto.Id.ToString(), reply.Id);
            Assert.Equal("NV001", reply.MaNhanVien);
            Assert.Equal("Nguyễn Văn A", reply.TenNhanVien);
            Assert.Equal("15000000.75", reply.LuongCoBan);
            Assert.Equal("1000000", reply.PhuCap);
            Assert.Equal(dto.PhongBan!.Id.ToString(), reply.PhongBanId);
            Assert.Equal(dto.ChucVu!.Id.ToString(), reply.ChucVuId);
            Assert.Equal(dto.NgaySinh, reply.NgaySinh.ToDateTime());
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public async Task GetNhanVienById_NgaySinhKhongCoKind_VanChuyenDuocSangTimestamp()
    {
        var dto = Dto(ngaySinh: new DateTime(1990, 1, 2)); // DateTimeKind.Unspecified
        _mediator.Setup(m => m.Send(It.IsAny<GetEmployeeByIdQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var reply = await Service().GetNhanVienById(new GetNhanVienByIdRequest { Id = dto.Id.ToString() }, _ctx);

        Assert.Equal(new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc), reply.NgaySinh.ToDateTime());
    }

    [Fact]
    public async Task GetNhanVienById_KhongCoPhongBanChucVu_TraChuoiRong()
    {
        var dto = Dto() with { PhongBan = null, ChucVu = null };
        _mediator.Setup(m => m.Send(It.IsAny<GetEmployeeByIdQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var reply = await Service().GetNhanVienById(new GetNhanVienByIdRequest { Id = dto.Id.ToString() }, _ctx);

        Assert.Equal(string.Empty, reply.PhongBanId);
        Assert.Equal(string.Empty, reply.ChucVuId);
    }

    [Fact]
    public async Task GetNhanVienById_KhongTonTai_TraNotFound()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetEmployeeByIdQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((NhanVienDto?)null);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Service().GetNhanVienById(new GetNhanVienByIdRequest { Id = Guid.NewGuid().ToString() }, _ctx));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-guid")]
    public async Task GetNhanVienById_IdKhongHopLe_TraInvalidArgument(string id)
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Service().GetNhanVienById(new GetNhanVienByIdRequest { Id = id }, _ctx));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAllNhanVien_TraDuDanhSach()
    {
        var list = new[] { Dto(), Dto(), Dto() };
        _mediator.Setup(m => m.Send(It.IsAny<GetAllEmployeesQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var reply = await Service().GetAllNhanVien(new GetAllNhanVienRequest(), _ctx);

        Assert.Equal(list.Select(x => x.Id.ToString()), reply.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetChucVuById_MapPhuCap()
    {
        var cv = new ChucVuDto(Guid.NewGuid(), "TP", "Trưởng phòng", 5_000_000.5m);
        _mediator.Setup(m => m.Send(It.Is<GetChucVuByIdQuery>(q => q.Id == cv.Id), It.IsAny<CancellationToken>())).ReturnsAsync(cv);

        var reply = await Service().GetChucVuById(new GetChucVuByIdRequest { Id = cv.Id.ToString() }, _ctx);

        Assert.Equal("TP", reply.MaChucVu);
        Assert.Equal("5000000.5", reply.PhuCap);
    }

    [Fact]
    public async Task GetChucVuById_KhongTonTai_TraNotFound()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChucVuByIdQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((ChucVuDto?)null);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Service().GetChucVuById(new GetChucVuByIdRequest { Id = Guid.NewGuid().ToString() }, _ctx));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateLuong_GuiLenhCapNhatVoiSoThapPhanChinhXac()
    {
        var id = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<UpdateNhanVienCapNhatLuongDTO>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var reply = await Service().UpdateLuong(new UpdateLuongRequest { NhanVienId = id.ToString(), LuongCoBan = "15000000.75" }, _ctx);

        Assert.True(reply.Success);
        _mediator.Verify(m => m.Send(new UpdateNhanVienCapNhatLuongDTO(id, 15_000_000.75m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public async Task UpdateLuong_SoKhongHopLe_TraInvalidArgument(string luong)
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Service().UpdateLuong(new UpdateLuongRequest { NhanVienId = Guid.NewGuid().ToString(), LuongCoBan = luong }, _ctx));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateLuong_HandlerNemLoi_TraFailedPreconditionKemThongDiep()
    {
        _mediator.Setup(m => m.Send(It.IsAny<UpdateNhanVienCapNhatLuongDTO>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Không tìm thấy nhân viên"));

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Service().UpdateLuong(new UpdateLuongRequest { NhanVienId = Guid.NewGuid().ToString(), LuongCoBan = "1" }, _ctx));

        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        Assert.Equal("Không tìm thấy nhân viên", ex.Status.Detail);
    }

    [Fact]
    public async Task UpdateBoNhiem_GuiLenhBoNhiem()
    {
        var id = Guid.NewGuid();
        var chucVu = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<UpdateNhanVienBoNhiemDTO>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var reply = await Service().UpdateBoNhiem(new UpdateBoNhiemRequest
        {
            NhanVienId = id.ToString(), ChucVuMoi = chucVu.ToString(), PhuCapMoi = "2500000.5"
        }, _ctx);

        Assert.True(reply.Success);
        _mediator.Verify(m => m.Send(new UpdateNhanVienBoNhiemDTO(id, chucVu, 2_500_000.5m), It.IsAny<CancellationToken>()), Times.Once);
    }
}
