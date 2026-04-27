namespace SmartHotel.API.Features.Availability.Dto;

public sealed record AvailableRoomDto(
    int RoomId,
    string RoomNumber,
    int RoomTypeId,
    string RoomTypeName,
    int MaxCapacity,
    string Features,
    decimal PricePerNight,
    decimal EstimatedTotalPrice);
