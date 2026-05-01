namespace SmartHotel.API.Features.Auth.Dto;

public sealed record EmployeeSelfProfileDto(
    int DocumentTypeId,
    string DocumentTypeName,
    string FirstName,
    string LastName,
    string DocumentNumber,
    DateOnly BirthDate);
