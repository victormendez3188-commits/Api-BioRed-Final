using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/invoices")]
[Authorize(Roles = "ADMIN")]
public sealed class BillingManagementController : ControllerBase
{
    private readonly IBillingManagementService _service;

    public BillingManagementController(IBillingManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetManagedInvoicesResponse>> GetAsync(
        [FromQuery(Name = "branchCode"), StringLength(15)] string? branchCode = null,
        [FromQuery(Name = "clientCode"), StringLength(15)] string? clientCode = null,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(
            new GetManagedInvoicesRequest
            {
                BranchCode = branchCode,
                ClientCode = clientCode,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken));

    [HttpGet("{invoiceId:int}", Name = "GetManagedInvoiceById")]
    public async Task<ActionResult<ManagedInvoiceResponse>> GetByIdAsync(
        int invoiceId,
        CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetByIdAsync(invoiceId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ManagedInvoiceResponse>> CreateAsync(
        [FromBody] CreateManagedInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        var result = await _service.CreateFromOrderAsync(
            actorCode,
            request,
            cancellationToken);

        if (result.Status == BillingManagementStatus.Success &&
            result.Invoice is not null)
        {
            return CreatedAtRoute(
                "GetManagedInvoiceById",
                new { invoiceId = result.Invoice.InvoiceId },
                result.Invoice);
        }

        return ToActionResult(result);
    }

    private ActionResult<ManagedInvoiceResponse> ToActionResult(
        BillingManagementResult result)
    {
        if (result.Status == BillingManagementStatus.Success && result.Invoice is not null)
        {
            return Ok(result.Invoice);
        }

        if (result.Status is BillingManagementStatus.NotFound or
            BillingManagementStatus.OrderNotFound or
            BillingManagementStatus.UserNotFound)
        {
            return NotFound(ProblemResult(404, "Información no encontrada", result.Detail));
        }

        if (result.Status is BillingManagementStatus.DuplicateOrder or
            BillingManagementStatus.DuplicateDocument or
            BillingManagementStatus.StatusNotConfigured or
            BillingManagementStatus.OrderNotDelivered)
        {
            return Conflict(ProblemResult(409, "No se puede emitir la factura", result.Detail));
        }

        return BadRequest(ProblemResult(400, "Factura inválida", result.Detail));
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
