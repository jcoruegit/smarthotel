using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartHotel.API.Common.Auth;
using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Auth.Dto;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Identity;
using SmartHotel.Infrastructure.Persistence;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartHotel.API.Features.Auth.Endpoints;

public static class AuthEndpoints
{
    private static readonly HashSet<string> AllowedManagedRoles = ["Guest", "Staff", "Admin"];
    private const string EmployeeEmailDomain = "smarthotel.dev";
    private const string DefaultEmployeeTemporaryPassword = "Temp#Hotel2026";
    private const string GuestDemoEmailConfigKey = "IdentitySeed:GuestEmail";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Registrar usuario")
            .WithDescription("Crea un usuario nuevo y asigna el rol Guest por defecto.")
            .Accepts<RegisterRequestDto>("application/json")
            .Produces<RegisterResponseDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/document-types", ListDocumentTypesAsync)
            .WithName("ListDocumentTypes")
            .WithSummary("Listar tipos de documento")
            .WithDescription("Retorna los tipos de documento configurados para registro y reservas.")
            .Produces<IReadOnlyList<DocumentTypeOptionDto>>(StatusCodes.Status200OK);

        group.MapGet("/me/guest-profile", GetCurrentGuestProfileAsync)
            .WithName("GetCurrentGuestProfile")
            .RequireAuthorization()
            .WithSummary("Obtener perfil de huesped actual")
            .WithDescription("Retorna los datos de Guests vinculados al usuario autenticado.")
            .Produces<GuestProfileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/me/guest-profile", UpdateCurrentGuestProfileAsync)
            .WithName("UpdateCurrentGuestProfile")
            .RequireAuthorization()
            .WithSummary("Actualizar perfil de huesped actual")
            .WithDescription("Actualiza datos personales en AspNetUsers y Guests para el usuario autenticado.")
            .Accepts<UpdateGuestProfileRequestDto>("application/json")
            .Produces<GuestProfileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/me/employee-profile", GetCurrentEmployeeProfileAsync)
            .WithName("GetCurrentEmployeeProfile")
            .RequireAuthorization("StaffOrAdmin")
            .WithSummary("Obtener perfil de empleado actual")
            .WithDescription("Retorna los datos de Employees vinculados al usuario interno autenticado.")
            .Produces<EmployeeSelfProfileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me/employee-profile", UpdateCurrentEmployeeProfileAsync)
            .WithName("UpdateCurrentEmployeeProfile")
            .RequireAuthorization("StaffOrAdmin")
            .WithSummary("Actualizar perfil de empleado actual")
            .WithDescription("Actualiza datos personales en AspNetUsers y Employees para el usuario interno autenticado.")
            .Accepts<UpdateEmployeeSelfProfileRequestDto>("application/json")
            .Produces<EmployeeSelfProfileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Iniciar sesion")
            .WithDescription("Valida credenciales y retorna un JWT Bearer.")
            .Accepts<LoginRequestDto>("application/json")
            .Produces<LoginResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .RequireAuthorization()
            .WithSummary("Cambiar password")
            .WithDescription("Permite al usuario autenticado cambiar su password.")
            .Accepts<ChangePasswordRequestDto>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/users", ListUsersAsync)
            .WithName("ListUsers")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Listar empleados")
            .WithDescription("Operacion administrativa. Lista usuarios internos (Staff/Admin) y sus roles.")
            .Produces<IReadOnlyList<AuthUserRolesDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/employees", ListEmployeesAsync)
            .WithName("ListEmployees")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Consultar empleados")
            .WithDescription("Operacion administrativa. Lista empleados internos con filtros opcionales.")
            .Produces<IReadOnlyList<EmployeeListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/employees", CreateEmployeeAsync)
            .WithName("CreateEmployee")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Dar de alta empleado")
            .WithDescription("Operacion administrativa. Crea empleado interno y su usuario Identity asociado.")
            .Accepts<CreateEmployeeRequestDto>("application/json")
            .Produces<CreateEmployeeResponseDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/employees/{id:int}", GetEmployeeByIdAsync)
            .WithName("GetEmployeeById")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Obtener empleado")
            .WithDescription("Operacion administrativa. Obtiene un empleado por id.")
            .Produces<EmployeeListItemDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut("/employees/{id:int}", UpdateEmployeeAsync)
            .WithName("UpdateEmployee")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Modificar empleado")
            .WithDescription("Operacion administrativa. Actualiza datos de un empleado interno.")
            .Accepts<UpdateEmployeeRequestDto>("application/json")
            .Produces<EmployeeListItemDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapDelete("/employees/{id:int}", DeleteEmployeeAsync)
            .WithName("DeleteEmployee")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Eliminar empleado")
            .WithDescription("Operacion administrativa. Elimina un empleado interno y su usuario asociado.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/users/{id}/roles", GetUserRolesAsync)
            .WithName("GetUserRoles")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Obtener roles de usuario")
            .WithDescription("Operacion administrativa. Obtiene los roles de un usuario por id.")
            .Produces<AuthUserRolesDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut("/users/{id}/roles", UpdateUserRolesAsync)
            .WithName("UpdateUserRoles")
            .RequireAuthorization("AdminOnly")
            .WithSummary("Actualizar roles de usuario")
            .WithDescription("Operacion administrativa. Reemplaza los roles de un usuario por el conjunto enviado.")
            .Accepts<UpdateUserRolesRequestDto>("application/json")
            .Produces<AuthUserRolesDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequestDto? request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext dbContext,
        IOptions<IdentityOptions> identityOptions,
        ILogger<AuthEndpointsLogContext> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var firstName = NormalizeRequiredValue(request.FirstName, "firstName");
        var lastName = NormalizeRequiredValue(request.LastName, "lastName");
        if (request.DocumentTypeId <= 0)
        {
            throw new UserFriendlyException("El campo 'documentTypeId' es obligatorio.");
        }

