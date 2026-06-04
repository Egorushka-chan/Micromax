using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroMax.Server.Infrastructure.Api;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAllowAnonymous =
            context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() ||
            context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() == true;

        if (hasAllowAnonymous)
        {
            return;
        }

        var hasAuthorize =
            context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() ||
            context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true;

        if (!hasAuthorize)
        {
            return;
        }

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Требуется аутентификация", Content = ProblemContent() });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Доступ запрещен", Content = ProblemContent() });
        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }
                ] = []
            }
        ];
    }

    private static IDictionary<string, OpenApiMediaType> ProblemContent() =>
        new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new()
            {
                Schema = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = nameof(ProblemDetails)
                    }
                }
            }
        };
}
