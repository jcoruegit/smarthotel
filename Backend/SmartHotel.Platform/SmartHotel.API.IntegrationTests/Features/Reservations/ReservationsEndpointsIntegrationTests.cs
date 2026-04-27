using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartHotel.API.Features.Reservations.Dto;
using SmartHotel.API.IntegrationTests.Infrastructure;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.IntegrationTests.Features.Reservations;

public sealed class ReservationsEndpointsIntegrationTests
{
    [Fact]
    public async Task CreateReservation_ShouldReturnCreated_WhenRequestIsValid()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, dbContext => SeedCreateReservationBaseAsync(dbContext));

        using var client = CreateAuthenticatedClient(factory, "guest-create", "Guest");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new CreateReservationRequestDto(
            new PassengerInputDto("DNI", "Juan", "Perez", "12345678", "1990-01-01", "juan@example.com", "123"),
            today.AddDays(7).ToString("yyyy-MM-dd"),
            today.AddDays(9).ToString("yyyy-MM-dd"),
            2,
            null,
            null);

        var response = await client.PostAsJsonAsync("/api/reservations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateReservationResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(ReservationStatus.Pending.ToString(), payload.Status);
    }

    [Fact]
    public async Task CreateReservation_ShouldReturnConflict_WhenNoRoomIsAvailable()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, dbContext => SeedCreateReservationBaseAsync(dbContext, reserveRoom: true));

        using var client = CreateAuthenticatedClient(factory, "guest-create", "Guest");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new CreateReservationRequestDto(
            new PassengerInputDto("DNI", "Juan", "Perez", "12345678", "1990-01-01", "juan@example.com", "123"),
            today.AddDays(7).ToString("yyyy-MM-dd"),
            today.AddDays(9).ToString("yyyy-MM-dd"),
            2,
            null,
            null);

        var response = await client.PostAsJsonAsync("/api/reservations", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("No encontramos habitaciones disponibles", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReservationById_ShouldReturnOk_ForGuestOwner()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedGetReservationByIdBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var response = await client.GetAsync("/api/reservations/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GetReservationByIdResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.ReservationId);
        Assert.Equal(100m, payload.TotalPaid);
    }

    [Fact]
    public async Task GetReservationById_ShouldReturnForbidden_ForGuestNotOwner()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedGetReservationByIdBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-other", "Guest");
        var response = await client.GetAsync("/api/reservations/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListMyReservations_ShouldReturnOnlyOwnerReservations()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedListMineBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var response = await client.GetAsync("/api/reservations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<GuestReservationListItemDto>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.All(payload, item => Assert.True(item.ReservationId == 1 || item.ReservationId == 2));
    }

    [Fact]
    public async Task ListMyReservations_ShouldApplyDateFilters()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedListMineBaseAsync);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var filter = today.AddDays(5).ToString("yyyy-MM-dd");

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var response = await client.GetAsync($"/api/reservations/mine?fromDate={filter}&toDate={filter}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<GuestReservationListItemDto>>();
        Assert.NotNull(payload);
        Assert.Single(payload);
        Assert.Equal(1, payload[0].ReservationId);
    }

    [Fact]
    public async Task UpdateReservation_ShouldReturnOk_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedUpdateReservationBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new UpdateReservationRequestDto(
            today.AddDays(10).ToString("yyyy-MM-dd"),
            today.AddDays(12).ToString("yyyy-MM-dd"),
            2,
            2);

        var response = await client.PutAsJsonAsync("/api/reservations/1", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<UpdateReservationResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.RoomId);
    }

    [Fact]
    public async Task UpdateReservation_ShouldReturnForbidden_ForGuest()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedUpdateReservationBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new UpdateReservationRequestDto(
            today.AddDays(10).ToString("yyyy-MM-dd"),
            today.AddDays(12).ToString("yyyy-MM-dd"),
            2,
            2);

        var response = await client.PutAsJsonAsync("/api/reservations/1", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelReservation_ShouldReturnOk_ForAdmin()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedCancelReservationBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "admin-user", "Admin");
        var response = await client.DeleteAsync("/api/reservations/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CancelReservationResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(ReservationStatus.Cancelled.ToString(), payload.Status);
    }

    [Fact]
    public async Task CancelReservation_ShouldReturnForbidden_ForStaff()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedCancelReservationBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "staff-user", "Staff");
        var response = await client.DeleteAsync("/api/reservations/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_ShouldReturnCreated_AndConfirmReservation_WhenFullyPaid()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPaymentBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var response = await client.PostAsJsonAsync("/api/reservations/1/payments", new CreateReservationPaymentRequestDto(300m, "Juan Perez"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateReservationPaymentResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(PaymentStatus.Paid.ToString(), payload.PaymentStatus);
        Assert.True(payload.IsFullyPaid);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), payload.ReservationStatus);
    }

    [Fact]
    public async Task CreatePayment_ShouldReturnForbidden_ForGuestNotOwner()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPaymentBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-other", "Guest");
        var response = await client.PostAsJsonAsync("/api/reservations/1/payments", new CreateReservationPaymentRequestDto(100m, "Juan Perez"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_ShouldReturnBadRequest_WhenAmountExceedsRemainingBalance()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPaymentBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var response = await client.PostAsJsonAsync("/api/reservations/1/payments", new CreateReservationPaymentRequestDto(500m, "Juan Perez"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("saldo pendiente", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePayment_ShouldReturnBadRequest_WhenCardHolderDiffersFromGuestName()
    {
        using var factory = new ApiWebApplicationFactory();
        await ResetDatabaseAsync(factory, SeedPaymentBaseAsync);

        using var client = CreateAuthenticatedClient(factory, "guest-owner", "Guest");
        var response = await client.PostAsJsonAsync("/api/reservations/1/payments", new CreateReservationPaymentRequestDto(100m, "Nombre Distinto"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("titular de la tarjeta debe coincidir", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateAuthenticatedClient(ApiWebApplicationFactory factory, string userId, params string[] roles)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var token = CreateJwtToken(
            userId,
            ApiWebApplicationFactory.TestJwtKey,
            ApiWebApplicationFactory.TestJwtIssuer,
            ApiWebApplicationFactory.TestJwtAudience,
            roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static string CreateJwtToken(string userId, string key, string issuer, string audience, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task ResetDatabaseAsync(ApiWebApplicationFactory factory, Func<AppDbContext, Task> seedAction)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        await seedAction(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCreateReservationBaseAsync(AppDbContext dbContext, bool reserveRoom = false)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
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
        dbContext.RoomTypes.Add(roomType);
        dbContext.Rooms.Add(room);

        if (reserveRoom)
        {
            var guest = new Guest
            {
                Id = 1,
                UserId = "guest-existing",
                DocumentTypeId = documentType.Id,
                DocumentType = documentType,
                FirstName = "Ana",
                LastName = "Gomez",
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

        await Task.CompletedTask;
    }

    private static async Task SeedGetReservationByIdBaseAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            UserId = "guest-owner",
            DocumentTypeId = documentType.Id,
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var reservation = new Reservation
        {
            Id = 1,
            GuestId = guest.Id,
            Guest = guest,
            RoomId = room.Id,
            Room = room,
            CheckInDate = today.AddDays(6).ToDateTime(TimeOnly.MinValue),
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

        await Task.CompletedTask;
    }

    private static async Task SeedUpdateReservationBaseAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            UserId = "guest-owner",
            DocumentTypeId = documentType.Id,
            DocumentType = documentType,
            FirstName = "Juan",
            LastName = "Perez",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };

        var standard = new RoomType { Id = 1, Name = "Standard", BasePrice = 100m };
        var suite = new RoomType { Id = 2, Name = "Suite", BasePrice = 220m };
        var room101 = new Room
        {
            Id = 1,
            Number = "101",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 2 camas",
            RoomTypeId = standard.Id,
            RoomType = standard
        };
        var room201 = new Room
        {
            Id = 2,
            Number = "201",
            Capacity = 2,
            Features = "Refrigerador | TV por cable | 1 cama king",
            RoomTypeId = suite.Id,
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

        await Task.CompletedTask;
    }

    private static async Task SeedListMineBaseAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var ownerGuest = new Guest
        {
            Id = 1,
            UserId = "guest-owner",
            DocumentTypeId = documentType.Id,
            DocumentType = documentType,
            FirstName = "Juan",
            LastName = "Owner",
            DocumentNumber = "12345678",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var otherGuest = new Guest
        {
            Id = 2,
            UserId = "guest-other",
            DocumentTypeId = documentType.Id,
            DocumentType = documentType,
            FirstName = "Ana",
            LastName = "Other",
            DocumentNumber = "87654321",
            BirthDate = new DateTime(1991, 1, 1)
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
                Status = ReservationStatus.Pending,
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
                Status = ReservationStatus.Confirmed,
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
                TotalPrice = 120m,
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await Task.CompletedTask;
    }

    private static async Task SeedCancelReservationBaseAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            DocumentTypeId = documentType.Id,
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
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            TotalPrice = 300m,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private static async Task SeedPaymentBaseAsync(AppDbContext dbContext)
    {
        var documentType = new DocumentType { Id = 1, Name = "DNI" };
        var guest = new Guest
        {
            Id = 1,
            UserId = "guest-owner",
            DocumentTypeId = documentType.Id,
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

        await Task.CompletedTask;
    }
}
