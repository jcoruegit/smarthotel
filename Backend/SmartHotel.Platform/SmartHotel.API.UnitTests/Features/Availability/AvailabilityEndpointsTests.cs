using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Availability.Dto;
using SmartHotel.API.Features.Availability.Endpoints;
using SmartHotel.API.Features.Availability.Handler;
using SmartHotel.API.Features.Availability.Validator;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.UnitTests.Features.Availability;

public sealed class AvailabilityEndpointsTests
{
    [Theory]
    [MemberData(nameof(HandleCases))]
    public async Task HandleAsync_ShouldHandleSuccessAndErrorCases(
        string _,
        string checkIn,
        string checkOut,
        int guests,
        int? roomTypeId,
        bool shouldThrow,
        int expectedRoomsCount,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedHandleDataAsync(dbContext);

        var handler = CreateHandler(dbContext);
        var operation = InvokeEndpointAsync(
            "HandleAsync",
            checkIn,
            checkOut,
            guests,
            roomTypeId,
            handler,
            CancellationToken.None);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<AvailabilityResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(expectedRoomsCount, payload.Rooms.Count);
    }

    public static IEnumerable<object?[]> HandleCases()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var checkIn = today.AddDays(6).ToString("yyyy-MM-dd");
        var checkOut = today.AddDays(8).ToString("yyyy-MM-dd");

        yield return
        [
            "success",
            checkIn,
            checkOut,
            2,
            null,
            false,
            2,
            null,
            null
        ];

        yield return
        [
            "error_invalid_check_in_format",
            "10/05/2026",
            checkOut,
            2,
            null,
            true,
            0,
            "checkIn",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "error_invalid_room_type_id",
            checkIn,
            checkOut,
            2,
            0,
            true,
            0,
            "roomTypeId",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "error_invalid_guests",
            checkIn,
            checkOut,
            0,
            null,
            true,
            0,
            "guests",
            StatusCodes.Status400BadRequest
        ];
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public async Task GetRoomTypesAsync_ShouldReturnCatalog(bool seedRoomTypes, int expectedCount)
    {
        await using var dbContext = CreateContext();

        if (seedRoomTypes)
        {
            dbContext.RoomTypes.AddRange(
                new RoomType { Id = 1, Name = "Standard", BasePrice = 100m },
                new RoomType { Id = 2, Name = "Suite", BasePrice = 200m });

            await dbContext.SaveChangesAsync();
        }

        var result = await InvokeEndpointAsync(
            "GetRoomTypesAsync",
            dbContext,
            CancellationToken.None);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var payload = Assert.IsType<List<RoomTypeOptionDto>>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(expectedCount, payload.Count);

        if (seedRoomTypes)
        {
            Assert.Equal("Standard", payload[0].Name);
            Assert.Equal("Suite", payload[1].Name);
        }
    }

    [Fact]
    public async Task GetRoomTypesAsync_ShouldThrowWhenCancellationRequested()
    {
        await using var dbContext = CreateContext();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InvokeEndpointAsync("GetRoomTypesAsync", dbContext, cancellationSource.Token));
    }

    [Theory]
    [MemberData(nameof(AvailabilityPublicRoutes))]
    public void MapAvailabilityEndpoints_ShouldAllowAnonymous(string httpMethod, string route)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        builder.Services.AddScoped<AvailabilityQueryValidator>();
        builder.Services.AddScoped<ReservationPricingService>();
        builder.Services.AddScoped<ReservationLifecycleService>();
        builder.Services.AddScoped<GetAvailabilityQueryHandler>();
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.MapAvailabilityEndpoints();

        var allRouteEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var matchingEndpoints = allRouteEndpoints
            .Where(endpoint => HasHttpMethod(endpoint, httpMethod) && RouteMatches(endpoint, route))
            .ToList();

        Assert.True(
            matchingEndpoints.Count > 0,
            $"No se encontro endpoint para {httpMethod} {route}. Endpoints disponibles: {string.Join(", ", allRouteEndpoints.Select(FormatEndpoint))}");

        var endpoint = Assert.Single(matchingEndpoints);
        var allowAnonymousMetadata = endpoint.Metadata.GetOrderedMetadata<IAllowAnonymous>();

        Assert.NotEmpty(allowAnonymousMetadata);
    }

    public static IEnumerable<object[]> AvailabilityPublicRoutes()
    {
        yield return ["GET", "/api/availability"];
        yield return ["GET", "/api/availability/room-types"];
    }

    private static async Task<IResult> InvokeEndpointAsync(string methodName, params object?[] arguments)
    {
        var method = typeof(AvailabilityEndpoints).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontro el metodo {methodName}.");

        var invocationResult = method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"La invocacion de {methodName} no devolvio resultado.");

        var task = Assert.IsAssignableFrom<Task<IResult>>(invocationResult);
        return await task;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static GetAvailabilityQueryHandler CreateHandler(AppDbContext dbContext)
    {
        var pricingService = new ReservationPricingService(dbContext);
        var reservationLifecycleService = new ReservationLifecycleService(
            dbContext,
            NullLogger<ReservationLifecycleService>.Instance);
        var validator = new AvailabilityQueryValidator();

        return new GetAvailabilityQueryHandler(
            dbContext,
            pricingService,
            reservationLifecycleService,
            validator);
    }

    private static async Task SeedHandleDataAsync(AppDbContext dbContext)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 200m };

        dbContext.RoomTypes.AddRange(standard, suite);
        dbContext.Rooms.AddRange(
            new Room
            {
                Id = 1,
                Number = "101",
                Capacity = 2,
                Features = "Refrigerador | TV por cable | 2 camas",
                RoomTypeId = standard.Id,
                RoomType = standard
            },
            new Room
            {
                Id = 2,
                Number = "201",
                Capacity = 4,
                Features = "Refrigerador | TV por cable | 1 cama king",
                RoomTypeId = suite.Id,
                RoomType = suite
            });

        await dbContext.SaveChangesAsync();
    }

    private static bool HasHttpMethod(RouteEndpoint endpoint, string method)
    {
        var metadata = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
        return metadata?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool RouteMatches(RouteEndpoint endpoint, string expectedRoute)
    {
        return string.Equals(
            CanonicalizeRoute(GetRouteTemplate(endpoint)),
            CanonicalizeRoute(expectedRoute),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalizeRoute(string route)
    {
        var segments = NormalizeRoute(route)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.StartsWith('{') && segment.EndsWith('}') ? "{}" : segment.ToLowerInvariant());

        return "/" + string.Join("/", segments);
    }

    private static string NormalizeRoute(string? route)
    {
        return "/" + (route ?? string.Empty).Trim('/');
    }

    private static string GetRouteTemplate(RouteEndpoint endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
        {
            return endpoint.RoutePattern.RawText!;
        }

        var segments = endpoint.RoutePattern.PathSegments
            .Select(pathSegment => string.Concat(pathSegment.Parts.Select(GetRoutePartText)));

        return "/" + string.Join("/", segments);
    }

    private static string GetRoutePartText(RoutePatternPart part)
    {
        return part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart parameter => $"{{{parameter.Name}}}",
            _ => string.Empty
        };
    }

    private static string FormatEndpoint(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        var methodsText = methods.Count == 0 ? "*" : string.Join("|", methods);
        return $"{methodsText} {NormalizeRoute(GetRouteTemplate(endpoint))}";
    }
}
