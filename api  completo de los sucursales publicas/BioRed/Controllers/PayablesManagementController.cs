using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/payables")]
[Authorize(Roles = "ADMIN")]
public sealed class PayablesManagementController : ControllerBase
{
    private readonly IPayableManagementService _service;

    public PayablesManagementController(IPayableManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetPayablesResponse>> GetAsync(
        [FromQuery(Name = "supplierCode"), StringLength(15)] string? supplierCode = null,
        [FromQuery(Name = "branchCode"), StringLength(15)] string? branchCode = null,
        [FromQuery(Name = "includePaid")] bool includePaid = false,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(
            new GetPayablesRequest
            {
                SupplierCode = supplierCode,
                BranchCode = branchCode,
                IncludePaid = includePaid,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken));

    [HttpGet("{payableId:int}", Name = "GetManagedPayableById")]
    public async Task<ActionResult<PayableResponse>> GetByIdAsync(
        int payableId,
        CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetByIdAsync(payableId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PayableResponse>> CreateAsync(
        [FromBody] CreatePayableFromPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        var result = await _service.CreateFromPurchaseAsync(
            actorCode,
            request,
            cancellationToken);

        if (result.Status == PayableManagementStatus.Success && result.Payable is not null)
        {
            return CreatedAtRoute(
                "GetManagedPayableById",
                new { payableId = result.Payable.PayableId },
                result.Payable);
        }

        return ToActionResult(result);
    }

    [HttpPost("{payableId:int}/payments")]
    public async Task<ActionResult<PayableResponse>> RegisterPaymentAsync(
        int payableId,
        [FromBody] RegisterPayablePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        return ToActionResult(await _service.RegisterPaymentAsync(
            payableId,
            actorCode,
            request,
            cancellationToken));
    }

    private ActionResult<PayableResponse> ToActionResult(PayableManagementResult result)
    {
        if (result.Status == PayableManagementStatus.Success && result.Payable is not null)
        {
            return Ok(result.Payable);
        }

        if (result.Status is PayableManagementStatus.NotFound or
            PayableManagementStatus.PurchaseNotFound or
            PayableManagementStatus.UserNotFound or
            PayableManagementStatus.PaymentTypeNotFound)
        {
            return NotFound(ProblemResult(404, "Información no encontrada", result.Detail));
        }

        if (result.Status is PayableManagementStatus.DuplicatePurchase or
            PayableManagementStatus.PurchaseCancelled or
            PayableManagementStatus.AlreadyPaid or
            PayableManagementStatus.StatusNotConfigured)
        {
            return Conflict(ProblemResult(409, "No se puede completar la cuenta por pagar", result.Detail));
        }

        return BadRequest(ProblemResult(400, "Cuenta por pagar inválida", result.Detail));
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
