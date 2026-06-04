using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Infrastructure.Api;

public sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsApiRequest(context))
        {
            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled API exception for {Path}", context.Request.Path);

        var problemDetails = CreateProblemDetails(context, exception);
        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            ApiException apiException => (apiException.StatusCode, apiException.Title, apiException.Message),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                "Конфликт состояния",
                "Не удалось сохранить данные из-за конфликта ограничений."),
            InvalidOperationException invalidOperationException => (
                StatusCodes.Status400BadRequest,
                "Некорректный запрос",
                invalidOperationException.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера",
                "На сервере произошла непредвиденная ошибка.")
        };

        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
