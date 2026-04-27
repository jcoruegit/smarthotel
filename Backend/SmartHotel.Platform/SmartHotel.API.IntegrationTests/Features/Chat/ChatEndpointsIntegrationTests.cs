using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.API.Features.Chat.Dto;
using SmartHotel.API.IntegrationTests.Infrastructure;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.IntegrationTests.Features.Chat;

public sealed class ChatEndpointsIntegrationTests
{
    [Fact]
    public async Task SendMessage_ShouldReturnAmenitiesIntent_WhenAskingForServices()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            dbContext.HotelAmenities.Add(new HotelAmenity
            {
                Id = 1,
                Name = "Gimnasio",
                Description = "Area fitness",
                AvailableFrom = new TimeOnly(6, 0),
                AvailableTo = new TimeOnly(22, 0),
                IsComplimentary = true,
                DaysOfWeek = "Mon,Tue,Wed,Thu,Fri,Sat,Sun",
                IsActive = true,
                DisplayOrder = 1
            });

            return Task.CompletedTask;
        });

        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequestDto("Hola, que servicios tiene el hotel?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ChatMessageResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("es", payload.DetectedLanguage);
        Assert.Equal("consultar_servicios", payload.DetectedIntent);
        Assert.Contains("Gimnasio", payload.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnAvailabilityIntent_WhenProvidingDatesAndGuests()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 120m };
            dbContext.RoomTypes.Add(roomType);
            dbContext.Rooms.Add(new Room
            {
                Id = 1,
                Number = "101",
                Capacity = 2,
                Features = "TV",
                RoomTypeId = roomType.Id,
                RoomType = roomType
            });

            return Task.CompletedTask;
        });

        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/chat/message",
            new ChatMessageRequestDto("Hay disponibilidad del 2026-06-10 al 2026-06-12 para 2 huespedes?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ChatMessageResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("consultar_disponibilidad", payload.DetectedIntent);
        Assert.Contains("disponibilidad", payload.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("101", payload.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnMixedIntent_WhenAskingAvailabilityAndAmenities()
    {
        using var factory = new ApiWebApplicationFactory();
        await SeedDatabaseAsync(factory, dbContext =>
        {
            var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 120m };
            dbContext.RoomTypes.Add(roomType);
            dbContext.Rooms.Add(new Room
            {
                Id = 1,
                Number = "101",
                Capacity = 2,
                Features = "TV",
                RoomTypeId = roomType.Id,
                RoomType = roomType
            });

            dbContext.HotelAmenities.Add(new HotelAmenity
            {
                Id = 1,
                Name = "Sauna",
                Description = "Sauna seco",
                AvailableFrom = new TimeOnly(10, 0),
                AvailableTo = new TimeOnly(20, 0),
                IsComplimentary = false,
                Price = 15m,
                Currency = "USD",
                DaysOfWeek = "Mon,Tue,Wed,Thu,Fri,Sat,Sun",
                IsActive = true,
                DisplayOrder = 1
            });

            return Task.CompletedTask;
        });

        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/chat/message",
            new ChatMessageRequestDto("Hay disponibilidad del 2026-06-10 al 2026-06-12 y tambien sauna?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ChatMessageResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("consulta_mixta", payload.DetectedIntent);
        Assert.Contains("Disponibilidad", payload.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Servicios", payload.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnBadRequest_WhenMessageIsEmpty()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequestDto(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("message", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(ApiWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
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
