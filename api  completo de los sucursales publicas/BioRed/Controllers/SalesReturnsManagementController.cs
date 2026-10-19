using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/returns")]
[Authorize(Roles = "ADMIN")]
public sealed class SalesReturnsManagementController : ControllerBase
{
    private readonly ISalesReturnManagementService _service;

    public SalesReturnsManagementController(ISalesReturnManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetManagedReturnsResponse>> GetAsync(
        [FromQuery(Name = "branchCode"), StringLength(15)] string? branchCode = null,
        [FromQuery(Name = "clientCode"), StringLength(15)] string? clientCode = null,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(
            new GetManagedReturnsRequest
            {
                BranchCode = branchCode,
                ClientCode = clientCode,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken));

    [HttpGet("{returnId:int}", Name = "GetManagedReturnById")]
    public async Task<ActionResult<ManagedReturnResponse>> GetByIdAsync(
        int returnId,
        CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetByIdAsync(returnId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ManagedReturnResponse>> CreateAsync(
        [FromBody] CreateManagedReturnRequest request,
        CancellationToken cancellationToken)
    {
        var actorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorCode))
        {
            return Unauthorized();
        }

        var result = await _service.CreateAsync(actorCode, request, cancellationToken);
        if (result.Status == SalesReturnManagementStatus.Success && result.Return is not null)
        {
            return CreatedAtRoute(
                "GetManagedReturnById",
                new { returnId = result.Return.ReturnId },
                result.Return);
        }

        return ToActionResult(result);
    }

    private ActionResult<ManagedReturnResponse> ToActionResult(
        SalesReturnManagementResult result)
    {
        if (result.Status == SalesReturnManagementStatus.Success && result.Return is not null)
        {
            return Ok(result.Return);
        }

        if (result.Status is SalesReturnManagementStatus.NotFound or
            SalesReturnManagementStatus.InvoiceNotFound or
            SalesReturnManagementStatus.UserNotFound or
            SalesReturnManagementStatus.LotNotFound)
        {
            return NotFound(ProblemResult(404, "Información no encontrada", result.Detail));
        }

        if (result.Status is SalesReturnManagementStatus.QuantityExceeded or
            SalesReturnManagementStatus.InventoryLimitExceeded or
            SalesReturnManagementStatus.StatusNotConfigured)
        {
            return Conflict(ProblemResult(409, "No se puede registrar la devolución", result.Detail));
        }

        return BadRequest(ProblemResult(400, "Devolución inválida", result.Detail));
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
