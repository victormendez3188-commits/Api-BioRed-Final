using System.Security.Claims;
using BioRed.Application.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/pharmacy-associations")]
[Authorize(Roles = "ADMIN")]
public sealed class PharmacyAssociationsController : ControllerBase
{
    private readonly IPharmacyAssociationService _associationService;

    public PharmacyAssociationsController(
        IPharmacyAssociationService associationService)
    {
        _associationService = associationService;
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PharmacyAssociationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PharmacyAssociationResponse>> CreateAsync(
        [FromBody] CreatePharmacyAssociationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _associationService.CreateAsync(
            request,
            cancellationToken);

        return result.Status switch
        {
            PharmacyAssociationStatus.Success when result.Association is not null =>
                CreatedAtRoute(
                    "GetPharmacyAssociationById",
                    new { associationId = result.Association.AssociationId },
                    result.Association),

            PharmacyAssociationStatus.Duplicate =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Solicitud duplicada",
                    result.Detail)),

            _ =>
                BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "URL de API inválida",
                    result.Detail))
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PharmacyAssociationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PharmacyAssociationResponse>>> GetAllAsync(
        CancellationToken cancellationToken) =>
        Ok(await _associationService.GetAllAsync(cancellationToken));

    [HttpGet("{associationId:int}", Name = "GetPharmacyAssociationById")]
    [ProducesResponseType(typeof(PharmacyAssociationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PharmacyAssociationResponse>> GetByIdAsync(
        int associationId,
        CancellationToken cancellationToken)
    {
        var result = await _associationService.GetByIdAsync(
            associationId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("{associationId:int}/validate")]
    [ProducesResponseType(typeof(PharmacyAssociationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PharmacyAssociationResponse>> ValidateAsync(
        int associationId,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _associationService.ValidateConnectionAsync(
            associationId,
            actorReferenceId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("{associationId:int}/approve")]
    [ProducesResponseType(typeof(PharmacyAssociationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PharmacyAssociationResponse>> ApproveAsync(
        int associationId,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _associationService.ApproveAsync(
            associationId,
            actorReferenceId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("{associationId:int}/suspend")]
    [ProducesResponseType(typeof(PharmacyAssociationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PharmacyAssociationResponse>> SuspendAsync(
        int associationId,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _associationService.SuspendAsync(
            associationId,
            actorReferenceId,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<PharmacyAssociationResponse> ToActionResult(
        PharmacyAssociationResult result)
    {
        if (result.Status == PharmacyAssociationStatus.Success &&
            result.Association is not null)
        {
            return Ok(result.Association);
        }

        var (status, title) = result.Status switch
        {
            PharmacyAssociationStatus.NotFound =>
                (StatusCodes.Status404NotFound, "Solicitud no encontrada"),

            PharmacyAssociationStatus.ConnectionFailed =>
                (StatusCodes.Status503ServiceUnavailable, "Conexión no disponible"),

            _ =>
                (StatusCodes.Status409Conflict, "Operación no permitida")
        };

        return StatusCode(status, CreateProblem(status, title, result.Detail));
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
