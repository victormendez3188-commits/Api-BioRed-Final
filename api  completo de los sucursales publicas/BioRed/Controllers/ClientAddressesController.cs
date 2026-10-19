using System.Security.Claims;
using BioRed.Application.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/clients/me/addresses")]
[Authorize(Roles = "CLIENTE")]
public sealed class ClientAddressesController : ControllerBase
{
    private readonly IClientAddressService _addressService;

    public ClientAddressesController(IClientAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ClientAddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ClientAddressResponse>>> GetAsync(
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();

        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _addressService.GetAsync(
            clientCode,
            cancellationToken);

        return result.Status == ClientAddressStatus.Success &&
               result.Addresses is not null
            ? Ok(result.Addresses)
            : NotFound(CreateProblem(result.Detail));
    }

    [HttpGet(
        "{addressId:int}",
        Name = "GetClientAddressById")]
    [ProducesResponseType(typeof(ClientAddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAddressResponse>> GetByIdAsync(
        int addressId,
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();

        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _addressService.GetByIdAsync(
            clientCode,
            addressId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClientAddressResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAddressResponse>> CreateAsync(
        [FromBody] SaveClientAddressRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();

        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _addressService.CreateAsync(
            clientCode,
            request,
            cancellationToken);

        return result.Status == ClientAddressStatus.Success &&
               result.Address is not null
            ? CreatedAtRoute(
                "GetClientAddressById",
                new { addressId = result.Address.AddressId },
                result.Address)
            : NotFound(CreateProblem(result.Detail));
    }

    [HttpPut("{addressId:int}")]
    [ProducesResponseType(typeof(ClientAddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAddressResponse>> UpdateAsync(
        int addressId,
        [FromBody] SaveClientAddressRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();

        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _addressService.UpdateAsync(
            clientCode,
            addressId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("{addressId:int}/default")]
    [ProducesResponseType(typeof(ClientAddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAddressResponse>> SetDefaultAsync(
        int addressId,
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();

        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _addressService.SetDefaultAsync(
            clientCode,
            addressId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{addressId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAsync(
        int addressId,
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();

        if (clientCode is null)
        {
            return Unauthorized();
        }

        var status = await _addressService.DeactivateAsync(
            clientCode,
            addressId,
            cancellationToken);

        return status == ClientAddressStatus.Success
            ? NoContent()
            : NotFound(CreateProblem(
                "No se encontró una dirección activa perteneciente al cliente."));
    }

    private string? GetClientCode() =>
        User.FindFirstValue("reference_id");

    private ActionResult<ClientAddressResponse> ToActionResult(
        ClientAddressResult result) =>
        result.Status == ClientAddressStatus.Success &&
        result.Address is not null
            ? Ok(result.Address)
            : NotFound(CreateProblem(result.Detail));

    private ProblemDetails CreateProblem(string? detail) =>
        new()
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Dirección no encontrada",
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
