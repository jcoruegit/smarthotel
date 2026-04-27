namespace SmartHotel.API.Features.Auth.Dto;

public sealed record RegisterRequestDto(
    string FirstName,
    string LastName,
    int DocumentTypeId,
    string DocumentNumber,
    DateOnly BirthDate,
    string Email,
    string Password);
