namespace SmartHotel.API.Features.Auth.Dto;

public sealed record UpdateEmployeeRequestDto(
    string FirstName,
    string LastName,
    int DocumentTypeId,
    string DocumentNumber,
    DateOnly BirthDate,
    string Profile);
