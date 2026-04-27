using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Command;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.API.Features.Reservations.Validator;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Reservations.Handler;

public sealed class UpdateReservationCommandHandler(
    AppDbContext dbContext,
    ReservationPricingService pricingService,
    ReservationLifecycleService reservationLifecycleService,
    UpdateReservationCommandValidator validator,
    ILogger<UpdateReservationCommandHandler> logger)
{
    public async Task<UpdateReservationResponseDto> HandleAsync(
        UpdateReservationCommand command,
        CancellationToken cancellationToken)
    {
        validator.Validate(command);
        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        var reservation = await dbContext.Reservations
            .SingleOrDefaultAsync(entity => entity.Id == command.ReservationId, cancellationToken);

        if (reservation is null)
        {
            throw new UserFriendlyException("No encontramos la reserva indicada.", StatusCodes.Status404NotFound);
        }

        if (reservation.Status is ReservationStatus.Cancelled or ReservationStatus.Completed)
        {
            throw new UserFriendlyException(
                "Solo se pueden modificar reservas en estado Pending o Confirmed.",
                StatusCodes.Status409Conflict);
        }

        var room = await dbContext.Rooms
            .AsNoTracking()
            .Include(entity => entity.RoomType)
            .SingleOrDefaultAsync(entity => entity.Id == command.RoomId, cancellationToken);

        if (room is null)
        {
            throw new UserFriendlyException("No encontramos la habitacion indicada.", StatusCodes.Status404NotFound);
        }

        if (room.Capacity < command.Guests)
        {
            throw new UserFriendlyException(
                "La habitacion indicada no tiene capacidad suficiente para la cantidad de huespedes.",
                StatusCodes.Status409Conflict);
        }

        var checkInDateTime = command.CheckIn.ToDateTime(TimeOnly.MinValue);
        var checkOutDateTime = command.CheckOut.ToDateTime(TimeOnly.MinValue);
        var activeStatuses = new[] { ReservationStatus.Pending, ReservationStatus.Confirmed };

        var isRoomUnavailable = await dbContext.Reservations
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.Id != reservation.Id
                && entity.RoomId == room.Id
                && activeStatuses.Contains(entity.Status)
                && entity.CheckInDate < checkOutDateTime
                && entity.CheckOutDate > checkInDateTime,
                cancellationToken);

        if (isRoomUnavailable)
        {
            throw new UserFriendlyException(
                "La habitacion indicada no esta disponible para las fechas seleccionadas.",
                StatusCodes.Status409Conflict);
        }

        var nights = command.CheckOut.DayNumber - command.CheckIn.DayNumber;
        var pricing = await pricingService.GetPricingAsync(
            room.RoomTypeId,
            room.RoomType.BasePrice,
            command.CheckIn,
            command.CheckOut,
            cancellationToken);
        var totalPrice = pricing.TotalPrice;

        reservation.RoomId = room.Id;
        reservation.CheckInDate = checkInDateTime;
        reservation.CheckOutDate = checkOutDateTime;
        reservation.TotalPrice = totalPrice;
        reservation.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reserva actualizada por usuario interno. ReservationId={ReservationId}, RoomId={RoomId}, CheckIn={CheckIn}, CheckOut={CheckOut}, TotalPrice={TotalPrice}",
            reservation.Id,
            reservation.RoomId,
            reservation.CheckInDate,
            reservation.CheckOutDate,
            reservation.TotalPrice);

        var totalPaid = await dbContext.Payments
            .AsNoTracking()
            .Where(entity => entity.ReservationId == reservation.Id && entity.Status == PaymentStatus.Paid)
            .SumAsync(entity => (decimal?)entity.Amount, cancellationToken) ?? 0m;

        var remainingBalance = Math.Max(reservation.TotalPrice - totalPaid, 0m);

        return new UpdateReservationResponseDto(
            reservation.Id,
            room.Id,
            room.Number,
            room.RoomTypeId,
            room.RoomType.Name,
            command.CheckIn,
            command.CheckOut,
            nights,
            command.Guests,
            pricing.PricePerNight,
            reservation.TotalPrice,
            totalPaid,
            remainingBalance,
            reservation.Status.ToString(),
            reservation.UpdatedAt);
    }
}
