namespace SmartHotel.API.Features.HotelInfo.Dto;

public sealed record HotelAmenityDto(
    int Id,
    string Name,
    string Description,
    string? AvailableFrom,
    string? AvailableTo,
    string? DaysOfWeek,
    bool IsComplimentary,
    decimal? Price,
    string? Currency,
    bool RequiresReservation);
