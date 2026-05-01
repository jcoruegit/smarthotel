namespace SmartHotel.API.Features.Auth.Dto;

public sealed record CreateEmployeeResponseDto(
    int EmployeeId,
    string UserId,
    string FullName,
    string Email,
    string Profile,
    string TemporaryPassword);
