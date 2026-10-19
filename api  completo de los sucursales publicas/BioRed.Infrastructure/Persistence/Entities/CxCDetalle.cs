using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CxCDetalle")]
public partial class CxCDetalle
{
    [Key]
    public long MovimientoCxCId { get; set; }

    public long CuentaPorCobrarId { get; set; }

    public long? PagoVentaId { get; set; }

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

    [ForeignKey("CuentaPorCobrarId")]
    [InverseProperty("CxCDetalles")]
    public virtual CxCEncabezado CuentaPorCobrar { get; set; } = null!;

    [ForeignKey("PagoVentaId")]
    [InverseProperty("CxCDetalles")]
    public virtual PagosVentum? PagoVenta { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("CxCDetalles")]
    public virtual Usuario Usuario { get; set; } = null!;
}
