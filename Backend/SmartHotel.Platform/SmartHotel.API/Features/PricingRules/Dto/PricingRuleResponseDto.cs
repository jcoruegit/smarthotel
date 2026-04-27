namespace SmartHotel.API.Features.PricingRules.Dto;

public sealed record PricingRuleResponseDto(
    int Id,
    int RoomTypeId,
    string RoomTypeName,
    string Date,
    decimal Price,
    string Reason);
