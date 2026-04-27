namespace SmartHotel.API.Features.Auth.Dto;

public sealed record GuestProfileDto(
    int DocumentTypeId,
    string DocumentTypeName,
    string FirstName,
    string LastName,
    string DocumentNumber,
    DateOnly BirthDate,
    string? Email,
    string? Phone);
