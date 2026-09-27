using BioRed.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/catalog")]
[AllowAnonymous]
public sealed class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("branches/{branchCode}/products")]
    [ProducesResponseType(typeof(GetCatalogProductsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetCatalogProductsResponse>> GetProductsAsync(
        [FromRoute] string branchCode,
        [FromQuery] GetCatalogProductsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductsAsync(
            branchCode,
            request,
            cancellationToken);

        return result.Status switch
        {
            CatalogQueryStatus.Success when result.Catalog is not null =>
                Ok(result.Catalog),

            _ =>
                NotFound(CreateProblem(result.Detail))
        };
    }

    [HttpGet("branches/{branchCode}/products/{productCode}")]
    [ProducesResponseType(typeof(CatalogProductDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogProductDetailResponse>> GetProductByCodeAsync(
        [FromRoute] string branchCode,
        [FromRoute] string productCode,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductByCodeAsync(
            branchCode,
            productCode,
            cancellationToken);

        return result.Status switch
        {
            CatalogQueryStatus.Success when result.Product is not null =>
                Ok(result.Product),

            _ =>
                NotFound(CreateProblem(result.Detail))
        };
    }

    private ProblemDetails CreateProblem(string? detail) =>
        new()
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Catálogo no encontrado",
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
