using System.Security.Claims;
using BioRed.Application.Prescriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/prescriptions")]
[Authorize]
public sealed class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _service;

    public PrescriptionsController(IPrescriptionService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = "CLIENTE")]
    public async Task<ActionResult<GetPrescriptionsResponse>> GetMineAsync(
        [FromQuery] GetPrescriptionsRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(clientCode))
        {
            return Unauthorized();
        }

        return Ok(await _service.GetForClientAsync(
            clientCode,
            request,
            cancellationToken));
    }

    [HttpGet("{prescriptionId:long}", Name = "GetPrescriptionById")]
    public async Task<ActionResult<PrescriptionResponse>> GetByIdAsync(
        long prescriptionId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            prescriptionId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            User.IsInRole("ADMIN"),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("{prescriptionId:long}/document")]
    public async Task<IActionResult> DownloadDocumentAsync(
        long prescriptionId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetDocumentAsync(
            prescriptionId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            User.IsInRole("ADMIN"),
            cancellationToken);

        if (result.Status == PrescriptionManagementStatus.Success &&
            result.Document is not null)
        {
            return File(
                result.Document.Content,
                result.Document.ContentType,
                result.Document.FileName);
        }

        if (result.Status == PrescriptionManagementStatus.Forbidden)
        {
            return Forbid();
        }

        if (result.Status is
            PrescriptionManagementStatus.NotFound or
            PrescriptionManagementStatus.ClientNotFound or
            PrescriptionManagementStatus.UserNotFound or
            PrescriptionManagementStatus.ProductNotFound)
        {
            return NotFound(ProblemResult(
                404,
                "Información no encontrada",
                result.Detail));
        }

        if (result.Status == PrescriptionManagementStatus.InvalidTransition)
        {
            return Conflict(ProblemResult(
                409,
                "Transición de receta inválida",
                result.Detail));
        }

        return BadRequest(ProblemResult(
            400,
            "Receta inválida",
            result.Detail));
    }

    [HttpPost]
    [Authorize(Roles = "CLIENTE")]
    [RequestSizeLimit(7_500_000)]
    public async Task<ActionResult<PrescriptionResponse>> CreateAsync(
        [FromBody] CreatePrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(clientCode))
        {
            return Unauthorized();
        }

        var result = await _service.CreateAsync(
            clientCode,
            request,
            cancellationToken);

        if (result.Status == PrescriptionManagementStatus.Success &&
            result.Prescription is not null)
        {
            return CreatedAtRoute(
                "GetPrescriptionById",
                new { prescriptionId = result.Prescription.PrescriptionId },
                result.Prescription);
        }

        return ToActionResult(result);
    }

    private ActionResult<PrescriptionResponse> ToActionResult(
        PrescriptionManagementResult result)
    {
        if (result.Status == PrescriptionManagementStatus.Success &&
            result.Prescription is not null)
        {
            return Ok(result.Prescription);
        }

        if (result.Status == PrescriptionManagementStatus.Forbidden)
        {
            return Forbid();
        }

        if (result.Status is
            PrescriptionManagementStatus.NotFound or
            PrescriptionManagementStatus.ClientNotFound or
            PrescriptionManagementStatus.UserNotFound or
            PrescriptionManagementStatus.ProductNotFound)
        {
            return NotFound(ProblemResult(404, "Información no encontrada", result.Detail));
        }

        if (result.Status == PrescriptionManagementStatus.InvalidTransition)
        {
            return Conflict(ProblemResult(409, "Transición de receta inválida", result.Detail));
        }

        return BadRequest(ProblemResult(400, "Receta inválida", result.Detail));
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
