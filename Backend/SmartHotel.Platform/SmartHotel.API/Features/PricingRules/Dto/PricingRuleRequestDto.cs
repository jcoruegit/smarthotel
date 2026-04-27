namespace SmartHotel.API.Features.PricingRules.Dto;

public sealed record PricingRuleRequestDto(
    int RoomTypeId,
    string Date,
    decimal Price,
    string Reason);
