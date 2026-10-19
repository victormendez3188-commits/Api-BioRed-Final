using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/suppliers")]
[Authorize(Roles = "ADMIN")]
public sealed class SuppliersManagementController : ControllerBase
{
    private readonly ISupplierManagementService _supplierService;

    public SuppliersManagementController(ISupplierManagementService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetManagedSuppliersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetManagedSuppliersResponse>> GetAsync(
        [FromQuery(Name = "companyCode"), StringLength(15)] string? companyCode = null,
        [FromQuery(Name = "search"), StringLength(100)] string? search = null,
        [FromQuery(Name = "includeInactive")] bool includeInactive = false,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var request = new GetManagedSuppliersRequest
        {
            CompanyCode = companyCode,
            Search = search,
            IncludeInactive = includeInactive,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _supplierService.GetAsync(request, cancellationToken));
    }

    [HttpGet("{supplierCode}", Name = "GetManagedSupplierByCode")]
    [ProducesResponseType(typeof(ManagedSupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedSupplierResponse>> GetByCodeAsync(
        string supplierCode,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetByCodeAsync(
            supplierCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagedSupplierResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedSupplierResponse>> CreateAsync(
        [FromBody] CreateManagedSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _supplierService.CreateAsync(
            actorReferenceId,
            request,
            cancellationToken);

        if (result.Status == SupplierManagementStatus.Success &&
            result.Supplier is not null)
        {
            return CreatedAtRoute(
                "GetManagedSupplierByCode",
                new { supplierCode = result.Supplier.SupplierCode },
                result.Supplier);
        }

        return ToActionResult(result);
    }

    [HttpPut("{supplierCode}")]
    [ProducesResponseType(typeof(ManagedSupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedSupplierResponse>> UpdateAsync(
        string supplierCode,
        [FromBody] UpdateManagedSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateAsync(
            supplierCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{supplierCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateAsync(
        string supplierCode,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.DeactivateAsync(
            supplierCode,
            cancellationToken);

        return result.Status == SupplierManagementStatus.Success
            ? NoContent()
            : NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Proveedor no encontrado",
                result.Detail));
    }

    private ActionResult<ManagedSupplierResponse> ToActionResult(
        SupplierManagementResult result)
    {
        if (result.Status == SupplierManagementStatus.Success &&
            result.Supplier is not null)
        {
            return Ok(result.Supplier);
        }

        if (result.Status == SupplierManagementStatus.NotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Proveedor no encontrado",
                result.Detail));
        }

        if (result.Status == SupplierManagementStatus.CompanyNotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Empresa no encontrada",
                result.Detail));
        }

        var title = result.Status switch
        {
            SupplierManagementStatus.DuplicateTaxId => "CUI/NIT duplicado",
            SupplierManagementStatus.DuplicateEmail => "Correo duplicado",
            _ => "Proveedor duplicado"
        };

        return Conflict(CreateProblem(
            StatusCodes.Status409Conflict,
            title,
            result.Detail));
    }

    private ProblemDetails CreateProblem(int status, string title, string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
