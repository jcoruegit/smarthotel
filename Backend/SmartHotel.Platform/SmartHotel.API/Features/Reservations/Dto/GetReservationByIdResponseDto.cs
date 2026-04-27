namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record GetReservationByIdResponseDto(
    int ReservationId,
    ReservationPassengerDto Passenger,
    int RoomId,
    string RoomNumber,
    int RoomTypeId,
    string RoomTypeName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    decimal PricePerNight,
    decimal TotalPrice,
    decimal TotalPaid,
    decimal RemainingBalance,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ReservationPaymentDto> Payments);
