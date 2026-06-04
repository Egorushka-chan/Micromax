using MicroMax.Server.Api.Warehouses;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет назначениями пользователей на склад.
/// </summary>
[Authorize]
[Route("api/warehouses/{warehouseId:int}/users")]
[Produces("application/json", "application/problem+json")]
public sealed class WarehouseUsersController(
    WarehouseUsersApiService warehouseUsersApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает пользователей выбранного склада.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<WarehouseUserResponse>>> GetAsync(
        int warehouseId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehouseUsersApiService.GetAsync(userId, warehouseId, cancellationToken));
    }

    /// <summary>
    /// Добавляет пользователя в выбранный склад.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseUserResponse>> CreateAsync(
        int warehouseId,
        [FromBody] AddWarehouseUserRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var membership = await warehouseUsersApiService.CreateAsync(userId, warehouseId, request, cancellationToken);
        return CreatedResource($"/api/warehouses/{warehouseId}/users/{membership.UserId}", membership);
    }

    /// <summary>
    /// Изменяет роль пользователя в выбранном складе.
    /// </summary>
    [HttpPatch("{userId:int}/role")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseUserResponse>> UpdateRoleAsync(
        int warehouseId,
        int userId,
        [FromBody] UpdateWarehouseUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehouseUsersApiService.UpdateRoleAsync(currentUserId, warehouseId, userId, request, cancellationToken));
    }

    /// <summary>
    /// Удаляет пользователя из выбранного склада.
    /// </summary>
    [HttpDelete("{userId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        int warehouseId,
        int userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetRequiredUserId(User);
        await warehouseUsersApiService.DeleteAsync(currentUserId, warehouseId, userId, cancellationToken);
        return NoContent();
    }
}
