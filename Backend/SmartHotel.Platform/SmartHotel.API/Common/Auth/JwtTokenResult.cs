namespace SmartHotel.API.Common.Auth;

public sealed record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);
