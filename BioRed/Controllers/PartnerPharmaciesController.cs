using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BioRed.Application.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/integrations/pharmacies/{companyCode}")]
[Authorize]
public sealed class PartnerPharmaciesController : ControllerBase
{
    private readonly IPartnerPharmacyService _partnerPharmacyService;

    public PartnerPharmaciesController(
        IPartnerPharmacyService partnerPharmacyService)
    {
        _partnerPharmacyService = partnerPharmacyService;
    }

    [HttpGet("products")]
    [ProducesResponseType(typeof(PartnerProductListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerProductListResponse>> GetProductsAsync(
        [FromRoute, Required, StringLength(15)] string companyCode,
        CancellationToken cancellationToken)
    {
        var result = await _partnerPharmacyService.GetProductsAsync(
            companyCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("products/{productCode}")]
    [ProducesResponseType(typeof(PartnerProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerProductResponse>> GetProductAsync(
        [FromRoute, Required, StringLength(15)] string companyCode,
        [FromRoute, Required, StringLength(50)] string productCode,
        CancellationToken cancellationToken)
    {
        var result = await _partnerPharmacyService.GetProductAsync(
            companyCode,
            productCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("inventory")]
    [ProducesResponseType(typeof(PartnerInventoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerInventoryListResponse>> GetInventoryAsync(
        [FromRoute, Required, StringLength(15)] string companyCode,
        CancellationToken cancellationToken)
    {
        var result = await _partnerPharmacyService.GetInventoryAsync(
            companyCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("inventory/{productCode}")]
    [ProducesResponseType(typeof(PartnerInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerInventoryResponse>> GetInventoryAsync(
        [FromRoute, Required, StringLength(15)] string companyCode,
        [FromRoute, Required, StringLength(50)] string productCode,
        CancellationToken cancellationToken)
    {
        var result = await _partnerPharmacyService.GetInventoryAsync(
            companyCode,
            productCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("orders")]
    [Authorize(Roles = "CLIENTE")]
    [ProducesResponseType(typeof(PartnerOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerOrderResponse>> CreateOrderAsync(
        [FromRoute, Required, StringLength(15)] string companyCode,
        [FromBody] CreatePartnerOrderRequest request,
        CancellationToken cancellationToken)
    {
        var clientName = User.FindFirstValue("full_name");

        if (string.IsNullOrWhiteSpace(clientName))
        {
            return Unauthorized();
        }

        var result = await _partnerPharmacyService.CreateOrderAsync(
            companyCode,
            clientName,
            request,
            cancellationToken);

        return ToActionResult(result, StatusCodes.Status201Created);
    }

    [HttpGet("orders/{orderCode}")]
    [ProducesResponseType(typeof(PartnerOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerOrderResponse>> GetOrderAsync(
        [FromRoute, Required, StringLength(15)] string companyCode,
        [FromRoute, Required, StringLength(50)] string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _partnerPharmacyService.GetOrderAsync(
            companyCode,
            orderCode,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<T> ToActionResult<T>(
        PartnerPharmacyResult<T> result,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.Status == PartnerPharmacyStatus.Success && result.Value is not null)
        {
            return StatusCode(successStatusCode, result.Value);
        }

        var (status, title) = result.Status switch
        {
            PartnerPharmacyStatus.PartnerNotConfigured =>
                (StatusCodes.Status404NotFound, "Integración no configurada"),

            PartnerPharmacyStatus.NotFound =>
                (StatusCodes.Status404NotFound, "Recurso no encontrado"),

            PartnerPharmacyStatus.InvalidRequest =>
                (StatusCodes.Status400BadRequest, "Solicitud rechazada"),

            PartnerPharmacyStatus.Unavailable =>
                (StatusCodes.Status503ServiceUnavailable, "Farmacia no disponible"),

            _ =>
                (StatusCodes.Status502BadGateway, "Respuesta externa inválida")
        };

        return StatusCode(
            status,
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = result.Detail,
                Instance = HttpContext.Request.Path.Value
            });
    }
}
