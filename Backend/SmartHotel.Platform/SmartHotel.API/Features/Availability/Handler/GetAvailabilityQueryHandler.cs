using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Features.Availability.Dto;
using SmartHotel.API.Features.Availability.Query;
using SmartHotel.API.Features.Availability.Validator;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Availability.Handler;

public sealed class GetAvailabilityQueryHandler(
    AppDbContext dbContext,
    ReservationPricingService pricingService,
    ReservationLifecycleService reservationLifecycleService,
    AvailabilityQueryValidator validator)
{
    public async Task<AvailabilityResponseDto> HandleAsync(GetAvailabilityQuery query, CancellationToken cancellationToken)
    {
        validator.Validate(query);
        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        var checkInDateTime = query.CheckIn.ToDateTime(TimeOnly.MinValue);
        var checkOutDateTime = query.CheckOut.ToDateTime(TimeOnly.MinValue);
        var nights = query.CheckOut.DayNumber - query.CheckIn.DayNumber;

        var activeStatuses = new[] { ReservationStatus.Pending, ReservationStatus.Confirmed };

        var reservedRoomIds = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                activeStatuses.Contains(reservation.Status)
                && reservation.CheckInDate < checkOutDateTime
                && reservation.CheckOutDate > checkInDateTime)
            .Select(reservation => reservation.RoomId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var roomsQuery = dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .Where(room => room.Capacity >= query.Guests);

        if (query.RoomTypeId.HasValue)
        {
            roomsQuery = roomsQuery.Where(room => room.RoomTypeId == query.RoomTypeId.Value);
        }

        var availableRooms = await roomsQuery
            .Where(room => !reservedRoomIds.Contains(room.Id))
            .OrderBy(room => room.RoomTypeId)
            .ThenBy(room => room.Number)
            .ToListAsync(cancellationToken);

        var pricingByRoomType = await pricingService.GetPricingByRoomTypeAsync(
            availableRooms
                .Select(room => room.RoomTypeId)
                .Distinct()
                .Select(roomTypeId =>
                {
                    var sampleRoom = availableRooms.First(room => room.RoomTypeId == roomTypeId);
                    return new RoomTypePricingInput(roomTypeId, sampleRoom.RoomType.BasePrice);
                })
                .ToArray(),
            query.CheckIn,
            query.CheckOut,
            cancellationToken);

        var roomCards = availableRooms
            .Select(room =>
            {
                var pricing = pricingByRoomType.TryGetValue(room.RoomTypeId, out var value)
                    ? value
                    : new PricingSummary(room.RoomType.BasePrice, room.RoomType.BasePrice * nights);

                return new AvailableRoomDto(
                    room.Id,
                    room.Number,
                    room.RoomTypeId,
                    room.RoomType.Name,
                    room.Capacity,
                    room.Features,
                    pricing.PricePerNight,
                    pricing.TotalPrice);
            })
            .OrderBy(room => room.PricePerNight)
            .ThenBy(room => room.RoomNumber)
            .ToList();

        var message = roomCards.Count == 0
            ? "No encontramos habitaciones disponibles para las fechas y cantidad de huespedes indicadas."
            : null;

        return new AvailabilityResponseDto(
            query.CheckIn,
            query.CheckOut,
            nights,
            query.Guests,
            roomCards,
            message);
    }
}
