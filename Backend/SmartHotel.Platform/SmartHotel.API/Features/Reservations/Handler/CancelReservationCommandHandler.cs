using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Command;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Reservations.Handler;

public sealed class CancelReservationCommandHandler(
    AppDbContext dbContext,
    ReservationLifecycleService reservationLifecycleService,
    ILogger<CancelReservationCommandHandler> logger)
{
    public async Task<CancelReservationResponseDto> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken)
    {
        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        if (command.ReservationId <= 0)
        {
            throw new UserFriendlyException("El parametro 'id' debe ser un numero entero positivo.");
        }

        var reservation = await dbContext.Reservations
            .SingleOrDefaultAsync(entity => entity.Id == command.ReservationId, cancellationToken);

        if (reservation is null)
        {
            throw new UserFriendlyException("No encontramos la reserva indicada.", StatusCodes.Status404NotFound);
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            throw new UserFriendlyException(
                "La reserva ya se encuentra cancelada.",
                StatusCodes.Status409Conflict);
        }

        if (reservation.Status == ReservationStatus.Completed)
        {
            throw new UserFriendlyException(
                "No es posible cancelar una reserva completada.",
                StatusCodes.Status409Conflict);
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reserva cancelada por admin. ReservationId={ReservationId}, UpdatedAt={UpdatedAt}",
            reservation.Id,
            reservation.UpdatedAt);

        return new CancelReservationResponseDto(
            reservation.Id,
            reservation.Status.ToString(),
            reservation.UpdatedAt);
    }
}
