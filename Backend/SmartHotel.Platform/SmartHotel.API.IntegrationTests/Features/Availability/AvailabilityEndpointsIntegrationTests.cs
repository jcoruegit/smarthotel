using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.API.Features.Availability.Dto;
using SmartHotel.API.IntegrationTests.Infrastructure;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.IntegrationTests.Features.Availability;

public sealed class AvailabilityEndpointsIntegrationTests
{
    [Fact]
    public async Task GetAvailability_ShouldReturnAvailableRooms_ForAnonymousUser()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
            var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 220m };

            var roomStandard = new Room
            {
                Id = 1,
                Number = "101",
                Capacity = 2,
                Features = "Refrigerador | TV por cable | 2 camas",
                RoomTypeId = standard.Id,
                RoomType = standard
            };

            var roomSuite = new Room
            {
                Id = 2,
                Number = "201",
                Capacity = 4,
                Features = "Refrigerador | TV por cable | 1 cama king",
                RoomTypeId = suite.Id,
                RoomType = suite
            };

            var documentType = new DocumentType { Id = 1, Name = "DNI" };
            var guest = new Guest
            {
                Id = 1,
                DocumentTypeId = documentType.Id,
                DocumentType = documentType,
                FirstName = "Ana",
                LastName = "Gomez",
                DocumentNumber = "12345678",
                BirthDate = new DateTime(1990, 1, 1)
            };

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            dbContext.RoomTypes.AddRange(standard, suite);
            dbContext.Rooms.AddRange(roomStandard, roomSuite);
            dbContext.DocumentTypes.Add(documentType);
            dbContext.Guests.Add(guest);
            dbContext.Reservations.Add(new Reservation
            {
                Id = 1,
                GuestId = guest.Id,
                Guest = guest,
                RoomId = roomStandard.Id,
                Room = roomStandard,
                CheckInDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
                CheckOutDate = today.AddDays(7).ToDateTime(TimeOnly.MinValue),
                TotalPrice = 200m,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var todayQuery = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.GetAsync(
            $"/api/availability?checkIn={todayQuery.AddDays(5):yyyy-MM-dd}&checkOut={todayQuery.AddDays(7):yyyy-MM-dd}&guests=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AvailabilityResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload.Rooms);
        Assert.Equal("Suite", payload.Rooms[0].RoomTypeName);
    }

    [Fact]
    public async Task GetAvailability_ShouldReturnBadRequest_WhenCheckInHasInvalidFormat()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/availability?checkIn=10-05-2026&checkOut=2026-05-12&guests=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("checkIn", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAvailabilityRoomTypes_ShouldReturnCatalog_ForAnonymousUser()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            dbContext.RoomTypes.AddRange(
                new RoomType { Id = 1, Name = "Suite", BasePrice = 220m },
                new RoomType { Id = 2, Name = "Standard", BasePrice = 100m });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/availability/room-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<RoomTypeOptionDto>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal("Standard", payload[0].Name);
        Assert.Equal("Suite", payload[1].Name);
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
