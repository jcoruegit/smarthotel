namespace SmartHotel.API.Features.HotelInfo.Dto;

public sealed record HotelScheduleDto(
    int Id,
    string Code,
    string Title,
    string? StartTime,
    string? EndTime,
    string? Notes,
    string? DaysOfWeek,
    string? ValidFrom,
    string? ValidTo);
