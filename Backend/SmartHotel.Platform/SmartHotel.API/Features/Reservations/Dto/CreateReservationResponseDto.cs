namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record CreateReservationResponseDto(
    int ReservationId,
    ReservationPassengerDto Passenger,
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
    string Status);
