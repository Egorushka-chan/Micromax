using MicroMax.Server.Api.Roles;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет ролями пользователей микросклада.
/// </summary>
[Authorize]
[Route("api/roles")]
[Produces("application/json", "application/problem+json")]
public sealed class RolesController(
    RolesApiService rolesApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает список доступных ролей.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await rolesApiService.GetAsync(cancellationToken));
    }

    /// <summary>
    /// Создаёт новую роль.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleResponse>> CreateAsync(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await rolesApiService.CreateAsync(request, cancellationToken);
        return CreatedResource($"/api/roles/{role.Id}", role);
    }

    /// <summary>
    /// Удаляет пользовательскую роль.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetRequiredUserId(User);
        await rolesApiService.DeleteAsync(currentUserId, id, cancellationToken);
        return NoContent();
    }
}
