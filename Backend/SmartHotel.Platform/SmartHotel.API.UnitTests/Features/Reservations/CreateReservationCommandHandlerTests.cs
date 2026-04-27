using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Command;
using SmartHotel.API.Features.Reservations.Handler;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.API.Features.Reservations.Validator;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.UnitTests.Features.Reservations;

public sealed class CreateReservationCommandHandlerTests
{
    [Theory]
    [MemberData(nameof(HandleCases))]
    public async Task HandleAsync_ShouldHandleReservationSuccessAndErrorCases(
        string scenario,
        CreateReservationCommand command,
        bool shouldSucceed,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedScenarioAsync(dbContext, scenario, command);

        var handler = CreateHandler(dbContext);
        var reservationsBefore = await dbContext.Reservations.CountAsync();

        if (!shouldSucceed)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(
                () => handler.HandleAsync(command, CancellationToken.None));

            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);

            var reservationsAfterError = await dbContext.Reservations.CountAsync();
            Assert.Equal(reservationsBefore, reservationsAfterError);
            return;
        }

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.ReservationId > 0);
        Assert.Equal(ReservationStatus.Pending.ToString(), result.Status);
        Assert.Equal(command.CheckOut.DayNumber - command.CheckIn.DayNumber, result.Nights);
        Assert.Equal(command.Guests, result.Guests);
        Assert.Equal(100m, result.PricePerNight);
        Assert.Equal(result.PricePerNight * result.Nights, result.TotalPrice);

        var reservationsAfterSuccess = await dbContext.Reservations.CountAsync();
        Assert.Equal(reservationsBefore + 1, reservationsAfterSuccess);
    }

    public static IEnumerable<object[]> HandleCases()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var checkIn = today.AddDays(7);
        var checkOut = today.AddDays(10);

        yield return
        [
            "success",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI", documentNumber: "12345678"),
            true,
            null,
            null
        ];

        yield return
        [
            "success_with_7_digits_document",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI", documentNumber: "1234567"),
            true,
            null,
            null
        ];

        yield return
        [
            "invalid_guests",
            CreateCommand(checkIn, checkOut, guests: 0, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI"),
            false,
            "guests",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "no_available_rooms",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI"),
            false,
            "No encontramos habitaciones disponibles",
            StatusCodes.Status409Conflict
        ];

        yield return
        [
            "document_type_not_found",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "PASAPORTE"),
            false,
            "tipo de documento indicado no existe",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "guest_without_user_id",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: true, documentTypeName: "DNI"),
            false,
            "Token invalido",
            StatusCodes.Status401Unauthorized
        ];

        yield return
        [
            "selected_room_not_available",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: 1, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI"),
            false,
            "habitacion seleccionada no esta disponible",
            StatusCodes.Status409Conflict
        ];

        yield return
        [
            "invalid_document_number_too_short",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI", documentNumber: "123456"),
            false,
            "al menos 7 digitos",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "invalid_document_number_too_long",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI", documentNumber: "123456789"),
            false,
            "maximo 8",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "invalid_document_number_non_numeric",
            CreateCommand(checkIn, checkOut, guests: 2, roomId: null, roomTypeId: null, requesterUserId: null, requesterIsGuest: false, documentTypeName: "DNI", documentNumber: "1234A67"),
            false,
            "usando solo numeros",
            StatusCodes.Status400BadRequest
        ];
    }

    private static CreateReservationCommand CreateCommand(
        DateOnly checkIn,
        DateOnly checkOut,
        int guests,
        int? roomId,
        int? roomTypeId,
        string? requesterUserId,
        bool requesterIsGuest,
        string documentTypeName,
        string documentNumber = "12345678")
    {
        return new CreateReservationCommand(
            PassengerDocumentTypeName: documentTypeName,
            PassengerFirstName: "Juan",
            PassengerLastName: "Perez",
            PassengerDocumentNumber: documentNumber,
            PassengerBirthDate: new DateOnly(1990, 1, 1),
            PassengerEmail: "juan.perez@example.com",
            PassengerPhone: "123456",
            CheckIn: checkIn,
            CheckOut: checkOut,
            Guests: guests,
            RoomId: roomId,
            RoomTypeId: roomTypeId,
            RequesterUserId: requesterUserId,
            RequesterIsGuest: requesterIsGuest);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static CreateReservationCommandHandler CreateHandler(AppDbContext dbContext)
    {
        var pricingService = new ReservationPricingService(dbContext);
        var reservationLifecycleService = new ReservationLifecycleService(
            dbContext,
            NullLogger<ReservationLifecycleService>.Instance);
        var validator = new ReservationCommandValidator();

        return new CreateReservationCommandHandler(
            dbContext,
            pricingService,
            reservationLifecycleService,
            validator,
            NullLogger<CreateReservationCommandHandler>.Instance);
    }

    private static async Task SeedScenarioAsync(AppDbContext dbContext, string scenario, CreateReservationCommand command)
    {
        var dni = new DocumentType { Id = 1, Name = "DNI" };
        var passport = new DocumentType { Id = 2, Name = "PASAPORTE" };

        dbContext.DocumentTypes.Add(dni);
        if (!string.Equals(scenario, "document_type_not_found", StringComparison.Ordinal))
        {
            dbContext.DocumentTypes.Add(passport);
        }

        var roomType = new RoomType
        {
            Id = 1,
            Name = "Standard",
            BasePrice = 100m
        };

        dbContext.RoomTypes.Add(roomType);

        var room101 = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = roomType.Id,
            RoomType = roomType
        };

        dbContext.Rooms.Add(room101);

        if (string.Equals(scenario, "success", StringComparison.Ordinal)
            || string.Equals(scenario, "success_with_7_digits_document", StringComparison.Ordinal))
        {
            dbContext.Rooms.Add(new Room
            {
                Id = 2,
                Number = "102",
                Capacity = 2,
                Features = "Refrigerador | TV por cable | 1 cama queen",
                RoomTypeId = roomType.Id,
                RoomType = roomType
            });
        }

        if (string.Equals(scenario, "success", StringComparison.Ordinal)
            || string.Equals(scenario, "no_available_rooms", StringComparison.Ordinal)
            || string.Equals(scenario, "selected_room_not_available", StringComparison.Ordinal))
        {
            var checkInDateTime = command.CheckIn.ToDateTime(TimeOnly.MinValue);
            var checkOutDateTime = command.CheckOut.ToDateTime(TimeOnly.MinValue);

            var guest = new Guest
            {
                Id = 1,
                DocumentTypeId = dni.Id,
                DocumentType = dni,
                FirstName = "Guest",
                LastName = "Reservado",
                DocumentNumber = "87654321",
                BirthDate = new DateTime(1991, 1, 1),
                Email = "guest@example.com",
                Phone = "987654"
            };

            dbContext.Guests.Add(guest);

            dbContext.Reservations.Add(new Reservation
            {
                Id = 1,
                GuestId = guest.Id,
                Guest = guest,
                RoomId = room101.Id,
                Room = room101,
                CheckInDate = checkInDateTime,
                CheckOutDate = checkOutDateTime,
                TotalPrice = 300m,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
