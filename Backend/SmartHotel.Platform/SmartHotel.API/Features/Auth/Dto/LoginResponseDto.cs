namespace SmartHotel.API.Features.Auth.Dto;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserId,
    string Email,
    IReadOnlyList<string> Roles);
