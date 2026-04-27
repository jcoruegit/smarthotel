using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Query;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Reservations.Handler;

public sealed class GetReservationByIdQueryHandler(
    AppDbContext dbContext,
    ReservationLifecycleService reservationLifecycleService,
    ILogger<GetReservationByIdQueryHandler> logger)
{
    public async Task<GetReservationByIdResponseDto> HandleAsync(GetReservationByIdQuery query, CancellationToken cancellationToken)
    {
        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        var reservation = await dbContext.Reservations
            .AsNoTracking()
            .Include(entity => entity.Guest)
            .ThenInclude(guest => guest.DocumentType)
            .Include(entity => entity.Room)
            .ThenInclude(room => room.RoomType)
            .SingleOrDefaultAsync(entity => entity.Id == query.ReservationId, cancellationToken);

        if (reservation is null)
        {
            throw new UserFriendlyException("No encontramos la reserva indicada.", StatusCodes.Status404NotFound);
        }

        if (query.RequesterIsGuest)
        {
            if (string.IsNullOrWhiteSpace(query.RequesterUserId))
            {
                throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
            }

            if (!string.Equals(reservation.Guest.UserId, query.RequesterUserId, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Acceso denegado a reserva por ownership. ReservationId={ReservationId}, ReservationGuestUserId={ReservationGuestUserId}, RequesterUserId={RequesterUserId}",
                    reservation.Id,
                    reservation.Guest.UserId,
                    query.RequesterUserId);
                throw new UserFriendlyException(
                    "No tenes permisos para consultar esta reserva.",
                    StatusCodes.Status403Forbidden);
            }
        }

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(entity => entity.ReservationId == reservation.Id)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new ReservationPaymentDto(
                entity.Id,
                entity.Amount,
                entity.Status.ToString(),
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        var totalPaid = await dbContext.Payments
            .AsNoTracking()
            .Where(entity => entity.ReservationId == reservation.Id && entity.Status == PaymentStatus.Paid)
            .SumAsync(entity => (decimal?)entity.Amount, cancellationToken) ?? 0m;

        var checkIn = DateOnly.FromDateTime(reservation.CheckInDate);
        var checkOut = DateOnly.FromDateTime(reservation.CheckOutDate);
        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var pricePerNight = nights > 0 ? reservation.TotalPrice / nights : reservation.TotalPrice;
        var remainingBalance = Math.Max(reservation.TotalPrice - totalPaid, 0m);

        logger.LogInformation(
            "Consulta de reserva exitosa. ReservationId={ReservationId}, RequesterUserId={RequesterUserId}, RequesterIsGuest={RequesterIsGuest}",
            reservation.Id,
            query.RequesterUserId,
            query.RequesterIsGuest);

        return new GetReservationByIdResponseDto(
            reservation.Id,
            new ReservationPassengerDto(
                reservation.GuestId,
                reservation.Guest.DocumentTypeId,
                reservation.Guest.DocumentType.Name,
                reservation.Guest.FirstName,
                reservation.Guest.LastName,
                reservation.Guest.DocumentNumber,
                DateOnly.FromDateTime(reservation.Guest.BirthDate),
                reservation.Guest.Email,
                reservation.Guest.Phone),
            reservation.RoomId,
            reservation.Room.Number,
            reservation.Room.RoomTypeId,
            reservation.Room.RoomType.Name,
            checkIn,
            checkOut,
            nights,
            pricePerNight,
            reservation.TotalPrice,
            totalPaid,
            remainingBalance,
            reservation.Status.ToString(),
            reservation.CreatedAt,
            reservation.UpdatedAt,
            payments);
    }
}
