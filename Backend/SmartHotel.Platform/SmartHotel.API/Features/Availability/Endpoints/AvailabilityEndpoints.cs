using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Availability.Dto;
using SmartHotel.API.Features.Availability.Handler;
using SmartHotel.API.Features.Availability.Query;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Availability.Endpoints;

public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/availability")
            .WithTags("Availability")
            .AllowAnonymous();

        group.MapGet(string.Empty, HandleAsync)
            .WithName("GetAvailability")
            .WithSummary("Consultar disponibilidad")
            .WithDescription("Devuelve habitaciones disponibles segun fechas, cantidad de huespedes y tipo de habitacion, incluyendo caracteristicas.")
            .Produces<AvailabilityResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/room-types", GetRoomTypesAsync)
            .WithName("GetAvailabilityRoomTypes")
            .WithSummary("Listar tipos de habitacion")
            .WithDescription("Devuelve el catalogo de tipos de habitacion y precios base.")
            .Produces<IReadOnlyList<RoomTypeOptionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] string checkIn,
        [FromQuery] string checkOut,
        [FromQuery] int guests,
        [FromQuery] int? roomTypeId,
        GetAvailabilityQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var parsedCheckIn = ParseDate(checkIn, "checkIn");
        var parsedCheckOut = ParseDate(checkOut, "checkOut");
        var parsedRoomTypeId = roomTypeId;
        if (parsedRoomTypeId.HasValue && parsedRoomTypeId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' debe ser un numero entero positivo.");
        }

        var query = new GetAvailabilityQuery(parsedCheckIn, parsedCheckOut, guests, parsedRoomTypeId);
        var response = await handler.HandleAsync(query, cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetRoomTypesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var roomTypes = await dbContext.RoomTypes
            .AsNoTracking()
            .OrderBy(roomType => roomType.Name)
            .Select(roomType => new RoomTypeOptionDto(roomType.Id, roomType.Name, roomType.BasePrice))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(roomTypes);
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
}
