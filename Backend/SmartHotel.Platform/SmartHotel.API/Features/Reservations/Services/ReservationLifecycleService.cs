using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Reservations.Services;

public sealed class ReservationLifecycleService(
    AppDbContext dbContext,
    ILogger<ReservationLifecycleService> logger)
{
    public async Task<int> CompleteExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var expiredReservations = await dbContext.Reservations
            .Where(reservation =>
                (reservation.Status == ReservationStatus.Pending || reservation.Status == ReservationStatus.Confirmed)
                && reservation.CheckOutDate <= now)
            .ToListAsync(cancellationToken);

        if (expiredReservations.Count == 0)
        {
            return 0;
        }

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = ReservationStatus.Completed;
            reservation.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reservas completadas automaticamente por check-out vencido. Count={Count}",
            expiredReservations.Count);

        return expiredReservations.Count;
    }
}
