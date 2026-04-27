using Microsoft.EntityFrameworkCore;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Pricing.Services;

public sealed record RoomTypePricingInput(int RoomTypeId, decimal BasePrice);

public sealed record PricingSummary(decimal PricePerNight, decimal TotalPrice);

public sealed class ReservationPricingService(AppDbContext dbContext)
{
    public async Task<PricingSummary> GetPricingAsync(
        int roomTypeId,
        decimal basePrice,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        var pricingByRoomType = await GetPricingByRoomTypeAsync(
            [new RoomTypePricingInput(roomTypeId, basePrice)],
            checkIn,
            checkOut,
            cancellationToken);

        return pricingByRoomType[roomTypeId];
    }

    public async Task<IReadOnlyDictionary<int, PricingSummary>> GetPricingByRoomTypeAsync(
        IReadOnlyCollection<RoomTypePricingInput> roomTypes,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        if (roomTypes.Count == 0 || checkOut <= checkIn)
        {
            return new Dictionary<int, PricingSummary>();
        }

        var startDateTime = checkIn.ToDateTime(TimeOnly.MinValue);
        var endDateTime = checkOut.ToDateTime(TimeOnly.MinValue);

        var roomTypeBasePrices = roomTypes
            .GroupBy(roomType => roomType.RoomTypeId)
            .ToDictionary(group => group.Key, group => group.First().BasePrice);

        var roomTypeIds = roomTypeBasePrices.Keys.ToArray();

        var pricingRules = await dbContext.PricingRules
            .AsNoTracking()
            .Where(rule =>
                roomTypeIds.Contains(rule.RoomTypeId)
                && rule.Date >= startDateTime
                && rule.Date < endDateTime)
            .ToListAsync(cancellationToken);

        var rulePriceByRoomTypeAndDate = pricingRules
            .GroupBy(rule => new { rule.RoomTypeId, Date = DateOnly.FromDateTime(rule.Date) })
            .ToDictionary(
                group => (group.Key.RoomTypeId, group.Key.Date),
                group => group.OrderByDescending(rule => rule.Id).First().Price);

        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var pricingByRoomType = new Dictionary<int, PricingSummary>(roomTypeBasePrices.Count);

        foreach (var (roomTypeId, basePrice) in roomTypeBasePrices)
        {
            var totalPrice = 0m;
            for (var date = checkIn; date < checkOut; date = date.AddDays(1))
            {
                totalPrice += rulePriceByRoomTypeAndDate.TryGetValue((roomTypeId, date), out var rulePrice)
                    ? rulePrice
                    : basePrice;
            }

            var pricePerNight = Math.Round(totalPrice / nights, 2, MidpointRounding.AwayFromZero);
            pricingByRoomType[roomTypeId] = new PricingSummary(pricePerNight, totalPrice);
        }

        return pricingByRoomType;
    }
}
