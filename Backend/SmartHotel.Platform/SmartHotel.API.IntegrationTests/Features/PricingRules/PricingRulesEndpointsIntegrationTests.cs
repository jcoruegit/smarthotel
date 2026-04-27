using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.API.Features.PricingRules.Dto;
using SmartHotel.API.IntegrationTests.Infrastructure;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.IntegrationTests.Features.PricingRules;

public sealed class PricingRulesEndpointsIntegrationTests
{
    [Fact]
    public async Task ListPricingRules_ShouldReturnOk_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPricingRulesBaseAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var response = await client.GetAsync("/api/pricing-rules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<PricingRuleResponseDto>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
    }

    [Fact]
    public async Task ListPricingRules_ShouldReturnForbidden_ForGuest()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPricingRulesBaseAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "guest-user", "Guest");
        var response = await client.GetAsync("/api/pricing-rules");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePricingRule_ShouldReturnCreated_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedRoomTypesOnlyAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var request = new PricingRuleRequestDto(1, "2026-06-10", 180m, "Alta demanda");

        var response = await client.PostAsJsonAsync("/api/pricing-rules", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PricingRuleResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.RoomTypeId);
        Assert.Equal("2026-06-10", payload.Date);
    }

    [Fact]
    public async Task CreatePricingRule_ShouldReturnConflict_WhenDuplicated()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPricingRulesBaseAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var request = new PricingRuleRequestDto(1, "2026-06-10", 220m, "Intento duplicado");

        var response = await client.PostAsJsonAsync("/api/pricing-rules", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Ya existe una regla de precio", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPricingRuleById_ShouldReturnOk_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPricingRulesBaseAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var response = await client.GetAsync("/api/pricing-rules/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PricingRuleResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.Id);
    }

    [Fact]
    public async Task GetPricingRuleById_ShouldReturnNotFound_WhenRuleDoesNotExist()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedRoomTypesOnlyAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var response = await client.GetAsync("/api/pricing-rules/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePricingRule_ShouldReturnOk_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPricingRulesBaseAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var request = new PricingRuleRequestDto(2, "2026-06-12", 330m, "Temporada alta");

        var response = await client.PutAsJsonAsync("/api/pricing-rules/2", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PricingRuleResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.RoomTypeId);
        Assert.Equal("Temporada alta", payload.Reason);
    }

    [Fact]
    public async Task DeletePricingRule_ShouldReturnNoContent_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPricingRulesBaseAsync);

        using var client = TestJwtClientFactory.CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var response = await client.DeleteAsync("/api/pricing-rules/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task ResetDatabaseAsync(ApiWebApplicationFactory factory, Func<AppDbContext, Task> seedAction)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        await seedAction(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRoomTypesOnlyAsync(AppDbContext dbContext)
    {
        dbContext.RoomTypes.AddRange(
            new RoomType { Id = 1, Name = "Standard", BasePrice = 100m },
            new RoomType { Id = 2, Name = "Suite", BasePrice = 220m });

        await Task.CompletedTask;
    }

    private static async Task SeedPricingRulesBaseAsync(AppDbContext dbContext)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 220m };

        dbContext.RoomTypes.AddRange(standard, suite);
        dbContext.PricingRules.AddRange(
            new PricingRule
            {
                Id = 1,
                RoomTypeId = standard.Id,
                RoomType = standard,
                Date = new DateTime(2026, 6, 10),
                Price = 180m,
                Reason = "Alta demanda"
            },
            new PricingRule
            {
                Id = 2,
                RoomTypeId = suite.Id,
                RoomType = suite,
                Date = new DateTime(2026, 6, 12),
                Price = 300m,
                Reason = "Feriado"
            });

        await Task.CompletedTask;
    }
}
