using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;

namespace QuanLyNhanSu.Tests;

/// <summary>Dữ liệu mẫu dùng chung cho các test của NhanSu.</summary>
internal static class TestData
{
    public static readonly DateTime NgaySinh = new(1995, 5, 10, 0, 0, 0, DateTimeKind.Utc);

    public static PhongBan PhongBan() => QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.PhongBan.Create("PB01", "Kỹ thuật");

    public static ChucVu ChucVu(decimal phuCap = 1_000_000m) =>
        QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.ChucVu.Create("NV", "Nhân viên", phuCap);

    public static TinhTrangNhanVien DangLamViec() =>
        TinhTrangNhanVien.Create(TinhTrangNhanVien.MaDangLamViec, "Đang làm việc", khongConCongTac: false);

    public static TinhTrangNhanVien DaNghiViec() =>
        TinhTrangNhanVien.Create(TinhTrangNhanVien.MaDaNghiViec, "Đã nghỉ việc", khongConCongTac: true);

    public static NhanVien NhanVien() =>
        QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.NhanVien.Create(
            "NV001", "Nguyễn Văn A", NgaySinh, "Nam", "a@example.test", PhongBan());
}
