using MicroMax.Server.Api.Auth;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Выполняет регистрацию, вход и обновление токенов доступа.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json", "application/problem+json")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    /// <summary>
    /// Регистрирует нового пользователя и выдаёт пару токенов.
    /// </summary>
    [HttpPost("register")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.RegisterAsync(request, cancellationToken));
    }

    /// <summary>
    /// Аутентифицирует пользователя по email и паролю.
    /// </summary>
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginAsync(request, cancellationToken));
    }

    /// <summary>
    /// Обновляет access token по действующему refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.RefreshAsync(request, cancellationToken));
    }

    /// <summary>
    /// Отзывает refresh token текущей сессии.
    /// </summary>
    [HttpPost("logout")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
