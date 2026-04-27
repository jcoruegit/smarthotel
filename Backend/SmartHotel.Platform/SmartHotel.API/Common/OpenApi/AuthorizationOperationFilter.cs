using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartHotel.API.Common.OpenApi;

public sealed class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        var authorizeAttributes = endpointMetadata.OfType<IAuthorizeData>().ToArray();

        if (allowAnonymous || authorizeAttributes.Length == 0)
        {
            AppendAccessDescription(operation, "Acceso: Publico (AllowAnonymous o sin autorizacion).");
            operation.Security?.Clear();
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        var roles = authorizeAttributes
            .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var policies = authorizeAttributes
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accessParts = new List<string> { "Acceso: Requiere JWT Bearer." };
        if (roles.Length > 0)
        {
            accessParts.Add($"Roles: {string.Join(", ", roles)}.");
        }

        if (policies.Length > 0)
        {
            accessParts.Add($"Politicas: {string.Join(", ", policies)}.");
        }

        if (roles.Length == 0 && policies.Length == 0)
        {
            accessParts.Add("Rol: cualquier usuario autenticado.");
        }

        AppendAccessDescription(operation, string.Join(" ", accessParts));
    }

    private static void AppendAccessDescription(OpenApiOperation operation, string accessDescription)
    {
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? accessDescription
            : $"{operation.Description}\n\n{accessDescription}";
    }
}
