using MicroMax.Server.Api.Barcodes;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Работает с поиском и деактивацией штрих-кодов.
/// </summary>
[Authorize]
[Route("api/barcodes")]
[Produces("application/json", "application/problem+json")]
public sealed class BarcodesController(
    BarcodesApiService barcodesApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(BarcodeResolveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BarcodeResolveResponse>> ResolveAsync(
        [FromQuery] string value,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await barcodesApiService.ResolveAsync(userId, value, cancellationToken));
    }

    [HttpGet("/api/warehouses/{warehouseId:int}/barcodes/resolve")]
    [ProducesResponseType(typeof(BarcodeResolveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BarcodeResolveResponse>> ResolveForWarehouseAsync(
        int warehouseId,
        [FromQuery] string value,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await barcodesApiService.ResolveAsync(userId, warehouseId, value, cancellationToken));
    }

    [HttpDelete("{barcodeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int barcodeId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await barcodesApiService.DeactivateAsync(userId, barcodeId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("/api/warehouses/{warehouseId:int}/barcodes/{barcodeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteForWarehouseAsync(
        int warehouseId,
        int barcodeId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await barcodesApiService.DeactivateAsync(userId, warehouseId, barcodeId, cancellationToken);
        return NoContent();
    }
}
