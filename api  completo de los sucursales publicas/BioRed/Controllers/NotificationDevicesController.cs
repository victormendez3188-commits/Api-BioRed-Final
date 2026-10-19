using System.Security.Claims;
using BioRed.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/notification-devices")]
[Authorize]
[EnableRateLimiting("notification-devices")]
public sealed class NotificationDevicesController : ControllerBase
{
    private readonly IDeviceNotificationService _service;

    public NotificationDevicesController(IDeviceNotificationService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(NotificationDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationDeviceResponse>> RegisterAsync(
        [FromBody] RegisterNotificationDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized();
        }

        return Ok(await _service.RegisterAsync(accountId, request, cancellationToken));
    }

    [HttpPost("unregister")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnregisterAsync(
        [FromBody] UnregisterNotificationDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized();
        }

        await _service.UnregisterAsync(accountId, request.Token, cancellationToken);
        return NoContent();
    }

    private bool TryGetAccountId(out int accountId) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out accountId) && accountId > 0;
}
