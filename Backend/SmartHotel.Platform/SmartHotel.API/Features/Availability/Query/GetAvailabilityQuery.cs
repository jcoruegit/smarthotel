namespace SmartHotel.API.Features.Availability.Query;

public sealed record GetAvailabilityQuery(
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    int? RoomTypeId);
