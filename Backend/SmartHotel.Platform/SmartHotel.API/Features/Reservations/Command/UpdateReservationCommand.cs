namespace SmartHotel.API.Features.Reservations.Command;

public sealed record UpdateReservationCommand(
    int ReservationId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    int RoomId);
