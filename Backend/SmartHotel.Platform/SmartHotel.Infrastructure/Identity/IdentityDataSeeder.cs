using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SmartHotel.Infrastructure.Identity;

public static class IdentityDataSeeder
{
    private static readonly string[] DefaultRoles = ["Guest", "Staff", "Admin"];

    public static async Task SeedIdentityDataAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");

        foreach (var roleName in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No se pudo crear el rol '{roleName}': {string.Join(", ", roleResult.Errors.Select(error => error.Description))}");
                }
            }
        }

        var adminEmail = configuration["IdentitySeed:AdminEmail"];
        var adminPassword = configuration["IdentitySeed:AdminPassword"];
        var adminFullName = configuration["IdentitySeed:AdminFullName"] ?? "SmartHotel Admin";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("No se configuro IdentitySeed:AdminEmail o IdentitySeed:AdminPassword. Se crearon roles, pero no usuario admin.");
            return;
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = adminFullName
            };

            var createUserResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createUserResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario admin '{adminEmail}': {string.Join(", ", createUserResult.Errors.Select(error => error.Description))}");
            }
        }
        else if (!string.Equals(adminUser.FullName, adminFullName, StringComparison.Ordinal))
        {
            adminUser.FullName = adminFullName;
            var updateUserResult = await userManager.UpdateAsync(adminUser);
            if (!updateUserResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo actualizar el usuario admin '{adminEmail}': {string.Join(", ", updateUserResult.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            var addRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo asignar rol Admin al usuario '{adminEmail}': {string.Join(", ", addRoleResult.Errors.Select(error => error.Description))}");
            }
        }
    }
}
