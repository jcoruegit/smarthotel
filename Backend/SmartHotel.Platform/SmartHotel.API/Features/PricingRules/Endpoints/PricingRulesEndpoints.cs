using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.PricingRules.Dto;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.PricingRules.Endpoints;

public static class PricingRulesEndpoints
{
    public static IEndpointRouteBuilder MapPricingRulesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/pricing-rules")
            .WithTags("Pricing Rules")
            .RequireAuthorization("StaffOrAdmin");

        group.MapGet(string.Empty, ListAsync)
            .WithName("GetPricingRules")
            .WithSummary("Listar reglas de precio")
            .WithDescription("Lista reglas de precio dinamico con filtros opcionales por rango de fechas y tipo de habitacion.")
            .Produces<IReadOnlyList<PricingRuleResponseDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id}", GetByIdAsync)
            .WithName("GetPricingRuleById")
            .WithSummary("Obtener regla de precio por id")
            .WithDescription("Obtiene una regla de precio dinamico por identificador.")
            .Produces<PricingRuleResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost(string.Empty, CreateAsync)
            .WithName("CreatePricingRule")
            .WithSummary("Crear regla de precio")
            .WithDescription("Crea una regla de precio dinamico para un tipo de habitacion y fecha.")
            .Accepts<PricingRuleRequestDto>("application/json")
            .Produces<PricingRuleResponseDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut("/{id}", UpdateAsync)
            .WithName("UpdatePricingRule")
            .WithSummary("Actualizar regla de precio")
            .WithDescription("Actualiza una regla de precio dinamico existente.")
            .Accepts<PricingRuleRequestDto>("application/json")
            .Produces<PricingRuleResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{id}", DeleteAsync)
            .WithName("DeletePricingRule")
            .WithSummary("Eliminar regla de precio")
            .WithDescription("Elimina una regla de precio dinamico.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? roomTypeId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var fromDate = ParseOptionalDate(from, "from");
        var toDate = ParseOptionalDate(to, "to");

        if (roomTypeId.HasValue && roomTypeId.Value <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' debe ser un numero entero positivo.");
        }

        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            throw new UserFriendlyException("El parametro 'to' debe ser mayor o igual a 'from'.");
        }

        var query = dbContext.PricingRules
            .AsNoTracking()
            .Include(rule => rule.RoomType)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var fromDateTime = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(rule => rule.Date >= fromDateTime);
        }

        if (toDate.HasValue)
        {
            var toDateTimeExclusive = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(rule => rule.Date < toDateTimeExclusive);
        }

        if (roomTypeId.HasValue)
        {
            query = query.Where(rule => rule.RoomTypeId == roomTypeId.Value);
        }

        var rules = await query
            .OrderBy(rule => rule.Date)
            .ThenBy(rule => rule.RoomType.Name)
            .Select(rule => new PricingRuleResponseDto(
                rule.Id,
                rule.RoomTypeId,
                rule.RoomType.Name,
                DateOnly.FromDateTime(rule.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rule.Price,
                rule.Reason))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(rules);
    }

    private static async Task<IResult> GetByIdAsync(
        [FromRoute] string id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var parsedId = ParseRequiredInt(id, "id");

        var rule = await dbContext.PricingRules
            .AsNoTracking()
            .Include(entity => entity.RoomType)
            .SingleOrDefaultAsync(entity => entity.Id == parsedId, cancellationToken);

        if (rule is null)
        {
            throw new UserFriendlyException("No encontramos la regla de precio indicada.", StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(MapToResponse(rule));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] PricingRuleRequestDto? request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var parsedDate = ParseRequiredDate(request.Date, "date");
        var normalizedReason = NormalizeReason(request.Reason);

        if (request.RoomTypeId <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' debe ser un numero entero positivo.");
        }

        if (request.Price <= 0)
        {
            throw new UserFriendlyException("El parametro 'price' debe ser mayor a cero.");
        }

        var roomType = await dbContext.RoomTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.RoomTypeId, cancellationToken);

        if (roomType is null)
        {
            throw new UserFriendlyException("No encontramos el tipo de habitacion indicado.", StatusCodes.Status404NotFound);
        }

        var dateTime = parsedDate.ToDateTime(TimeOnly.MinValue);

        var exists = await dbContext.PricingRules
            .AsNoTracking()
            .AnyAsync(entity => entity.RoomTypeId == request.RoomTypeId && entity.Date == dateTime, cancellationToken);

        if (exists)
        {
            throw new UserFriendlyException(
                "Ya existe una regla de precio para ese tipo de habitacion y fecha.",
                StatusCodes.Status409Conflict);
        }

        var rule = new PricingRule
        {
            RoomTypeId = request.RoomTypeId,
            Date = dateTime,
            Price = request.Price,
            Reason = normalizedReason
        };

        dbContext.PricingRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new PricingRuleResponseDto(
            rule.Id,
            rule.RoomTypeId,
            roomType.Name,
            parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            rule.Price,
            rule.Reason);

        return TypedResults.Created($"/api/pricing-rules/{rule.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        [FromRoute] string id,
        [FromBody] PricingRuleRequestDto? request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var parsedId = ParseRequiredInt(id, "id");
        var parsedDate = ParseRequiredDate(request.Date, "date");
        var normalizedReason = NormalizeReason(request.Reason);

        if (request.RoomTypeId <= 0)
        {
            throw new UserFriendlyException("El parametro 'roomTypeId' debe ser un numero entero positivo.");
        }

        if (request.Price <= 0)
        {
            throw new UserFriendlyException("El parametro 'price' debe ser mayor a cero.");
        }

        var rule = await dbContext.PricingRules
            .SingleOrDefaultAsync(entity => entity.Id == parsedId, cancellationToken);

        if (rule is null)
        {
            throw new UserFriendlyException("No encontramos la regla de precio indicada.", StatusCodes.Status404NotFound);
        }

        var roomType = await dbContext.RoomTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.RoomTypeId, cancellationToken);

        if (roomType is null)
        {
            throw new UserFriendlyException("No encontramos el tipo de habitacion indicado.", StatusCodes.Status404NotFound);
        }

        var dateTime = parsedDate.ToDateTime(TimeOnly.MinValue);

        var duplicatedRule = await dbContext.PricingRules
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.Id != rule.Id
                && entity.RoomTypeId == request.RoomTypeId
                && entity.Date == dateTime,
                cancellationToken);

        if (duplicatedRule)
        {
            throw new UserFriendlyException(
                "Ya existe una regla de precio para ese tipo de habitacion y fecha.",
                StatusCodes.Status409Conflict);
        }

        rule.RoomTypeId = request.RoomTypeId;
        rule.Date = dateTime;
        rule.Price = request.Price;
        rule.Reason = normalizedReason;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new PricingRuleResponseDto(
            rule.Id,
            rule.RoomTypeId,
            roomType.Name,
            parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            rule.Price,
            rule.Reason);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> DeleteAsync(
        [FromRoute] string id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var parsedId = ParseRequiredInt(id, "id");

        var rule = await dbContext.PricingRules
            .SingleOrDefaultAsync(entity => entity.Id == parsedId, cancellationToken);

        if (rule is null)
        {
            throw new UserFriendlyException("No encontramos la regla de precio indicada.", StatusCodes.Status404NotFound);
        }

        dbContext.PricingRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static PricingRuleResponseDto MapToResponse(PricingRule rule)
    {
        return new PricingRuleResponseDto(
            rule.Id,
            rule.RoomTypeId,
            rule.RoomType.Name,
            DateOnly.FromDateTime(rule.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            rule.Price,
            rule.Reason);
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

    private static DateOnly ParseRequiredDate(string value, string parameterName)
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

    private static string NormalizeReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException("El parametro 'reason' es obligatorio.");
        }

        return value.Trim();
    }
}
