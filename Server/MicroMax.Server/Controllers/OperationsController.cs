using MicroMax.Server.Api.Operations;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Выполняет складские операции и возвращает журнал операций.
/// </summary>
[Authorize]
[Route("api/operations")]
[Produces("application/json", "application/problem+json")]
public sealed class OperationsController(
    OperationsApiService operationsApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает журнал складских операций с необязательной фильтрацией по периоду.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseOperationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseOperationResponse>>> GetAsync(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.GetAsync(userId, from, to, cancellationToken));
    }

    /// <summary>
    /// Выполняет приёмку товара в целевую ячейку.
    /// </summary>
    [HttpPost("receive")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> ReceiveAsync(
        [FromBody] ReceiveRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.ReceiveAsync(userId, request, cancellationToken));
    }

    /// <summary>
    /// Выполняет перемещение товара между ячейками.
    /// </summary>
    [HttpPost("move")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> MoveAsync(
        [FromBody] MoveRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.MoveAsync(userId, request, cancellationToken));
    }

    /// <summary>
    /// Выполняет списание товара из выбранной ячейки.
    /// </summary>
    [HttpPost("write-off")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> WriteOffAsync(
        [FromBody] WriteOffRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.WriteOffAsync(userId, request, cancellationToken));
    }

    /// <summary>
    /// Корректирует итоговый остаток товара в ячейке.
    /// </summary>
    [HttpPost("adjust")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> AdjustAsync(
        [FromBody] AdjustRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.AdjustAsync(userId, request, cancellationToken));
    }
}
