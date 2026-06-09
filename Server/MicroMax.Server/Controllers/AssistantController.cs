using MicroMax.Server.Api.Assistant;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Обрабатывает команды складского помощника и подтверждение операций.
/// </summary>
[Authorize]
[Route("api/assistant")]
[Produces("application/json", "application/problem+json")]
public sealed class AssistantController(
    AssistantApiService assistantApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    [HttpGet("commands")]
    [ProducesResponseType(typeof(IReadOnlyList<AssistantCommandDefinitionResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AssistantCommandDefinitionResponse>> GetCommands() =>
        Ok(assistantApiService.GetCommands());

    [HttpPost("interpret")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AssistantCommandResponse>> InterpretAsync(
        [FromBody] AssistantRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await assistantApiService.InterpretAsync(userId, request, cancellationToken));
    }

    [HttpPost("/api/warehouses/{warehouseId:int}/assistant/interpret")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AssistantCommandResponse>> InterpretForWarehouseAsync(
        int warehouseId,
        [FromBody] AssistantRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await assistantApiService.InterpretAsync(userId, warehouseId, request, cancellationToken));
    }

    [HttpPost("confirm")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantCommandResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssistantCommandResultResponse>> ConfirmAsync(
        [FromBody] AssistantConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await assistantApiService.ConfirmAsync(userId, request, cancellationToken));
    }

    [HttpPost("/api/warehouses/{warehouseId:int}/assistant/confirm")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantCommandResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssistantCommandResultResponse>> ConfirmForWarehouseAsync(
        int warehouseId,
        [FromBody] AssistantConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await assistantApiService.ConfirmAsync(userId, warehouseId, request, cancellationToken));
    }

    [HttpPost("clarify")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssistantCommandResponse>> ClarifyAsync(
        [FromBody] AssistantClarificationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await assistantApiService.ClarifyAsync(userId, request, cancellationToken));
    }

    [HttpPost("/api/warehouses/{warehouseId:int}/assistant/clarify")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssistantCommandResponse>> ClarifyForWarehouseAsync(
        int warehouseId,
        [FromBody] AssistantClarificationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await assistantApiService.ClarifyAsync(userId, warehouseId, request, cancellationToken));
    }
}
