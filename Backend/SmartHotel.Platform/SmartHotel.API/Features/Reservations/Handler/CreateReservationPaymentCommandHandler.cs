using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Command;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.API.Features.Reservations.Validator;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace SmartHotel.API.Features.Reservations.Handler;

public sealed class CreateReservationPaymentCommandHandler(
    AppDbContext dbContext,
    ReservationLifecycleService reservationLifecycleService,
    ReservationPaymentCommandValidator validator,
    ILogger<CreateReservationPaymentCommandHandler> logger)
{
    public async Task<CreateReservationPaymentResponseDto> HandleAsync(
        CreateReservationPaymentCommand command,
        CancellationToken cancellationToken)
    {
        validator.Validate(command);
        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        var reservation = await dbContext.Reservations
            .Include(entity => entity.Guest)
            .SingleOrDefaultAsync(entity => entity.Id == command.ReservationId, cancellationToken);

        if (reservation is null)
        {
            throw new UserFriendlyException("No encontramos la reserva indicada.", StatusCodes.Status404NotFound);
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            throw new UserFriendlyException("No es posible registrar pagos sobre una reserva cancelada.", StatusCodes.Status409Conflict);
        }

        if (reservation.Status == ReservationStatus.Completed)
        {
            throw new UserFriendlyException("No es posible registrar pagos sobre una reserva completada.", StatusCodes.Status409Conflict);
        }

        if (command.RequesterIsGuest)
        {
            if (string.IsNullOrWhiteSpace(command.RequesterUserId))
            {
                throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
            }

            if (!string.Equals(reservation.Guest.UserId, command.RequesterUserId, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Pago denegado por ownership. ReservationId={ReservationId}, ReservationGuestUserId={ReservationGuestUserId}, RequesterUserId={RequesterUserId}",
                    reservation.Id,
                    reservation.Guest.UserId,
                    command.RequesterUserId);
                throw new UserFriendlyException(
                    "No tenes permisos para registrar pagos sobre esta reserva.",
                    StatusCodes.Status403Forbidden);
            }
        }

        var totalPaidBefore = await dbContext.Payments
            .AsNoTracking()
            .Where(entity => entity.ReservationId == reservation.Id && entity.Status == PaymentStatus.Paid)
            .SumAsync(entity => (decimal?)entity.Amount, cancellationToken) ?? 0m;

        var remainingBalanceBefore = Math.Max(reservation.TotalPrice - totalPaidBefore, 0m);
        if (remainingBalanceBefore == 0m)
        {
            throw new UserFriendlyException("La reserva no tiene saldo pendiente.", StatusCodes.Status400BadRequest);
        }

        if (command.Amount > remainingBalanceBefore)
        {
            throw new UserFriendlyException(
                $"El monto ingresado ({command.Amount:0.00}) supera el saldo pendiente ({remainingBalanceBefore:0.00}).",
                StatusCodes.Status400BadRequest);
        }

        var normalizedGuestFullName = NormalizePersonName($"{reservation.Guest.FirstName} {reservation.Guest.LastName}");
        var normalizedCardHolderName = NormalizePersonName(command.CardHolderName);
        if (!string.Equals(normalizedCardHolderName, normalizedGuestFullName, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                "El titular de la tarjeta debe coincidir con el nombre y apellido del cliente.",
                StatusCodes.Status400BadRequest);
        }

        var paymentStatus = command.Amount < remainingBalanceBefore
            ? PaymentStatus.Pending
            : PaymentStatus.Paid;

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            ReservationId = reservation.Id,
            Amount = command.Amount,
            Status = paymentStatus,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Pago registrado. PaymentId={PaymentId}, ReservationId={ReservationId}, Amount={Amount}, Status={Status}, RequesterUserId={RequesterUserId}",
            payment.Id,
            reservation.Id,
            payment.Amount,
            payment.Status,
            command.RequesterUserId);

        var totalPaid = totalPaidBefore + (payment.Status == PaymentStatus.Paid ? payment.Amount : 0m);
        var remainingBalance = Math.Max(reservation.TotalPrice - totalPaid, 0m);
        var isFullyPaid = remainingBalance == 0m;

        if (isFullyPaid && reservation.Status == ReservationStatus.Pending)
        {
            reservation.Status = ReservationStatus.Confirmed;
            reservation.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Reserva confirmada por pago completo. ReservationId={ReservationId}",
                reservation.Id);
        }

        return new CreateReservationPaymentResponseDto(
            payment.Id,
            reservation.Id,
            payment.Amount,
            payment.Status.ToString(),
            reservation.TotalPrice,
            totalPaid,
            remainingBalance,
            isFullyPaid,
            reservation.Status.ToString(),
            payment.CreatedAt);
    }

    private static string NormalizePersonName(string value)
    {
        return Regex.Replace(value.Trim(), "\\s+", " ").ToUpperInvariant();
    }
}
