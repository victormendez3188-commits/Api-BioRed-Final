using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("CorreoNormalizado", Name = "UQ_Usuarios_CorreoNormalizado", IsUnique = true)]
[Index("NombreUsuarioNormalizado", Name = "UQ_Usuarios_NombreUsuarioNormalizado", IsUnique = true)]
public partial class Usuario
{
    [Key]
    public long UsuarioId { get; set; }

    [StringLength(80)]
    public string NombreUsuario { get; set; } = null!;

    [StringLength(80)]
    public string NombreUsuarioNormalizado { get; set; } = null!;

    [StringLength(150)]
    public string Correo { get; set; } = null!;

    [StringLength(150)]
    public string CorreoNormalizado { get; set; } = null!;

    public bool CorreoConfirmado { get; set; }

    [StringLength(500)]
    public string? ContrasenaHash { get; set; }

    [StringLength(100)]
    public string SelloSeguridad { get; set; } = null!;

    [StringLength(100)]
    public string SelloConcurrencia { get; set; } = null!;

    [StringLength(100)]
    public string Nombres { get; set; } = null!;

    [StringLength(100)]
    public string Apellidos { get; set; } = null!;

    [StringLength(25)]
    public string? Telefono { get; set; }

    public bool TelefonoConfirmado { get; set; }

    public bool DosFactoresHabilitado { get; set; }

    public bool BloqueoHabilitado { get; set; }

    public bool DebeCambiarContrasena { get; set; }

    public short IntentosFallidos { get; set; }

    [Precision(0)]
    public DateTime? BloqueadoHastaUtc { get; set; }

    [Precision(0)]
    public DateTime? UltimoAccesoUtc { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Usuario")]
    public virtual ICollection<AuditoriaSistema> AuditoriaSistemas { get; set; } = new List<AuditoriaSistema>();

    [InverseProperty("CambiadoPorUsuario")]
    public virtual ICollection<Auditoria_Contrasena> Auditoria_ContrasenaCambiadoPorUsuarios { get; set; } = new List<Auditoria_Contrasena>();

    [InverseProperty("Usuario")]
    public virtual ICollection<Auditoria_Contrasena> Auditoria_ContrasenaUsuarios { get; set; } = new List<Auditoria_Contrasena>();

    [InverseProperty("Usuario")]
    public virtual ICollection<CompraEncabezado> CompraEncabezados { get; set; } = new List<CompraEncabezado>();

    [InverseProperty("Usuario")]
    public virtual ICollection<CxCDetalle> CxCDetalles { get; set; } = new List<CxCDetalle>();

    [InverseProperty("Usuario")]
    public virtual ICollection<CxPDetalle> CxPDetalles { get; set; } = new List<CxPDetalle>();

    [InverseProperty("Usuario")]
    public virtual ICollection<DevolucionCompraEncabezado> DevolucionCompraEncabezados { get; set; } = new List<DevolucionCompraEncabezado>();

    [InverseProperty("Usuario")]
    public virtual ICollection<DevolucionEncabezado> DevolucionEncabezados { get; set; } = new List<DevolucionEncabezado>();

    [InverseProperty("UsuarioAnulacion")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezadoUsuarioAnulacions { get; set; } = new List<FacturaEncabezado>();

    [InverseProperty("Usuario")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezadoUsuarios { get; set; } = new List<FacturaEncabezado>();

    [InverseProperty("Usuario")]
    public virtual ICollection<IntentosSesion> IntentosSesions { get; set; } = new List<IntentosSesion>();

    [InverseProperty("Usuario")]
    public virtual ICollection<Kardex> Kardices { get; set; } = new List<Kardex>();

    [InverseProperty("Usuario")]
    public virtual ICollection<MovimientosCaja> MovimientosCajas { get; set; } = new List<MovimientosCaja>();

    [InverseProperty("Usuario")]
    public virtual ICollection<OrdenCompraEncabezado> OrdenCompraEncabezados { get; set; } = new List<OrdenCompraEncabezado>();

    [InverseProperty("Usuario")]
    public virtual ICollection<PagosCompra> PagosCompras { get; set; } = new List<PagosCompra>();

    [InverseProperty("Usuario")]
    public virtual ICollection<PagosVentum> PagosVenta { get; set; } = new List<PagosVentum>();

    [InverseProperty("Usuario")]
    public virtual ICollection<RecuperacionContraseña> RecuperacionContraseñas { get; set; } = new List<RecuperacionContraseña>();

    [InverseProperty("Usuario")]
    public virtual ICollection<ReembolsosVentum> ReembolsosVenta { get; set; } = new List<ReembolsosVentum>();

    [InverseProperty("Usuario")]
    public virtual ICollection<TokensRenovacion> TokensRenovacions { get; set; } = new List<TokensRenovacion>();

    [InverseProperty("Usuario")]
    public virtual ICollection<TransaccionesBancaria> TransaccionesBancaria { get; set; } = new List<TransaccionesBancaria>();

    [InverseProperty("UsuarioApertura")]
    public virtual ICollection<TurnosCaja> TurnosCajaUsuarioAperturas { get; set; } = new List<TurnosCaja>();

    [InverseProperty("UsuarioCierre")]
    public virtual ICollection<TurnosCaja> TurnosCajaUsuarioCierres { get; set; } = new List<TurnosCaja>();

    [InverseProperty("Usuario")]
    public virtual ICollection<UsuarioClaim> UsuarioClaims { get; set; } = new List<UsuarioClaim>();

    [InverseProperty("Usuario")]
    public virtual ICollection<UsuarioLogin> UsuarioLogins { get; set; } = new List<UsuarioLogin>();

    [InverseProperty("Usuario")]
    public virtual ICollection<UsuarioRol> UsuarioRols { get; set; } = new List<UsuarioRol>();

    [InverseProperty("Usuario")]
    public virtual UsuarioSucursal? UsuarioSucursal { get; set; }

    [InverseProperty("Usuario")]
    public virtual ICollection<UsuarioToken> UsuarioTokens { get; set; } = new List<UsuarioToken>();
}
