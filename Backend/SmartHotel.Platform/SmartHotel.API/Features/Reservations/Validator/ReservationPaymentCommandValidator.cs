using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Command;

namespace SmartHotel.API.Features.Reservations.Validator;

public sealed class ReservationPaymentCommandValidator
{
    public void Validate(CreateReservationPaymentCommand command)
    {
        if (command.ReservationId <= 0)
        {
            throw new UserFriendlyException("El parametro 'id' debe ser un numero entero positivo.");
        }

        if (command.Amount <= 0)
        {
            throw new UserFriendlyException("El parametro 'amount' debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(command.CardHolderName))
        {
            throw new UserFriendlyException("El parametro 'cardHolderName' es obligatorio.");
        }
    }
}
