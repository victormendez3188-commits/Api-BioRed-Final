using BioRed.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/companies")]
[Authorize(Roles = "ADMIN")]
public sealed class CompaniesManagementController : ControllerBase
{
    private readonly ICompanyManagementService _companyService;

    public CompaniesManagementController(
        ICompanyManagementService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetManagedCompaniesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetManagedCompaniesResponse>> GetAsync(
        [FromQuery(Name = "search"), StringLength(100)] string? search = null,
        [FromQuery(Name = "includeInactive")] bool includeInactive = false,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var request = new GetManagedCompaniesRequest
        {
            Search = search,
            IncludeInactive = includeInactive,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _companyService.GetAsync(request, cancellationToken));
    }

    [HttpGet("{companyCode}", Name = "GetManagedCompanyByCode")]
    [ProducesResponseType(typeof(ManagedCompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedCompanyResponse>> GetByCodeAsync(
        string companyCode,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.GetByCodeAsync(
            companyCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagedCompanyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedCompanyResponse>> CreateAsync(
        [FromBody] CreateManagedCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateAsync(
            request,
            cancellationToken);

        if (result.Status == CompanyManagementStatus.Success &&
            result.Company is not null)
        {
            return CreatedAtRoute(
                "GetManagedCompanyByCode",
                new { companyCode = result.Company.CompanyCode },
                result.Company);
        }

        return ToActionResult(result);
    }

    [HttpPut("{companyCode}")]
    [ProducesResponseType(typeof(ManagedCompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedCompanyResponse>> UpdateAsync(
        string companyCode,
        [FromBody] UpdateManagedCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateAsync(
            companyCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{companyCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateAsync(
        string companyCode,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.DeactivateAsync(
            companyCode,
            cancellationToken);

        return result.Status == CompanyManagementStatus.Success
            ? NoContent()
            : NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Empresa no encontrada",
                result.Detail));
    }

    private ActionResult<ManagedCompanyResponse> ToActionResult(
        CompanyManagementResult result)
    {
        if (result.Status == CompanyManagementStatus.Success &&
            result.Company is not null)
        {
            return Ok(result.Company);
        }

        if (result.Status == CompanyManagementStatus.NotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Empresa no encontrada",
                result.Detail));
        }

        var title = result.Status == CompanyManagementStatus.DuplicateEmail
            ? "Correo duplicado"
            : "Empresa duplicada";

        return Conflict(CreateProblem(
            StatusCodes.Status409Conflict,
            title,
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
