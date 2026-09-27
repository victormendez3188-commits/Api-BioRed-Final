using BioRed.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/inventory-management")]
[Authorize(Roles = "ADMIN")]
public sealed class InventoryManagementController : ControllerBase
{
    private readonly IInventoryManagementService _inventoryService;

    public InventoryManagementController(IInventoryManagementService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("branches/{branchCode}")]
    [ProducesResponseType(typeof(GetBranchInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetBranchInventoryResponse>> GetInventoryAsync(
        string branchCode,
        [FromQuery] GetBranchInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetInventoryAsync(
            branchCode,
            request,
            cancellationToken);

        if (result is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Sucursal no encontrada",
                $"No existe la sucursal activa {branchCode}."));
        }

        return Ok(result);
    }

    [HttpGet("products/{productCode}/lots")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductLotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ProductLotResponse>>> GetLotsAsync(
        string productCode,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetLotsAsync(
            productCode,
            cancellationToken);

        if (result is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Producto no encontrado",
                $"No existe el producto {productCode}."));
        }

        return Ok(result);
    }

    [HttpPost("movements")]
    [ProducesResponseType(typeof(InventoryMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InventoryMovementResponse>> RegisterMovementAsync(
        [FromBody] RegisterInventoryMovementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.RegisterMovementAsync(
            request,
            cancellationToken);

        if (result.Status == InventoryManagementStatus.Success &&
            result.Movement is not null)
        {
            return StatusCode(StatusCodes.Status201Created, result.Movement);
        }

        if (result.Status is
            InventoryManagementStatus.BranchNotFound or
            InventoryManagementStatus.ProductNotFound or
            InventoryManagementStatus.InventoryNotFound or
            InventoryManagementStatus.LotNotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Información no encontrada",
                result.Detail));
        }

        if (result.Status == InventoryManagementStatus.InsufficientStock)
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Existencias insuficientes",
                result.Detail));
        }

        return BadRequest(CreateProblem(
            StatusCodes.Status400BadRequest,
            "Movimiento inválido",
            result.Detail));
    }

    [HttpGet("kardex")]
    [ProducesResponseType(typeof(GetKardexResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetKardexResponse>> GetKardexAsync(
        [FromQuery] GetKardexRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _inventoryService.GetKardexAsync(request, cancellationToken));

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
