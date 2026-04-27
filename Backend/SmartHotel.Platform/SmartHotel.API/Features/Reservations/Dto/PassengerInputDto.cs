namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record PassengerInputDto(
    string DocumentType,
    string FirstName,
    string LastName,
    string DocumentNumber,
    string BirthDate,
    string? Email,
    string? Phone);
