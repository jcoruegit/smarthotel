namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record CancelReservationResponseDto(
    int ReservationId,
    string Status,
    DateTime UpdatedAtUtc);
