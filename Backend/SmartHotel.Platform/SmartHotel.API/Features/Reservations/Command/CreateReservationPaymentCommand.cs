namespace SmartHotel.API.Features.Reservations.Command;

public sealed record CreateReservationPaymentCommand(
    int ReservationId,
    decimal Amount,
    string CardHolderName,
    string? RequesterUserId,
    bool RequesterIsGuest);
