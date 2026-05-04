using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartHotel.API.Common.Auth;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Auth.Dto;
using SmartHotel.Domain.Entities;
using SmartHotel.API.Features.Auth.Endpoints;
using SmartHotel.Infrastructure.Identity;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.UnitTests.Features.Auth;

public sealed class AuthEndpointsTests
{
    [Theory]
    [MemberData(nameof(RegisterCases))]
    public async Task RegisterAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        RegisterRequestDto? request,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var harness = CreateHarness();
        await SeedDocumentTypesAsync(harness.DbContext);

        if (string.Equals(scenario, "duplicate_email", StringComparison.Ordinal))
        {
            await CreateUserAsync(harness, "guest@hotel.com", "Guest123!");
        }

        var operation = InvokeEndpointAsync(
            "RegisterAsync",
            request,
            harness.UserManager,
            harness.RoleManager,
            harness.DbContext,
            harness.IdentityOptions,
            harness.AuthLogger,
            CancellationToken.None);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<RegisterResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        Assert.Equal("guest@hotel.com", payload.Email);
        Assert.Contains("Guest", payload.Roles);

        var createdUser = await harness.UserManager.FindByEmailAsync("guest@hotel.com");
        Assert.NotNull(createdUser);
        Assert.Equal("Usuario Guest", createdUser.FullName);
        Assert.True(await harness.UserManager.IsInRoleAsync(createdUser, "Guest"));

