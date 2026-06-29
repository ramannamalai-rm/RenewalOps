using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RenewalOps.Application;
using RenewalOps.Infrastructure;
using RenewalOps.Infrastructure.Jobs;
using RenewalOps.Infrastructure.Persistence;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Background jobs require a live Postgres connection for Hangfire storage; the
    // integration-test host disables them so it can run DB-free. The env var is read
    // directly (not via builder.Configuration) because top-level startup code runs
    // before builder.Build(), and WebApplicationFactory's config overrides only apply
    // post-build — the env var is the one source guaranteed to be visible here.
    var backgroundJobsEnabled =
        bool.TryParse(Environment.GetEnvironmentVariable("BackgroundJobs__Enabled"), out var bgEnv)
            ? bgEnv
            : builder.Configuration.GetValue("BackgroundJobs:Enabled", true);
    if (backgroundJobsEnabled)
        builder.Services.AddBackgroundJobs(builder.Configuration);

    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            {
                KeyId = "renewalops-key"
            },
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "RenewalOps API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new()
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token"
        });
        c.AddSecurityRequirement(new()
        {
            {
                new()
                {
                    Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        await SeedData.InitializeAsync(scope.ServiceProvider);
    }

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RenewalOps API v1"));

    app.UseAuthentication();
    app.UseAuthorization();

    if (backgroundJobsEnabled)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[]
            {
                new HangfireDashboardAuthorizationFilter(app.Environment.IsDevelopment())
            }
        });

        // Nightly recurring job that recomputes ExpiringSoon/Expired statuses.
        var recomputeCron = app.Configuration["BackgroundJobs:StatusRecomputeCron"] ?? Cron.Daily();
        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<StatusRecomputeJob>(
            "nightly-status-recompute",
            job => job.RunAsync(CancellationToken.None),
            recomputeCron);
    }

    app.MapControllers();

    Log.Information("RenewalOps API starting on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
