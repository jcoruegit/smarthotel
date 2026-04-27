namespace SmartHotel.API.Features.Availability.Dto;

public sealed record RoomTypeOptionDto(
    int Id,
    string Name,
    decimal BasePrice);
