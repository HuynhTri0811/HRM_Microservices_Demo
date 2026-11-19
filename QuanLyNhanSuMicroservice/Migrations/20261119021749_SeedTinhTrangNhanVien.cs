using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuanLyNhanSuMicroservice.Migrations
{
    /// <inheritdoc />
    public partial class SeedTinhTrangNhanVien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "TinhTrangNhanVien",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "KhongConCongTac", "MaQuanLy", "TenTinhTrang", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("5d1c9a3e-6f0b-4c2e-9a71-1b8e2f4d0a01"), new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, null, false, false, "DANG_LAM_VIEC", "Đang làm việc", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("5d1c9a3e-6f0b-4c2e-9a71-1b8e2f4d0a02"), new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, null, false, true, "DA_NGHI_VIEC", "Đã nghỉ việc", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TinhTrangNhanVien",
                keyColumn: "Id",
                keyValue: new Guid("5d1c9a3e-6f0b-4c2e-9a71-1b8e2f4d0a01"));

            migrationBuilder.DeleteData(
                table: "TinhTrangNhanVien",
                keyColumn: "Id",
                keyValue: new Guid("5d1c9a3e-6f0b-4c2e-9a71-1b8e2f4d0a02"));
        }
    }
}
