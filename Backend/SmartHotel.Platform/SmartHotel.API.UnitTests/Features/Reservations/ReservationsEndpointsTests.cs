using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.Features.Reservations.Endpoints;
using SmartHotel.API.Features.Reservations.Handler;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.API.Features.Reservations.Validator;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.UnitTests.Features.Reservations;

public sealed class ReservationsEndpointsTests
{
    [Theory]
    [MemberData(nameof(CreateReservationCases))]
    public async Task HandleAsync_ShouldHandleCreateReservationSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedCreateReservationDataAsync(dbContext, scenario);

        var handler = CreateCreateReservationHandler(dbContext);
        var (request, principal) = BuildCreateReservationInput(scenario);

        var operation = InvokeEndpointAsync(
            "HandleAsync",
            request,
            principal,
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
        var payload = Assert.IsType<CreateReservationResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        Assert.True(payload.ReservationId > 0);
        Assert.Equal(ReservationStatus.Pending.ToString(), payload.Status);
        Assert.Equal(1, await dbContext.Reservations.CountAsync());
    }

    public static IEnumerable<object?[]> CreateReservationCases()
    {
        yield return ["success", false, null, null];
        yield return ["error_missing_passenger", true, "passenger", StatusCodes.Status400BadRequest];
        yield return ["error_invalid_room_type_id", true, "roomTypeId", StatusCodes.Status400BadRequest];
        yield return ["error_invalid_document_number", true, "al menos 7 digitos", StatusCodes.Status400BadRequest];
        yield return ["error_guest_without_sub", true, "claim 'sub'", StatusCodes.Status401Unauthorized];
        yield return ["error_no_availability", true, "No encontramos habitaciones disponibles", StatusCodes.Status409Conflict];
    }

    [Theory]
    [MemberData(nameof(GetByIdCases))]
    public async Task GetByIdAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedGetByIdDataAsync(dbContext);

        var handler = CreateGetByIdHandler(dbContext);
        var (id, principal) = BuildGetByIdInput(scenario);

        var operation = InvokeEndpointAsync(
            "GetByIdAsync",
            id,
            principal,
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
        var payload = Assert.IsType<GetReservationByIdResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(1, payload.ReservationId);
        Assert.Equal(2, payload.Payments.Count);
        Assert.Equal(100m, payload.TotalPaid);
    }

    public static IEnumerable<object?[]> GetByIdCases()
    {
        yield return ["success", false, null, null];
        yield return ["error_invalid_id", true, "id", StatusCodes.Status400BadRequest];
        yield return ["error_guest_without_sub", true, "claim 'sub'", StatusCodes.Status401Unauthorized];
        yield return ["error_not_found", true, "reserva indicada", StatusCodes.Status404NotFound];
    }

