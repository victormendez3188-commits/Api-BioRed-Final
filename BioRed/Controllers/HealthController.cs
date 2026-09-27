using BioRed.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly BioRedDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        BioRedDbContext dbContext,
        ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("database")]
    public async Task<IActionResult> CheckDatabase(
        CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _dbContext.Database
                .CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        status = "Unhealthy",
                        database = "BioRed",
                        message = "No fue posible conectar con SQL Server."
                    });
            }

            return Ok(new
            {
                status = "Healthy",
                database = "BioRed",
                provider = _dbContext.Database.ProviderName,
                checkedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Error al comprobar la conexión con BioRed.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    status = "Unhealthy",
                    database = "BioRed",
                    message = "Ocurrió un error al conectar con SQL Server."
                });
        }
    }
}