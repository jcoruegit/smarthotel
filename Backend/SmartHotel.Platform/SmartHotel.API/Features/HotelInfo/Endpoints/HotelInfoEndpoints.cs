using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Features.HotelInfo.Dto;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.HotelInfo.Endpoints;

public static class HotelInfoEndpoints
{
    public static IEndpointRouteBuilder MapHotelInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/hotel-info")
            .WithTags("Hotel Info")
            .AllowAnonymous();

        group.MapGet("/amenities", GetAmenitiesAsync)
            .WithName("GetHotelAmenities")
            .WithSummary("Listar servicios del hotel")
            .WithDescription("Devuelve los servicios activos del hotel con horario, costo y si requieren reserva.")
            .Produces<IReadOnlyList<HotelAmenityDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/policies", GetPoliciesAsync)
            .WithName("GetHotelPolicies")
            .WithSummary("Listar politicas del hotel")
            .WithDescription("Devuelve las politicas activas del hotel por categoria.")
            .Produces<IReadOnlyList<HotelPolicyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/schedules", GetSchedulesAsync)
            .WithName("GetHotelSchedules")
            .WithSummary("Listar horarios del hotel")
            .WithDescription("Devuelve horarios activos como check-in, check-out y desayuno.")
            .Produces<IReadOnlyList<HotelScheduleDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> GetAmenitiesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var amenities = await dbContext.HotelAmenities
            .AsNoTracking()
            .Where(amenity => amenity.IsActive)
            .OrderBy(amenity => amenity.DisplayOrder)
            .ThenBy(amenity => amenity.Name)
            .Select(amenity => new HotelAmenityDto(
                amenity.Id,
                amenity.Name,
                amenity.Description,
                FormatTime(amenity.AvailableFrom),
                FormatTime(amenity.AvailableTo),
                amenity.DaysOfWeek,
                amenity.IsComplimentary,
                amenity.Price,
                amenity.Currency,
                amenity.RequiresReservation))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok((IReadOnlyList<HotelAmenityDto>)amenities);
    }

    private static async Task<IResult> GetPoliciesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var policies = await dbContext.HotelPolicies
            .AsNoTracking()
            .Where(policy => policy.IsActive)
            .OrderBy(policy => policy.DisplayOrder)
            .ThenBy(policy => policy.Title)
            .Select(policy => new HotelPolicyDto(
                policy.Id,
                policy.Code,
                policy.Title,
                policy.Description,
                policy.Category))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok((IReadOnlyList<HotelPolicyDto>)policies);
    }

    private static async Task<IResult> GetSchedulesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var schedules = await dbContext.HotelSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsActive)
            .OrderBy(schedule => schedule.DisplayOrder)
            .ThenBy(schedule => schedule.Title)
            .Select(schedule => new HotelScheduleDto(
                schedule.Id,
                schedule.Code,
                schedule.Title,
                FormatTime(schedule.StartTime),
                FormatTime(schedule.EndTime),
                schedule.Notes,
                schedule.DaysOfWeek,
                FormatDate(schedule.ValidFrom),
                FormatDate(schedule.ValidTo)))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok((IReadOnlyList<HotelScheduleDto>)schedules);
    }

    private static string? FormatTime(TimeOnly? value)
    {
        return value?.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string? FormatDate(DateOnly? value)
    {
        return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
