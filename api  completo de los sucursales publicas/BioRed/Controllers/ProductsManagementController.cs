using System.Security.Claims;
using BioRed.Application.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/products")]
[Authorize(Roles = "ADMIN")]
public sealed class ProductsManagementController : ControllerBase
{
    private readonly IProductManagementService _productService;

    public ProductsManagementController(IProductManagementService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetManagedProductsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetManagedProductsResponse>> GetAsync(
        [FromQuery] GetManagedProductsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _productService.GetAsync(request, cancellationToken));

    [HttpGet("{productCode}", Name = "GetManagedProductByCode")]
    [ProducesResponseType(typeof(ManagedProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedProductResponse>> GetByCodeAsync(
        string productCode,
        CancellationToken cancellationToken)
    {
        var result = await _productService.GetByCodeAsync(
            productCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagedProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedProductResponse>> CreateAsync(
        [FromBody] CreateManagedProductRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _productService.CreateAsync(
            actorReferenceId,
            request,
            cancellationToken);

        return result.Status switch
        {
            ProductManagementStatus.Success when result.Product is not null =>
                CreatedAtRoute(
                    "GetManagedProductByCode",
                    new { productCode = result.Product.ProductCode },
                    result.Product),

            ProductManagementStatus.Duplicate =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Producto duplicado",
                    result.Detail)),

            ProductManagementStatus.CompanyNotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Empresa no encontrada",
                    result.Detail)),

            _ =>
                BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Precio inválido",
                    result.Detail))
        };
    }

    [HttpPut("{productCode}")]
    [ProducesResponseType(typeof(ManagedProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedProductResponse>> UpdateAsync(
        string productCode,
        [FromBody] UpdateManagedProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(
            productCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{productCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateAsync(
        string productCode,
        CancellationToken cancellationToken)
    {
        var result = await _productService.DeactivateAsync(
            productCode,
            cancellationToken);

        if (result.Status == ProductManagementStatus.Success)
        {
            return NoContent();
        }

        return NotFound(CreateProblem(
            StatusCodes.Status404NotFound,
            "Producto no encontrado",
            result.Detail));
    }

    private ActionResult<ManagedProductResponse> ToActionResult(
        ProductManagementResult result)
    {
        if (result.Status == ProductManagementStatus.Success &&
            result.Product is not null)
        {
            return Ok(result.Product);
        }

        if (result.Status == ProductManagementStatus.NotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Producto no encontrado",
                result.Detail));
        }

        return BadRequest(CreateProblem(
            StatusCodes.Status400BadRequest,
            "Precio inválido",
            result.Detail));
    }

    private ProblemDetails CreateProblem(
        int status,
        string title,
        string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
