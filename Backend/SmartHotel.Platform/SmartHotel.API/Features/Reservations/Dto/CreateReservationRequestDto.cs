namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record CreateReservationRequestDto(
    PassengerInputDto Passenger,
    string CheckIn,
    string CheckOut,
    int Guests,
    int? RoomId,
    int? RoomTypeId);
