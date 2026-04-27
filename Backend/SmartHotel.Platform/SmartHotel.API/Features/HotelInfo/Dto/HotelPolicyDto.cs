namespace SmartHotel.API.Features.HotelInfo.Dto;

public sealed record HotelPolicyDto(
    int Id,
    string Code,
    string Title,
    string Description,
    string Category);
