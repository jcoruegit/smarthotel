namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record CreateReservationPaymentResponseDto(
    int PaymentId,
    int ReservationId,
    decimal Amount,
    string PaymentStatus,
    decimal ReservationTotalPrice,
    decimal TotalPaid,
    decimal RemainingBalance,
    bool IsFullyPaid,
    string ReservationStatus,
    DateTime CreatedAtUtc);