    [Theory]
    [MemberData(nameof(ListMineCases))]
    public async Task ListMineAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode,
        int expectedCount)
    {
        await using var dbContext = CreateContext();
        await SeedListMineDataAsync(dbContext);

        var reservationLifecycleService = new ReservationLifecycleService(dbContext, NullLogger<ReservationLifecycleService>.Instance);
        var (fromDate, toDate, principal) = BuildListMineInput(scenario);

        var operation = InvokeEndpointAsync(
            "ListMineAsync",
            fromDate,
            toDate,
            principal,
            reservationLifecycleService,
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
        var payload = Assert.IsAssignableFrom<IReadOnlyList<GuestReservationListItemDto>>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(expectedCount, payload.Count);

        if (payload.Count > 0)
        {
            Assert.All(payload, item => Assert.True(item.ReservationId == 1 || item.ReservationId == 2));
        }
    }

    public static IEnumerable<object?[]> ListMineCases()
    {
        yield return ["success_all", false, null, null, 2];
        yield return ["success_filtered", false, null, null, 1];
        yield return ["error_invalid_range", true, "fromDate", StatusCodes.Status400BadRequest, 0];
        yield return ["error_guest_without_sub", true, "claim 'sub'", StatusCodes.Status401Unauthorized, 0];
    }

    [Theory]
    [MemberData(nameof(UpdateCases))]
    public async Task UpdateAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedUpdateDataAsync(dbContext);

        var handler = CreateUpdateHandler(dbContext);
        var (id, request) = BuildUpdateInput(scenario);

        var operation = InvokeEndpointAsync(
            "UpdateAsync",
            id,
            request,
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
        var payload = Assert.IsType<UpdateReservationResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(2, payload.RoomId);
        Assert.Equal("Suite", payload.RoomTypeName);
    }

    public static IEnumerable<object?[]> UpdateCases()
    {
        yield return ["success", false, null, null];
        yield return ["error_null_request", true, "cuerpo", StatusCodes.Status400BadRequest];
        yield return ["error_invalid_room_id", true, "roomId", StatusCodes.Status400BadRequest];
        yield return ["error_not_found", true, "reserva indicada", StatusCodes.Status404NotFound];
    }

    [Theory]
    [MemberData(nameof(CancelCases))]
    public async Task CancelAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedCancelDataAsync(dbContext, scenario);

        var handler = CreateCancelHandler(dbContext);
        var id = scenario switch
        {
            "error_invalid_id" => "abc",
            "error_not_found" => "999",
            _ => "1"
        };

        var operation = InvokeEndpointAsync(
            "CancelAsync",
            id,
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
        var payload = Assert.IsType<CancelReservationResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        Assert.Equal(ReservationStatus.Cancelled.ToString(), payload.Status);

        var reservation = await dbContext.Reservations.SingleAsync(entity => entity.Id == 1);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    public static IEnumerable<object?[]> CancelCases()
    {
        yield return ["success", false, null, null];
        yield return ["error_invalid_id", true, "id", StatusCodes.Status400BadRequest];
        yield return ["error_not_found", true, "reserva indicada", StatusCodes.Status404NotFound];
        yield return ["error_already_cancelled", true, "ya se encuentra cancelada", StatusCodes.Status409Conflict];
    }

    [Theory]
    [MemberData(nameof(CreatePaymentCases))]
    public async Task CreatePaymentAsync_ShouldHandleSuccessAndErrorCases(
        string scenario,
        bool shouldThrow,
        string? expectedErrorFragment,
        int? expectedStatusCode)
    {
        await using var dbContext = CreateContext();
        await SeedPaymentDataAsync(dbContext);

        var handler = CreatePaymentHandler(dbContext);
        var (id, request, principal) = BuildPaymentInput(scenario);

        var operation = InvokeEndpointAsync(
            "CreatePaymentAsync",
            id,
            request,
            principal,
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
        var payload = Assert.IsType<CreateReservationPaymentResponseDto>(valueResult.Value);

        Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        Assert.Equal(PaymentStatus.Paid.ToString(), payload.PaymentStatus);
        Assert.True(payload.IsFullyPaid);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), payload.ReservationStatus);
    }

    public static IEnumerable<object?[]> CreatePaymentCases()
    {
        yield return ["success", false, null, null];
        yield return ["error_guest_without_sub", true, "claim 'sub'", StatusCodes.Status401Unauthorized];
        yield return ["error_invalid_amount", true, "amount", StatusCodes.Status400BadRequest];
        yield return ["error_missing_card_holder", true, "cardHolderName", StatusCodes.Status400BadRequest];
        yield return ["error_card_holder_mismatch", true, "titular de la tarjeta debe coincidir", StatusCodes.Status400BadRequest];
        yield return ["error_forbidden_by_ownership", true, "No tenes permisos", StatusCodes.Status403Forbidden];
        yield return ["error_overpayment", true, "supera el saldo pendiente", StatusCodes.Status400BadRequest];
    }

    [Theory]
    [MemberData(nameof(AuthorizationCases))]
    public void MapReservationsEndpoints_ShouldConfigureExpectedPolicies(
        string httpMethod,
        string route,
        string[] expectedPolicies)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        builder.Services.AddScoped<ReservationPricingService>();
        builder.Services.AddScoped<ReservationLifecycleService>();
        builder.Services.AddScoped<ReservationCommandValidator>();
        builder.Services.AddScoped<UpdateReservationCommandValidator>();
        builder.Services.AddScoped<ReservationPaymentCommandValidator>();
        builder.Services.AddScoped<CreateReservationCommandHandler>();
        builder.Services.AddScoped<GetReservationByIdQueryHandler>();
        builder.Services.AddScoped<UpdateReservationCommandHandler>();
        builder.Services.AddScoped<CancelReservationCommandHandler>();
        builder.Services.AddScoped<CreateReservationPaymentCommandHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ReservationAccess", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole("Staff", "Admin"));
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        });

        var app = builder.Build();
        app.MapReservationsEndpoints();

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
        foreach (var expectedPolicy in expectedPolicies)
        {
            Assert.Contains(authorizeMetadata, item => string.Equals(item.Policy, expectedPolicy, StringComparison.Ordinal));
        }
    }

    public static IEnumerable<object[]> AuthorizationCases()
    {
        yield return ["POST", "/api/reservations", new[] { "ReservationAccess" }];
        yield return ["GET", "/api/reservations/{id}", new[] { "ReservationAccess" }];
        yield return ["GET", "/api/reservations/mine", new[] { "ReservationAccess", "GuestOnly" }];
        yield return ["PUT", "/api/reservations/{id}", new[] { "ReservationAccess", "StaffOrAdmin" }];
        yield return ["DELETE", "/api/reservations/{id}", new[] { "ReservationAccess", "AdminOnly" }];
        yield return ["POST", "/api/reservations/{id}/payments", new[] { "ReservationAccess" }];
    }

    private static (CreateReservationRequestDto? Request, ClaimsPrincipal Principal) BuildCreateReservationInput(string scenario)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new CreateReservationRequestDto(
            scenario == "error_missing_passenger"
                ? null!
                : new PassengerInputDto(
                    "DNI",
                    "Juan",
                    "Perez",
                    scenario == "error_invalid_document_number" ? "12345A" : "12345678",
                    "1990-01-01",
                    "juan@example.com",
                    "123"),
            today.AddDays(7).ToString("yyyy-MM-dd"),
            today.AddDays(9).ToString("yyyy-MM-dd"),
            2,
            null,
            scenario == "error_invalid_room_type_id" ? 0 : null);

        var principal = scenario == "error_guest_without_sub"
            ? CreatePrincipal(null, "Guest")
            : CreatePrincipal("staff-1", "Staff");

        return (request, principal);
    }

    private static (string Id, ClaimsPrincipal Principal) BuildGetByIdInput(string scenario)
    {
        return scenario switch
        {
            "success" => ("1", CreatePrincipal("staff-1", "Staff")),
            "error_invalid_id" => ("abc", CreatePrincipal("staff-1", "Staff")),
            "error_guest_without_sub" => ("1", CreatePrincipal(null, "Guest")),
            "error_not_found" => ("999", CreatePrincipal("staff-1", "Staff")),
            _ => throw new InvalidOperationException($"Escenario no soportado: {scenario}")
        };
    }

    private static (string? FromDate, string? ToDate, ClaimsPrincipal Principal) BuildListMineInput(string scenario)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return scenario switch
        {
            "success_all" => (null, null, CreatePrincipal("guest-owner", "Guest")),
            "success_filtered" => (
                today.AddDays(5).ToString("yyyy-MM-dd"),
                today.AddDays(5).ToString("yyyy-MM-dd"),
                CreatePrincipal("guest-owner", "Guest")),
            "error_invalid_range" => (
                today.AddDays(10).ToString("yyyy-MM-dd"),
                today.AddDays(1).ToString("yyyy-MM-dd"),
                CreatePrincipal("guest-owner", "Guest")),
            "error_guest_without_sub" => (null, null, CreatePrincipal(null, "Guest")),
            _ => throw new InvalidOperationException($"Escenario no soportado: {scenario}")
        };
    }

    private static (string Id, UpdateReservationRequestDto? Request) BuildUpdateInput(string scenario)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return scenario switch
        {
            "success" => ("1", new UpdateReservationRequestDto(
                today.AddDays(10).ToString("yyyy-MM-dd"),
                today.AddDays(12).ToString("yyyy-MM-dd"),
                2,
                2)),
            "error_null_request" => ("1", null),
            "error_invalid_room_id" => ("1", new UpdateReservationRequestDto(
                today.AddDays(10).ToString("yyyy-MM-dd"),
                today.AddDays(12).ToString("yyyy-MM-dd"),
                2,
                0)),
            "error_not_found" => ("999", new UpdateReservationRequestDto(
                today.AddDays(10).ToString("yyyy-MM-dd"),
                today.AddDays(12).ToString("yyyy-MM-dd"),
                2,
                2)),
            _ => throw new InvalidOperationException($"Escenario no soportado: {scenario}")
        };
    }

    private static (string Id, CreateReservationPaymentRequestDto? Request, ClaimsPrincipal Principal) BuildPaymentInput(string scenario)
    {
        return scenario switch
        {
            "success" => ("1", new CreateReservationPaymentRequestDto(300m, "Juan Perez"), CreatePrincipal("guest-1", "Guest")),
            "error_guest_without_sub" => ("1", new CreateReservationPaymentRequestDto(100m, "Juan Perez"), CreatePrincipal(null, "Guest")),
            "error_invalid_amount" => ("1", new CreateReservationPaymentRequestDto(0m, "Juan Perez"), CreatePrincipal("guest-1", "Guest")),
            "error_missing_card_holder" => ("1", new CreateReservationPaymentRequestDto(100m, "   "), CreatePrincipal("guest-1", "Guest")),
            "error_card_holder_mismatch" => ("1", new CreateReservationPaymentRequestDto(100m, "Nombre Distinto"), CreatePrincipal("guest-1", "Guest")),
            "error_forbidden_by_ownership" => ("1", new CreateReservationPaymentRequestDto(100m, "Juan Perez"), CreatePrincipal("guest-2", "Guest")),
            "error_overpayment" => ("1", new CreateReservationPaymentRequestDto(500m, "Juan Perez"), CreatePrincipal("guest-1", "Guest")),
            _ => throw new InvalidOperationException($"Escenario no soportado: {scenario}")
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string? userId, params string[] roles)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, authenticationType: "Tests");
        return new ClaimsPrincipal(identity);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static CreateReservationCommandHandler CreateCreateReservationHandler(AppDbContext dbContext)
    {
        return new CreateReservationCommandHandler(
            dbContext,
            new ReservationPricingService(dbContext),
            new ReservationLifecycleService(dbContext, NullLogger<ReservationLifecycleService>.Instance),
            new ReservationCommandValidator(),
            NullLogger<CreateReservationCommandHandler>.Instance);
    }

    private static GetReservationByIdQueryHandler CreateGetByIdHandler(AppDbContext dbContext)
    {
        return new GetReservationByIdQueryHandler(
            dbContext,
            new ReservationLifecycleService(dbContext, NullLogger<ReservationLifecycleService>.Instance),
            NullLogger<GetReservationByIdQueryHandler>.Instance);
    }

    private static UpdateReservationCommandHandler CreateUpdateHandler(AppDbContext dbContext)
    {
        return new UpdateReservationCommandHandler(
            dbContext,
            new ReservationPricingService(dbContext),
            new ReservationLifecycleService(dbContext, NullLogger<ReservationLifecycleService>.Instance),
            new UpdateReservationCommandValidator(),
            NullLogger<UpdateReservationCommandHandler>.Instance);
    }

    private static CancelReservationCommandHandler CreateCancelHandler(AppDbContext dbContext)
    {
        return new CancelReservationCommandHandler(
            dbContext,
            new ReservationLifecycleService(dbContext, NullLogger<ReservationLifecycleService>.Instance),
            NullLogger<CancelReservationCommandHandler>.Instance);
    }

    private static CreateReservationPaymentCommandHandler CreatePaymentHandler(AppDbContext dbContext)
    {
        return new CreateReservationPaymentCommandHandler(
            dbContext,
            new ReservationLifecycleService(dbContext, NullLogger<ReservationLifecycleService>.Instance),
            new ReservationPaymentCommandValidator(),
            NullLogger<CreateReservationPaymentCommandHandler>.Instance);
    }

    private static async Task SeedCreateReservationDataAsync(AppDbContext dbContext, string scenario)
    {
        var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var room = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = 1,
            RoomType = roomType
        };
        var documentType = new DocumentType { Id = 1, Name = "DNI" };

        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.Add(room);
        dbContext.DocumentTypes.Add(documentType);

        if (string.Equals(scenario, "error_no_availability", StringComparison.Ordinal))
        {
            var guest = new Guest
            {
                Id = 1,
                DocumentTypeId = 1,
                DocumentType = documentType,
                FirstName = "Otro",
                LastName = "Huesped",
                DocumentNumber = "87654321",
                BirthDate = new DateTime(1990, 1, 1)
            };

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            dbContext.Guests.Add(guest);
            dbContext.Reservations.Add(new Reservation
            {
                Id = 1,
                GuestId = guest.Id,
                Guest = guest,
                RoomId = room.Id,
                Room = room,
                CheckInDate = today.AddDays(7).ToDateTime(TimeOnly.MinValue),
                CheckOutDate = today.AddDays(9).ToDateTime(TimeOnly.MinValue),
                TotalPrice = 200m,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedGetByIdDataAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            UserId = "guest-1",
            DocumentTypeId = 1,
            DocumentType = documentType,
            FirstName = "Juan",
            LastName = "Perez",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };

        var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var room = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = 1,
            RoomType = roomType
        };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var reservation = new Reservation
        {
            Id = 1,
            GuestId = guest.Id,
            Guest = guest,
            RoomId = room.Id,
            Room = room,
            CheckInDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
            CheckOutDate = today.AddDays(8).ToDateTime(TimeOnly.MinValue),
            TotalPrice = 300m,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.DocumentTypes.Add(documentType);
        dbContext.Guests.Add(guest);
        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.Add(room);
        dbContext.Reservations.Add(reservation);
        dbContext.Payments.AddRange(
            new Payment
            {
                Id = 1,
                ReservationId = reservation.Id,
                Reservation = reservation,
                Amount = 100m,
                Status = PaymentStatus.Paid,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Payment
            {
                Id = 2,
                ReservationId = reservation.Id,
                Reservation = reservation,
                Amount = 50m,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUpdateDataAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            DocumentTypeId = 1,
            DocumentType = documentType,
            FirstName = "Juan",
            LastName = "Perez",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 200m };
        var room101 = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = 1,
            RoomType = standard
        };
        var room201 = new Room
        {
            Id = 2,
            Number = "201",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 1 cama king",
            RoomTypeId = 2,
            RoomType = suite
        };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        dbContext.DocumentTypes.Add(documentType);
        dbContext.Guests.Add(guest);
        dbContext.RoomTypes.AddRange(standard, suite);
        dbContext.Rooms.AddRange(room101, room201);
        dbContext.Reservations.Add(new Reservation
        {
            Id = 1,
            GuestId = guest.Id,
            Guest = guest,
            RoomId = room101.Id,
            Room = room101,
            CheckInDate = today.AddDays(8).ToDateTime(TimeOnly.MinValue),
            CheckOutDate = today.AddDays(10).ToDateTime(TimeOnly.MinValue),
            TotalPrice = 200m,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedListMineDataAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var ownerGuest = new Guest
        {
            Id = 1,
            UserId = "guest-owner",
            DocumentTypeId = 1,
            DocumentType = documentType,
            FirstName = "Owner",
            LastName = "Guest",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var otherGuest = new Guest
        {
            Id = 2,
            UserId = "guest-other",
            DocumentTypeId = 1,
            DocumentType = documentType,
            FirstName = "Other",
            LastName = "Guest",
            DocumentNumber = "87654321",
            BirthDate = new DateTime(1990, 1, 1)
        };

        var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var room = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = 1,
            RoomType = roomType
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        dbContext.DocumentTypes.Add(documentType);
        dbContext.Guests.AddRange(ownerGuest, otherGuest);
        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.Add(room);
        dbContext.Reservations.AddRange(
            new Reservation
            {
                Id = 1,
                GuestId = ownerGuest.Id,
                Guest = ownerGuest,
                RoomId = room.Id,
                Room = room,
                CheckInDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
                CheckOutDate = today.AddDays(7).ToDateTime(TimeOnly.MinValue),
                TotalPrice = 200m,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Reservation
            {
                Id = 2,
                GuestId = ownerGuest.Id,
                Guest = ownerGuest,
                RoomId = room.Id,
                Room = room,
                CheckInDate = today.AddDays(12).ToDateTime(TimeOnly.MinValue),
                CheckOutDate = today.AddDays(14).ToDateTime(TimeOnly.MinValue),
                TotalPrice = 220m,
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Reservation
            {
                Id = 3,
                GuestId = otherGuest.Id,
                Guest = otherGuest,
                RoomId = room.Id,
                Room = room,
                CheckInDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
                CheckOutDate = today.AddDays(6).ToDateTime(TimeOnly.MinValue),
                TotalPrice = 100m,
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        dbContext.Payments.AddRange(
            new Payment
            {
                Id = 1,
                ReservationId = 1,
                Amount = 50m,
                Status = PaymentStatus.Paid,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Payment
            {
                Id = 2,
                ReservationId = 2,
                Amount = 20m,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCancelDataAsync(AppDbContext dbContext, string scenario)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            DocumentTypeId = 1,
            DocumentType = documentType,
            FirstName = "Juan",
            LastName = "Perez",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var room = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = roomType.Id,
            RoomType = roomType
        };
        var status = scenario == "error_already_cancelled"
            ? ReservationStatus.Cancelled
            : ReservationStatus.Pending;

        dbContext.DocumentTypes.Add(documentType);
        dbContext.Guests.Add(guest);
        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.Add(room);
        dbContext.Reservations.Add(new Reservation
        {
            Id = 1,
            GuestId = guest.Id,
            Guest = guest,
            RoomId = room.Id,
            Room = room,
            CheckInDate = DateTime.UtcNow.AddDays(4),
            CheckOutDate = DateTime.UtcNow.AddDays(6),
            TotalPrice = 200m,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedPaymentDataAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            UserId = "guest-1",
            DocumentTypeId = 1,
            DocumentType = documentType,
            FirstName = "Juan",
            LastName = "Perez",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var roomType = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var room = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = roomType.Id,
            RoomType = roomType
        };

        dbContext.DocumentTypes.Add(documentType);
        dbContext.Guests.Add(guest);
        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.Add(room);
        dbContext.Reservations.Add(new Reservation
        {
            Id = 1,
            GuestId = guest.Id,
            Guest = guest,
            RoomId = room.Id,
            Room = room,
            CheckInDate = DateTime.UtcNow.AddDays(6),
            CheckOutDate = DateTime.UtcNow.AddDays(8),
            TotalPrice = 300m,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<IResult> InvokeEndpointAsync(string methodName, params object?[] arguments)
    {
        var method = typeof(ReservationsEndpoints).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontro el metodo {methodName}.");

        var invocationResult = method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"La invocacion de {methodName} no devolvio resultado.");

        var task = Assert.IsAssignableFrom<Task<IResult>>(invocationResult);
        return await task;
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
