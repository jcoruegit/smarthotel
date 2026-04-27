using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.PricingRules.Dto;
using SmartHotel.API.Features.PricingRules.Endpoints;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.UnitTests.Features.PricingRules;

public sealed class PricingRulesEndpointsTests
{
    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task ListAsync_ShouldHandleSuccessAndErrorCases(
        string _,
        string? from,
        string? to,
        int? roomTypeId,
        bool shouldThrow,
        int expectedCount,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedListDataAsync(dbContext);

        var operation = InvokeEndpointAsync(
            "ListAsync",
            from,
            to,
            roomTypeId,
            dbContext,
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
        var payload = Assert.IsType<List<PricingRuleResponseDto>>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(expectedCount, payload.Count);
    }

    public static IEnumerable<object?[]> ListCases()
    {
        yield return ["success_without_filters", null, null, null, false, 3, null, null];
        yield return ["success_with_filters", "2026-05-02", "2026-05-03", 1, false, 1, null, null];
        yield return ["error_invalid_room_type_id", null, null, 0, true, 0, "roomTypeId", StatusCodes.Status400BadRequest];
        yield return ["error_invalid_date_range", "2026-05-04", "2026-05-03", null, true, 0, "to", StatusCodes.Status400BadRequest];
        yield return ["error_invalid_from_format", "05-04-2026", null, null, true, 0, "from", StatusCodes.Status400BadRequest];
    }

    [Theory]
    [MemberData(nameof(CreateCases))]
    public async Task CreateAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        PricingRuleRequestDto? request,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedCreateDataAsync(dbContext, scenario);

        var operation = InvokeEndpointAsync(
            "CreateAsync",
            request,
            dbContext,
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
        var payload = Assert.IsType<PricingRuleResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        Assert.Equal(1, payload.RoomTypeId);
        Assert.Equal("2026-05-10", payload.Date);
        Assert.Equal(150m, payload.Price);
        Assert.Equal("Alta demanda", payload.Reason);

        var createdRule = await dbContext.PricingRules
            .SingleAsync(rule => rule.RoomTypeId == 1 && rule.Date == new DateTime(2026, 5, 10));

        Assert.Equal(150m, createdRule.Price);
        Assert.Equal("Alta demanda", createdRule.Reason);
    }

    public static IEnumerable<object?[]> CreateCases()
    {
        yield return
        [
            "success",
            new PricingRuleRequestDto(1, "2026-05-10", 150m, "  Alta demanda  "),
            false,
            null,
            null
        ];

        yield return ["error_null_request", null, true, "cuerpo", StatusCodes.Status400BadRequest];

        yield return
        [
            "error_invalid_price",
            new PricingRuleRequestDto(1, "2026-05-10", 0m, "Alta demanda"),
            true,
            "price",
            StatusCodes.Status400BadRequest
        ];

        yield return
        [
            "error_room_type_not_found",
            new PricingRuleRequestDto(999, "2026-05-10", 150m, "Alta demanda"),
            true,
            "tipo de habitacion",
            StatusCodes.Status404NotFound
        ];

        yield return
        [
            "error_duplicated_rule",
            new PricingRuleRequestDto(1, "2026-05-10", 150m, "Alta demanda"),
            true,
            "Ya existe una regla de precio",
            StatusCodes.Status409Conflict
        ];
    }

    [Theory]
    [MemberData(nameof(GetByIdCases))]
    public async Task GetByIdAsync_ShouldHandleSuccessAndErrorCases(
        string _,
        string id,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedGetByIdDataAsync(dbContext);

        var operation = InvokeEndpointAsync(
            "GetByIdAsync",
            id,
            dbContext,
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
        var payload = Assert.IsType<PricingRuleResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(1, payload.Id);
        Assert.Equal("Standard", payload.RoomTypeName);
    }

    public static IEnumerable<object?[]> GetByIdCases()
    {
        yield return ["success", "1", false, null, null];
        yield return ["error_invalid_id", "abc", true, "id", StatusCodes.Status400BadRequest];
        yield return ["error_not_found", "999", true, "regla de precio indicada", StatusCodes.Status404NotFound];
    }

    [Theory]
    [MemberData(nameof(UpdateCases))]
    public async Task UpdateAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        string id,
        PricingRuleRequestDto? request,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedUpdateDataAsync(dbContext, scenario);

        var operation = InvokeEndpointAsync(
            "UpdateAsync",
            id,
            request,
            dbContext,
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
        var payload = Assert.IsType<PricingRuleResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(1, payload.Id);
        Assert.Equal(2, payload.RoomTypeId);
        Assert.Equal("Suite", payload.RoomTypeName);
        Assert.Equal("2026-05-20", payload.Date);
        Assert.Equal(260m, payload.Price);
        Assert.Equal("Temporada alta", payload.Reason);
    }

    public static IEnumerable<object?[]> UpdateCases()
    {
        yield return
        [
            "success",
            "1",
            new PricingRuleRequestDto(2, "2026-05-20", 260m, " Temporada alta "),
            false,
            null,
            null
        ];

        yield return ["error_null_request", "1", null, true, "cuerpo", StatusCodes.Status400BadRequest];
        yield return ["error_invalid_id", "abc", new PricingRuleRequestDto(1, "2026-05-20", 260m, "X"), true, "id", StatusCodes.Status400BadRequest];
        yield return ["error_rule_not_found", "999", new PricingRuleRequestDto(1, "2026-05-20", 260m, "X"), true, "regla de precio indicada", StatusCodes.Status404NotFound];
        yield return ["error_room_type_not_found", "1", new PricingRuleRequestDto(999, "2026-05-20", 260m, "X"), true, "tipo de habitacion", StatusCodes.Status404NotFound];
        yield return ["error_duplicated_rule", "1", new PricingRuleRequestDto(2, "2026-05-21", 260m, "X"), true, "Ya existe una regla de precio", StatusCodes.Status409Conflict];
    }

    [Theory]
    [MemberData(nameof(DeleteCases))]
    public async Task DeleteAsync_ShouldHandleSuccessAndErrorCases(
        string _,
        string id,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedDeleteDataAsync(dbContext);

        var rulesBefore = await dbContext.PricingRules.CountAsync();

        var operation = InvokeEndpointAsync(
            "DeleteAsync",
            id,
            dbContext,
            CancellationToken.None);

        if (shouldThrow)
        {
            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => operation);
            Assert.Contains(expectedErrorFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.Equal(rulesBefore, await dbContext.PricingRules.CountAsync());
            return;
        }

        var result = await operation;
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, statusResult.StatusCode);
        Assert.Equal(rulesBefore - 1, await dbContext.PricingRules.CountAsync());
    }

    public static IEnumerable<object?[]> DeleteCases()
    {
        yield return ["success", "1", false, null, null];
        yield return ["error_invalid_id", "abc", true, "id", StatusCodes.Status400BadRequest];
        yield return ["error_not_found", "999", true, "regla de precio indicada", StatusCodes.Status404NotFound];
    }

    [Theory]
    [MemberData(nameof(AuthorizationCases))]
    public void MapPricingRulesEndpoints_ShouldRequireStaffOrAdminPolicy(string httpMethod, string route)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole("Staff", "Admin")));

        var app = builder.Build();
        app.MapPricingRulesEndpoints();

        var allRouteEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var matchingEndpoints = allRouteEndpoints
            .Where(endpoint => HasHttpMethod(endpoint, httpMethod) && RouteMatches(endpoint, route))
            .ToList();

        Assert.True(
            matchingEndpoints.Count > 0,
            $"No se encontro endpoint para {httpMethod} {route}. Endpoints disponibles: {string.Join(", ", allRouteEndpoints.Select(FormatEndpoint))}");

        var endpoint = Assert.Single(matchingEndpoints);
        var authorizeMetadata = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();

        Assert.NotEmpty(authorizeMetadata);
        Assert.Contains(authorizeMetadata, item => string.Equals(item.Policy, "StaffOrAdmin", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> AuthorizationCases()
    {
        yield return ["GET", "/api/pricing-rules"];
        yield return ["POST", "/api/pricing-rules"];
        yield return ["GET", "/api/pricing-rules/{id}"];
        yield return ["PUT", "/api/pricing-rules/{id}"];
        yield return ["DELETE", "/api/pricing-rules/{id}"];
    }

    private static async Task<IResult> InvokeEndpointAsync(string methodName, params object?[] arguments)
    {
        var method = typeof(PricingRulesEndpoints).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
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

    private static async Task SeedListDataAsync(AppDbContext dbContext)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 200m };

        dbContext.RoomTypes.AddRange(standard, suite);
        dbContext.PricingRules.AddRange(
            new PricingRule { Id = 1, RoomTypeId = 1, RoomType = standard, Date = new DateTime(2026, 5, 1), Price = 110m, Reason = "Evento" },
            new PricingRule { Id = 2, RoomTypeId = 1, RoomType = standard, Date = new DateTime(2026, 5, 2), Price = 120m, Reason = "Fin de semana" },
            new PricingRule { Id = 3, RoomTypeId = 2, RoomType = suite, Date = new DateTime(2026, 5, 3), Price = 220m, Reason = "Demanda" });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCreateDataAsync(AppDbContext dbContext, string scenario)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        dbContext.RoomTypes.Add(standard);

        if (string.Equals(scenario, "error_duplicated_rule", StringComparison.Ordinal))
        {
            dbContext.PricingRules.Add(new PricingRule
            {
                Id = 1,
                RoomTypeId = 1,
                RoomType = standard,
                Date = new DateTime(2026, 5, 10),
                Price = 140m,
                Reason = "Regla existente"
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedGetByIdDataAsync(AppDbContext dbContext)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        dbContext.RoomTypes.Add(standard);
        dbContext.PricingRules.Add(new PricingRule
        {
            Id = 1,
            RoomTypeId = standard.Id,
            RoomType = standard,
            Date = new DateTime(2026, 5, 1),
            Price = 110m,
            Reason = "Evento"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUpdateDataAsync(AppDbContext dbContext, string scenario)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 200m };

        dbContext.RoomTypes.AddRange(standard, suite);
        dbContext.PricingRules.Add(new PricingRule
        {
            Id = 1,
            RoomTypeId = 1,
            RoomType = standard,
            Date = new DateTime(2026, 5, 20),
            Price = 150m,
            Reason = "Base"
        });

        if (string.Equals(scenario, "error_duplicated_rule", StringComparison.Ordinal))
        {
            dbContext.PricingRules.Add(new PricingRule
            {
                Id = 2,
                RoomTypeId = 2,
                RoomType = suite,
                Date = new DateTime(2026, 5, 21),
                Price = 240m,
                Reason = "Duplicada objetivo"
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDeleteDataAsync(AppDbContext dbContext)
    {
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        dbContext.RoomTypes.Add(standard);
        dbContext.PricingRules.Add(new PricingRule
        {
            Id = 1,
            RoomTypeId = standard.Id,
            RoomType = standard,
            Date = new DateTime(2026, 6, 1),
            Price = 130m,
            Reason = "Temporada"
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
