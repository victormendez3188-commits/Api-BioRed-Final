using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _service;
    private readonly IPaymentReceiptService _receiptService;

    public PaymentsController(
        IPaymentService service,
        IPaymentReceiptService receiptService)
    {
        _service = service;
        _receiptService = receiptService;
    }

    [HttpGet("orders/{orderId:int}", Name = "GetPaymentsByOrder")]
    public async Task<ActionResult<GetOrderPaymentsResponse>> GetByOrderAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByOrderAsync(
            orderId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            User.IsInRole("ADMIN"),
            cancellationToken);

        if (result.Status == PaymentManagementStatus.Success &&
            result.OrderPayments is not null)
        {
            return Ok(result.OrderPayments);
        }

        return MapProblem<GetOrderPaymentsResponse>(result);
    }

    [HttpPost("orders/{orderId:int}/paypal")]
    [Authorize(Roles = "CLIENTE")]
    public async Task<ActionResult<PaymentResponse>> CreatePayPalOrderAsync(
        int orderId,
        [FromBody] CreatePayPalOrderRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(clientCode) ||
            !int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var actorAccountId))
        {
            return Unauthorized();
        }

        var result = await _service.CreatePayPalOrderAsync(
            orderId,
            clientCode,
            actorAccountId,
            request,
            cancellationToken);

        if (result.Status == PaymentManagementStatus.Success &&
            result.Payment is not null)
        {
            return CreatedAtRoute(
                "GetPaymentsByOrder",
                new { orderId = result.Payment.OrderId },
                result.Payment);
        }

        return MapProblem<PaymentResponse>(result);
    }

    [HttpPost("paypal/{providerOrderId}/capture")]
    [Authorize(Roles = "CLIENTE")]
    public async Task<ActionResult<PaymentResponse>> CapturePayPalOrderAsync(
        [StringLength(100)] string providerOrderId,
        CancellationToken cancellationToken)
    {
        var clientCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(clientCode))
        {
            return Unauthorized();
        }

        var result = await _service.CapturePayPalOrderAsync(
            providerOrderId,
            clientCode,
            cancellationToken);

        return result.Status == PaymentManagementStatus.Success &&
               result.Payment is not null
            ? Ok(result.Payment)
            : MapProblem<PaymentResponse>(result);
    }

    [HttpGet("paypal/return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult PayPalReturn(
        [FromQuery] string? token,
        [FromQuery(Name = "PayerID")] string? payerId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ModelState.AddModelError(
                nameof(token),
                "PayPal no devolvió el identificador de la orden.");
            return ValidationProblem(ModelState);
        }

        return Ok(new
        {
            message = "Pago aprobado en PayPal. Regrese a Scalar para capturar la orden.",
            providerOrderId = token,
            payerId,
            captureEndpoint = $"/api/v1/payments/paypal/{Uri.EscapeDataString(token)}/capture"
        });
    }

    [HttpGet("paypal/cancel")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult PayPalCancel([FromQuery] string? token) =>
        Ok(new
        {
            message = "El comprador canceló la aprobación. No se realizó ningún cobro.",
            providerOrderId = token
        });

    [HttpGet("orders/{orderId:int}/receipt", Name = "GetPaymentReceiptByOrder")]
    [Authorize(Roles = "CLIENTE,ADMIN")]
    public async Task<ActionResult<PaymentReceiptResponse>> GetReceiptAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var result = await _receiptService.GetByOrderAsync(
            orderId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            User.IsInRole("ADMIN"),
            cancellationToken);

        return result.Status == PaymentReceiptStatus.Success &&
               result.Receipt is not null
            ? Ok(result.Receipt)
            : MapReceiptProblem(result);
    }

    [HttpPost("orders/{orderId:int}/receipt")]
    [Authorize(Roles = "CLIENTE,ADMIN")]
    [ProducesResponseType(typeof(PaymentReceiptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> DeliverReceiptAsync(
        int orderId,
        [FromBody] DeliverPaymentReceiptRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.DeliveryMethod.HasValue)
        {
            ModelState.AddModelError(
                nameof(request.DeliveryMethod),
                "Seleccione DOWNLOAD_PDF o EMAIL.");
            return ValidationProblem(ModelState);
        }

        var actorType = User.FindFirstValue("user_type") ?? string.Empty;
        var actorReference = User.FindFirstValue("reference_id") ?? string.Empty;
        var isAdministrator = User.IsInRole("ADMIN");

        var result = request.DeliveryMethod.Value == ReceiptDeliveryMethod.DownloadPdf
            ? await _receiptService.DownloadByOrderAsync(
                orderId,
                actorType,
                actorReference,
                isAdministrator,
                cancellationToken)
            : await _receiptService.EmailByOrderAsync(
                orderId,
                actorType,
                actorReference,
                isAdministrator,
                cancellationToken);

        if (result.Status != PaymentReceiptStatus.Success)
        {
            return MapReceiptProblem(result);
        }

        if (request.DeliveryMethod.Value == ReceiptDeliveryMethod.DownloadPdf &&
            result.File is not null)
        {
            return File(
                result.File.Content,
                "application/pdf",
                result.File.FileName);
        }

        return Ok(result.Receipt);
    }

    private ActionResult<T> MapProblem<T>(PaymentManagementResult result)
    {
        if (result.Status == PaymentManagementStatus.Forbidden)
        {
            return Forbid();
        }

        var (status, title) = result.Status switch
        {
            PaymentManagementStatus.NotFound =>
                (404, "Información no encontrada"),
            PaymentManagementStatus.ConfigurationError =>
                (503, "Integración de pagos no configurada"),
            PaymentManagementStatus.ProviderError =>
                (502, "Error del proveedor de pagos"),
            PaymentManagementStatus.InvalidAmount =>
                (400, "Monto de pago inválido"),
            _ =>
                (409, "No se puede completar el pago")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Detail,
            Instance = HttpContext.Request.Path.Value
        };

        return status switch
        {
            400 => BadRequest(problem),
            404 => NotFound(problem),
            502 => StatusCode(502, problem),
            503 => StatusCode(503, problem),
            _ => Conflict(problem)
        };
    }

    private ActionResult MapReceiptProblem(PaymentReceiptResult result)
    {
        if (result.Status == PaymentReceiptStatus.Forbidden)
        {
            return Forbid();
        }

        var (status, title) = result.Status switch
        {
            PaymentReceiptStatus.NotFound =>
                (404, "Información no encontrada"),
            PaymentReceiptStatus.PaymentNotCompleted =>
                (409, "Pago todavía no completado"),
            PaymentReceiptStatus.RegisteredEmailMissing =>
                (409, "Correo del cliente no registrado"),
            PaymentReceiptStatus.EmailNotConfigured =>
                (503, "Servicio de correo no configurado"),
            PaymentReceiptStatus.EmailDeliveryFailed =>
                (502, "No fue posible enviar el correo"),
            _ =>
                (409, "No se puede entregar el comprobante")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Detail,
            Instance = HttpContext.Request.Path.Value
        };

        return StatusCode(status, problem);
    }
}
