using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.API.Features.HotelInfo.Dto;
using SmartHotel.API.IntegrationTests.Infrastructure;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.IntegrationTests.Features.HotelInfo;

public sealed class HotelInfoEndpointsIntegrationTests
{
    [Fact]
    public async Task GetAmenities_ShouldReturnActiveAmenitiesOrdered_ForAnonymousUser()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            dbContext.HotelAmenities.AddRange(
                new HotelAmenity
                {
                    Id = 1,
                    Name = "Sauna",
                    Description = "Sauna seco",
                    AvailableFrom = new TimeOnly(10, 0),
                    AvailableTo = new TimeOnly(20, 0),
                    DaysOfWeek = "Mon,Tue,Wed,Thu,Fri,Sat,Sun",
                    IsComplimentary = false,
                    Price = 15m,
                    Currency = "USD",
                    RequiresReservation = false,
                    IsActive = true,
                    DisplayOrder = 2
                },
                new HotelAmenity
                {
                    Id = 2,
                    Name = "Gimnasio",
                    Description = "Area fitness",
                    AvailableFrom = new TimeOnly(6, 0),
                    AvailableTo = new TimeOnly(22, 0),
                    DaysOfWeek = "Mon,Tue,Wed,Thu,Fri,Sat,Sun",
                    IsComplimentary = true,
                    IsActive = true,
                    DisplayOrder = 1
                },
                new HotelAmenity
                {
                    Id = 3,
                    Name = "Servicio inactivo",
                    Description = "No deberia mostrarse",
                    IsActive = false,
                    DisplayOrder = 3
                });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/hotel-info/amenities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<HotelAmenityDto>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal("Gimnasio", payload[0].Name);
        Assert.Equal("Sauna", payload[1].Name);
    }

    [Fact]
    public async Task GetPolicies_ShouldReturnActivePoliciesOrdered_ForAnonymousUser()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            dbContext.HotelPolicies.AddRange(
                new HotelPolicy
                {
                    Id = 1,
                    Code = "CHECKOUT_POLICY",
                    Title = "Check-out",
                    Description = "Hasta las 11:00",
                    Category = "CheckInOut",
                    IsActive = true,
                    DisplayOrder = 2
                },
                new HotelPolicy
                {
                    Id = 2,
                    Code = "CHECKIN_POLICY",
                    Title = "Check-in",
                    Description = "Desde las 15:00",
                    Category = "CheckInOut",
                    IsActive = true,
                    DisplayOrder = 1
                },
                new HotelPolicy
                {
                    Id = 3,
                    Code = "INACTIVE_POLICY",
                    Title = "Inactiva",
                    Description = "No deberia mostrarse",
                    Category = "General",
                    IsActive = false,
                    DisplayOrder = 3
                });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/hotel-info/policies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<HotelPolicyDto>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal("CHECKIN_POLICY", payload[0].Code);
        Assert.Equal("CHECKOUT_POLICY", payload[1].Code);
    }

    [Fact]
    public async Task GetSchedules_ShouldReturnActiveSchedulesOrdered_ForAnonymousUser()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            dbContext.HotelSchedules.AddRange(
                new HotelSchedule
                {
                    Id = 1,
                    Code = "BREAKFAST",
                    Title = "Desayuno",
                    StartTime = new TimeOnly(7, 0),
                    EndTime = new TimeOnly(10, 30),
                    IsActive = true,
                    DisplayOrder = 2
                },
                new HotelSchedule
                {
                    Id = 2,
                    Code = "CHECKIN",
                    Title = "Check-in",
                    StartTime = new TimeOnly(15, 0),
                    IsActive = true,
                    DisplayOrder = 1
                },
                new HotelSchedule
                {
                    Id = 3,
                    Code = "INACTIVE_SCHEDULE",
                    Title = "Inactivo",
                    IsActive = false,
                    DisplayOrder = 3
                });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/hotel-info/schedules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<HotelScheduleDto>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal("CHECKIN", payload[0].Code);
        Assert.Equal("BREAKFAST", payload[1].Code);
        Assert.Equal("15:00", payload[0].StartTime);
        Assert.Equal("10:30", payload[1].EndTime);
    }

    private static async Task SeedDatabaseAsync(
        ApiWebApplicationFactory factory,
        Func<AppDbContext, Task> seedAction)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        await seedAction(dbContext);
        await dbContext.SaveChangesAsync();
    }
}
