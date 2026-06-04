using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroMax.Server.Infrastructure.Api;

public sealed class ProblemDetailsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var response in operation.Responses.Values)
        {
            if (!response.Content.TryGetValue("application/json", out var mediaType))
            {
                continue;
            }

            if (!IsProblemSchema(mediaType.Schema))
            {
                continue;
            }

            response.Content.Remove("application/json");
            response.Content["application/problem+json"] = mediaType;
        }
    }

    private static bool IsProblemSchema(OpenApiSchema? schema)
    {
        var schemaId = schema?.Reference?.Id;
        return schemaId is nameof(Microsoft.AspNetCore.Mvc.ProblemDetails)
            or nameof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails);
    }
}
