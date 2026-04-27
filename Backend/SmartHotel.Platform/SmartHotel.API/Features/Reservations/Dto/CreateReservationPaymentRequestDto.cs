namespace SmartHotel.API.Features.Reservations.Dto;

public sealed record CreateReservationPaymentRequestDto(
    decimal Amount,
    string CardHolderName);
