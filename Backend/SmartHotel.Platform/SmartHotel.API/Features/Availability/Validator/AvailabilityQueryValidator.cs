using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Availability.Query;

namespace SmartHotel.API.Features.Availability.Validator;

public sealed class AvailabilityQueryValidator
{
    public void Validate(GetAvailabilityQuery query)
    {
        if (query.Guests <= 0)
        {
            throw new UserFriendlyException("El parametro 'guests' debe ser mayor a cero.");
        }

        if (query.RoomTypeId.HasValue && query.RoomTypeId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' no es valido.");
        }

        if (query.CheckOut <= query.CheckIn)
        {
            throw new UserFriendlyException("La fecha de check-out debe ser posterior al check-in.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (query.CheckIn < today)
        {
            throw new UserFriendlyException("La fecha de check-in no puede estar en el pasado.");
        }
    }
}
