using MicroMax.Server.Api.Cells;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Работает со складскими ячейками и их содержимым.
/// </summary>
[Authorize]
[Route("api/cells")]
[Produces("application/json", "application/problem+json")]
public sealed class CellsController(
    CellsApiService cellsApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает ячейки хранения, доступные пользователю.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StorageCellResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StorageCellResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await cellsApiService.GetAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Возвращает содержимое выбранной ячейки.
    /// </summary>
    [HttpGet("{id:int}/contents")]
    [ProducesResponseType(typeof(IReadOnlyList<CellContentsItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CellContentsItemResponse>>> GetContentsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await cellsApiService.GetContentsAsync(userId, id, cancellationToken));
    }

    /// <summary>
    /// Создаёт новую ячейку хранения.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(StorageCellResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StorageCellResponse>> CreateAsync(
        [FromBody] CreateStorageCellRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var cell = await cellsApiService.CreateAsync(userId, request, cancellationToken);
        return CreatedResource($"/api/cells/{cell.Id}", cell);
    }

    /// <summary>
    /// Удаляет ячейку хранения.
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
        await cellsApiService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }
}
