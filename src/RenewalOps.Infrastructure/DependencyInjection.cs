using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Interfaces;
using RenewalOps.Infrastructure.Persistence;
using RenewalOps.Infrastructure.Persistence.Repositories;
using RenewalOps.Infrastructure.Services;

namespace RenewalOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var minioClient = new MinioClient()
            .WithEndpoint(config["MinIO:Endpoint"])
            .WithCredentials(config["MinIO:AccessKey"], config["MinIO:SecretKey"])
            .WithSSL(false)
            .Build();
        services.AddSingleton<IMinioClient>(minioClient);

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddScoped<IAuthService, JwtTokenService>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services;
    }
}
