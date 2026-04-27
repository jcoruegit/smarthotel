using Microsoft.AspNetCore.Identity;

namespace SmartHotel.API.Common.Auth;

public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
        => new() { Code = nameof(DefaultError), Description = "Ocurrio un error inesperado." };

    public override IdentityError ConcurrencyFailure()
        => new()
        {
            Code = nameof(ConcurrencyFailure),
            Description = "El registro fue modificado por otro proceso. Intenta nuevamente."
        };

    public override IdentityError PasswordMismatch()
        => new() { Code = nameof(PasswordMismatch), Description = "La password es incorrecta." };

    public override IdentityError InvalidToken()
        => new() { Code = nameof(InvalidToken), Description = "El token es invalido." };

    public override IdentityError LoginAlreadyAssociated()
        => new() { Code = nameof(LoginAlreadyAssociated), Description = "Ese login ya esta asociado a otra cuenta." };

    public override IdentityError InvalidUserName(string? userName)
        => new() { Code = nameof(InvalidUserName), Description = $"El nombre de usuario '{userName}' es invalido." };

    public override IdentityError InvalidEmail(string? email)
        => new() { Code = nameof(InvalidEmail), Description = $"El email '{email}' es invalido." };

    public override IdentityError DuplicateUserName(string userName)
        => new() { Code = nameof(DuplicateUserName), Description = $"El nombre de usuario '{userName}' ya existe." };

    public override IdentityError DuplicateEmail(string email)
        => new() { Code = nameof(DuplicateEmail), Description = $"El email '{email}' ya existe." };

    public override IdentityError InvalidRoleName(string? role)
        => new() { Code = nameof(InvalidRoleName), Description = $"El nombre de rol '{role}' es invalido." };

    public override IdentityError DuplicateRoleName(string role)
        => new() { Code = nameof(DuplicateRoleName), Description = $"El rol '{role}' ya existe." };

    public override IdentityError UserAlreadyHasPassword()
        => new() { Code = nameof(UserAlreadyHasPassword), Description = "El usuario ya tiene una password configurada." };

    public override IdentityError UserLockoutNotEnabled()
        => new() { Code = nameof(UserLockoutNotEnabled), Description = "El lockout no esta habilitado para este usuario." };

    public override IdentityError UserAlreadyInRole(string role)
        => new() { Code = nameof(UserAlreadyInRole), Description = $"El usuario ya pertenece al rol '{role}'." };

    public override IdentityError UserNotInRole(string role)
        => new() { Code = nameof(UserNotInRole), Description = $"El usuario no pertenece al rol '{role}'." };

    public override IdentityError PasswordTooShort(int length)
        => new()
        {
            Code = nameof(PasswordTooShort),
            Description = $"La password debe tener al menos {length} caracteres."
        };

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => new()
        {
            Code = nameof(PasswordRequiresNonAlphanumeric),
            Description = "La password debe tener al menos un caracter no alfanumerico."
        };

    public override IdentityError PasswordRequiresDigit()
        => new()
        {
            Code = nameof(PasswordRequiresDigit),
            Description = "La password debe tener al menos un numero ('0'-'9')."
        };

    public override IdentityError PasswordRequiresLower()
        => new()
        {
            Code = nameof(PasswordRequiresLower),
            Description = "La password debe tener al menos una minuscula ('a'-'z')."
        };

    public override IdentityError PasswordRequiresUpper()
        => new()
        {
            Code = nameof(PasswordRequiresUpper),
            Description = "La password debe tener al menos una mayuscula ('A'-'Z')."
        };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => new()
        {
            Code = nameof(PasswordRequiresUniqueChars),
            Description = $"La password debe tener al menos {uniqueChars} caracteres unicos."
        };

    public override IdentityError RecoveryCodeRedemptionFailed()
        => new()
        {
            Code = nameof(RecoveryCodeRedemptionFailed),
            Description = "No se pudo canjear el codigo de recuperacion."
        };
}
