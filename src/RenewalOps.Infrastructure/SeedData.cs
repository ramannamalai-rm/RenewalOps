using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Domain.Entities;
using RenewalOps.Domain.Enums;

namespace RenewalOps.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var config = serviceProvider.GetRequiredService<IConfiguration>();

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@renewalops.local";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin123!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new User
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                Role = UserRole.Admin,
                CreatedUtc = DateTime.UtcNow
            };
            await userManager.CreateAsync(admin, adminPassword);
        }
    }
}
