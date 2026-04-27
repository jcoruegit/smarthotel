using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Command;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.API.Features.Reservations.Validator;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Reservations.Handler;

public sealed class CreateReservationCommandHandler(
    AppDbContext dbContext,
    ReservationPricingService pricingService,
    ReservationLifecycleService reservationLifecycleService,
    ReservationCommandValidator validator,
    ILogger<CreateReservationCommandHandler> logger)
{
    public async Task<CreateReservationResponseDto> HandleAsync(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        validator.Validate(command);
        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        var normalizedDocumentTypeName = NormalizeDocumentTypeName(command.PassengerDocumentTypeName);
        var normalizedDocumentNumber = NormalizeDocumentNumber(command.PassengerDocumentNumber);
        var normalizedFirstName = NormalizeRequiredValue(command.PassengerFirstName);
        var normalizedLastName = NormalizeRequiredValue(command.PassengerLastName);
        var normalizedEmail = NormalizeOptionalValue(command.PassengerEmail);
        var normalizedPhone = NormalizeOptionalValue(command.PassengerPhone);
        var requesterUserId = NormalizeOptionalValue(command.RequesterUserId);

        if (command.RequesterIsGuest && string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var documentType = await dbContext.DocumentTypes
            .SingleOrDefaultAsync(
                entity => entity.Name.ToUpper() == normalizedDocumentTypeName,
                cancellationToken);

        if (documentType is null)
        {
            throw new UserFriendlyException("El tipo de documento indicado no existe.", StatusCodes.Status400BadRequest);
        }

        var guest = await dbContext.Guests
            .Include(entity => entity.DocumentType)
            .SingleOrDefaultAsync(
                entity => entity.DocumentTypeId == documentType.Id && entity.DocumentNumber == normalizedDocumentNumber,
                cancellationToken);

        if (guest is null)
        {
            guest = new Guest
            {
                UserId = command.RequesterIsGuest ? requesterUserId : null,
                DocumentTypeId = documentType.Id,
                FirstName = normalizedFirstName,
                LastName = normalizedLastName,
                DocumentNumber = normalizedDocumentNumber,
                BirthDate = command.PassengerBirthDate.ToDateTime(TimeOnly.MinValue),
                Email = normalizedEmail,
                Phone = normalizedPhone
            };

            dbContext.Guests.Add(guest);
        }
        else
        {
            if (command.RequesterIsGuest)
            {
                if (!string.IsNullOrWhiteSpace(guest.UserId) && !string.Equals(guest.UserId, requesterUserId, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "Intento de crear reserva con guest ajeno. GuestId={GuestId}, GuestUserId={GuestUserId}, RequesterUserId={RequesterUserId}",
                        guest.Id,
                        guest.UserId,
                        requesterUserId);
                    throw new UserFriendlyException(
                        "No tenes permisos para crear o modificar reservas con los datos de otro usuario.",
                        StatusCodes.Status403Forbidden);
                }

                guest.UserId ??= requesterUserId;
            }

            guest.DocumentTypeId = documentType.Id;
            guest.FirstName = normalizedFirstName;
            guest.LastName = normalizedLastName;
            guest.BirthDate = command.PassengerBirthDate.ToDateTime(TimeOnly.MinValue);
            guest.Email = normalizedEmail;
            guest.Phone = normalizedPhone;
        }

        var checkInDateTime = command.CheckIn.ToDateTime(TimeOnly.MinValue);
        var checkOutDateTime = command.CheckOut.ToDateTime(TimeOnly.MinValue);
        var nights = command.CheckOut.DayNumber - command.CheckIn.DayNumber;

        var activeStatuses = new[] { ReservationStatus.Pending, ReservationStatus.Confirmed };

        var reservedRoomIds = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                activeStatuses.Contains(reservation.Status)
                && reservation.CheckInDate < checkOutDateTime
                && reservation.CheckOutDate > checkInDateTime)
            .Select(reservation => reservation.RoomId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var roomsQuery = dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .Where(room => room.Capacity >= command.Guests);

        if (command.RoomId.HasValue)
        {
            roomsQuery = roomsQuery.Where(room => room.Id == command.RoomId.Value);
        }

        if (command.RoomTypeId.HasValue)
        {
            roomsQuery = roomsQuery.Where(room => room.RoomTypeId == command.RoomTypeId.Value);
        }

        var availableRooms = await roomsQuery
            .Where(room => !reservedRoomIds.Contains(room.Id))
            .OrderBy(room => room.Number)
            .ToListAsync(cancellationToken);

        if (availableRooms.Count == 0)
        {
            var errorMessage = command.RoomId.HasValue
                ? "La habitacion seleccionada no esta disponible para las fechas y cantidad de huespedes indicadas."
                : "No encontramos habitaciones disponibles para las fechas y cantidad de huespedes indicadas.";

            throw new UserFriendlyException(
                errorMessage,
                StatusCodes.Status409Conflict);
        }

        var pricingByRoomType = await pricingService.GetPricingByRoomTypeAsync(
            availableRooms
                .GroupBy(room => room.RoomTypeId)
                .Select(group => new RoomTypePricingInput(group.Key, group.First().RoomType.BasePrice))
                .ToArray(),
            command.CheckIn,
            command.CheckOut,
            cancellationToken);

        var selectedRoom = command.RoomId.HasValue
            ? availableRooms.Single(room => room.Id == command.RoomId.Value)
            : availableRooms
                .OrderBy(room => pricingByRoomType[room.RoomTypeId].TotalPrice)
                .ThenBy(room => room.Number)
                .First();

        var selectedRoomPricing = pricingByRoomType[selectedRoom.RoomTypeId];
        var totalPrice = selectedRoomPricing.TotalPrice;
        var now = DateTime.UtcNow;

        var reservation = new Reservation
        {
            Guest = guest,
            RoomId = selectedRoom.Id,
            CheckInDate = checkInDateTime,
            CheckOutDate = checkOutDateTime,
            TotalPrice = totalPrice,
            Status = ReservationStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reserva creada. ReservationId={ReservationId}, GuestId={GuestId}, GuestUserId={GuestUserId}, RoomId={RoomId}, Status={Status}",
            reservation.Id,
            guest.Id,
            guest.UserId,
            reservation.RoomId,
            reservation.Status);

        return new CreateReservationResponseDto(
            reservation.Id,
            new ReservationPassengerDto(
                guest.Id,
                documentType.Id,
                documentType.Name,
                guest.FirstName,
                guest.LastName,
                guest.DocumentNumber,
                DateOnly.FromDateTime(guest.BirthDate),
                guest.Email,
                guest.Phone),
            selectedRoom.Id,
            selectedRoom.Number,
            selectedRoom.RoomTypeId,
            selectedRoom.RoomType.Name,
            command.CheckIn,
            command.CheckOut,
            nights,
            command.Guests,
            selectedRoomPricing.PricePerNight,
            totalPrice,
            0m,
            totalPrice,
            reservation.Status.ToString());
    }

    private static string NormalizeRequiredValue(string value)
    {
        return value.Trim();
    }

    private static string NormalizeDocumentNumber(string value)
    {
        return value.Trim();
    }

    private static string NormalizeDocumentTypeName(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
