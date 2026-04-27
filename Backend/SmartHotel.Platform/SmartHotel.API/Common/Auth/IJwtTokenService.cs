using SmartHotel.Infrastructure.Identity;

namespace SmartHotel.API.Common.Auth;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(ApplicationUser user, IEnumerable<string> roles);
}
