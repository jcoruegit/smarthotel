namespace SmartHotel.API.Features.Reservations.Query;

public sealed record GetReservationByIdQuery(
    int ReservationId,
    string? RequesterUserId,
    bool RequesterIsGuest);
