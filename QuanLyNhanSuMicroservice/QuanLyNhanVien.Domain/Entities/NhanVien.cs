using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.Base;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities
{
    public class NhanVien : ObjectBase
    {
        public string MaNhanVien { get; private set; }
        public string TenNhanVien { get; private set; } = string.Empty;
        public DateTime NgaySinh { get; private set; }
        public string GioiTinh { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;


        public decimal TongLuong => LuongCoBan + PhuCap;
        public decimal LuongCoBan { get; private set; }
        public decimal PhuCap { get; private set; }


        public DateTime NgayVaoCongTy { get; private set; }
        public DateTime? NgayThoiViec { get; private set; }
        public string? LiDoThoiViec { get; private set; }
        public TinhTrangNhanVien TinhTrangNhanVien { get; private set; }




        public ChucVu? ChucVu { get; private set; }

        public PhongBan PhongBan { get; private set; }

        public ICollection<VanBang> VanBangs { get; private set; } = new List<VanBang>();

        private NhanVien() { }

        private NhanVien(string MaNhanVien, string TenNhanVien, DateTime NgaySinh, string GioiTinh, string Email, PhongBan PhongBan)
        {
            this.MaNhanVien = MaNhanVien;
            this.TenNhanVien = TenNhanVien;
            this.NgaySinh = NgaySinh;
            this.GioiTinh = GioiTinh;
            this.Email = Email;
            this.PhongBan = PhongBan;
        }

        public static NhanVien Create(string MaNhanVien, string TenNhanVien, DateTime NgaySinh, string GioiTinh, string Email, PhongBan PhongBan)
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(MaNhanVien))
                errors.Add("MaNhanVien", new[] { "Mã nhân viên không được để trống" });
            if (string.IsNullOrWhiteSpace(TenNhanVien))
                errors.Add("TenNhanVien", new[] { "Tên nhân viên không được để trống" });
            if (string.IsNullOrWhiteSpace(GioiTinh))
                errors.Add("GioiTinh", new[] { "Giới tính không được để trống" });
            if (string.IsNullOrWhiteSpace(Email))
                errors.Add("Email", new[] { "Email không được để trống" });
            if (errors.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", errors, "REQUEST_VALIDATION_ERROR");

            if (NgaySinh > DateTime.Now)
                throw new BadRequestException("Ngày sinh không được lớn hơn ngày hiện tại", "INVALID_DATE");
            if (PhongBan == null)
                throw new BadRequestException("Phòng ban không được để trống","NOT_FOUND");
            if(Email.Contains("@") == false)
                throw new BadRequestException("Email không hợp lệ","INVALID_EMAIL");
            
            return new NhanVien(MaNhanVien, TenNhanVien, NgaySinh, GioiTinh, Email, PhongBan);
        }

        public void UpdateInfo(string TenNhanVien, DateTime NgaySinh, string GioiTinh, string Email, PhongBan PhongBan)
        {

            if (string.IsNullOrWhiteSpace(TenNhanVien) || string.IsNullOrWhiteSpace(GioiTinh) || string.IsNullOrWhiteSpace(Email))
                throw new BadRequestException("Thông tin nhân viên không được để trống", "REQUEST_VALIDATION_ERROR");
            if (NgaySinh > DateTime.Now)
                throw new BadRequestException("Ngày sinh không được lớn hơn ngày hiện tại", "INVALID_DATE");
            if (PhongBan == null)
                throw new BadRequestException("Phòng ban không được để trống","NOT_FOUND");
            if(Email.Contains("@") == false)
                throw new BadRequestException("Email không hợp lệ","INVALID_EMAIL");

            this.TenNhanVien = TenNhanVien;
            this.NgaySinh = NgaySinh;
            this.GioiTinh = GioiTinh;
            this.Email = Email;
            this.PhongBan = PhongBan;
        }

        public void SetNhanVienLamViec(TinhTrangNhanVien tinhTrangNhanVien,DateTime NgayVaoCongTy)
        {
            if (tinhTrangNhanVien == null)
                throw new BadRequestException("Tình trạng nhân viên không được để trống", "REQUEST_VALIDATION_ERROR");
            if(tinhTrangNhanVien.KhongConCongTac == true)
                throw new BadRequestException("Tình trạng nhân viên phải là còn công tác", "REQUEST_VALIDATION_ERROR");
            if(NgayVaoCongTy > DateTime.Now)
                throw new BadRequestException("Ngày vào công ty không được lớn hơn ngày hiện tại", "INVALID_DATE");
            if(NgayVaoCongTy < NgaySinh)
                throw new BadRequestException("Ngày vào công ty không được nhỏ hơn ngày sinh","INVALID_DATE");
            
            this.TinhTrangNhanVien = tinhTrangNhanVien;
            this.NgayVaoCongTy = NgayVaoCongTy;
            
        }

        public void SetNhanVienThoiViec(TinhTrangNhanVien tinhTrangNhanVien,DateTime NgayThoiViec,string LiDoThoiViec)
        {
            if (tinhTrangNhanVien == null)
                throw new BadRequestException("Tình trạng nhân viên không được để trống", "REQUEST_VALIDATION_ERROR");
            if(string.IsNullOrWhiteSpace(LiDoThoiViec))
                throw new BadRequestException("Lý do thôi việc không được để trống","REQUEST_VALIDATION_ERROR");
            if(NgayThoiViec > DateTime.Now)
                throw new BadRequestException("Ngày thôi việc không được lớn hơn ngày hiện tại","INVALID_DATE");
            if(NgayThoiViec < NgayVaoCongTy)
                throw new BadRequestException("Ngày thôi việc không được nhỏ hơn ngày vào công ty","INVALID_DATE");
            if(TinhTrangNhanVien.KhongConCongTac == false)
                throw new BadRequestException("Tình trạng nhân viên phải là không còn công tác", "REQUEST_VALIDATION_ERROR");
                
            this.TinhTrangNhanVien = tinhTrangNhanVien;
            this.NgayThoiViec = NgayThoiViec;
            this.LiDoThoiViec = LiDoThoiViec;
        }



        public void UpdateLuong(decimal LuongCoBan, decimal PhuCap)
        {
            if (LuongCoBan < 0 || PhuCap < 0)
                throw new BadRequestException("Lương và phụ cấp không được nhỏ hơn 0","INVALID_MONEY");
            this.LuongCoBan = LuongCoBan;
            this.PhuCap = PhuCap;
        }

        public void CapNhatLuong(decimal luongCoBan)
        {
            if (luongCoBan < 0)
                throw new BadRequestException("Lương không được nhỏ hơn 0","INVALID_MONEY");
            this.LuongCoBan = luongCoBan;
        }

        private void UpdateChucVu(ChucVu chucVu)
        {
            if (chucVu == null)
                throw new BadRequestException("Chức vụ không được để trống", "NOT_FOUND");
            this.ChucVu = chucVu;
            this.PhuCap = chucVu.PhuCap;
        }

        public void UpdatePosition(ChucVu chucVu)
        {
            if (chucVu == null)
                throw new BadRequestException("Chức vụ không được để trống", "NOT_FOUND");


            UpdateChucVu(chucVu);
        }

        internal void DeletePosition()
        {
            this.ChucVu = null;
            this.PhuCap = 0;
        }
    }
}
