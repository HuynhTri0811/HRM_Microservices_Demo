using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Repositories
{
    public interface ITinhTrangNhanVienRepository
    {
        Task<TinhTrangNhanVien?> GetByIdAsync(Guid id);
    }
}
