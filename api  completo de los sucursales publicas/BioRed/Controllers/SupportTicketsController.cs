using System.Security.Claims;
using BioRed.Application.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/support/tickets")]
[Authorize(Roles = "CLIENTE")]
[EnableRateLimiting("messaging")]
public sealed class SupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _service;

    public SupportTicketsController(ISupportTicketService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SupportTicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupportTicketResponse>> CreateAsync(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var accountId, out var referenceId))
        {
            return Unauthorized();
        }

        var result = await _service.CreateAsync(
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            referenceId,
            request,
            cancellationToken);

        return result.Status switch
        {
            SupportTicketOperationStatus.Success when result.Ticket is not null =>
                StatusCode(StatusCodes.Status201Created, result.Ticket),
            SupportTicketOperationStatus.NotFound =>
                NotFound(CreateProblem(404, "Información no encontrada", result.Detail)),
            SupportTicketOperationStatus.Forbidden => Forbid(),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(SupportTicketsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SupportTicketsResponse>> GetAsync(
        [FromQuery] GetSupportTicketsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var accountId, out var referenceId))
        {
            return Unauthorized();
        }

        var result = await _service.GetAsync(
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            referenceId,
            false,
            request,
            cancellationToken);

        return result.Status switch
        {
            SupportTicketOperationStatus.Success when result.Tickets is not null => Ok(result.Tickets),
            SupportTicketOperationStatus.Forbidden => Forbid(),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };
    }

    [HttpGet("{ticketId:long}")]
    [ProducesResponseType(typeof(SupportTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupportTicketResponse>> GetByIdAsync(
        long ticketId,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var accountId, out var referenceId))
        {
            return Unauthorized();
        }

        var result = await _service.GetByIdAsync(
            ticketId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            referenceId,
            false,
            cancellationToken);

        return result.Status switch
        {
            SupportTicketOperationStatus.Success when result.Ticket is not null => Ok(result.Ticket),
            SupportTicketOperationStatus.Forbidden => Forbid(),
            SupportTicketOperationStatus.NotFound =>
                NotFound(CreateProblem(404, "Ticket no encontrado", result.Detail)),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };
    }

    [HttpGet("{ticketId:long}/messages")]
    [ProducesResponseType(typeof(SupportTicketMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupportTicketMessagesResponse>> GetMessagesAsync(
        long ticketId,
        [FromQuery] long afterMessageId = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var accountId, out var referenceId))
        {
            return Unauthorized();
        }

        var result = await _service.GetMessagesAsync(
            ticketId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            referenceId,
            false,
            afterMessageId,
            pageSize,
            cancellationToken);

        return result.Status switch
        {
            SupportTicketOperationStatus.Success when result.Messages is not null => Ok(result.Messages),
            SupportTicketOperationStatus.Forbidden => Forbid(),
            SupportTicketOperationStatus.NotFound =>
                NotFound(CreateProblem(404, "Ticket no encontrado", result.Detail)),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };
    }

    [HttpPost("{ticketId:long}/messages")]
    [ProducesResponseType(typeof(SupportTicketMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupportTicketMessageResponse>> SendMessageAsync(
        long ticketId,
        [FromBody] SendSupportTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var accountId, out var referenceId))
        {
            return Unauthorized();
        }

        var result = await _service.SendMessageAsync(
            ticketId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            referenceId,
            false,
            request,
            cancellationToken);

        return result.Status switch
        {
            SupportTicketOperationStatus.Success when result.Message is not null =>
                StatusCode(StatusCodes.Status201Created, result.Message),
            SupportTicketOperationStatus.Forbidden => Forbid(),
            SupportTicketOperationStatus.NotFound =>
                NotFound(CreateProblem(404, "Ticket no encontrado", result.Detail)),
            SupportTicketOperationStatus.InvalidState =>
                Conflict(CreateProblem(409, "El ticket no acepta mensajes", result.Detail)),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };
    }

    private bool TryGetIdentity(out int accountId, out string referenceId)
    {
        referenceId = User.FindFirstValue("reference_id") ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId) &&
               accountId > 0 &&
               !string.IsNullOrWhiteSpace(referenceId);
    }

    private ProblemDetails CreateProblem(int status, string title, string? detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path.Value
    };
}
