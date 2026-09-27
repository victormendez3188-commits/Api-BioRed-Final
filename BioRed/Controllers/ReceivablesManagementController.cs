using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/receivables")]
[Authorize(Roles = "ADMIN")]
public sealed class ReceivablesManagementController : ControllerBase
{
    private readonly IReceivableManagementService _service;

    public ReceivablesManagementController(IReceivableManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetReceivablesResponse>> GetAsync(
        [FromQuery(Name = "clientCode"), StringLength(15)] string? clientCode = null,
        [FromQuery(Name = "branchCode"), StringLength(15)] string? branchCode = null,
        [FromQuery(Name = "includePaid")] bool includePaid = false,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(
            new GetReceivablesRequest
            {
                ClientCode = clientCode,
                BranchCode = branchCode,
                IncludePaid = includePaid,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken));

    [HttpGet("{receivableId:int}")]
    public async Task<ActionResult<ReceivableResponse>> GetByIdAsync(
        int receivableId,
        CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetByIdAsync(receivableId, cancellationToken));

    [HttpPost("{receivableId:int}/payments")]
    public async Task<ActionResult<ReceivableResponse>> RegisterPaymentAsync(
        int receivableId,
        [FromBody] RegisterReceivablePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        return ToActionResult(await _service.RegisterPaymentAsync(
            receivableId,
            actorCode,
            request,
            cancellationToken));
    }

    private ActionResult<ReceivableResponse> ToActionResult(
        ReceivableManagementResult result)
    {
        if (result.Status == ReceivableManagementStatus.Success && result.Receivable is not null)
        {
            return Ok(result.Receivable);
        }

        if (result.Status is ReceivableManagementStatus.NotFound or
            ReceivableManagementStatus.UserNotFound or
            ReceivableManagementStatus.PaymentTypeNotFound)
        {
            return NotFound(ProblemResult(404, "Información no encontrada", result.Detail));
        }

        if (result.Status is ReceivableManagementStatus.AlreadyPaid or
            ReceivableManagementStatus.CashRequiresDeliveredOrder or
            ReceivableManagementStatus.PayPalRequiresVerifiedCapture or
            ReceivableManagementStatus.StatusNotConfigured)
        {
            return Conflict(ProblemResult(409, "No se puede registrar el cobro", result.Detail));
        }

        return BadRequest(ProblemResult(400, "Cobro inválido", result.Detail));
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
