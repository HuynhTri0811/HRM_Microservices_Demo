using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.Base;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities
{
    public class TinhTrangNhanVien : ObjectBase
    {
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
