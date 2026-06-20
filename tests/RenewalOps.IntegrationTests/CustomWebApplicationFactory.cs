using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minio;
using RenewalOps.Application.Interfaces;
using RenewalOps.Infrastructure.Persistence;

namespace RenewalOps.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = "RenewalOpsTests_" + Guid.NewGuid();

    static CustomWebApplicationFactory()
    {
        // Program.cs reads BackgroundJobs:Enabled from builder.Configuration BEFORE
        // builder.Build(), so WebApplicationFactory's ConfigureAppConfiguration override
        // (which only applies post-build) is too late. Environment variables are read by
        // WebApplication.CreateBuilder immediately, so set it here before any host builds.
        // Every integration test runs DB-free, so disabling the Hangfire server globally
        // for this test process is safe.
        Environment.SetEnvironmentVariable("BackgroundJobs__Enabled", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "ThisIsADevelopmentSecretKeyThatIsLongEnoughForHS256!@#$%",
                ["Jwt:Issuer"] = "RenewalOps",
                ["Jwt:Audience"] = "RenewalOps",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Seed:AdminEmail"] = "admin@renewalops.local",
                ["Seed:AdminPassword"] = "Admin123!",
                ["MinIO:Endpoint"] = "localhost:9000",
                ["MinIO:AccessKey"] = "minioadmin",
                ["MinIO:SecretKey"] = "minioadmin",
                ["Ocr:TessdataPath"] = "./tessdata",
                ["BackgroundJobs:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Strip every DbContext-related registration the production wiring left behind.
            // AddDbContext registers options, configurations, internal services, etc; if we
            // only remove DbContextOptions<T>, the original Npgsql wiring still pollutes the
            // container and InMemory store isolation breaks across request scopes.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName, _databaseRoot);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService, FakeStorageService>();

            services.RemoveAll<IOcrService>();
            services.AddSingleton<IOcrService, FakeOcrService>();

            services.RemoveAll<IMinioClient>();
        });
    }
}

public class FakeStorageService : IStorageService
{
    private readonly Dictionary<string, byte[]> _store = new();

    public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _store[key] = ms.ToArray();
        return Task.FromResult(key);
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(key, out var data))
            throw new FileNotFoundException($"Key not found: {key}");
        return Task.FromResult<Stream>(new MemoryStream(data));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}

public class FakeOcrService : IOcrService
{
    public Task<OcrResult> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken ct = default)
    {
        return Task.FromResult(new OcrResult(
            RawText: "PASSPORT\nName: John Doe\nExpiry Date: 12/31/2025\nIssue Date: 01/01/2020",
            DetectedExpiryDate: new DateTime(2025, 12, 31),
            DetectedIssueDate: new DateTime(2020, 1, 1)));
    }
}
