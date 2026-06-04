using MicroMax.Server.Api.Users;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает и изменяет пользователей системы.
/// </summary>
[Authorize]
[Route("api/users")]
[Produces("application/json", "application/problem+json")]
public sealed class UsersController(
    UsersApiService usersApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает профиль текущего пользователя.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserResponse>> GetMeAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await usersApiService.GetCurrentAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Возвращает список пользователей.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await usersApiService.GetAsync(cancellationToken));
    }

    /// <summary>
    /// Создаёт пользователя.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await usersApiService.CreateAsync(request, cancellationToken);
        return CreatedResource($"/api/users/{user.Id}", user);
    }

    /// <summary>
    /// Удаляет пользователя.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await usersApiService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
