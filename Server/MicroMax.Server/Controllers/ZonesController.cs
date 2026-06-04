using MicroMax.Server.Api.Zones;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает и изменяет зоны хранения.
/// </summary>
[Authorize]
[Route("api/zones")]
[Produces("application/json", "application/problem+json")]
public sealed class ZonesController(
    ZonesApiService zonesApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает зоны хранения в доступных пользователю складах.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StorageZoneResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StorageZoneResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await zonesApiService.GetAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Создаёт новую зону хранения.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(StorageZoneResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StorageZoneResponse>> CreateAsync(
        [FromBody] CreateStorageZoneRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var zone = await zonesApiService.CreateAsync(userId, request, cancellationToken);
        return CreatedResource($"/api/zones/{zone.Id}", zone);
    }

    /// <summary>
    /// Удаляет зону хранения.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await zonesApiService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }
}
