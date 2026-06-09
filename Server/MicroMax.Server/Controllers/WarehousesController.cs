using MicroMax.Server.Api.Warehouses;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет складами и складским контекстом пользователя.
/// </summary>
[Authorize]
[Route("api/warehouses")]
[Produces("application/json", "application/problem+json")]
public sealed class WarehousesController(
    WarehousesApiService warehousesApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает склады, доступные текущему пользователю.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehousesApiService.GetAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Возвращает список складов текущего пользователя вместе с ролью в каждом складе.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<UserWarehouseResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserWarehouseResponse>>> GetMyAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehousesApiService.GetMyAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Возвращает карточку выбранного склада с ролью текущего пользователя.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(WarehouseDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDetailsResponse>> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehousesApiService.GetByIdAsync(userId, id, cancellationToken));
    }

    /// <summary>
    /// Возвращает структуру выбранного склада: зоны и ячейки.
    /// </summary>
    [HttpGet("{id:int}/structure")]
    [ProducesResponseType(typeof(WarehouseStructureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseStructureResponse>> GetStructureAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehousesApiService.GetStructureAsync(userId, id, cancellationToken));
    }

    /// <summary>
    /// Возвращает рабочий снимок выбранного склада для мобильного приложения.
    /// </summary>
    [HttpGet("{id:int}/snapshot")]
    [ProducesResponseType(typeof(WarehouseSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseSnapshotResponse>> GetSnapshotAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehousesApiService.GetSnapshotAsync(userId, id, cancellationToken));
    }

    /// <summary>
    /// Создаёт новый склад и назначает текущего пользователя администратором склада.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseResponse>> CreateAsync(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouse = await warehousesApiService.CreateAsync(userId, request, cancellationToken);
        return CreatedResource($"/api/warehouses/{warehouse.Id}", warehouse);
    }

    /// <summary>
    /// Обновляет карточку склада.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseResponse>> UpdateAsync(
        int id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await warehousesApiService.UpdateAsync(userId, id, request, cancellationToken));
    }

    /// <summary>
    /// Удаляет склад.
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
        await warehousesApiService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }
}
