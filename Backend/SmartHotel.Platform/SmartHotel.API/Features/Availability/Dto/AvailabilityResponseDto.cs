namespace SmartHotel.API.Features.Availability.Dto;

public sealed record AvailabilityResponseDto(
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Guests,
    IReadOnlyList<AvailableRoomDto> Rooms,
    string? Message);
