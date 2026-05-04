using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SmartHotel.Infrastructure.Identity;

public static class IdentityDataSeeder
{
    private static readonly string[] DefaultRoles = ["Guest", "Staff", "Admin"];
    private const string AdminRoleName = "Admin";
    private const string GuestRoleName = "Guest";

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

        await SeedAdminUserAsync(userManager, configuration, logger);
        await SeedGuestUserAsync(userManager, configuration, logger);
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var adminEmail = configuration["IdentitySeed:AdminEmail"];
        var adminPassword = configuration["IdentitySeed:AdminPassword"];
        var adminFullName = configuration["IdentitySeed:AdminFullName"] ?? "SmartHotel Admin";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("No se configuro IdentitySeed:AdminEmail o IdentitySeed:AdminPassword. Se crearon roles, pero no usuario admin.");
            return;
        }

        var adminUser = await EnsureUserAsync(userManager, adminEmail, adminPassword, adminFullName, "admin");
        await EnsureUserInRoleAsync(userManager, adminUser, AdminRoleName);
    }

    private static async Task SeedGuestUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var guestEmail = configuration["IdentitySeed:GuestEmail"];
        var guestPassword = configuration["IdentitySeed:GuestPassword"];
        var guestFullName = configuration["IdentitySeed:GuestFullName"] ?? "SmartHotel Guest";

        if (string.IsNullOrWhiteSpace(guestEmail) || string.IsNullOrWhiteSpace(guestPassword))
        {
            logger.LogWarning("No se configuro IdentitySeed:GuestEmail o IdentitySeed:GuestPassword. No se creo usuario guest demo.");
            return;
        }

        var guestUser = await EnsureUserAsync(userManager, guestEmail, guestPassword, guestFullName, "guest demo");
        await EnsureUserInRoleAsync(userManager, guestUser, GuestRoleName);

        var currentRoles = await userManager.GetRolesAsync(guestUser);
        var rolesToRemove = currentRoles
            .Where(role => !string.Equals(role, GuestRoleName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeRolesResult = await userManager.RemoveFromRolesAsync(guestUser, rolesToRemove);
            if (!removeRolesResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudieron ajustar los roles del usuario guest demo '{guestEmail}': {string.Join(", ", removeRolesResult.Errors.Select(error => error.Description))}");
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName,
        string userLabel)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName
            };

            var createUserResult = await userManager.CreateAsync(user, password);
            if (!createUserResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario {userLabel} '{email}': {string.Join(", ", createUserResult.Errors.Select(error => error.Description))}");
            }

            return user;
        }

        var shouldUpdateUser = false;
        if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
        {
            user.FullName = fullName;
            shouldUpdateUser = true;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            shouldUpdateUser = true;
        }

        if (shouldUpdateUser)
        {
            var updateUserResult = await userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo actualizar el usuario {userLabel} '{email}': {string.Join(", ", updateUserResult.Errors.Select(error => error.Description))}");
            }
        }

        return user;
    }

    private static async Task EnsureUserInRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string roleName)
    {
        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
        if (!addRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo asignar rol {roleName} al usuario '{user.Email}': {string.Join(", ", addRoleResult.Errors.Select(error => error.Description))}");
        }
    }
}
