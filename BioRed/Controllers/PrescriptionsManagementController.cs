using System.Security.Claims;
using BioRed.Application.Prescriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/prescriptions")]
[Authorize(Roles = "ADMIN")]
public sealed class PrescriptionsManagementController : ControllerBase
{
    private readonly IPrescriptionService _service;

    public PrescriptionsManagementController(IPrescriptionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetPrescriptionsResponse>> GetAsync(
        [FromQuery] GetPrescriptionsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetForManagementAsync(request, cancellationToken));

    [HttpPatch("{prescriptionId:long}/review")]
    public async Task<ActionResult<PrescriptionResponse>> ReviewAsync(
        long prescriptionId,
        [FromBody] ReviewPrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        var result = await _service.ReviewAsync(
            prescriptionId,
            actorCode,
            request,
            cancellationToken);

        if (result.Status == PrescriptionManagementStatus.Success &&
            result.Prescription is not null)
        {
            return Ok(result.Prescription);
        }

        var problem = new ProblemDetails
        {
            Status = result.Status == PrescriptionManagementStatus.InvalidTransition
                ? 409
                : result.Status is PrescriptionManagementStatus.NotFound or
                    PrescriptionManagementStatus.UserNotFound
                    ? 404
                    : 400,
            Title = "No se puede revisar la receta",
            Detail = result.Detail,
            Instance = HttpContext.Request.Path.Value
        };

        return problem.Status == 409
            ? Conflict(problem)
            : problem.Status == 404
                ? NotFound(problem)
                : BadRequest(problem);
    }
}
