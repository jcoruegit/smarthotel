namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record UpdateReservationRequestDto(
    string CheckIn,
    string CheckOut,
    int Guests,
    int RoomId);
