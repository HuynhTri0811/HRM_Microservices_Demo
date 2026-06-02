using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.Base;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Exceptions;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities
{
    public class TaiKhoan : ObjectBase
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } = "User";

        public TaiKhoan()
        {
            Username = string.Empty;
            PasswordHash = string.Empty;
        }

        public TaiKhoan(string username, string passwordHash, string role = "User")
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(username))
                errors.Add("Username", new[] { "Username không được để trống" });
            if (string.IsNullOrWhiteSpace(passwordHash))
                errors.Add("PasswordHash", new[] { "PasswordHash không được để trống" });
            if (errors.Count > 0)
                throw new BadRequestException("Dữ liệu đầu vào không hợp lệ", errors, "REQUEST_VALIDATION_ERROR");

            Username = username;
            PasswordHash = passwordHash;
            Role = role;
        }
    }
}
