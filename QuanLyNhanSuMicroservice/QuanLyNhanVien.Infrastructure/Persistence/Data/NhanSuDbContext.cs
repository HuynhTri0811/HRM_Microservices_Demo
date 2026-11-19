using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities.Interface;
using System.Linq.Expressions;
using System.Security.Claims;

namespace QuanLyNhanSuMicroservice.QuanLyNhanVien.Infrastructure.Persistence.Data
{
    public class NhanSuDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NhanSuDbContext(DbContextOptions<NhanSuDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<NhanVien> NhanViens { get; set; }
        public DbSet<PhongBan> PhongBans { get; set; }
        public DbSet<ChucVu> ChucVus { get; set; }
        public DbSet<TaiKhoan> TaiKhoans { get; set; }
        public DbSet<VanBang> VanBangs { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new SoftInterceptor(_httpContextAccessor));
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tự động lọc các bản ghi đã xóa cho toàn bộ các bảng có dùng ISoftDelete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(IDelete).IsAssignableFrom(entityType.ClrType) && entityType.BaseType == null)
                {
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(ConvertFilterExpression(entityType.ClrType));
                }
            }

            SeedTinhTrangNhanVien(modelBuilder);
        }

        // Danh mục tình trạng nhân viên bắt buộc phải có (NhanViens.TinhTrangNhanVienId là khoá ngoại NOT NULL).
        private static void SeedTinhTrangNhanVien(ModelBuilder modelBuilder)
        {
            var seededAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<TinhTrangNhanVien>().HasData(
                new
                {
                    Id = TinhTrangNhanVien.DangLamViecId,
                    MaQuanLy = TinhTrangNhanVien.MaDangLamViec,
                    TenTinhTrang = "Đang làm việc",
                    KhongConCongTac = false,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt,
                    CreatedBy = "System",
                    UpdatedBy = "System",
                    IsDeleted = false
                },
                new
                {
                    Id = TinhTrangNhanVien.DaNghiViecId,
                    MaQuanLy = TinhTrangNhanVien.MaDaNghiViec,
                    TenTinhTrang = "Đã nghỉ việc",
                    KhongConCongTac = true,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt,
                    CreatedBy = "System",
                    UpdatedBy = "System",
                    IsDeleted = false
                });
        }

        private LambdaExpression ConvertFilterExpression(Type entityType)
        {
            // Tạo biểu thức: e => !e.IsDeleted
            var parameter = Expression.Parameter(entityType, "e");
            var property = Expression.Property(parameter, nameof(IDelete.IsDeleted));
            var body = Expression.Not(property);
            return Expression.Lambda(body, parameter);
        }


        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var user = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value
                      ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? "System";

            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is IAuditable && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entityEntry in entries)
            {
                var entity = (IAuditable)entityEntry.Entity;

                if (entityEntry.State == EntityState.Added)
                {
                    entity.UpdateBase(user);
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
