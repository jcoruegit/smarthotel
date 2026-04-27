namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record UpdateReservationResponseDto(
    int ReservationId,
    int RoomId,
    string RoomNumber,
    int RoomTypeId,
    string RoomTypeName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Guests,
    decimal PricePerNight,
    decimal TotalPrice,
    decimal TotalPaid,
    decimal RemainingBalance,
    string Status,
    DateTime UpdatedAtUtc);
