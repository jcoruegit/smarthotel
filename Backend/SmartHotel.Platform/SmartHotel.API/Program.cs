using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartHotel.API.Common.Auth;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Common.OpenApi;
using SmartHotel.API.Features.Auth.Endpoints;
using SmartHotel.API.Features.Availability.Endpoints;
using SmartHotel.API.Features.Availability.Handler;
using SmartHotel.API.Features.Availability.Validator;
using SmartHotel.API.Features.Chat.Endpoints;
using SmartHotel.API.Features.Chat.Services;
using SmartHotel.API.Features.HotelInfo.Endpoints;
using SmartHotel.API.Features.Pricing.Services;
using SmartHotel.API.Features.PricingRules.Endpoints;
using SmartHotel.API.Features.Reservations.Endpoints;
using SmartHotel.API.Features.Reservations.Handler;
using SmartHotel.API.Features.Reservations.Services;
using SmartHotel.API.Features.Reservations.Validator;
using SmartHotel.Infrastructure.Identity;
using SmartHotel.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DBConnection")
    ?? throw new InvalidOperationException("Connection string 'DBConnection' no configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwtOptions = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException("La seccion 'Jwt' no esta configurada.");
var requireConfirmedEmail = builder.Configuration.GetValue("Identity:RequireConfirmedEmail", false);
var maxFailedAccessAttempts = builder.Configuration.GetValue("Identity:Lockout:MaxFailedAccessAttempts", 5);
var defaultLockoutMinutes = builder.Configuration.GetValue("Identity:Lockout:DefaultLockoutMinutes", 15);
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    throw new InvalidOperationException("JWT key no configurada en 'Jwt:Key'.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
{
    throw new InvalidOperationException("JWT issuer no configurado en 'Jwt:Issuer'.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException("JWT audience no configurado en 'Jwt:Audience'.");
}

if (jwtOptions.ExpiresMinutes <= 0)
{
    throw new InvalidOperationException("JWT expires minutes debe ser mayor a cero en 'Jwt:ExpiresMinutes'.");
}

if (allowedCorsOrigins.Length == 0)
{
    throw new InvalidOperationException("CORS no configurado. Defini al menos un origen en 'Cors:AllowedOrigins'.");
}

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 3;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(defaultLockoutMinutes);

    options.SignIn.RequireConfirmedEmail = requireConfirmedEmail;
})
    .AddErrorDescriber<SpanishIdentityErrorDescriber>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("RestrictedCors", policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type");
    });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GuestOnly", policy => policy.RequireRole("Guest"));
    options.AddPolicy("StaffOrAdmin", policy => policy.RequireRole("Staff", "Admin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ReservationAccess", policy => policy.RequireRole("Guest", "Staff", "Admin"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.OperationFilter<AuthorizationOperationFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<AvailabilityQueryValidator>();
builder.Services.AddScoped<GetAvailabilityQueryHandler>();
builder.Services.AddScoped<ChatResponseService>();
builder.Services.AddScoped<ReservationPricingService>();
builder.Services.AddScoped<ReservationLifecycleService>();
builder.Services.AddScoped<ReservationCommandValidator>();
builder.Services.AddScoped<CreateReservationCommandHandler>();
builder.Services.AddScoped<GetReservationByIdQueryHandler>();
builder.Services.AddScoped<UpdateReservationCommandValidator>();
builder.Services.AddScoped<UpdateReservationCommandHandler>();
builder.Services.AddScoped<CancelReservationCommandHandler>();
builder.Services.AddScoped<ReservationPaymentCommandValidator>();
builder.Services.AddScoped<CreateReservationPaymentCommandHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateAndSeedDevelopmentDataAsync();
}

await app.Services.SeedIdentityDataAsync(builder.Configuration);
await app.Services.SeedCatalogDataAsync();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("RestrictedCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapAvailabilityEndpoints();
app.MapChatEndpoints();
app.MapHotelInfoEndpoints();
app.MapReservationsEndpoints();
app.MapPricingRulesEndpoints();

app.Run();

public partial class Program;
