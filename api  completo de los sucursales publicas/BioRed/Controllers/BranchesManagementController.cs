using BioRed.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/branches")]
[Authorize(Roles = "ADMIN")]
public sealed class BranchesManagementController : ControllerBase
{
    private readonly IBranchManagementService _branchService;

    public BranchesManagementController(IBranchManagementService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetManagedBranchesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetManagedBranchesResponse>> GetAsync(
        [FromQuery(Name = "companyCode"), StringLength(15)] string? companyCode = null,
        [FromQuery(Name = "search"), StringLength(100)] string? search = null,
        [FromQuery(Name = "includeInactive")] bool includeInactive = false,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var request = new GetManagedBranchesRequest
        {
            CompanyCode = companyCode,
            Search = search,
            IncludeInactive = includeInactive,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _branchService.GetAsync(request, cancellationToken));
    }

    [HttpGet("{branchCode}", Name = "GetManagedBranchByCode")]
    [ProducesResponseType(typeof(ManagedBranchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedBranchResponse>> GetByCodeAsync(
        string branchCode,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.GetByCodeAsync(
            branchCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagedBranchResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedBranchResponse>> CreateAsync(
        [FromBody] CreateManagedBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.CreateAsync(
            request,
            cancellationToken);

        if (result.Status == BranchManagementStatus.Success &&
            result.Branch is not null)
        {
            return CreatedAtRoute(
                "GetManagedBranchByCode",
                new { branchCode = result.Branch.BranchCode },
                result.Branch);
        }

        return ToActionResult(result);
    }

    [HttpPut("{branchCode}")]
    [ProducesResponseType(typeof(ManagedBranchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedBranchResponse>> UpdateAsync(
        string branchCode,
        [FromBody] UpdateManagedBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.UpdateAsync(
            branchCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("{branchCode}/delivery-configuration")]
    [ProducesResponseType(typeof(ManagedBranchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedBranchResponse>> UpsertDeliveryConfigurationAsync(
        string branchCode,
        [FromBody] UpsertDeliveryConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.UpsertDeliveryConfigurationAsync(
            branchCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{branchCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateAsync(
        string branchCode,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.DeactivateAsync(
            branchCode,
            cancellationToken);

        return result.Status == BranchManagementStatus.Success
            ? NoContent()
            : NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Sucursal no encontrada",
                result.Detail));
    }

    private ActionResult<ManagedBranchResponse> ToActionResult(
        BranchManagementResult result)
    {
        if (result.Status == BranchManagementStatus.Success &&
            result.Branch is not null)
        {
            return Ok(result.Branch);
        }

        if (result.Status == BranchManagementStatus.NotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Sucursal no encontrada",
                result.Detail));
        }

        if (result.Status == BranchManagementStatus.CompanyNotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Empresa no encontrada",
                result.Detail));
        }

        if (result.Status is
            BranchManagementStatus.DuplicateCode or
            BranchManagementStatus.DuplicateEmail or
            BranchManagementStatus.DuplicateData)
        {
            var title = result.Status == BranchManagementStatus.DuplicateEmail
                ? "Correo duplicado"
                : "Sucursal duplicada";

            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                title,
                result.Detail));
        }

        return BadRequest(CreateProblem(
            StatusCodes.Status400BadRequest,
            "Configuración inválida",
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
