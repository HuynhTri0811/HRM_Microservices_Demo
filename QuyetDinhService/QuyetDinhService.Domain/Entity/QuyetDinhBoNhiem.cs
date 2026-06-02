
namespace QuyetDinhService.Domain.Entities
{
    public class QuyetDinhBoNhiem : QuyetDinh
    {
        public Guid MaNhanVien { get; private set; }
        public Guid ChucVuCu { get; private set; }
        public Guid ChucVuMoi { get; private set; }
        public decimal PhuCapCu { get; private set; }
        public decimal PhuCapMoi { get; private set; }
        public string LyDo { get; set; } = string.Empty;

        private QuyetDinhBoNhiem() : base()
        {
            MaNhanVien = Guid.Empty;
            ChucVuCu = Guid.Empty;
            ChucVuMoi = Guid.Empty;
            PhuCapCu = 0;
            PhuCapMoi = 0;
        }

        private QuyetDinhBoNhiem(string soQuyetDinh, DateTime ngayQuyetDinh, string noiDung, DateTime ngayHieuLuc)
            : base(soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc)
        {
        }

        public static QuyetDinhBoNhiem Create(string soQuyetDinh, DateTime ngayQuyetDinh, string noiDung, DateTime ngayHieuLuc)
        {
            var error = new Dictionary<string, string[]>(); 
            if (string.IsNullOrWhiteSpace(soQuyetDinh))
                error.Add("SoQuyetDinh", new[] { "Số quyết định không được để trống" });
            if (string.IsNullOrWhiteSpace(noiDung))
                error.Add("NoiDung", new[] { "Nội dung không được để trống" });
            if (error.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", error, "REQUEST_VALIDATION_ERROR");
            return new QuyetDinhBoNhiem(soQuyetDinh, ngayQuyetDinh, noiDung, ngayHieuLuc);
        }

        public void BoNhiem(Guid maNhanVien, Guid chucVuCu, Guid chucVuMoi, decimal phuCapCu, decimal phuCapMoi, string lyDo)
        {
            var error = new Dictionary<string, string[]>();
            if (maNhanVien == Guid.Empty)
                error.Add("MaNhanVien", new[] { "Mã nhân viên không được để trống" });
            if (chucVuCu == Guid.Empty || chucVuMoi == Guid.Empty)
                error.Add("ChucVu", new[] { "Mã chức vụ không được để trống" });
            if (phuCapCu < 0)
                error.Add("PhuCapCu", new[] { "Phụ cấp không được nhỏ hơn 0" });
            if (phuCapMoi < 0)
                error.Add("PhuCapMoi", new[] { "Phụ cấp không được nhỏ hơn 0" });
            if (error.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", error, "REQUEST_VALIDATION_ERROR");
            MaNhanVien = maNhanVien;
            ChucVuCu = chucVuCu;
            ChucVuMoi = chucVuMoi;
            PhuCapCu = phuCapCu;
            PhuCapMoi = phuCapMoi;
            LyDo = lyDo;
        }
    }
}
