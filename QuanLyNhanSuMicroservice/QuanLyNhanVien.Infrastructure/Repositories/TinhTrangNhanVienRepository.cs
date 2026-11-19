using Microsoft.EntityFrameworkCore;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Repositories;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Infrastructure.Persistence.Data;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Infrastructure.Repositories
{
    // Được Scrutor tự đăng ký (tên class kết thúc bằng "Repository").
    public class TinhTrangNhanVienRepository(NhanSuDbContext context) : ITinhTrangNhanVienRepository
    {
        public async Task<TinhTrangNhanVien?> GetByIdAsync(Guid id)
        {
            return await context.Set<TinhTrangNhanVien>().FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
