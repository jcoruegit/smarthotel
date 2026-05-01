using Microsoft.AspNetCore.Identity;
using SmartHotel.Domain.Entities;

namespace SmartHotel.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Optional profile fields. Credentials remain managed by Identity.
    public string? FullName { get; set; }
    public Guest? Guest { get; set; }
    public Employee? Employee { get; set; }
}
