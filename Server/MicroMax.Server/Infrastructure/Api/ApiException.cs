namespace MicroMax.Server.Infrastructure.Api;

public abstract class ApiException : InvalidOperationException
{
    protected ApiException(string message, int statusCode, string title)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

public sealed class ApiValidationException(string message)
    : ApiException(message, StatusCodes.Status400BadRequest, "Некорректный запрос");

public sealed class ApiUnauthorizedException(string message)
    : ApiException(message, StatusCodes.Status401Unauthorized, "Требуется аутентификация");

public sealed class ApiForbiddenException(string message)
    : ApiException(message, StatusCodes.Status403Forbidden, "Доступ запрещен");

public sealed class ApiNotFoundException(string message)
    : ApiException(message, StatusCodes.Status404NotFound, "Ресурс не найден");

public sealed class ApiConflictException(string message)
    : ApiException(message, StatusCodes.Status409Conflict, "Конфликт состояния");
