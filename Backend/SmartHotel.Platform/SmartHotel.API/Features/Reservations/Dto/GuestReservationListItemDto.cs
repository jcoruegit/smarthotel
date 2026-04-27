namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record GuestReservationListItemDto(
    int ReservationId,
    int RoomId,
    string RoomNumber,
    string RoomTypeName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    decimal TotalPrice,
    decimal TotalPaid,
    decimal RemainingBalance,
    string Status,
    DateTime CreatedAtUtc);
