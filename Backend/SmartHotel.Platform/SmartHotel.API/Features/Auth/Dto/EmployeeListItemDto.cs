namespace SmartHotel.API.Features.Auth.Dto;

public sealed record EmployeeListItemDto(
    int EmployeeId,
    string UserId,
    string FirstName,
    string LastName,
    int DocumentTypeId,
    string DocumentTypeName,
    string DocumentNumber,
    DateOnly BirthDate,
    string Email,
    string Profile);
