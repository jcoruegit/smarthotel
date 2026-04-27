using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Command;

namespace SmartHotel.API.Features.Reservations.Validator;

public sealed class UpdateReservationCommandValidator
{
    public void Validate(UpdateReservationCommand command)
    {
        if (command.ReservationId <= 0)
        {
            throw new UserFriendlyException("El parametro 'id' debe ser un numero entero positivo.");
        }

        if (command.Guests <= 0)
        {
            throw new UserFriendlyException("El parametro 'guests' debe ser mayor a cero.");
        }

        if (command.RoomId <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomId' debe ser un numero entero positivo.");
        }

        if (command.CheckOut <= command.CheckIn)
        {
            throw new UserFriendlyException("La fecha de check-out debe ser posterior al check-in.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (command.CheckIn < today)
        {
            throw new UserFriendlyException("La fecha de check-in no puede estar en el pasado.");
        }
    }
}
