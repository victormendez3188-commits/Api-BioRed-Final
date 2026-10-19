using BioRed.Application.Reporting;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;

namespace BioRed.Infrastructure.Reporting;

public sealed class ApiAuditWriter : IApiAuditWriter
{
    private readonly BioRedDbContext _dbContext;

    public ApiAuditWriter(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        ApiAuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _dbContext.BitacoraApi.Add(new BitacoraApi
        {
            IdCuenta = request.AccountId is > 0 ? request.AccountId : null,
            TipoUsuario = Limit(request.UserType, 20),
            ReferenciaId = Limit(request.ReferenceId, 50),
            Modulo = Limit(request.Module, 50) ?? "api",
            MetodoHttp = Limit(request.HttpMethod.ToUpperInvariant(), 10) ?? "UNKNOWN",
            Ruta = Limit(request.Path, 300) ?? "/",
            CodigoEstado = Math.Clamp(request.StatusCode, 100, 599),
            DuracionMs = Math.Max(0, request.DurationMs),
            DireccionIp = Limit(request.IpAddress, 45),
            AgenteUsuario = Limit(request.UserAgent, 500),
            CorrelationId = Limit(request.CorrelationId, 100) ?? Guid.NewGuid().ToString("N"),
            FechaUtc = request.RegisteredAtUtc
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Limit(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength
            ? trimmed
            : trimmed[..maximumLength];
    }
}
