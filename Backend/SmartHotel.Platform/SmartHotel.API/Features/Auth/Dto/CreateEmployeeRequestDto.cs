namespace SmartHotel.API.Features.Auth.Dto;

public sealed record CreateEmployeeRequestDto(
    string FirstName,
    string LastName,
    int DocumentTypeId,
    string DocumentNumber,
    DateOnly BirthDate,
    string Profile);
