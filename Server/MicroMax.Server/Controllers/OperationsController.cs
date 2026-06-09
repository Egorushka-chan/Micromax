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

    [HttpGet("/api/warehouses/{warehouseId:int}/operations")]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseOperationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<WarehouseOperationResponse>>> GetForWarehouseAsync(
        int warehouseId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.GetForWarehouseAsync(userId, warehouseId, from, to, cancellationToken));
    }

    [HttpPost("receive")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
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

    [HttpPost("/api/warehouses/{warehouseId:int}/operations/receive")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> ReceiveForWarehouseAsync(
        int warehouseId,
        [FromBody] ReceiveRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.ReceiveAsync(userId, warehouseId, request, cancellationToken));
    }

    [HttpPost("move")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
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

    [HttpPost("/api/warehouses/{warehouseId:int}/operations/move")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> MoveForWarehouseAsync(
        int warehouseId,
        [FromBody] MoveRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.MoveAsync(userId, warehouseId, request, cancellationToken));
    }

    [HttpPost("write-off")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
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

    [HttpPost("/api/warehouses/{warehouseId:int}/operations/write-off")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> WriteOffForWarehouseAsync(
        int warehouseId,
        [FromBody] WriteOffRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.WriteOffAsync(userId, warehouseId, request, cancellationToken));
    }

    [HttpPost("adjust")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
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

    [HttpPost("/api/warehouses/{warehouseId:int}/operations/adjust")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseOperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseOperationResultResponse>> AdjustForWarehouseAsync(
        int warehouseId,
        [FromBody] AdjustRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await operationsApiService.AdjustAsync(userId, warehouseId, request, cancellationToken));
    }
}
