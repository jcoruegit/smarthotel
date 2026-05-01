namespace SmartHotel.API.Features.Auth.Dto;

public sealed record UpdateEmployeeSelfProfileRequestDto(
    int DocumentTypeId,
    string FirstName,
    string LastName,
    string DocumentNumber,
    DateOnly BirthDate);
