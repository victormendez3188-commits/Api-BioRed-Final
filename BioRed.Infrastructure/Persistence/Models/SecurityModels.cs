namespace BioRed.Infrastructure.Persistence.Models;

public sealed class CuentaAcceso
{
    public int IdCuenta { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string TipoUsuario { get; set; } = string.Empty;
    public string ReferenciaId { get; set; } = string.Empty;
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public bool RequiereCambioPassword { get; set; }
    public DateTime? PasswordTemporalExpiraUtc { get; set; }
    public DateTime? PasswordActualizadaUtc { get; set; }
    public string EstadoEnvioPasswordTemporal { get; set; } = "NO_APLICA";
    public int IntentosEnvioPasswordTemporal { get; set; }
    public DateTime? UltimoEnvioPasswordTemporalUtc { get; set; }
    public string? DetalleFalloPasswordTemporal { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public sealed class RefreshToken
{
    public int IdToken { get; set; }
    public int IdCuenta { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaExpiracion { get; set; }
    public bool Revocado { get; set; }
    public CuentaAcceso Cuenta { get; set; } = null!;
}

public sealed class Usuario
{
    public string CodUsuario { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public string CodSucursal { get; set; } = string.Empty;
    public string CodRol { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string ApellidoUsuario { get; set; } = string.Empty;
    public string EmailUsuario { get; set; } = string.Empty;
    public bool PrimerCambio { get; set; }
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
}

public sealed class Cliente
{
    public string CodCliente { get; set; } = string.Empty;
    public int IdCliente { get; set; }
    public string CodSucursal { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string ApellidoCliente { get; set; } = string.Empty;
    public string CuiNit { get; set; } = string.Empty;
    public string? TelefonoCliente { get; set; }
    public string? EmailCliente { get; set; }
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
}

public sealed class Repartidor
{
    public string CodRepartidor { get; set; } = string.Empty;
    public int IdRepartidor { get; set; }
    public string NombreRepartidor { get; set; } = string.Empty;
    public string ApellidosRepartidor { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string DpiDocumento { get; set; } = string.Empty;
    public string TipoVehiculo { get; set; } = string.Empty;
    public string? PlacaVehiculo { get; set; }
    public decimal? LatitudActual { get; set; }
    public decimal? LongitudActual { get; set; }
    public string EstadoDisponibilidad { get; set; } = "Desconectado";
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public ICollection<UbicacionRepartidor> Ubicaciones { get; set; } = new List<UbicacionRepartidor>();
}

public sealed class Rol
{
    public string CodRol { get; set; } = string.Empty;
    public int IdRol { get; set; }
    public string NombreRol { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public ICollection<RolPermiso> Permisos { get; set; } = new List<RolPermiso>();
}

public sealed class Permiso
{
    public string CodPermiso { get; set; } = string.Empty;
    public string NombrePermiso { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public ICollection<RolPermiso> Roles { get; set; } = new List<RolPermiso>();
}

public sealed class RolPermiso
{
    public string CodRol { get; set; } = string.Empty;
    public string CodPermiso { get; set; } = string.Empty;
    public Rol Rol { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}
