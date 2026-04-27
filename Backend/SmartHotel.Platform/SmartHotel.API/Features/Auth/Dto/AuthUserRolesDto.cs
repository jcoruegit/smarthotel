namespace SmartHotel.API.Features.Auth.Dto;

public sealed record AuthUserRolesDto(
    string UserId,
    string Email,
    string? FullName,
    IReadOnlyList<string> Roles);
