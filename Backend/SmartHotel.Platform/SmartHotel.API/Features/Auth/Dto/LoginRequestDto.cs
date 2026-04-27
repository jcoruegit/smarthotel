namespace SmartHotel.API.Features.Auth.Dto;

public sealed record LoginRequestDto(
    string Email,
    string Password);
