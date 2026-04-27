using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Availability.Handler;
using SmartHotel.API.Features.Availability.Query;
using SmartHotel.API.Features.Availability.Validator;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.UnitTests.Features.Availability;

public sealed class GetAvailabilityQueryHandlerTests
{
    [Theory]
    [MemberData(nameof(HandleCases))]
    public async Task HandleAsync_ShouldHandleSuccessAndErrorCases(
        string _,
        GetAvailabilityQuery query,
        bool shouldThrow,
        int expectedRoomsCount,
        string? expectedErrorFragment)
    {
        await using var dbContext = CreateContext();
        await SeedRoomsAndReservationsAsync(dbContext, query);

        var handler = CreateHandler(dbContext);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(
                () => handler.HandleAsync(query, CancellationToken.None));

            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Equal(expectedRoomsCount, result.Rooms.Count);
        Assert.Equal(query.CheckOut.DayNumber - query.CheckIn.DayNumber, result.Nights);
        Assert.Null(result.Message);

        var room = Assert.Single(result.Rooms);
        Assert.Equal("102", room.RoomNumber);
        Assert.Equal("Refrigerador | TV por cable | 1 cama queen", room.Features);
        Assert.Equal(100m, room.PricePerNight);
        Assert.Equal(100m * result.Nights, room.EstimatedTotalPrice);
    }

    public static IEnumerable<object[]> HandleCases()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var checkIn = today.AddDays(5);
        var checkOut = today.AddDays(7);

        yield return
        [
            "Caso exitoso",
            new GetAvailabilityQuery(checkIn, checkOut, 2, null),
            false,
            1,
            null
        ];

        yield return
        [
            "Error por guests en cero",
            new GetAvailabilityQuery(checkIn, checkOut, 0, null),
            true,
            0,
            "guests"
        ];
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static GetAvailabilityQueryHandler CreateHandler(AppDbContext dbContext)
    {
        var pricingService = new ReservationPricingService(dbContext);
        var reservationLifecycleService = new ReservationLifecycleService(
            dbContext,
            NullLogger<ReservationLifecycleService>.Instance);
        var validator = new AvailabilityQueryValidator();

        return new GetAvailabilityQueryHandler(
            dbContext,
            pricingService,
            reservationLifecycleService,
            validator);
    }

    private static async Task SeedRoomsAndReservationsAsync(AppDbContext dbContext, GetAvailabilityQuery query)
    {
        var roomType = new RoomType
        {
            Id = 1,
            Name = "Standard",
            BasePrice = 100m
        };

        var room101 = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas individuales",
            RoomTypeId = roomType.Id,
            RoomType = roomType
        };

        var room102 = new Room
        {
            Id = 2,
            Number = "102",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 1 cama queen",
            RoomTypeId = roomType.Id,
            RoomType = roomType
        };

        var checkInDateTime = query.CheckIn.ToDateTime(TimeOnly.MinValue);
        var checkOutDateTime = query.CheckOut.ToDateTime(TimeOnly.MinValue);

        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.AddRange(room101, room102);

        dbContext.Reservations.Add(new Reservation
        {
            Id = 1,
            GuestId = 1,
            Guest = null!,
            RoomId = room101.Id,
            Room = room101,
            CheckInDate = checkInDateTime,
            CheckOutDate = checkOutDateTime,
            TotalPrice = 200m,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }
}
