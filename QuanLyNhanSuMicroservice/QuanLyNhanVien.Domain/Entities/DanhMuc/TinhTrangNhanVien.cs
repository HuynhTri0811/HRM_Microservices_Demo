using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.Base;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities
{
    public class TinhTrangNhanVien : ObjectBase
    {
        // Danh mục mặc định được seed qua migration SeedTinhTrangNhanVien (xem NhanSuDbContext.OnModelCreating).
        public static readonly Guid DangLamViecId = new("5d1c9a3e-6f0b-4c2e-9a71-1b8e2f4d0a01");
        public static readonly Guid DaNghiViecId = new("5d1c9a3e-6f0b-4c2e-9a71-1b8e2f4d0a02");
        public const string MaDangLamViec = "DANG_LAM_VIEC";
        public const string MaDaNghiViec = "DA_NGHI_VIEC";

        public string MaQuanLy {get;private set;}=string.Empty;
        public string TenTinhTrang {get;private set;}=string.Empty;
        public bool KhongConCongTac {get;private set;}=false;

        private TinhTrangNhanVien()
        {
            MaQuanLy = string.Empty;
            TenTinhTrang = string.Empty;
            KhongConCongTac = false;
        }

        private TinhTrangNhanVien(string maQuanLy, string tenTinhTrang, bool khongConCongTac)
        {
            MaQuanLy = maQuanLy;
            TenTinhTrang = tenTinhTrang;
            KhongConCongTac = khongConCongTac;
        }

        internal static TinhTrangNhanVien Create(string maQuanLy, string tenTinhTrang, bool khongConCongTac)
        {
            if (string.IsNullOrEmpty(maQuanLy))
                throw new Exception("Mã quản lý không được để trống");
            if (string.IsNullOrEmpty(tenTinhTrang))
                throw new Exception("Tên tình trạng không được để trống");
            

            return new TinhTrangNhanVien(maQuanLy, tenTinhTrang, khongConCongTac);
        }

        public void UpdateTinhTrangNhanVien(string maQuanLy, string tenTinhTrang, bool khongConCongTac)
        {
            if (string.IsNullOrEmpty(maQuanLy))
                throw new Exception("Mã quản lý không được để trống");
            if (string.IsNullOrEmpty(tenTinhTrang))
                throw new Exception("Tên tình trạng không được để trống");


            MaQuanLy = maQuanLy;
            TenTinhTrang = tenTinhTrang;
            KhongConCongTac = khongConCongTac;
        }

    }
}
