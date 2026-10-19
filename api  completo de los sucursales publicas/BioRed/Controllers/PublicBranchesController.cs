using System.ComponentModel.DataAnnotations;
using BioRed.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

/// <summary>
/// Consulta pública utilizada por la pantalla de registro de clientes.
/// </summary>
[ApiController]
[Route("api/v1/public/branches")]
[AllowAnonymous]
public sealed class PublicBranchesController : ControllerBase
{
    private readonly IPublicBranchService _publicBranchService;

    public PublicBranchesController(IPublicBranchService publicBranchService)
    {
        _publicBranchService = publicBranchService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetPublicBranchesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GetPublicBranchesResponse>> GetAsync(
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPublicBranchesRequest
        {
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _publicBranchService.GetAsync(request, cancellationToken));
    }
}
