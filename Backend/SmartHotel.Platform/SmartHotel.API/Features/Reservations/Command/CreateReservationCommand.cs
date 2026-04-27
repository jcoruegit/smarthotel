namespace SmartHotel.API.Features.Reservations.Command;

public sealed record CreateReservationCommand(
    string PassengerDocumentTypeName,
    string PassengerFirstName,
    string PassengerLastName,
    string PassengerDocumentNumber,
    DateOnly PassengerBirthDate,
    string? PassengerEmail,
    string? PassengerPhone,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    int? RoomId,
    int? RoomTypeId,
    string? RequesterUserId,
    bool RequesterIsGuest);
