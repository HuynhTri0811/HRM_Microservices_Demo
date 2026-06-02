using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhanSuMicroservice.Migrations
{
    /// <inheritdoc />
    public partial class LyDoThoiViec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiDoThoiViec",
                table: "NhanViens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayThoiViec",
                table: "NhanViens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayVaoCongTy",
                table: "NhanViens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "TinhTrangNhanVienId",
                table: "NhanViens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "TinhTrangNhanVien",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaQuanLy = table.Column<string>(type: "text", nullable: false),
                    TenTinhTrang = table.Column<string>(type: "text", nullable: false),
                    KhongConCongTac = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinhTrangNhanVien", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhanViens_TinhTrangNhanVienId",
                table: "NhanViens",
                column: "TinhTrangNhanVienId");

            migrationBuilder.AddForeignKey(
                name: "FK_NhanViens_TinhTrangNhanVien_TinhTrangNhanVienId",
                table: "NhanViens",
                column: "TinhTrangNhanVienId",
                principalTable: "TinhTrangNhanVien",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NhanViens_TinhTrangNhanVien_TinhTrangNhanVienId",
                table: "NhanViens");

            migrationBuilder.DropTable(
                name: "TinhTrangNhanVien");

            migrationBuilder.DropIndex(
                name: "IX_NhanViens_TinhTrangNhanVienId",
                table: "NhanViens");

            migrationBuilder.DropColumn(
                name: "LiDoThoiViec",
                table: "NhanViens");

            migrationBuilder.DropColumn(
                name: "NgayThoiViec",
                table: "NhanViens");

            migrationBuilder.DropColumn(
                name: "NgayVaoCongTy",
                table: "NhanViens");

            migrationBuilder.DropColumn(
                name: "TinhTrangNhanVienId",
                table: "NhanViens");
        }
    }
}
