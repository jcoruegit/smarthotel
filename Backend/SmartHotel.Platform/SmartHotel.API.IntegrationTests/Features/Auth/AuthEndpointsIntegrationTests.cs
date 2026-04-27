using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.API.Features.Auth.Dto;
using SmartHotel.API.IntegrationTests.Infrastructure;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.IntegrationTests.Features.Auth;

public sealed class AuthEndpointsIntegrationTests
{
    [Fact]
    public async Task Register_ShouldReturnCreated_AndAssignGuestRole()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var client = CreateAnonymousClient(factory);
        var request = CreateRegisterRequest("guest.register@hotel.com", "Guest123!", "Guest", "Register", "10000001");

        var response = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("guest.register@hotel.com", payload.Email);
        Assert.Contains("Guest", payload.Roles);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persistedGuest = dbContext.Guests.SingleOrDefault(guest => guest.UserId == payload.UserId);
        Assert.NotNull(persistedGuest);
        Assert.Equal("Guest", persistedGuest.FirstName);
        Assert.Equal("Register", persistedGuest.LastName);
        Assert.Equal("10000001", persistedGuest.DocumentNumber);
        Assert.Equal("guest.register@hotel.com", persistedGuest.Email);

        var persistedUser = await dbContext.Users.FindAsync(payload.UserId);
        Assert.NotNull(persistedUser);
        Assert.Equal("Guest Register", persistedUser.FullName);
    }

    [Fact]
    public async Task Register_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var client = CreateAnonymousClient(factory);
        var request = CreateRegisterRequest("guest.duplicate@hotel.com", "Guest123!", "Guest", "Duplicate", "10000002");

        var firstResponse = await client.PostAsJsonAsync("/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenDocumentNumberIsInvalid()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var client = CreateAnonymousClient(factory);
        var request = CreateRegisterRequest("guest.invalid.doc@hotel.com", "Guest123!", "Guest", "Invalid Doc", "12345A");

        var response = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("al menos 7 digitos", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldReturnOk_WhenCredentialsAreValid()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var client = CreateAnonymousClient(factory);
        var email = "guest.login@hotel.com";
        var password = "Guest123!";

        await client.PostAsJsonAsync("/auth/register", CreateRegisterRequest(email, password, "Guest", "Login", "10000003"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Contains("Guest", payload.Roles);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenPasswordIsInvalid()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var client = CreateAnonymousClient(factory);
        var email = "guest.invalid.password@hotel.com";

        await client.PostAsJsonAsync("/auth/register", CreateRegisterRequest(email, "Guest123!", "Guest", "Invalid Password", "10000004"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, "Wrong123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task GetCurrentGuestProfile_ShouldReturnGuestData_ForAuthenticatedUser()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        const string email = "guest.profile@hotel.com";
        const string password = "Guest123!";

        await anonymousClient.PostAsJsonAsync(
            "/auth/register",
            CreateRegisterRequest(email, password, "Guest", "Profile", "10000011"));

        var loginResponse = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, password));
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginPayload);

        using var authenticatedClient = CreateAnonymousClient(factory);
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginPayload.AccessToken);

        var profileResponse = await authenticatedClient.GetAsync("/auth/me/guest-profile");

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profilePayload = await profileResponse.Content.ReadFromJsonAsync<GuestProfileDto>();
        Assert.NotNull(profilePayload);
        Assert.Equal(1, profilePayload.DocumentTypeId);
        Assert.Equal("DNI", profilePayload.DocumentTypeName);
        Assert.Equal("Guest", profilePayload.FirstName);
        Assert.Equal("Profile", profilePayload.LastName);
        Assert.Equal("10000011", profilePayload.DocumentNumber);
        Assert.Equal(new DateOnly(1990, 1, 1), profilePayload.BirthDate);
        Assert.Equal(email, profilePayload.Email);
    }

    [Fact]
    public async Task UpdateCurrentGuestProfile_ShouldReturnOk_AndPersistChangesInUserAndGuest()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        const string email = "guest.update.profile@hotel.com";
        const string password = "Guest123!";

        await anonymousClient.PostAsJsonAsync(
            "/auth/register",
            CreateRegisterRequest(email, password, "Guest", "Before", "10000012"));

        var loginResponse = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, password));
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginPayload);

        using var authenticatedClient = CreateAnonymousClient(factory);
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginPayload.AccessToken);

        var updateResponse = await authenticatedClient.PutAsJsonAsync(
            "/auth/me/guest-profile",
            new UpdateGuestProfileRequestDto(
                2,
                "Guest",
                "After",
                "10000013",
                new DateOnly(1992, 5, 20),
                "guest.update.profile.new@hotel.com",
                "123456"));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatePayload = await updateResponse.Content.ReadFromJsonAsync<GuestProfileDto>();
        Assert.NotNull(updatePayload);
        Assert.Equal(2, updatePayload.DocumentTypeId);
        Assert.Equal("Pasaporte", updatePayload.DocumentTypeName);
        Assert.Equal("Guest", updatePayload.FirstName);
        Assert.Equal("After", updatePayload.LastName);
        Assert.Equal("10000013", updatePayload.DocumentNumber);
        Assert.Equal("guest.update.profile.new@hotel.com", updatePayload.Email);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persistedGuest = dbContext.Guests.Single(guest => guest.UserId == loginPayload.UserId);
        Assert.Equal(2, persistedGuest.DocumentTypeId);
        Assert.Equal("10000013", persistedGuest.DocumentNumber);
        Assert.Equal("guest.update.profile.new@hotel.com", persistedGuest.Email);
        Assert.Equal("123456", persistedGuest.Phone);

        var persistedUser = await dbContext.Users.FindAsync(loginPayload.UserId);
        Assert.NotNull(persistedUser);
        Assert.Equal("guest.update.profile.new@hotel.com", persistedUser.Email);
        Assert.Equal("guest.update.profile.new@hotel.com", persistedUser.UserName);
        Assert.Equal("Guest After", persistedUser.FullName);
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnNoContent_AndAllowLoginWithNewPassword()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        var email = "guest.change.password@hotel.com";
        const string oldPassword = "OldPass123!";
        const string newPassword = "NewPass123!";

        await anonymousClient.PostAsJsonAsync("/auth/register", CreateRegisterRequest(email, oldPassword, "Guest", "Change Password", "10000005"));
        var loginResponse = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, oldPassword));
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginPayload);

        using var authenticatedClient = CreateAnonymousClient(factory);
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginPayload.AccessToken);

        var changePasswordResponse = await authenticatedClient.PostAsJsonAsync(
            "/auth/change-password",
            new ChangePasswordRequestDto(oldPassword, newPassword));

        Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);

        var reloginResponse = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, newPassword));
        Assert.Equal(HttpStatusCode.OK, reloginResponse.StatusCode);
    }

    [Fact]
    public async Task ListUsers_ShouldReturnForbidden_ForGuestToken()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        var email = "guest.list.users@hotel.com";
        var password = "Guest123!";

        await anonymousClient.PostAsJsonAsync("/auth/register", CreateRegisterRequest(email, password, "Guest", "List Users", "10000006"));
        var loginResponse = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequestDto(email, password));
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginPayload);

        using var guestClient = CreateAnonymousClient(factory);
        guestClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginPayload.AccessToken);

        var response = await guestClient.GetAsync("/auth/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_ShouldReturnOnlyEmployees_ForAdminToken()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        var employeeRegisterResponse = await anonymousClient.PostAsJsonAsync(
            "/auth/register",
            CreateRegisterRequest("employee.visible@hotel.com", "Guest123!", "Employee", "Visible", "10000007"));
        var employeeRegisterPayload = await employeeRegisterResponse.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(employeeRegisterPayload);

        await anonymousClient.PostAsJsonAsync(
            "/auth/register",
            CreateRegisterRequest("guest.only@hotel.com", "Guest123!", "Guest", "Only", "10000008"));

        using var adminClient = TestJwtClientFactory.CreateAuthenticatedClient(factory, "admin-user", "Admin");

        var promoteResponse = await adminClient.PutAsJsonAsync(
            $"/auth/users/{employeeRegisterPayload.UserId}/roles",
            new UpdateUserRolesRequestDto(["Staff"]));
        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);

        var response = await adminClient.GetAsync("/auth/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<AuthUserRolesDto>>();
        Assert.NotNull(payload);
        Assert.Contains(payload, user => string.Equals(user.Email, "employee.visible@hotel.com", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(payload, user => string.Equals(user.Email, "guest.only@hotel.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateAndGetUserRoles_ShouldWork_ForAdminToken()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        var registerResponse = await anonymousClient.PostAsJsonAsync(
            "/auth/register",
            CreateRegisterRequest("guest.roles@hotel.com", "Guest123!", "Guest", "Roles", "10000009"));

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(registerPayload);

        using var adminClient = TestJwtClientFactory.CreateAuthenticatedClient(factory, "admin-user", "Admin");

        var updateResponse = await adminClient.PutAsJsonAsync(
            $"/auth/users/{registerPayload.UserId}/roles",
            new UpdateUserRolesRequestDto(["Guest", "Staff"]));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedPayload = await updateResponse.Content.ReadFromJsonAsync<AuthUserRolesDto>();
        Assert.NotNull(updatedPayload);
        Assert.Contains("Staff", updatedPayload.Roles);

        var getResponse = await adminClient.GetAsync($"/auth/users/{registerPayload.UserId}/roles");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getPayload = await getResponse.Content.ReadFromJsonAsync<AuthUserRolesDto>();
        Assert.NotNull(getPayload);
        Assert.Contains("Staff", getPayload.Roles);
    }

    [Fact]
    public async Task UpdateUserRoles_ShouldReturnBadRequest_WhenRoleIsInvalid()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory);

        using var anonymousClient = CreateAnonymousClient(factory);
        var registerResponse = await anonymousClient.PostAsJsonAsync(
            "/auth/register",
            CreateRegisterRequest("guest.invalid.role@hotel.com", "Guest123!", "Guest", "Invalid Role", "10000010"));
        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(registerPayload);

        using var adminClient = TestJwtClientFactory.CreateAuthenticatedClient(factory, "admin-user", "Admin");
        var response = await adminClient.PutAsJsonAsync(
            $"/auth/users/{registerPayload.UserId}/roles",
            new UpdateUserRolesRequestDto(["Root"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Roles no permitidos", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateAnonymousClient(ApiWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task ResetDatabaseAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.DocumentTypes.AddRange(
            new DocumentType { Id = 1, Name = "DNI" },
            new DocumentType { Id = 2, Name = "Pasaporte" });

        await dbContext.SaveChangesAsync();
    }

    private static RegisterRequestDto CreateRegisterRequest(
        string email,
        string password,
        string firstName,
        string lastName,
        string documentNumber)
    {
        return new RegisterRequestDto(
            firstName,
            lastName,
            1,
            documentNumber,
            new DateOnly(1990, 1, 1),
            email,
            password);
    }
}
