using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SmartHotel.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace SmartHotel.API.IntegrationTests.Infrastructure;

public sealed class ApiWebApplicationFactory(string? databaseName = null)
    : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "0123456789ABCDEF0123456789ABCDEF";
    public const string TestJwtIssuer = "SmartHotel.Tests";
    public const string TestJwtAudience = "SmartHotel.Tests.Clients";

    private static readonly ServiceProvider InMemoryEfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private readonly string _databaseName = databaseName ?? $"SmartHotelTests_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var testConfiguration = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = "Server=(localdb)\\mssqllocaldb;Database=IgnoredForTests;Trusted_Connection=True;",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:ExpiresMinutes"] = "60",
                ["Cors:AllowedOrigins:0"] = "https://localhost",
                ["Identity:RequireConfirmedEmail"] = "false"
            };

            configurationBuilder.AddInMemoryCollection(testConfiguration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            services.AddDbContext<AppDbContext>(options =>
                options
                    .UseInMemoryDatabase(_databaseName)
                    .UseInternalServiceProvider(InMemoryEfServiceProvider));

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestJwtIssuer,
                    ValidAudience = TestJwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey)),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }
}
