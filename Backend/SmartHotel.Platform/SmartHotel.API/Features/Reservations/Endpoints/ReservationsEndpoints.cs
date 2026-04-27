using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Reservations.Command;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Handler;
using SmartHotel.API.Features.Reservations.Query;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Reservations.Endpoints;

public static class ReservationsEndpoints
{
    public static IEndpointRouteBuilder MapReservationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reservations")
            .WithTags("Reservations")
            .RequireAuthorization("ReservationAccess");

        group.MapPost(string.Empty, HandleAsync)
            .WithName("CreateReservation")
            .RequireAuthorization("ReservationAccess")
            .WithSummary("Crear reserva")
            .WithDescription("Crea una reserva para un pasajero y asigna una habitacion disponible.")
            .Accepts<CreateReservationRequestDto>("application/json")
            .Produces<CreateReservationResponseDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id}", GetByIdAsync)
            .WithName("GetReservationById")
            .RequireAuthorization("ReservationAccess")
            .WithSummary("Obtener reserva por id")
            .WithDescription("Obtiene detalle de reserva y pagos asociados.")
            .Produces<GetReservationByIdResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/mine", ListMineAsync)
            .WithName("ListMyReservations")
            .RequireAuthorization("GuestOnly")
            .WithSummary("Listar reservas del huesped autenticado")
            .WithDescription("Retorna las reservas del usuario Guest autenticado. Permite filtro opcional por rango de fechas.")
            .Produces<IReadOnlyList<GuestReservationListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateReservation")
            .RequireAuthorization("StaffOrAdmin")
            .WithSummary("Actualizar reserva")
            .WithDescription("Operacion interna para Staff/Admin. Reprograma o cambia habitacion de la reserva.")
            .Accepts<UpdateReservationRequestDto>("application/json")
            .Produces<UpdateReservationResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{id}", CancelAsync)
            .WithName("CancelReservation")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Cancelar reserva")
            .WithDescription("Operacion administrativa para cancelar una reserva activa.")
            .Produces<CancelReservationResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/{id}/payments", CreatePaymentAsync)
            .WithName("CreateReservationPayment")
            .RequireAuthorization("ReservationAccess")
            .WithSummary("Registrar pago de reserva")
            .WithDescription("Registra un pago sobre una reserva existente y actualiza su estado si corresponde.")
            .Accepts<CreateReservationPaymentRequestDto>("application/json")
            .Produces<CreateReservationPaymentResponseDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateReservationRequestDto? request,
        ClaimsPrincipal principal,
        CreateReservationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        if (request.Passenger is null)
        {
            throw new UserFriendlyException("El parametro 'passenger' es obligatorio.");
        }

        if (request.RoomTypeId.HasValue && request.RoomTypeId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' debe ser un numero entero positivo.");
        }

        if (request.RoomId.HasValue && request.RoomId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomId' debe ser un numero entero positivo.");
        }

        var parsedCheckIn = ParseDate(request.CheckIn, "checkIn");
        var parsedCheckOut = ParseDate(request.CheckOut, "checkOut");
        var parsedPassengerBirthDate = ParseDate(request.Passenger.BirthDate, "passenger.birthDate");
        var requesterIsGuest = IsGuest(principal) && !IsInternalUser(principal);
        var requesterUserId = GetRequesterUserId(principal);

        if (requesterIsGuest && string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var command = new CreateReservationCommand(
            request.Passenger.DocumentType,
            request.Passenger.FirstName,
            request.Passenger.LastName,
            request.Passenger.DocumentNumber,
            parsedPassengerBirthDate,
            request.Passenger.Email,
            request.Passenger.Phone,
            parsedCheckIn,
            parsedCheckOut,
            request.Guests,
            request.RoomId,
            request.RoomTypeId,
            requesterUserId,
            requesterIsGuest);

        var response = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"/api/reservations/{response.ReservationId}", response);
    }

    private static async Task<IResult> GetByIdAsync(
        [FromRoute] string id,
        ClaimsPrincipal principal,
        GetReservationByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var parsedId = ParseRequiredInt(id, "id");
        var requesterIsGuest = IsGuest(principal) && !IsInternalUser(principal);
        var requesterUserId = GetRequesterUserId(principal);

        if (requesterIsGuest && string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var query = new GetReservationByIdQuery(parsedId, requesterUserId, requesterIsGuest);
        var response = await handler.HandleAsync(query, cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ListMineAsync(
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        ClaimsPrincipal principal,
        ReservationLifecycleService reservationLifecycleService,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var parsedFromDate = ParseOptionalDate(fromDate, "fromDate");
        var parsedToDate = ParseOptionalDate(toDate, "toDate");

        if (parsedFromDate.HasValue && parsedToDate.HasValue && parsedFromDate.Value > parsedToDate.Value)
        {
            throw new UserFriendlyException("El parametro 'fromDate' no puede ser mayor que 'toDate'.");
        }

        var requesterUserId = GetRequesterUserId(principal);
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        await reservationLifecycleService.CompleteExpiredReservationsAsync(cancellationToken);

        var reservationsQuery = dbContext.Reservations
            .AsNoTracking()
            .Where(entity => entity.Guest.UserId == requesterUserId);

        if (parsedFromDate.HasValue)
        {
            var fromDateTime = parsedFromDate.Value.ToDateTime(TimeOnly.MinValue);
            reservationsQuery = reservationsQuery.Where(entity => entity.CheckInDate >= fromDateTime);
        }

        if (parsedToDate.HasValue)
        {
            var toDateTimeExclusive = parsedToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            reservationsQuery = reservationsQuery.Where(entity => entity.CheckInDate < toDateTimeExclusive);
        }

        var reservations = await reservationsQuery
            .OrderByDescending(entity => entity.CheckInDate)
            .ThenByDescending(entity => entity.Id)
            .Select(entity => new
            {
                entity.Id,
                entity.RoomId,
                RoomNumber = entity.Room.Number,
                RoomTypeName = entity.Room.RoomType.Name,
                entity.CheckInDate,
                entity.CheckOutDate,
                entity.TotalPrice,
                Status = entity.Status.ToString(),
                entity.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var reservationIds = reservations.Select(entity => entity.Id).ToArray();

        var paidByReservationId = reservationIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await dbContext.Payments
                .AsNoTracking()
                .Where(entity => reservationIds.Contains(entity.ReservationId) && entity.Status == PaymentStatus.Paid)
                .GroupBy(entity => entity.ReservationId)
                .Select(group => new
                {
                    ReservationId = group.Key,
                    TotalPaid = group.Sum(payment => payment.Amount)
                })
                .ToDictionaryAsync(entity => entity.ReservationId, entity => entity.TotalPaid, cancellationToken);

        var response = reservations
            .Select(entity =>
            {
                var checkIn = DateOnly.FromDateTime(entity.CheckInDate);
                var checkOut = DateOnly.FromDateTime(entity.CheckOutDate);
                var nights = Math.Max(checkOut.DayNumber - checkIn.DayNumber, 0);
                var totalPaid = paidByReservationId.GetValueOrDefault(entity.Id, 0m);

                return new GuestReservationListItemDto(
                    entity.Id,
                    entity.RoomId,
                    entity.RoomNumber,
                    entity.RoomTypeName,
                    checkIn,
                    checkOut,
                    nights,
                    entity.TotalPrice,
                    totalPaid,
                    Math.Max(entity.TotalPrice - totalPaid, 0m),
                    entity.Status,
                    entity.CreatedAt);
            })
            .ToList();

        return TypedResults.Ok((IReadOnlyList<GuestReservationListItemDto>)response);
    }

    private static async Task<IResult> UpdateAsync(
        [FromRoute] string id,
        [FromBody] UpdateReservationRequestDto? request,
        UpdateReservationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var reservationId = ParseRequiredInt(id, "id");
        var parsedCheckIn = ParseDate(request.CheckIn, "checkIn");
        var parsedCheckOut = ParseDate(request.CheckOut, "checkOut");

        if (request.RoomId <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomId' debe ser un numero entero positivo.");
        }

        var command = new UpdateReservationCommand(
            reservationId,
            parsedCheckIn,
            parsedCheckOut,
            request.Guests,
            request.RoomId);

        var response = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CancelAsync(
        [FromRoute] string id,
        CancelReservationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var reservationId = ParseRequiredInt(id, "id");
        var command = new CancelReservationCommand(reservationId);
        var response = await handler.HandleAsync(command, cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CreatePaymentAsync(
        [FromRoute] string id,
        [FromBody] CreateReservationPaymentRequestDto? request,
        ClaimsPrincipal principal,
        CreateReservationPaymentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var reservationId = ParseRequiredInt(id, "id");
        var requesterIsGuest = IsGuest(principal) && !IsInternalUser(principal);
        var requesterUserId = GetRequesterUserId(principal);

        if (requesterIsGuest && string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var command = new CreateReservationPaymentCommand(
            reservationId,
            request.Amount,
            request.CardHolderName,
            requesterUserId,
            requesterIsGuest);
        var response = await handler.HandleAsync(command, cancellationToken);

        return TypedResults.Created($"/api/reservations/{reservationId}/payments/{response.PaymentId}", response);
    }

    private static int ParseRequiredInt(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException($"El parametro '{parameterName}' es obligatorio y debe ser un numero entero positivo.");
        }

        if (!int.TryParse(value, out var parsedValue) || parsedValue <= 0)
        {
            throw new UserFriendlyException($"El parametro '{parameterName}' debe ser un numero entero positivo.");
        }

        return parsedValue;
    }

    private static DateOnly ParseDate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException($"El parametro '{parameterName}' es obligatorio y debe tener formato yyyy-MM-dd.");
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new UserFriendlyException($"El parametro '{parameterName}' debe tener formato yyyy-MM-dd.");
        }

        return date;
    }

    private static DateOnly? ParseOptionalDate(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new UserFriendlyException($"El parametro '{parameterName}' debe tener formato yyyy-MM-dd.");
        }

        return date;
    }

    private static string? GetRequesterUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static bool IsGuest(ClaimsPrincipal principal)
    {
        return principal.IsInRole("Guest")
            || principal.Claims.Any(claim =>
                string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase)
                && string.Equals(claim.Value, "Guest", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInternalUser(ClaimsPrincipal principal)
    {
        return principal.IsInRole("Staff")
            || principal.IsInRole("Admin")
            || principal.Claims.Any(claim =>
                string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(claim.Value, "Staff", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase)));
    }
}
