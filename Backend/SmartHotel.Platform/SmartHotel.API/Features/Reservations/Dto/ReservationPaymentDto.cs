namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record ReservationPaymentDto(
    int PaymentId,
    decimal Amount,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
