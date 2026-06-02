
namespace QuyetDinhService.Domain.Entities
{
    public class QuyetDinhNangLuong : QuyetDinh
    {
        public Guid MaNhanVien { get; private set; }

        public decimal LuongCoBanCu { get; private set; }
        public decimal LuongCoBanMoi { get; set; }


        private QuyetDinhNangLuong() : base()
        {
            MaNhanVien = Guid.Empty;
            LuongCoBanCu = 0;
            LuongCoBanMoi = 0;
        }

        private QuyetDinhNangLuong(string soQuyetDinh, DateTime ngayQuyetDinh, string noiDung, DateTime ngayHieuLuc)
            : base(soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc)
        {
        }

        public static QuyetDinhNangLuong Create(string soQuyetDinh, DateTime ngayQuyetDinh, string noiDung, DateTime ngayHieuLuc)
        {
            var error = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(soQuyetDinh))
                error.Add("SoQuyetDinh", new[] { "Số quyết định không được để trống" });
            if (string.IsNullOrWhiteSpace(noiDung))
                error.Add("NoiDung", new[] { "Nội dung không được để trống" });
            if (error.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", error, "REQUEST_VALIDATION_ERROR");
            return new QuyetDinhNangLuong(soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc);
            
        }

        public void CapNhatLuongCoBan(Guid NhanVien,decimal luongCoBanCu, decimal luongCoBanMoi)
        {
            var error = new Dictionary<string, string[]>();
            if(NhanVien == Guid.Empty)
                error.Add("MaNhanVien", new[] { "Mã nhân viên không được để trống" });
            if (luongCoBanCu < 0 || luongCoBanMoi < 0)
                error.Add("LuongCoBan", new[] { "Lương cơ bản cũ không được nhỏ hơn 0","Lương cơ bản mới không được nhỏ hơn 0" });
            if (error.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", error, "REQUEST_VALIDATION_ERROR");
            MaNhanVien = NhanVien;
            LuongCoBanCu = luongCoBanCu;
            LuongCoBanMoi = luongCoBanMoi;
        }


    }
}