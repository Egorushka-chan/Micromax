using MicroMax.Server.Api.Barcodes;
using MicroMax.Server.Api.Products;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет номенклатурой и местонахождением товаров.
/// </summary>
[Authorize]
[Route("api/products")]
[Produces("application/json", "application/problem+json")]
public sealed class ProductsController(
    BarcodesApiService barcodesApiService,
    ProductsApiService productsApiService,
    CurrentUserService currentUserService) : MicroMaxControllerBase
{
    /// <summary>
    /// Возвращает список номенклатуры.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await productsApiService.GetAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Возвращает активные штрих-коды товара.
    /// </summary>
    [HttpGet("{productId:int}/barcodes")]
    [ProducesResponseType(typeof(IReadOnlyList<BarcodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BarcodeResponse>>> GetBarcodesAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await barcodesApiService.GetProductBarcodesAsync(userId, productId, cancellationToken));
    }

    /// <summary>
    /// Привязывает новый штрих-код к товару.
    /// </summary>
    [HttpPost("{productId:int}/barcodes")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BarcodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BarcodeResponse>> CreateBarcodeAsync(
        int productId,
        [FromBody] BarcodeDraftRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var barcode = await barcodesApiService.CreateProductBarcodeAsync(userId, productId, request, cancellationToken);
        return CreatedResource($"/api/barcodes/{barcode.Id}", barcode);
    }

    /// <summary>
    /// Возвращает ячейки, в которых есть остаток по выбранной номенклатуре.
    /// </summary>
    [HttpGet("{id:int}/locations")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductLocationResponse>>> GetLocationsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await productsApiService.GetLocationsAsync(userId, id, cancellationToken));
    }

    /// <summary>
    /// Создаёт новую позицию номенклатуры.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> CreateAsync(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var product = await productsApiService.CreateAsync(userId, request, cancellationToken);
        return CreatedResource($"/api/products/{product.Id}", product);
    }

    /// <summary>
    /// Обновляет свойства номенклатуры.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> UpdateAsync(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        return Ok(await productsApiService.UpdateAsync(userId, id, request, cancellationToken));
    }

    /// <summary>
    /// Удаляет номенклатуру.
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
        await productsApiService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }
}
