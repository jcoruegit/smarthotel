using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Command;
using System.Text.RegularExpressions;

namespace SmartHotel.API.Features.Reservations.Validator;

public sealed class ReservationCommandValidator
{
    public void Validate(CreateReservationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.PassengerDocumentTypeName))
        {
            throw new UserFriendlyException("El parametro 'passenger.documentType' es obligatorio.");
        }

        var normalizedDocumentType = command.PassengerDocumentTypeName.Trim().ToUpperInvariant();
        if (normalizedDocumentType is not ("DNI" or "PASAPORTE"))
        {
            throw new UserFriendlyException("El parametro 'passenger.documentType' solo admite 'DNI' o 'Pasaporte'.");
        }

        if (string.IsNullOrWhiteSpace(command.PassengerFirstName))
        {
            throw new UserFriendlyException("El parametro 'passenger.firstName' es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(command.PassengerLastName))
        {
            throw new UserFriendlyException("El parametro 'passenger.lastName' es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(command.PassengerDocumentNumber))
        {
            throw new UserFriendlyException("El parametro 'passenger.documentNumber' es obligatorio.");
        }

        var normalizedDocumentNumber = command.PassengerDocumentNumber.Trim();
        if (!Regex.IsMatch(normalizedDocumentNumber, "^[0-9]{7,8}$"))
        {
            throw new UserFriendlyException("El numero de documento debe tener al menos 7 digitos y como maximo 8, usando solo numeros.");
        }

        if (command.Guests <= 0)
        {
            throw new UserFriendlyException("El parametro 'guests' debe ser mayor a cero.");
        }

        if (command.RoomTypeId.HasValue && command.RoomTypeId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' no es valido.");
        }

        if (command.RoomId.HasValue && command.RoomId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomId' no es valido.");
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

        if (command.PassengerBirthDate > today)
        {
            throw new UserFriendlyException("La fecha de nacimiento del pasajero no puede estar en el futuro.");
        }

        if (command.PassengerBirthDate < new DateOnly(1900, 1, 1))
        {
            throw new UserFriendlyException("La fecha de nacimiento del pasajero no es valida.");
        }
    }
}
