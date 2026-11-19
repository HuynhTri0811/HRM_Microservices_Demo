using System;
using HRM.Contracts.NhanSu;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuyetDinhService.QuyetDinhService.Application.Services;
using QuyetDinhService.QuyetDinhService.Domain.Repositories;
using QuyetDinhService.QuyetDinhService.Infrastructure.Data;
using QuyetDinhService.QuyetDinhService.Infrastructure.Repositories;

namespace QuyetDinhService.Extensions
{
    public static class DatabaseExtensions
    {
        public static void ConfigureDatabase(this WebApplicationBuilder builder)
        {
            // Register DbContext
            builder.Services.AddDbContext<QuyetDinhDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
                    x => x.MigrationsHistoryTable("__QuyetDinhMigrationsHistory")));

            // Register Repositories
            builder.Services.AddScoped<IQuyetDinhNangLuongRepositoris, QuyetDinhNangLuongRepository>();
            builder.Services.AddScoped<IQuyetDinhBoNhiemRepository, QuyetDinhBoNhiemRepository>();

            // Register gRPC client tới NhanSu service (Protos/nhansu.proto)
            builder.Services.AddGrpcClient<NhanSuGrpc.NhanSuGrpcClient>(options =>
            {
                options.Address = new Uri(builder.Configuration["NhanSuGrpcUrl"] ?? "http://nhansu-api:9081");
            });
            builder.Services.AddScoped<INhanSuServiceClient, NhanSuServiceClient>();

            // Register MediatR
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        }

        public static void MigrateDatabase(this WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<QuyetDinhDbContext>();
                context.Database.Migrate();
            }
        }
    }
}