        var persistedGuest = await harness.DbContext.Guests.SingleOrDefaultAsync(guest => guest.UserId == createdUser.Id);
        Assert.NotNull(persistedGuest);
        Assert.Equal("Usuario", persistedGuest.FirstName);
        Assert.Equal("Guest", persistedGuest.LastName);
        Assert.Equal(1, persistedGuest.DocumentTypeId);
        Assert.Equal("12345678", persistedGuest.DocumentNumber);
        Assert.Equal("guest@hotel.com", persistedGuest.Email);
    }

    public static IEnumerable<object?[]> RegisterCases()
    {
        yield return
        [
            "success",
            new RegisterRequestDto("Usuario", "Guest", 1, "12345678", new DateOnly(1990, 1, 1), "guest@hotel.com", "Guest123!"),
            false,
            null,
            null
        ];

        yield return
        [
            "duplicate_email",
            new RegisterRequestDto("Usuario", "Guest", 1, "12345678", new DateOnly(1990, 1, 1), "guest@hotel.com", "Guest123!"),
            true,
            "cuenta registrada con ese email",
            StatusCodes.Status409Conflict
        ];

        yield return
        [
            "null_request",
            null,
            true,
            "cuerpo",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "missing_first_name",
            new RegisterRequestDto(null!, "Guest", 1, "12345678", new DateOnly(1990, 1, 1), "guest.no.name@hotel.com", "Guest123!"),
            true,
            "firstName",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "underage_birth_date",
            new RegisterRequestDto(
                "Guest",
                "Teen",
                1,
                "87654321",
                DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17),
                "guest.teen@hotel.com",
                "Guest123!"),
            true,
            "18 anos",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "invalid_document_number",
            new RegisterRequestDto("Guest", "Invalid Doc", 1, "12345A", new DateOnly(1990, 1, 1), "guest.invalid.doc@hotel.com", "Guest123!"),
            true,
            "al menos 7 digitos",
            StatusCodes.Status400BadRequest
        ];
    }

    [Theory]
    [MemberData(nameof(LoginCases))]
    public async Task LoginAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        LoginRequestDto request,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var harness = CreateHarness();

        if (!string.Equals(scenario, "user_not_found", StringComparison.Ordinal))
        {
            var user = await CreateUserAsync(harness, "guest@hotel.com", "Guest123!");
            await AddRolesAsync(harness, user, "Guest");
        }

        var operation = InvokeEndpointAsync(
            "LoginAsync",
            request,
            harness.UserManager,
            harness.SignInManager,
            harness.IdentityOptions,
            harness.AuthLogger,
            harness.JwtTokenService);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<LoginResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(FakeJwtTokenService.AccessTokenValue, payload.AccessToken);
        Assert.Equal("guest@hotel.com", payload.Email);
        Assert.Contains("Guest", payload.Roles);
    }

    public static IEnumerable<object?[]> LoginCases()
    {
        yield return
        [
            "success",
            new LoginRequestDto("guest@hotel.com", "Guest123!"),
            false,
            null,
            null
        ];

        yield return
        [
            "invalid_password",
            new LoginRequestDto("guest@hotel.com", "WrongPassword123!"),
            true,
            "Credenciales invalidas",
            StatusCodes.Status401Unauthorized
        ];

        yield return
        [
            "user_not_found",
            new LoginRequestDto("missing@hotel.com", "AnyPass123!"),
            true,
            "Credenciales invalidas",
            StatusCodes.Status401Unauthorized
        ];
    }

    [Theory]
    [MemberData(nameof(ChangePasswordCases))]
    public async Task ChangePasswordAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        ChangePasswordRequestDto request,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var harness = CreateHarness();

        ApplicationUser? user = null;
        ClaimsPrincipal principal;

        if (string.Equals(scenario, "missing_sub_claim", StringComparison.Ordinal))
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Tests"));
        }
        else
        {
            user = await CreateUserAsync(harness, "change.password@hotel.com", "OldPass123!");
            principal = CreatePrincipalWithSub(user.Id);
        }

        var operation = InvokeEndpointAsync(
            "ChangePasswordAsync",
            request,
            principal,
            harness.AuthLogger,
            harness.UserManager,
            harness.Configuration);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, statusResult.StatusCode);

        Assert.NotNull(user);
        Assert.True(await harness.UserManager.CheckPasswordAsync(user, "NewPass123!"));
    }

    public static IEnumerable<object?[]> ChangePasswordCases()
    {
        yield return
        [
            "success",
            new ChangePasswordRequestDto("OldPass123!", "NewPass123!"),
            false,
            null,
            null
        ];

        yield return
        [
            "missing_sub_claim",
            new ChangePasswordRequestDto("OldPass123!", "NewPass123!"),
            true,
            "claim 'sub'",
            StatusCodes.Status401Unauthorized
        ];

        yield return
        [
            "wrong_current_password",
            new ChangePasswordRequestDto("WrongCurrent123!", "NewPass123!"),
            true,
            "incorrect",
            StatusCodes.Status400BadRequest
        ];
    }

    [Theory]
    [MemberData(nameof(UpdateGuestProfileCases))]
    public async Task UpdateCurrentGuestProfileAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var harness = CreateHarness();
        await SeedDocumentTypesAsync(harness.DbContext);

        var owner = await CreateUserAsync(harness, "guest.profile@hotel.com", "Guest123!");
        await AddRolesAsync(harness, owner, "Guest");

        harness.DbContext.Guests.Add(new Guest
        {
            UserId = owner.Id,
            DocumentTypeId = 1,
            FirstName = "Guest",
            LastName = "Profile",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1),
            Email = "guest.profile@hotel.com",
            Phone = "111111"
        });

        if (string.Equals(scenario, "duplicate_email", StringComparison.Ordinal))
        {
            await CreateUserAsync(harness, "duplicate.email@hotel.com", "Guest123!");
        }

        if (string.Equals(scenario, "duplicate_document", StringComparison.Ordinal))
        {
            harness.DbContext.Guests.Add(new Guest
            {
                DocumentTypeId = 1,
                FirstName = "Otro",
                LastName = "Huesped",
                DocumentNumber = "87654321",
                BirthDate = new DateTime(1991, 1, 1),
                Email = "otro@hotel.com"
            });
        }

        await harness.DbContext.SaveChangesAsync();

        var principal = string.Equals(scenario, "missing_sub_claim", StringComparison.Ordinal)
            ? new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Tests"))
            : CreatePrincipalWithSub(owner.Id);

        var requestDocumentTypeId = string.Equals(scenario, "duplicate_document", StringComparison.Ordinal) ? 1 : 2;

        var request = new UpdateGuestProfileRequestDto(
            requestDocumentTypeId,
            "Nuevo",
            "Nombre",
            string.Equals(scenario, "duplicate_document", StringComparison.Ordinal) ? "87654321" : "87654322",
            new DateOnly(1992, 5, 20),
            string.Equals(scenario, "duplicate_email", StringComparison.Ordinal) ? "duplicate.email@hotel.com" : "updated@hotel.com",
            "222222");

        var operation = InvokeEndpointAsync(
            "UpdateCurrentGuestProfileAsync",
            request,
            principal,
            harness.DbContext,
            harness.UserManager,
            harness.Configuration,
            harness.AuthLogger,
            CancellationToken.None);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<GuestProfileDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(2, payload.DocumentTypeId);
        Assert.Equal("Pasaporte", payload.DocumentTypeName);
        Assert.Equal("Nuevo", payload.FirstName);
        Assert.Equal("Nombre", payload.LastName);
        Assert.Equal("updated@hotel.com", payload.Email);

        var persistedUser = await harness.UserManager.FindByIdAsync(owner.Id);
        Assert.NotNull(persistedUser);
        Assert.Equal("updated@hotel.com", persistedUser.Email);
        Assert.Equal("updated@hotel.com", persistedUser.UserName);
        Assert.Equal("Nuevo Nombre", persistedUser.FullName);

        var persistedGuest = await harness.DbContext.Guests.SingleAsync(entity => entity.UserId == owner.Id);
        Assert.Equal(2, persistedGuest.DocumentTypeId);
        Assert.Equal("87654322", persistedGuest.DocumentNumber);
        Assert.Equal("updated@hotel.com", persistedGuest.Email);
        Assert.Equal("222222", persistedGuest.Phone);
    }

    public static IEnumerable<object?[]> UpdateGuestProfileCases()
    {
        yield return ["success", false, null, null];
        yield return ["duplicate_email", true, "cuenta registrada con ese email", StatusCodes.Status409Conflict];
        yield return ["duplicate_document", true, "tipo y numero de documento", StatusCodes.Status409Conflict];
        yield return ["missing_sub_claim", true, "claim 'sub'", StatusCodes.Status401Unauthorized];
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task ListUsersAsync_ShouldReturnUsersAndRoles(bool seedUsers, int expectedCount)
    {
        await using var harness = CreateHarness();

        if (seedUsers)
        {
            var admin = await CreateUserAsync(harness, "admin@hotel.com", "Admin123!");
            var guest = await CreateUserAsync(harness, "guest@hotel.com", "Guest123!");

            await AddRolesAsync(harness, admin, "Admin");
            await AddRolesAsync(harness, guest, "Guest");
        }

        var result = await InvokeEndpointAsync(
            "ListUsersAsync",
            harness.UserManager,
            harness.DbContext,
            CancellationToken.None);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<List<AuthUserRolesDto>>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(expectedCount, payload.Count);

        if (!seedUsers)
        {
            return;
        }

        Assert.Equal("admin@hotel.com", payload[0].Email);
        Assert.Contains("Admin", payload[0].Roles);
        Assert.DoesNotContain(payload, user => string.Equals(user.Email, "guest@hotel.com", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(GetUserRolesCases))]
    public async Task GetUserRolesAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var harness = CreateHarness();

        string userId;
        if (string.Equals(scenario, "success", StringComparison.Ordinal))
        {
            var user = await CreateUserAsync(harness, "roles@hotel.com", "Roles123!");
            await AddRolesAsync(harness, user, "Guest", "Staff");
            userId = user.Id;
        }
        else
        {
            userId = "missing-user-id";
        }

        var operation = InvokeEndpointAsync("GetUserRolesAsync", userId, harness.UserManager);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<AuthUserRolesDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal("roles@hotel.com", payload.Email);
        Assert.Equal(new[] { "Guest", "Staff" }, payload.Roles);
    }

    public static IEnumerable<object?[]> GetUserRolesCases()
    {
        yield return ["success", false, null, null];
        yield return ["user_not_found", true, "usuario indicado", StatusCodes.Status404NotFound];
    }

    [Theory]
    [MemberData(nameof(UpdateUserRolesCases))]
    public async Task UpdateUserRolesAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var harness = CreateHarness();

        string targetUserId;
        ClaimsPrincipal principal;
        UpdateUserRolesRequestDto request;

        switch (scenario)
        {
            case "success":
            {
                var admin = await CreateUserAsync(harness, "admin@hotel.com", "Admin123!");
                var target = await CreateUserAsync(harness, "target@hotel.com", "Target123!");
                await AddRolesAsync(harness, admin, "Admin");
                await AddRolesAsync(harness, target, "Guest");

                targetUserId = target.Id;
                principal = CreatePrincipalWithSub(admin.Id);
                request = new UpdateUserRolesRequestDto(["Guest", "Staff"]);
                break;
            }

            case "invalid_role":
            {
                var admin = await CreateUserAsync(harness, "admin.invalid@hotel.com", "Admin123!");
                var target = await CreateUserAsync(harness, "target.invalid@hotel.com", "Target123!");
                await AddRolesAsync(harness, admin, "Admin");
                await AddRolesAsync(harness, target, "Guest");

                targetUserId = target.Id;
                principal = CreatePrincipalWithSub(admin.Id);
                request = new UpdateUserRolesRequestDto(["Root"]);
                break;
            }

            case "user_not_found":
            {
                var admin = await CreateUserAsync(harness, "admin.notfound@hotel.com", "Admin123!");
                await AddRolesAsync(harness, admin, "Admin");

                targetUserId = "missing-user-id";
                principal = CreatePrincipalWithSub(admin.Id);
                request = new UpdateUserRolesRequestDto(["Guest"]);
                break;
            }

            case "self_remove_admin":
            {
                var admin = await CreateUserAsync(harness, "self.admin@hotel.com", "Admin123!");
                await AddRolesAsync(harness, admin, "Admin");

                targetUserId = admin.Id;
                principal = CreatePrincipalWithSub(admin.Id);
                request = new UpdateUserRolesRequestDto(["Guest"]);
                break;
            }

            default:
                throw new InvalidOperationException($"Escenario no soportado: {scenario}");
        }

        var operation = InvokeEndpointAsync(
            "UpdateUserRolesAsync",
            targetUserId,
            request,
            principal,
            harness.UserManager,
            harness.RoleManager);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<AuthUserRolesDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(new[] { "Guest", "Staff" }, payload.Roles);

        var targetUser = await harness.UserManager.FindByIdAsync(targetUserId);
        Assert.NotNull(targetUser);

        var roles = (await harness.UserManager.GetRolesAsync(targetUser))
            .OrderBy(role => role)
            .ToArray();

        Assert.Equal(new[] { "Guest", "Staff" }, roles);
    }

    public static IEnumerable<object?[]> UpdateUserRolesCases()
    {
        yield return ["success", false, null, null];
        yield return ["invalid_role", true, "Roles no permitidos", StatusCodes.Status400BadRequest];
        yield return ["user_not_found", true, "usuario indicado", StatusCodes.Status404NotFound];
        yield return ["self_remove_admin", true, "quitarte el rol Admin", StatusCodes.Status409Conflict];
    }

    [Theory]
    [MemberData(nameof(AuthorizationCases))]
    public void MapAuthEndpoints_ShouldConfigureExpectedAuthorization(
        string httpMethod,
        string route,
        bool requiresAuthorization,
        string? expectedPolicy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")));
        builder.Services.AddSingleton<IJwtTokenService, FakeJwtTokenService>();

        var app = builder.Build();
        app.MapAuthEndpoints();

        var allRouteEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var matchingEndpoints = allRouteEndpoints
            .Where(routeEndpoint =>
                HasHttpMethod(routeEndpoint, httpMethod)
                && RouteMatches(routeEndpoint, route))
            .ToList();

        Assert.True(
            matchingEndpoints.Count > 0,
            $"No se encontro endpoint para {httpMethod} {route}. Endpoints disponibles: {string.Join(", ", allRouteEndpoints.Select(FormatEndpoint))}");

        var endpoint = Assert.Single(matchingEndpoints);

        var authorizeMetadata = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();

        if (!requiresAuthorization)
        {
            Assert.Empty(authorizeMetadata);
            return;
        }

        Assert.NotEmpty(authorizeMetadata);

        if (expectedPolicy is null)
        {
            Assert.DoesNotContain(authorizeMetadata, item => !string.IsNullOrWhiteSpace(item.Policy));
            return;
        }

        Assert.Contains(authorizeMetadata, item => string.Equals(item.Policy, expectedPolicy, StringComparison.Ordinal));
    }

    public static IEnumerable<object?[]> AuthorizationCases()
    {
        yield return ["POST", "/auth/register", false, null];
        yield return ["GET", "/auth/document-types", false, null];
        yield return ["POST", "/auth/login", false, null];
        yield return ["GET", "/auth/me/guest-profile", true, null];
        yield return ["PUT", "/auth/me/guest-profile", true, null];
        yield return ["POST", "/auth/change-password", true, null];
        yield return ["GET", "/auth/users", true, "AdminOnly"];
        yield return ["GET", "/auth/users/{id}/roles", true, "AdminOnly"];
        yield return ["PUT", "/auth/users/{id}/roles", true, "AdminOnly"];
    }

    private static async Task<IResult> InvokeEndpointAsync(string methodName, params object?[] arguments)
    {
        var method = typeof(AuthEndpoints).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontro el metodo {methodName}.");

        var invocationResult = method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"La invocacion de {methodName} no devolvio resultado.");

        var task = Assert.IsAssignableFrom<Task<IResult>>(invocationResult);
        return await task;
    }

    private static ClaimsPrincipal CreatePrincipalWithSub(string userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, userId)],
            authenticationType: "Tests");

        return new ClaimsPrincipal(identity);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        AuthHarness harness,
        string email,
        string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = email
        };

        var result = await harness.UserManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, FormatIdentityErrors(result.Errors));

        return user;
    }

    private static async Task AddRolesAsync(AuthHarness harness, ApplicationUser user, params string[] roles)
    {
        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await harness.RoleManager.RoleExistsAsync(role))
            {
                var createRoleResult = await harness.RoleManager.CreateAsync(new IdentityRole(role));
                Assert.True(createRoleResult.Succeeded, FormatIdentityErrors(createRoleResult.Errors));
            }
        }

        var addRolesResult = await harness.UserManager.AddToRolesAsync(user, roles);
        Assert.True(addRolesResult.Succeeded, FormatIdentityErrors(addRolesResult.Errors));
    }

    private static bool HasHttpMethod(RouteEndpoint endpoint, string method)
    {
        var metadata = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
        return metadata?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static string NormalizeRoute(string? route)
    {
        return "/" + (route ?? string.Empty).Trim('/');
    }

    private static bool RouteMatches(RouteEndpoint endpoint, string expectedRoute)
    {
        return string.Equals(
            CanonicalizeRoute(GetRouteTemplate(endpoint)),
            CanonicalizeRoute(expectedRoute),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalizeRoute(string route)
    {
        var segments = NormalizeRoute(route)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.StartsWith('{') && segment.EndsWith('}') ? "{}" : segment.ToLowerInvariant());

        return "/" + string.Join("/", segments);
    }

    private static string GetRouteTemplate(RouteEndpoint endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
        {
            return endpoint.RoutePattern.RawText!;
        }

        var segments = endpoint.RoutePattern.PathSegments
            .Select(pathSegment => string.Concat(pathSegment.Parts.Select(GetRoutePartText)));

        return "/" + string.Join("/", segments);
    }

    private static string GetRoutePartText(RoutePatternPart part)
    {
        return part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart parameter => $"{{{parameter.Name}}}",
            _ => string.Empty
        };
    }

    private static string FormatEndpoint(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        var methodsText = methods.Count == 0 ? "*" : string.Join("|", methods);
        return $"{methodsText} {NormalizeRoute(GetRouteTemplate(endpoint))}";
    }

    private static string FormatIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var messages = errors
            .Select(error => error.Description)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        return string.Join(" | ", messages);
    }

    private static async Task SeedDocumentTypesAsync(AppDbContext dbContext)
    {
        if (await dbContext.DocumentTypes.AnyAsync())
        {
            return;
        }

        dbContext.DocumentTypes.AddRange(
            new DocumentType { Id = 1, Name = "DNI" },
            new DocumentType { Id = 2, Name = "Pasaporte" });

        await dbContext.SaveChangesAsync();
    }

    private static AuthHarness CreateHarness()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var dbContext = new AppDbContext(dbContextOptions);
        dbContext.Database.EnsureCreated();

        var identityOptionsValue = new IdentityOptions
        {
            SignIn = { RequireConfirmedEmail = false }
        };
        var identityOptions = Options.Create(identityOptionsValue);

        var userStore = new UserStore<ApplicationUser, IdentityRole, AppDbContext, string>(dbContext);
        var roleStore = new RoleStore<IdentityRole, AppDbContext, string>(dbContext);

        var userManager = new UserManager<ApplicationUser>(
            userStore,
            identityOptions,
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            [new RoleValidator<IdentityRole>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var claimsFactory = new UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(
            userManager,
            roleManager,
            identityOptions);

        var services = new ServiceCollection().BuildServiceProvider();
        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services
            }
        };

        var schemes = new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions()));
        var signInManager = new SignInManager<ApplicationUser>(
            userManager,
            contextAccessor,
            claimsFactory,
            identityOptions,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            schemes,
            new DefaultUserConfirmation<ApplicationUser>());

        return new AuthHarness(
            dbContext,
            userManager,
            roleManager,
            signInManager,
            identityOptions,
            BuildTestConfiguration(),
            CreateAuthLogger(),
            new FakeJwtTokenService());
    }

    private static IConfiguration BuildTestConfiguration()
    {
        return new ConfigurationBuilder().Build();
    }

    private static object CreateAuthLogger()
    {
        var logContextType = typeof(AuthEndpoints).Assembly.GetType(
            "SmartHotel.API.Features.Auth.Endpoints.AuthEndpointsLogContext",
            throwOnError: true)!;

        var loggerType = typeof(Logger<>).MakeGenericType(logContextType);
        return Activator.CreateInstance(loggerType, NullLoggerFactory.Instance)
            ?? throw new InvalidOperationException("No se pudo crear logger para AuthEndpoints.");
    }

    private sealed class AuthHarness(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<IdentityOptions> identityOptions,
        IConfiguration configuration,
        object authLogger,
        IJwtTokenService jwtTokenService)
        : IAsyncDisposable
    {
        public AppDbContext DbContext { get; } = dbContext;
        public UserManager<ApplicationUser> UserManager { get; } = userManager;
        public RoleManager<IdentityRole> RoleManager { get; } = roleManager;
        public SignInManager<ApplicationUser> SignInManager { get; } = signInManager;
        public IOptions<IdentityOptions> IdentityOptions { get; } = identityOptions;
        public IConfiguration Configuration { get; } = configuration;
        public object AuthLogger { get; } = authLogger;
        public IJwtTokenService JwtTokenService { get; } = jwtTokenService;

        public async ValueTask DisposeAsync()
        {
            (SignInManager as IDisposable)?.Dispose();
            UserManager.Dispose();
            RoleManager.Dispose();
            await DbContext.DisposeAsync();
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public const string AccessTokenValue = "unit-test-token";

        public JwtTokenResult CreateToken(ApplicationUser user, IEnumerable<string> roles)
        {
            return new JwtTokenResult(AccessTokenValue, DateTime.UtcNow.AddHours(1));
        }
    }
}
