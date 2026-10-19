using System.Security.Claims;
using BioRed.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/orders/{orderId:int}/messages")]
[Authorize]
[EnableRateLimiting("messaging")]
public sealed class OrderMessagesController : ControllerBase
{
    private readonly IOrderMessageService _service;

    public OrderMessagesController(IOrderMessageService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetOrderMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetOrderMessagesResponse>> GetAsync(
        int orderId,
        [FromQuery] long afterMessageId = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAccount(out var accountId))
        {
            return Unauthorized();
        }

        var result = await _service.GetAsync(
            orderId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            afterMessageId,
            pageSize,
            cancellationToken);

        return result.Status switch
        {
            OrderMessageStatus.Success when result.Messages is not null => Ok(result.Messages),
            OrderMessageStatus.Forbidden => Forbid(),
            OrderMessageStatus.NotFound => NotFound(CreateProblem(404, "Pedido no encontrado", result.Detail)),
            OrderMessageStatus.InvalidRequest => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail)),
            _ => Conflict(CreateProblem(409, "No se pueden consultar los mensajes", result.Detail))
        };
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderMessageResponse>> SendAsync(
        int orderId,
        [FromBody] SendOrderMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccount(out var accountId))
        {
            return Unauthorized();
        }

        var result = await _service.SendAsync(
            orderId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            request,
            cancellationToken);

        return result.Status switch
        {
            OrderMessageStatus.Success when result.Message is not null =>
                StatusCode(StatusCodes.Status201Created, result.Message),
            OrderMessageStatus.Forbidden => Forbid(),
            OrderMessageStatus.NotFound => NotFound(CreateProblem(404, "Pedido no encontrado", result.Detail)),
            OrderMessageStatus.InvalidRequest => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail)),
            _ => Conflict(CreateProblem(409, "No se puede enviar el mensaje", result.Detail))
        };
    }

    [HttpPatch("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReadAsync(
        int orderId,
        [FromBody] MarkOrderMessagesReadRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccount(out var accountId))
        {
            return Unauthorized();
        }

        var result = await _service.MarkReadAsync(
            orderId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            request.LastMessageId,
            cancellationToken);

        return result.Status switch
        {
            OrderMessageStatus.Success => NoContent(),
            OrderMessageStatus.Forbidden => Forbid(),
            OrderMessageStatus.NotFound => NotFound(CreateProblem(404, "Pedido no encontrado", result.Detail)),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };
    }

    private bool TryGetAccount(out int accountId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId) && accountId > 0;

    private ProblemDetails CreateProblem(int status, string title, string? detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path.Value
    };
}
