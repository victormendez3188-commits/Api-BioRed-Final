using System.Security.Claims;
using BioRed.Application.Promotions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/promotions")]
[Authorize(Roles = "CLIENTE")]
public sealed class PromotionsController : ControllerBase
{
    private readonly IPromotionManagementService _service;

    public PromotionsController(IPromotionManagementService service)
    {
        _service = service;
    }

    [HttpGet("available")]
    public async Task<ActionResult<GetPromotionsResponse>> GetAvailableAsync(
        [FromQuery] GetAvailablePromotionsRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(clientCode))
        {
            return Unauthorized();
        }

        return Ok(await _service.GetAvailableAsync(
            clientCode,
            request,
            cancellationToken));
    }
}
