using MicroMax.Server.Api.Stocks;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает актуальные остатки по ячейкам хранения.
/// </summary>
[Authorize]
[Route("api/stocks")]
[Produces("application/json", "application/problem+json")]
public sealed class StocksController(
    StocksApiService stocksApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает положительные остатки в доступных пользователю складах.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StockBalanceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockBalanceResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await stocksApiService.GetAsync(userId, cancellationToken));
    }
}
