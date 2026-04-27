namespace SmartHotel.API.Features.Auth.Dto;

public sealed record UpdateUserRolesRequestDto(
    IReadOnlyList<string> Roles);