        var documentNumber = NormalizeRequiredValue(request.DocumentNumber, "documentNumber");
        if (!Regex.IsMatch(documentNumber, "^[0-9]{7,8}$"))
        {
            throw new UserFriendlyException("El numero de documento debe tener al menos 7 digitos y como maximo 8, usando solo numeros.");
        }

        var email = NormalizeRequiredValue(request.Email, "email");
        var password = NormalizeRequiredValue(request.Password, "password");
        var fullName = BuildFullName(firstName, lastName);

        var birthDate = request.BirthDate;
        if (!IsAtLeastAge(birthDate, 18))
        {
            throw new UserFriendlyException("Solo pueden crear cuenta personas de 18 anos o mas.");
        }

        ValidateEmailFormat(email);

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            logger.LogWarning("Registro rechazado: email ya existe. Email={Email}", email);
            throw new UserFriendlyException("Ya existe una cuenta registrada con ese email.", StatusCodes.Status409Conflict);
        }

        var documentType = await dbContext.DocumentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.DocumentTypeId, cancellationToken);

        if (documentType is null)
        {
            throw new UserFriendlyException("El tipo de documento indicado no existe.", StatusCodes.Status400BadRequest);
        }

        var existingGuest = await dbContext.Guests
            .SingleOrDefaultAsync(
                entity => entity.DocumentTypeId == request.DocumentTypeId && entity.DocumentNumber == documentNumber,
                cancellationToken);

        if (existingGuest is not null && !string.IsNullOrWhiteSpace(existingGuest.UserId))
        {
            throw new UserFriendlyException(
                "Ya existe una cuenta asociada al tipo y numero de documento indicados.",
                StatusCodes.Status409Conflict);
        }

        if (!await roleManager.RoleExistsAsync("Guest"))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole("Guest"));
            if (!createRoleResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(createRoleResult.Errors), StatusCodes.Status500InternalServerError);
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = !identityOptions.Value.SignIn.RequireConfirmedEmail
        };

        var createUserResult = await userManager.CreateAsync(user, password);
        if (!createUserResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(createUserResult.Errors));
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, "Guest");
        if (!addRoleResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(addRoleResult.Errors), StatusCodes.Status500InternalServerError);
        }

        if (existingGuest is null)
        {
            existingGuest = new Guest
            {
                UserId = user.Id,
                DocumentTypeId = request.DocumentTypeId,
                FirstName = firstName,
                LastName = lastName,
                DocumentNumber = documentNumber,
                BirthDate = birthDate.ToDateTime(TimeOnly.MinValue),
                Email = email
            };

            dbContext.Guests.Add(existingGuest);
        }
        else
        {
            existingGuest.UserId = user.Id;
            existingGuest.DocumentTypeId = request.DocumentTypeId;
            existingGuest.FirstName = firstName;
            existingGuest.LastName = lastName;
            existingGuest.DocumentNumber = documentNumber;
            existingGuest.BirthDate = birthDate.ToDateTime(TimeOnly.MinValue);
            existingGuest.Email = email;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await userManager.DeleteAsync(user);
            throw new UserFriendlyException(
                "No pudimos completar el registro porque el tipo y numero de documento ya estan en uso.",
                StatusCodes.Status409Conflict);
        }

        logger.LogInformation(
            "Usuario registrado. UserId={UserId}, Email={Email}, DocumentTypeId={DocumentTypeId}, DocumentNumber={DocumentNumber}, RequireConfirmedEmail={RequireConfirmedEmail}",
            user.Id,
            user.Email,
            request.DocumentTypeId,
            documentNumber,
            identityOptions.Value.SignIn.RequireConfirmedEmail);

        var response = new RegisterResponseDto(user.Id, user.Email ?? email, ["Guest"]);
        return TypedResults.Created($"/auth/users/{user.Id}", response);
    }

    private static async Task<IResult> ListDocumentTypesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var response = await dbContext.DocumentTypes
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .Select(entity => new DocumentTypeOptionDto(entity.Id, entity.Name))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetCurrentGuestProfileAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var guestProfile = await dbContext.Guests
            .AsNoTracking()
            .Include(entity => entity.DocumentType)
            .Where(entity => entity.UserId == userId)
            .Select(entity => new GuestProfileDto(
                entity.DocumentTypeId,
                entity.DocumentType.Name,
                entity.FirstName,
                entity.LastName,
                entity.DocumentNumber,
                DateOnly.FromDateTime(entity.BirthDate),
                entity.Email,
                entity.Phone))
            .SingleOrDefaultAsync(cancellationToken);

        return TypedResults.Ok(guestProfile);
    }

    private static async Task<IResult> UpdateCurrentGuestProfileAsync(
        [FromBody] UpdateGuestProfileRequestDto? request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<AuthEndpointsLogContext> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        if (request.DocumentTypeId <= 0)
        {
            throw new UserFriendlyException("El campo 'documentTypeId' es obligatorio.");
        }

        var firstName = NormalizeRequiredValue(request.FirstName, "firstName");
        var lastName = NormalizeRequiredValue(request.LastName, "lastName");
        var documentNumber = NormalizeRequiredValue(request.DocumentNumber, "documentNumber");
        if (!Regex.IsMatch(documentNumber, "^[0-9]{7,8}$"))
        {
            throw new UserFriendlyException("El numero de documento debe tener al menos 7 digitos y como maximo 8, usando solo numeros.");
        }

        var email = NormalizeRequiredValue(request.Email, "email");
        ValidateEmailFormat(email);

        if (!IsAtLeastAge(request.BirthDate, 18))
        {
            throw new UserFriendlyException("Solo pueden modificar datos personas de 18 anos o mas.");
        }

        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new UserFriendlyException("Usuario no encontrado.", StatusCodes.Status401Unauthorized);
        }

        if (IsConfiguredGuestDemoUser(user, configuration))
        {
            throw new UserFriendlyException(
                "La cuenta Guest demo no permite modificar datos personales.",
                StatusCodes.Status403Forbidden);
        }

        var documentType = await dbContext.DocumentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.DocumentTypeId, cancellationToken);

        if (documentType is null)
        {
            throw new UserFriendlyException("El tipo de documento indicado no existe.", StatusCodes.Status400BadRequest);
        }

        var existingUserWithEmail = await userManager.FindByEmailAsync(email);
        if (existingUserWithEmail is not null
            && !string.Equals(existingUserWithEmail.Id, user.Id, StringComparison.Ordinal))
        {
            throw new UserFriendlyException("Ya existe una cuenta registrada con ese email.", StatusCodes.Status409Conflict);
        }

        var existingGuestByDocument = await dbContext.Guests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.DocumentTypeId == request.DocumentTypeId
                    && entity.DocumentNumber == documentNumber,
                cancellationToken);

        if (existingGuestByDocument is not null
            && !string.Equals(existingGuestByDocument.UserId, user.Id, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                "Ya existe una cuenta asociada al tipo y numero de documento indicados.",
                StatusCodes.Status409Conflict);
        }

        var guest = await dbContext.Guests
            .SingleOrDefaultAsync(entity => entity.UserId == user.Id, cancellationToken);

        user.Email = email;
        user.UserName = email;
        user.FullName = BuildFullName(firstName, lastName);

        var updateUserResult = await userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(updateUserResult.Errors));
        }

        if (guest is null)
        {
            guest = new Guest
            {
                UserId = user.Id
            };

            dbContext.Guests.Add(guest);
        }

        guest.DocumentTypeId = request.DocumentTypeId;
        guest.FirstName = firstName;
        guest.LastName = lastName;
        guest.DocumentNumber = documentNumber;
        guest.BirthDate = request.BirthDate.ToDateTime(TimeOnly.MinValue);
        guest.Email = email;
        guest.Phone = NormalizeOptionalValue(request.Phone);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new UserFriendlyException(
                "No pudimos actualizar tus datos porque el tipo y numero de documento ya estan en uso.",
                StatusCodes.Status409Conflict);
        }

        logger.LogInformation(
            "Perfil Guest actualizado. UserId={UserId}, Email={Email}, DocumentTypeId={DocumentTypeId}, DocumentNumber={DocumentNumber}",
            user.Id,
            user.Email,
            guest.DocumentTypeId,
            guest.DocumentNumber);

        return TypedResults.Ok(new GuestProfileDto(
            guest.DocumentTypeId,
            documentType.Name,
            guest.FirstName,
            guest.LastName,
            guest.DocumentNumber,
            DateOnly.FromDateTime(guest.BirthDate),
            guest.Email,
            guest.Phone));
    }

    private static async Task<IResult> GetCurrentEmployeeProfileAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var employeeProfile = await dbContext.Employees
            .AsNoTracking()
            .Include(entity => entity.DocumentType)
            .Where(entity => entity.UserId == userId && entity.IsActive)
            .Select(entity => new EmployeeSelfProfileDto(
                entity.DocumentTypeId,
                entity.DocumentType.Name,
                entity.FirstName,
                entity.LastName,
                entity.DocumentNumber,
                DateOnly.FromDateTime(entity.BirthDate)))
            .SingleOrDefaultAsync(cancellationToken);

        if (employeeProfile is null)
        {
            throw new UserFriendlyException(
                "No encontramos un perfil de empleado asociado a tu usuario.",
                StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(employeeProfile);
    }

    private static async Task<IResult> UpdateCurrentEmployeeProfileAsync(
        [FromBody] UpdateEmployeeSelfProfileRequestDto? request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        if (request.DocumentTypeId <= 0)
        {
            throw new UserFriendlyException("El campo 'documentTypeId' es obligatorio.");
        }

        var firstName = NormalizeRequiredValue(request.FirstName, "firstName");
        var lastName = NormalizeRequiredValue(request.LastName, "lastName");
        var documentNumber = NormalizeDocumentNumber(request.DocumentNumber);

        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(entity => entity.UserId == userId && entity.IsActive, cancellationToken);

        if (employee is null)
        {
            throw new UserFriendlyException(
                "No encontramos un perfil de empleado asociado a tu usuario.",
                StatusCodes.Status404NotFound);
        }

        var documentType = await dbContext.DocumentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.DocumentTypeId, cancellationToken);

        if (documentType is null)
        {
            throw new UserFriendlyException("El tipo de documento indicado no existe.", StatusCodes.Status400BadRequest);
        }

        var existingEmployeeByDocument = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Id != employee.Id
                    && entity.DocumentTypeId == request.DocumentTypeId
                    && entity.DocumentNumber == documentNumber
                    && entity.IsActive,
                cancellationToken);

        if (existingEmployeeByDocument)
        {
            throw new UserFriendlyException(
                "Ya existe un empleado con el tipo y numero de documento indicados.",
                StatusCodes.Status409Conflict);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            user.FullName = BuildFullName(firstName, lastName);
            var updateUserResult = await userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(updateUserResult.Errors));
            }
        }

        employee.DocumentTypeId = request.DocumentTypeId;
        employee.FirstName = firstName;
        employee.LastName = lastName;
        employee.DocumentNumber = documentNumber;
        employee.BirthDate = request.BirthDate.ToDateTime(TimeOnly.MinValue);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new UserFriendlyException(
                "No pudimos actualizar tus datos porque el tipo y numero de documento ya estan en uso.",
                StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(new EmployeeSelfProfileDto(
            employee.DocumentTypeId,
            documentType.Name,
            employee.FirstName,
            employee.LastName,
            employee.DocumentNumber,
            DateOnly.FromDateTime(employee.BirthDate)));
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequestDto? request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<IdentityOptions> identityOptions,
        ILogger<AuthEndpointsLogContext> logger,
        IJwtTokenService jwtTokenService)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var email = NormalizeRequiredValue(request.Email, "email");
        var password = NormalizeRequiredValue(request.Password, "password");
        ValidateEmailFormat(email);

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            logger.LogWarning("Login rechazado: usuario no encontrado. Email={Email}", email);
            throw new UserFriendlyException("Credenciales invalidas.", StatusCodes.Status401Unauthorized);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("Login bloqueado por lockout. UserId={UserId}, Email={Email}", user.Id, email);
            throw new UserFriendlyException(
                "Tu cuenta esta bloqueada temporalmente por multiples intentos fallidos.",
                StatusCodes.Status423Locked);
        }

        if (signInResult.IsNotAllowed && identityOptions.Value.SignIn.RequireConfirmedEmail)
        {
            logger.LogWarning("Login rechazado: email no confirmado. UserId={UserId}, Email={Email}", user.Id, email);
            throw new UserFriendlyException(
                "Debes confirmar tu email antes de iniciar sesion.",
                StatusCodes.Status403Forbidden);
        }

        if (!signInResult.Succeeded)
        {
            logger.LogWarning("Login rechazado: credenciales invalidas. UserId={UserId}, Email={Email}", user.Id, email);
            throw new UserFriendlyException("Credenciales invalidas.", StatusCodes.Status401Unauthorized);
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = jwtTokenService.CreateToken(user, roles);
        var roleValues = roles.ToArray();

        logger.LogInformation("Login exitoso. UserId={UserId}, Email={Email}, Roles={Roles}", user.Id, user.Email, roleValues);

        var response = new LoginResponseDto(
            token.AccessToken,
            token.ExpiresAtUtc,
            user.Id,
            user.Email ?? email,
            roleValues);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequestDto? request,
        ClaimsPrincipal principal,
        ILogger<AuthEndpointsLogContext> logger,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var currentPassword = NormalizeRequiredValue(request.CurrentPassword, "currentPassword");
        var newPassword = NormalizeRequiredValue(request.NewPassword, "newPassword");

        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserFriendlyException("Token invalido o sin claim 'sub'.", StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new UserFriendlyException("Usuario no encontrado.", StatusCodes.Status401Unauthorized);
        }

        if (IsConfiguredGuestDemoUser(user, configuration))
        {
            throw new UserFriendlyException(
                "La cuenta Guest demo no permite cambiar la clave.",
                StatusCodes.Status403Forbidden);
        }

        var changePasswordResult = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changePasswordResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(changePasswordResult.Errors));
        }

        logger.LogInformation("Password actualizado. UserId={UserId}, Email={Email}", user.Id, user.Email);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListEmployeesAsync(
        [AsParameters] ListEmployeesRequestDto request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Employees
            .AsNoTracking()
            .Include(employee => employee.DocumentType)
            .Where(employee => employee.IsActive);

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            var firstNameFilter = request.FirstName.Trim().ToUpperInvariant();
            query = query.Where(employee => employee.FirstName.ToUpper().Contains(firstNameFilter));
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            var lastNameFilter = request.LastName.Trim().ToUpperInvariant();
            query = query.Where(employee => employee.LastName.ToUpper().Contains(lastNameFilter));
        }

        if (request.DocumentTypeId is > 0)
        {
            query = query.Where(employee => employee.DocumentTypeId == request.DocumentTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            var documentNumber = NormalizeDocumentNumber(request.DocumentNumber);
            query = query.Where(employee => employee.DocumentNumber == documentNumber);
        }

        var employees = await query
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ThenBy(employee => employee.Id)
            .Select(employee => new
            {
                employee.Id,
                employee.UserId,
                employee.FirstName,
                employee.LastName,
                employee.DocumentTypeId,
                DocumentTypeName = employee.DocumentType.Name,
                employee.DocumentNumber,
                employee.BirthDate,
                employee.Email
            })
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return TypedResults.Ok(Array.Empty<EmployeeListItemDto>());
        }

        var userIds = employees.Select(employee => employee.UserId).ToArray();
        var roleRows = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        var rolesByUserId = roleRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.Name!).ToArray(),
                StringComparer.Ordinal);

        var normalizedProfileFilter = NormalizeOptionalValue(request.Profile);
        if (normalizedProfileFilter is not null
            && !IsInternalProfile(normalizedProfileFilter))
        {
            throw new UserFriendlyException("El filtro 'profile' solo admite 'Staff' o 'Admin'.");
        }

        var response = employees
            .Select(employee =>
            {
                rolesByUserId.TryGetValue(employee.UserId, out var roles);
                var profile = ResolveEmployeeProfile(roles ?? []);
                return new EmployeeListItemDto(
                    employee.Id,
                    employee.UserId,
                    employee.FirstName,
                    employee.LastName,
                    employee.DocumentTypeId,
                    employee.DocumentTypeName,
                    employee.DocumentNumber,
                    DateOnly.FromDateTime(employee.BirthDate),
                    employee.Email,
                    profile);
            })
            .Where(employee => normalizedProfileFilter is null
                || string.Equals(employee.Profile, normalizedProfileFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CreateEmployeeAsync(
        [FromBody] CreateEmployeeRequestDto? request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext dbContext,
        IOptions<IdentityOptions> identityOptions,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var firstName = NormalizeRequiredValue(request.FirstName, "firstName");
        var lastName = NormalizeRequiredValue(request.LastName, "lastName");
        var profile = NormalizeRequiredValue(request.Profile, "profile");
        if (!IsInternalProfile(profile))
        {
            throw new UserFriendlyException("El campo 'profile' solo admite 'Staff' o 'Admin'.");
        }
        var normalizedProfile = NormalizeInternalProfile(profile);

        if (request.DocumentTypeId <= 0)
        {
            throw new UserFriendlyException("El campo 'documentTypeId' es obligatorio.");
        }

        var documentNumber = NormalizeDocumentNumber(request.DocumentNumber);
        var documentType = await dbContext.DocumentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.DocumentTypeId, cancellationToken);

        if (documentType is null)
        {
            throw new UserFriendlyException("El tipo de documento indicado no existe.", StatusCodes.Status400BadRequest);
        }

        var existingEmployeeByDocument = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(
                employee => employee.DocumentTypeId == request.DocumentTypeId
                    && employee.DocumentNumber == documentNumber
                    && employee.IsActive,
                cancellationToken);

        if (existingEmployeeByDocument)
        {
            throw new UserFriendlyException(
                "Ya existe un empleado con el tipo y numero de documento indicados.",
                StatusCodes.Status409Conflict);
        }

        if (!await roleManager.RoleExistsAsync(normalizedProfile))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(normalizedProfile));
            if (!createRoleResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(createRoleResult.Errors), StatusCodes.Status500InternalServerError);
            }
        }

        var email = await GenerateEmployeeEmailAsync(firstName, lastName, userManager, cancellationToken);
        var fullName = BuildFullName(firstName, lastName);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = !identityOptions.Value.SignIn.RequireConfirmedEmail
        };

        var createUserResult = await userManager.CreateAsync(user, DefaultEmployeeTemporaryPassword);
        if (!createUserResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(createUserResult.Errors));
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, normalizedProfile);
        if (!addRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new UserFriendlyException(FormatIdentityErrors(addRoleResult.Errors), StatusCodes.Status500InternalServerError);
        }

        var employee = new Employee
        {
            UserId = user.Id,
            DocumentTypeId = request.DocumentTypeId,
            FirstName = firstName,
            LastName = lastName,
            DocumentNumber = documentNumber,
            BirthDate = request.BirthDate.ToDateTime(TimeOnly.MinValue),
            Email = email,
            IsActive = true
        };

        dbContext.Employees.Add(employee);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await userManager.DeleteAsync(user);
            throw new UserFriendlyException(
                "No pudimos crear el empleado porque ya existe informacion duplicada.",
                StatusCodes.Status409Conflict);
        }

        var response = new CreateEmployeeResponseDto(
            employee.Id,
            user.Id,
            fullName,
            email,
            normalizedProfile,
            DefaultEmployeeTemporaryPassword);

        return TypedResults.Created($"/auth/employees/{employee.Id}", response);
    }

    private static async Task<IResult> GetEmployeeByIdAsync(
        [FromRoute] int id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new UserFriendlyException("El parametro 'id' es obligatorio.");
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Include(entity => entity.DocumentType)
            .SingleOrDefaultAsync(entity => entity.Id == id && entity.IsActive, cancellationToken);

        if (employee is null)
        {
            throw new UserFriendlyException("No encontramos el empleado indicado.", StatusCodes.Status404NotFound);
        }

        var profile = await ResolveEmployeeProfileByUserIdAsync(employee.UserId, dbContext, cancellationToken);

        return TypedResults.Ok(new EmployeeListItemDto(
            employee.Id,
            employee.UserId,
            employee.FirstName,
            employee.LastName,
            employee.DocumentTypeId,
            employee.DocumentType.Name,
            employee.DocumentNumber,
            DateOnly.FromDateTime(employee.BirthDate),
            employee.Email,
            profile));
    }

    private static async Task<IResult> UpdateEmployeeAsync(
        [FromRoute] int id,
        [FromBody] UpdateEmployeeRequestDto? request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new UserFriendlyException("El parametro 'id' es obligatorio.");
        }

        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(entity => entity.Id == id && entity.IsActive, cancellationToken);

        if (employee is null)
        {
            throw new UserFriendlyException("No encontramos el empleado indicado.", StatusCodes.Status404NotFound);
        }

        var user = await userManager.FindByIdAsync(employee.UserId);
        if (user is null)
        {
            throw new UserFriendlyException("No encontramos el usuario asociado al empleado.", StatusCodes.Status404NotFound);
        }

        var firstName = NormalizeRequiredValue(request.FirstName, "firstName");
        var lastName = NormalizeRequiredValue(request.LastName, "lastName");
        var profile = NormalizeRequiredValue(request.Profile, "profile");
        if (!IsInternalProfile(profile))
        {
            throw new UserFriendlyException("El campo 'profile' solo admite 'Staff' o 'Admin'.");
        }

        var normalizedProfile = NormalizeInternalProfile(profile);

        if (request.DocumentTypeId <= 0)
        {
            throw new UserFriendlyException("El campo 'documentTypeId' es obligatorio.");
        }

        var documentNumber = NormalizeDocumentNumber(request.DocumentNumber);
        var documentType = await dbContext.DocumentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == request.DocumentTypeId, cancellationToken);

        if (documentType is null)
        {
            throw new UserFriendlyException("El tipo de documento indicado no existe.", StatusCodes.Status400BadRequest);
        }

        var existingEmployeeByDocument = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Id != employee.Id
                    && entity.DocumentTypeId == request.DocumentTypeId
                    && entity.DocumentNumber == documentNumber
                    && entity.IsActive,
                cancellationToken);

        if (existingEmployeeByDocument)
        {
            throw new UserFriendlyException(
                "Ya existe un empleado con el tipo y numero de documento indicados.",
                StatusCodes.Status409Conflict);
        }

        if (!await roleManager.RoleExistsAsync(normalizedProfile))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(normalizedProfile));
            if (!createRoleResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(createRoleResult.Errors), StatusCodes.Status500InternalServerError);
            }
        }

        user.FullName = BuildFullName(firstName, lastName);
        var updateUserResult = await userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(updateUserResult.Errors));
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var internalRolesToRemove = currentRoles
            .Where(role => string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (internalRolesToRemove.Length > 0)
        {
            var removeRolesResult = await userManager.RemoveFromRolesAsync(user, internalRolesToRemove);
            if (!removeRolesResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(removeRolesResult.Errors), StatusCodes.Status500InternalServerError);
            }
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, normalizedProfile);
        if (!addRoleResult.Succeeded)
        {
            throw new UserFriendlyException(FormatIdentityErrors(addRoleResult.Errors), StatusCodes.Status500InternalServerError);
        }

        employee.FirstName = firstName;
        employee.LastName = lastName;
        employee.DocumentTypeId = request.DocumentTypeId;
        employee.DocumentNumber = documentNumber;
        employee.BirthDate = request.BirthDate.ToDateTime(TimeOnly.MinValue);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new UserFriendlyException(
                "No pudimos modificar el empleado porque el tipo y numero de documento ya estan en uso.",
                StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(new EmployeeListItemDto(
            employee.Id,
            employee.UserId,
            employee.FirstName,
            employee.LastName,
            employee.DocumentTypeId,
            documentType.Name,
            employee.DocumentNumber,
            DateOnly.FromDateTime(employee.BirthDate),
            employee.Email,
            normalizedProfile));
    }

    private static async Task<IResult> DeleteEmployeeAsync(
        [FromRoute] int id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new UserFriendlyException("El parametro 'id' es obligatorio.");
        }

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(entity => entity.Id == id && entity.IsActive, cancellationToken);

        if (employee is null)
        {
            throw new UserFriendlyException("No encontramos el empleado indicado.", StatusCodes.Status404NotFound);
        }

        var requesterUserId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.Equals(requesterUserId, employee.UserId, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                "No podes eliminar tu propio usuario.",
                StatusCodes.Status409Conflict);
        }

        var user = await userManager.FindByIdAsync(employee.UserId);
        if (user is null)
        {
            dbContext.Employees.Remove(employee);
            await dbContext.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        }

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Count <= 1)
            {
                throw new UserFriendlyException(
                    "No se puede eliminar el ultimo usuario administrador.",
                    StatusCodes.Status409Conflict);
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Employees.Remove(employee);
        await dbContext.SaveChangesAsync(cancellationToken);

        var deleteUserResult = await userManager.DeleteAsync(user);
        if (!deleteUserResult.Succeeded)
        {
            throw new UserFriendlyException(
                FormatIdentityErrors(deleteUserResult.Errors),
                StatusCodes.Status500InternalServerError);
        }

        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListUsersAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Select(user => new { user.Id, user.Email, user.FullName })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();

        var roleRows = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        var rolesByUserId = roleRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.Name!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(role => role)
                    .ToArray());

        var response = users
            .Select(user => new AuthUserRolesDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                rolesByUserId.TryGetValue(user.Id, out var roles) ? roles : []))
            .Where(user => user.Roles.Any(role => !string.Equals(role, "Guest", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetUserRolesAsync(
        [FromRoute] string id,
        UserManager<ApplicationUser> userManager)
    {
        var userId = NormalizeRequiredValue(id, "id");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new UserFriendlyException("No encontramos el usuario indicado.", StatusCodes.Status404NotFound);
        }

        var roles = (await userManager.GetRolesAsync(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToArray();

        return TypedResults.Ok(new AuthUserRolesDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles));
    }

    private static async Task<IResult> UpdateUserRolesAsync(
        [FromRoute] string id,
        [FromBody] UpdateUserRolesRequestDto? request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        var userId = NormalizeRequiredValue(id, "id");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new UserFriendlyException("No encontramos el usuario indicado.", StatusCodes.Status404NotFound);
        }

        if (request.Roles is null || request.Roles.Count == 0)
        {
            throw new UserFriendlyException("El campo 'roles' es obligatorio y debe tener al menos un rol.");
        }

        var normalizedRoles = request.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoles.Length == 0)
        {
            throw new UserFriendlyException("El campo 'roles' es obligatorio y debe tener al menos un rol.");
        }

        var invalidRoles = normalizedRoles
            .Where(role => !AllowedManagedRoles.Contains(role))
            .ToArray();

        if (invalidRoles.Length > 0)
        {
            throw new UserFriendlyException(
                $"Roles no permitidos: {string.Join(", ", invalidRoles)}. Roles validos: Guest, Staff, Admin.");
        }

        foreach (var role in normalizedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var createRoleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!createRoleResult.Succeeded)
                {
                    throw new UserFriendlyException(FormatIdentityErrors(createRoleResult.Errors), StatusCodes.Status500InternalServerError);
                }
            }
        }

        var currentRoles = (await userManager.GetRolesAsync(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requesterUserId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.Equals(requesterUserId, user.Id, StringComparison.Ordinal)
            && currentRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            && !normalizedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(
                "No podes quitarte el rol Admin a vos mismo.",
                StatusCodes.Status409Conflict);
        }

        if (currentRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            && !normalizedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Count <= 1)
            {
                throw new UserFriendlyException(
                    "No se puede quitar el rol Admin al ultimo usuario administrador.",
                    StatusCodes.Status409Conflict);
            }
        }

        var rolesToAdd = normalizedRoles
            .Except(currentRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rolesToRemove = currentRoles
            .Except(normalizedRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeRolesResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeRolesResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(removeRolesResult.Errors), StatusCodes.Status500InternalServerError);
            }
        }

        if (rolesToAdd.Length > 0)
        {
            var addRolesResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addRolesResult.Succeeded)
            {
                throw new UserFriendlyException(FormatIdentityErrors(addRolesResult.Errors), StatusCodes.Status500InternalServerError);
            }
        }

        var updatedRoles = (await userManager.GetRolesAsync(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToArray();

        return TypedResults.Ok(new AuthUserRolesDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            updatedRoles));
    }

    private static string NormalizeRequiredValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException($"El campo '{fieldName}' es obligatorio.");
        }

        return value.Trim();
    }

    private static bool IsInternalProfile(string profile)
    {
        return string.Equals(profile, "Staff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeInternalProfile(string profile)
    {
        return string.Equals(profile, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : "Staff";
    }

    private static string ResolveEmployeeProfile(IReadOnlyList<string> roles)
    {
        if (roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return "Admin";
        }

        if (roles.Any(role => string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase)))
        {
            return "Staff";
        }

        return "Staff";
    }

    private static async Task<string> ResolveEmployeeProfileByUserIdAsync(
        string userId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var roles = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name != null
            select role.Name!)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return ResolveEmployeeProfile(roles);
    }

    private static string NormalizeDocumentNumber(string value)
    {
        var documentNumber = NormalizeRequiredValue(value, "documentNumber");
        if (!Regex.IsMatch(documentNumber, "^[0-9]{7,8}$"))
        {
            throw new UserFriendlyException("El numero de documento debe tener al menos 7 digitos y como maximo 8, usando solo numeros.");
        }

        return documentNumber;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string BuildFullName(string firstName, string lastName)
    {
        return string.Join(" ", [firstName, lastName]);
    }

    private static async Task<string> GenerateEmployeeEmailAsync(
        string firstName,
        string lastName,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var normalizedFirst = SanitizeForEmail(firstName);
        var normalizedLast = SanitizeForEmail(lastName);
        if (string.IsNullOrWhiteSpace(normalizedFirst) || string.IsNullOrWhiteSpace(normalizedLast))
        {
            throw new UserFriendlyException("No pudimos generar el email del empleado con los datos ingresados.");
        }

        for (var letterCount = 1; letterCount <= normalizedFirst.Length; letterCount++)
        {
            var firstFragment = normalizedFirst[..letterCount];
            var localPart = $"{firstFragment}{normalizedLast}";
            var candidate = $"{localPart}@{EmployeeEmailDomain}";

            var existingUser = await userManager.FindByEmailAsync(candidate);
            cancellationToken.ThrowIfCancellationRequested();
            if (existingUser is null)
            {
                return candidate;
            }
        }

        var baseLocalPart = $"{normalizedFirst}{normalizedLast}";
        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{baseLocalPart}{suffix}@{EmployeeEmailDomain}";
            var existingUser = await userManager.FindByEmailAsync(candidate);
            cancellationToken.ThrowIfCancellationRequested();
            if (existingUser is null)
            {
                return candidate;
            }
        }

        throw new UserFriendlyException("No se pudo generar un email disponible para el empleado.");
    }

    private static string SanitizeForEmail(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        var withoutDiacritics = new string(normalized)
            .Normalize(NormalizationForm.FormC)
            .Trim()
            .ToLowerInvariant();

        var allowedCharacters = withoutDiacritics
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray();

        return new string(allowedCharacters);
    }

    private static bool IsAtLeastAge(DateOnly birthDate, int minimumAge)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Year;

        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age >= minimumAge;
    }

    private static string FormatIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var messages = errors
            .Select(error => error.Description?.Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return messages.Length == 0
            ? "No pudimos completar la operacion de identidad."
            : string.Join(" ", messages);
    }

    private static void ValidateEmailFormat(string email)
    {
        try
        {
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s\.]{2,}$";
            if (!Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase))
            {
                throw new UserFriendlyException("El campo 'email' debe tener un formato valido.");
            }
        }

        catch (FormatException)
        {
            throw new UserFriendlyException("El campo 'email' debe tener un formato valido.");
        }

    }

    private static bool IsConfiguredGuestDemoUser(ApplicationUser user, IConfiguration configuration)
    {
        var configuredGuestEmail = configuration[GuestDemoEmailConfigKey];
        if (string.IsNullOrWhiteSpace(configuredGuestEmail) || string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        return string.Equals(user.Email, configuredGuestEmail, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class AuthEndpointsLogContext;
