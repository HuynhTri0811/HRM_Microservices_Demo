using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.Base;
using System.Security;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities
{
    public class PhongBan : ObjectBase
    {
        public string MaQuanLy { get; private set; }
        public string TenPhongBan { get; private set; }
        private PhongBan()
        {

        }
        private PhongBan(string MaQuanLy, string TenPhongBan)
        {
            this.MaQuanLy = MaQuanLy;
            this.TenPhongBan = TenPhongBan;
        }

        private void Update(string MaQuanLy, string TenPhongBan)
        {
            this.MaQuanLy = MaQuanLy;
            this.TenPhongBan = TenPhongBan;
        }

        public static PhongBan Create(string MaQuanLy, string TenPhongBan)
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(MaQuanLy))
                errors.Add("MaQuanLy", new[] { "Mã quản lý không được để trống" });
            if (string.IsNullOrWhiteSpace(TenPhongBan))
                errors.Add("TenPhongBan", new[] { "Tên phòng ban không được để trống" });
            if (errors.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", errors, "REQUEST_VALIDATION_ERROR");
            return new PhongBan(MaQuanLy, TenPhongBan);
        }

        public void CapNhat(string MaQuanLy, string TenPhongBan)
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(MaQuanLy))
                errors.Add("MaQuanLy", new[] { "Mã quản lý không được để trống" });
            if (string.IsNullOrWhiteSpace(TenPhongBan))
                errors.Add("TenPhongBan", new[] { "Tên phòng ban không được để trống" });
            if (errors.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", errors, "REQUEST_VALIDATION_ERROR");
            Update(MaQuanLy, TenPhongBan);
        }

        public void Delete()
        {
            base.Delete("System");
        }


    }
}
