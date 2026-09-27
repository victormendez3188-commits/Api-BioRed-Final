using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CxPDetalle")]
public partial class CxPDetalle
{
    [Key]
    public long MovimientoCxPId { get; set; }

    public long CuentaPorPagarId { get; set; }

    public long? PagoCompraId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(15)]
    public string TipoMovimiento { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal SaldoAnterior { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal SaldoNuevo { get; set; }

    [StringLength(300)]
    public string? Observaciones { get; set; }

    [Precision(0)]
    public DateTime FechaMovimientoUtc { get; set; }

    [ForeignKey("CuentaPorPagarId")]
    [InverseProperty("CxPDetalles")]
    public virtual CxPEncabezado CuentaPorPagar { get; set; } = null!;

    [ForeignKey("PagoCompraId")]
    [InverseProperty("CxPDetalles")]
    public virtual PagosCompra? PagoCompra { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("CxPDetalles")]
    public virtual Usuario Usuario { get; set; } = null!;
}
