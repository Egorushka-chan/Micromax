using MicroMax.Server.Api.WarehouseSetup;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Создаёт склад по шаблону быстрой настройки.
/// </summary>
[Authorize]
[Route("api/warehouse-setup")]
[Produces("application/json", "application/problem+json")]
public sealed class WarehouseSetupController(
    WarehouseSetupService warehouseSetupService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает доступные шаблоны быстрой настройки склада.
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseSetupTemplateResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WarehouseSetupTemplateResponse>> GetTemplates() =>
        Ok(warehouseSetupService.GetTemplates());

    /// <summary>
    /// Создаёт новый склад и его структуру по выбранному шаблону.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WarehouseSetupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseSetupResponse>> CreateAsync(
        [FromBody] CreateWarehouseFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var result = await warehouseSetupService.CreateAsync(userId, request, cancellationToken);
        return CreatedResource($"/api/warehouses/{result.WarehouseId}/structure", result);
    }
}
