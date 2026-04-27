namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record ReservationPassengerDto(
    int GuestId,
    int DocumentTypeId,
    string DocumentTypeName,
    string FirstName,
    string LastName,
    string DocumentNumber,
    DateOnly BirthDate,
    string? Email,
    string? Phone);
