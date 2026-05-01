using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartHotel.Infrastructure.Persistence
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private const string DefaultConnectionString = "Server=localhost;Database=SmartHotel;Trusted_Connection=True;TrustServerCertificate=True;";

        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DBConnection");

            optionsBuilder.UseSqlServer(
                string.IsNullOrWhiteSpace(connectionString)
                    ? DefaultConnectionString
                    : connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
