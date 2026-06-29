using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Interfaces;
using RenewalOps.Infrastructure.Jobs;
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
        services.AddScoped<IReminderRunRepository, ReminderRunRepository>();
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddScoped<IAuthService, JwtTokenService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IReminderScheduler, ReminderScheduler>();

        // Background jobs + their default (inline) scheduler. When the Hangfire server is
        // running, AddBackgroundJobs overrides IDocumentJobScheduler with the Hangfire
        // implementation; otherwise OCR runs inline so the app still works end-to-end.
        services.AddScoped<OcrProcessingJob>();
        services.AddScoped<ReminderJob>();
        services.AddScoped<StatusRecomputeJob>();
        services.AddScoped<IDocumentJobScheduler, InlineDocumentJobScheduler>();

        return services;
    }

    /// <summary>
    /// Registers Hangfire with PostgreSQL-backed storage and a background server.
    /// Kept separate from <see cref="AddInfrastructure"/> so test hosts can opt out
    /// (the in-memory test stack does not run a real job server).
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        // With a real job server available, enqueue OCR onto Hangfire instead of running
        // it inline. Overrides the default registered in AddInfrastructure.
        services.AddScoped<IDocumentJobScheduler, HangfireDocumentJobScheduler>();

        return services;
    }
}
