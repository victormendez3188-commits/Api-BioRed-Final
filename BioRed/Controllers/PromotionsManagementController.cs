using System.Security.Claims;
using BioRed.Application.Promotions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/promotions")]
[Authorize(Roles = "ADMIN")]
public sealed class PromotionsManagementController : ControllerBase
{
    private readonly IPromotionManagementService _service;

    public PromotionsManagementController(IPromotionManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetPromotionsResponse>> GetAsync(
        [FromQuery] GetManagedPromotionsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetAsync(request, cancellationToken));

    [HttpGet("{promotionCode}", Name = "GetManagedPromotionByCode")]
    public async Task<ActionResult<PromotionResponse>> GetByCodeAsync(
        string promotionCode,
        CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetByCodeAsync(
            promotionCode,
            cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PromotionResponse>> CreateAsync(
        [FromBody] SavePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        var result = await _service.CreateAsync(
            actorCode,
            request,
            cancellationToken);

        if (result.Status == PromotionManagementStatus.Success &&
            result.Promotion is not null)
        {
            return CreatedAtRoute(
                "GetManagedPromotionByCode",
                new { promotionCode = result.Promotion.PromotionCode },
                result.Promotion);
        }

        return ToActionResult(result);
    }

    [HttpPut("{promotionCode}")]
    public async Task<ActionResult<PromotionResponse>> UpdateAsync(
        string promotionCode,
        [FromBody] UpdatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        return ToActionResult(await _service.UpdateAsync(
            promotionCode,
            actorCode,
            request,
            cancellationToken));
    }

    [HttpDelete("{promotionCode}")]
    public async Task<ActionResult<PromotionResponse>> DeactivateAsync(
        string promotionCode,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        return ToActionResult(await _service.DeactivateAsync(
            promotionCode,
            actorCode,
            cancellationToken));
    }

    private ActionResult<PromotionResponse> ToActionResult(
        PromotionManagementResult result)
    {
        if (result.Status == PromotionManagementStatus.Success &&
            result.Promotion is not null)
        {
            return Ok(result.Promotion);
        }

        if (result.Status is
            PromotionManagementStatus.NotFound or
            PromotionManagementStatus.CompanyNotFound or
            PromotionManagementStatus.BranchNotFound or
            PromotionManagementStatus.ProductNotFound)
        {
            return NotFound(ProblemResult(
                404,
                "Información no encontrada",
                result.Detail));
        }

        if (result.Status is
            PromotionManagementStatus.Duplicate or
            PromotionManagementStatus.AlreadyUsed)
        {
            return Conflict(ProblemResult(
                409,
                "No se puede completar la promoción",
                result.Detail));
        }

        return BadRequest(ProblemResult(
            400,
            "Promoción inválida",
            result.Detail));
    }

    private ProblemDetails ProblemResult(int status, string title, string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
