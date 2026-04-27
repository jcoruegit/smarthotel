using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Identity;

namespace SmartHotel.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Guest> Guests { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PricingRule> PricingRules { get; set; }
        public DbSet<HotelAmenity> HotelAmenities { get; set; }
        public DbSet<HotelPolicy> HotelPolicies { get; set; }
        public DbSet<HotelSchedule> HotelSchedules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Guest>(guestBuilder =>
            {
                guestBuilder.Property(guest => guest.UserId)
                    .HasMaxLength(450);

                guestBuilder.Property(guest => guest.DocumentNumber)
                    .HasMaxLength(8);

                guestBuilder.HasOne(guest => guest.DocumentType)
                    .WithMany()
                    .HasForeignKey(guest => guest.DocumentTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                guestBuilder.HasOne<ApplicationUser>()
                    .WithOne(user => user.Guest)
                    .HasForeignKey<Guest>(guest => guest.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                guestBuilder.HasIndex(guest => guest.UserId)
                    .IsUnique()
                    .HasFilter("[UserId] IS NOT NULL");

                guestBuilder.HasIndex(guest => new { guest.DocumentTypeId, guest.DocumentNumber })
                    .IsUnique();
            });

            modelBuilder.Entity<DocumentType>(documentTypeBuilder =>
            {
                documentTypeBuilder.Property(documentType => documentType.Name)
                    .HasMaxLength(30);

                documentTypeBuilder.HasIndex(documentType => documentType.Name)
                    .IsUnique();
            });

            modelBuilder.Entity<Room>(roomBuilder =>
            {
                roomBuilder.Property(room => room.Number)
                    .HasMaxLength(10);

                roomBuilder.Property(room => room.Features)
                    .HasMaxLength(600);
            });

            modelBuilder.Entity<RoomType>(roomTypeBuilder =>
            {
                roomTypeBuilder.Property(roomType => roomType.BasePrice)
                    .HasPrecision(18, 2);
            });

            modelBuilder.Entity<Reservation>(reservationBuilder =>
            {
                reservationBuilder.HasOne(reservation => reservation.Guest)
                    .WithMany()
                    .HasForeignKey(reservation => reservation.GuestId)
                    .OnDelete(DeleteBehavior.Restrict);

                reservationBuilder.Property(reservation => reservation.TotalPrice)
                    .HasPrecision(18, 2);
            });

            modelBuilder.Entity<Payment>(paymentBuilder =>
            {
                paymentBuilder.Property(payment => payment.Amount)
                    .HasPrecision(18, 2);
            });

            modelBuilder.Entity<PricingRule>(pricingRuleBuilder =>
            {
                pricingRuleBuilder.Property(pricingRule => pricingRule.Price)
                    .HasPrecision(18, 2);

                pricingRuleBuilder.HasOne(pricingRule => pricingRule.RoomType)
                    .WithMany()
                    .HasForeignKey(pricingRule => pricingRule.RoomTypeId)
                    .OnDelete(DeleteBehavior.Cascade);

                pricingRuleBuilder.HasIndex(pricingRule => new { pricingRule.RoomTypeId, pricingRule.Date });
            });

            modelBuilder.Entity<HotelAmenity>(amenityBuilder =>
            {
                amenityBuilder.Property(amenity => amenity.Name)
                    .HasMaxLength(100);

                amenityBuilder.Property(amenity => amenity.Description)
                    .HasMaxLength(1500);

                amenityBuilder.Property(amenity => amenity.DaysOfWeek)
                    .HasMaxLength(60);

                amenityBuilder.Property(amenity => amenity.Currency)
                    .HasMaxLength(3);

                amenityBuilder.Property(amenity => amenity.Price)
                    .HasPrecision(18, 2);

                amenityBuilder.Property(amenity => amenity.IsActive)
                    .HasDefaultValue(true);

                amenityBuilder.Property(amenity => amenity.RequiresReservation)
                    .HasDefaultValue(false);

                amenityBuilder.Property(amenity => amenity.DisplayOrder)
                    .HasDefaultValue(0);

                amenityBuilder.HasIndex(amenity => amenity.Name)
                    .IsUnique();
            });

            modelBuilder.Entity<HotelPolicy>(policyBuilder =>
            {
                policyBuilder.Property(policy => policy.Code)
                    .HasMaxLength(50);

                policyBuilder.Property(policy => policy.Title)
                    .HasMaxLength(120);

                policyBuilder.Property(policy => policy.Description)
                    .HasMaxLength(2000);

                policyBuilder.Property(policy => policy.Category)
                    .HasMaxLength(50);

                policyBuilder.Property(policy => policy.IsActive)
                    .HasDefaultValue(true);

                policyBuilder.Property(policy => policy.DisplayOrder)
                    .HasDefaultValue(0);

                policyBuilder.HasIndex(policy => policy.Code)
                    .IsUnique();
            });

            modelBuilder.Entity<HotelSchedule>(scheduleBuilder =>
            {
                scheduleBuilder.Property(schedule => schedule.Code)
                    .HasMaxLength(50);

                scheduleBuilder.Property(schedule => schedule.Title)
                    .HasMaxLength(120);

                scheduleBuilder.Property(schedule => schedule.Notes)
                    .HasMaxLength(500);

                scheduleBuilder.Property(schedule => schedule.DaysOfWeek)
                    .HasMaxLength(60);

                scheduleBuilder.Property(schedule => schedule.IsActive)
                    .HasDefaultValue(true);

                scheduleBuilder.Property(schedule => schedule.DisplayOrder)
                    .HasDefaultValue(0);

                scheduleBuilder.HasIndex(schedule => schedule.Code)
                    .IsUnique();
            });
        }
    }
}

