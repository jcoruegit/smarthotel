namespace SmartHotel.API.Features.Auth.Dto;

public sealed record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword);
