namespace SmartHotel.API.Features.Auth.Dto;

public sealed record UpdateGuestProfileRequestDto(
    int DocumentTypeId,
    string FirstName,
    string LastName,
    string DocumentNumber,
    DateOnly BirthDate,
    string Email,
    string? Phone);
