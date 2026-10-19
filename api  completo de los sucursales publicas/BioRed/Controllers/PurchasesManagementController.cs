using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/purchases")]
[Authorize(Roles = "ADMIN")]
public sealed class PurchasesManagementController : ControllerBase
{
    private readonly IPurchaseManagementService _purchaseService;

    public PurchasesManagementController(IPurchaseManagementService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetManagedPurchasesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetManagedPurchasesResponse>> GetAsync(
        [FromQuery(Name = "branchCode"), StringLength(15)] string? branchCode = null,
        [FromQuery(Name = "supplierCode"), StringLength(15)] string? supplierCode = null,
        [FromQuery(Name = "fromUtc")] DateTime? fromUtc = null,
        [FromQuery(Name = "toUtc")] DateTime? toUtc = null,
        [FromQuery(Name = "includeCancelled")] bool includeCancelled = false,
        [FromQuery(Name = "page"), Range(1, 1_000_000)] int page = 1,
        [FromQuery(Name = "pageSize"), Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            ModelState.AddModelError(
                nameof(fromUtc),
                "fromUtc no puede ser posterior a toUtc.");
            return ValidationProblem(ModelState);
        }

        var request = new GetManagedPurchasesRequest
        {
            BranchCode = branchCode,
            SupplierCode = supplierCode,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            IncludeCancelled = includeCancelled,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _purchaseService.GetAsync(request, cancellationToken));
    }

    [HttpGet("{purchaseId:int}", Name = "GetManagedPurchaseById")]
    [ProducesResponseType(typeof(ManagedPurchaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedPurchaseResponse>> GetByIdAsync(
        int purchaseId,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseService.GetByIdAsync(
            purchaseId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagedPurchaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedPurchaseResponse>> CreateAsync(
        [FromBody] CreateManagedPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _purchaseService.CreateAsync(
            actorReferenceId,
            request,
            cancellationToken);

        if (result.Status == PurchaseManagementStatus.Success &&
            result.Purchase is not null)
        {
            return CreatedAtRoute(
                "GetManagedPurchaseById",
                new { purchaseId = result.Purchase.PurchaseId },
                result.Purchase);
        }

        return ToActionResult(result);
    }

    [HttpPost("{purchaseId:int}/cancel")]
    [ProducesResponseType(typeof(ManagedPurchaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedPurchaseResponse>> CancelAsync(
        int purchaseId,
        [FromBody] CancelManagedPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _purchaseService.CancelAsync(
            purchaseId,
            actorReferenceId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<ManagedPurchaseResponse> ToActionResult(
        PurchaseManagementResult result)
    {
        if (result.Status == PurchaseManagementStatus.Success &&
            result.Purchase is not null)
        {
            return Ok(result.Purchase);
        }

        if (result.Status is
            PurchaseManagementStatus.NotFound or
            PurchaseManagementStatus.SupplierNotFound or
            PurchaseManagementStatus.BranchNotFound or
            PurchaseManagementStatus.UserNotFound or
            PurchaseManagementStatus.PaymentTypeNotFound or
            PurchaseManagementStatus.ProductNotFound)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Información no encontrada",
                result.Detail));
        }

        if (result.Status is
            PurchaseManagementStatus.DuplicateDocument or
            PurchaseManagementStatus.InventoryLimitExceeded or
            PurchaseManagementStatus.InsufficientStockToCancel or
            PurchaseManagementStatus.AlreadyCancelled or
            PurchaseManagementStatus.StatusNotConfigured)
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "No se puede completar la compra",
                result.Detail));
        }

        return BadRequest(CreateProblem(
            StatusCodes.Status400BadRequest,
            "Compra inválida",
            result.Detail));
    }

    private ProblemDetails CreateProblem(int status, string title, string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
