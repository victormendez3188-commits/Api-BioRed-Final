using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("FacturaEncabezado")]
[Index("SucursalId", "FechaVentaUtc", Name = "IX_Ventas_Fecha_Sucursal", IsDescending = new[] { false, true })]
[Index("SucursalId", "SerieDocumento", "NumeroDocumento", Name = "UQ_Ventas_Sucursal_Serie_Numero", IsUnique = true)]
public partial class FacturaEncabezado
{
    [Key]
    public long VentaId { get; set; }

    public int SucursalId { get; set; }

    public int AlmacenId { get; set; }

    public int CajaId { get; set; }

    public long TurnoCajaId { get; set; }

    public long? ClienteId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(10)]
    public string CanalVenta { get; set; } = null!;

    [StringLength(20)]
    public string TipoDocumento { get; set; } = null!;

    [StringLength(30)]
    public string SerieDocumento { get; set; } = null!;

    [StringLength(50)]
    public string NumeroDocumento { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaVentaUtc { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    [StringLength(10)]
    public string CondicionPago { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Descuento { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Impuesto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Total { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal SaldoPendiente { get; set; }

    public Guid? FelUuid { get; set; }

    [StringLength(50)]
    public string? FelSerie { get; set; }

    [StringLength(50)]
    public string? FelNumero { get; set; }

    [Precision(0)]
    public DateTime? FelFechaCertificacionUtc { get; set; }

    [StringLength(15)]
    public string? FelEstado { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    [Precision(0)]
    public DateTime? FechaAnulacionUtc { get; set; }

    public long? UsuarioAnulacionId { get; set; }

    [StringLength(300)]
    public string? MotivoAnulacion { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("AlmacenId, SucursalId")]
    [InverseProperty("FacturaEncabezados")]
    public virtual Almacene Almacene { get; set; } = null!;

    [ForeignKey("CajaId, SucursalId")]
    [InverseProperty("FacturaEncabezados")]
    public virtual Caja Caja { get; set; } = null!;

    [ForeignKey("ClienteId")]
    [InverseProperty("FacturaEncabezados")]
    public virtual Cliente? Cliente { get; set; }

    [InverseProperty("Venta")]
    public virtual CxCEncabezado? CxCEncabezado { get; set; }

    [InverseProperty("Venta")]
    public virtual ICollection<DevolucionEncabezado> DevolucionEncabezados { get; set; } = new List<DevolucionEncabezado>();

    [InverseProperty("Venta")]
    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();

    [InverseProperty("Venta")]
    public virtual ICollection<PagosVentum> PagosVenta { get; set; } = new List<PagosVentum>();

    [ForeignKey("SucursalId")]
    [InverseProperty("FacturaEncabezados")]
    public virtual Sucursal Sucursal { get; set; } = null!;

    [ForeignKey("TurnoCajaId, CajaId")]
    [InverseProperty("FacturaEncabezados")]
    public virtual TurnosCaja TurnosCaja { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("FacturaEncabezadoUsuarios")]
    public virtual Usuario Usuario { get; set; } = null!;

    [ForeignKey("UsuarioAnulacionId")]
    [InverseProperty("FacturaEncabezadoUsuarioAnulacions")]
    public virtual Usuario? UsuarioAnulacion { get; set; }
}
