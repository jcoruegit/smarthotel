namespace SmartHotel.API.Features.Auth.Dto;

public sealed record RegisterResponseDto(
    string UserId,
    string Email,
    IReadOnlyList<string> Roles);
