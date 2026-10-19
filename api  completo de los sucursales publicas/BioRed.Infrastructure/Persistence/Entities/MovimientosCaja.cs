using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("MovimientosCaja")]
public partial class MovimientosCaja
{
    [Key]
    public long MovimientoCajaId { get; set; }

    public long TurnoCajaId { get; set; }

    public int MetodoPagoId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(10)]
    public string TipoMovimiento { get; set; } = null!;

    [StringLength(200)]
    public string Concepto { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [StringLength(120)]
    public string? Referencia { get; set; }

    [Precision(0)]
    public DateTime FechaMovimientoUtc { get; set; }

    [StringLength(10)]
    public string Estado { get; set; } = null!;

    [ForeignKey("MetodoPagoId")]
    [InverseProperty("MovimientosCajas")]
    public virtual Tipo_Pago MetodoPago { get; set; } = null!;

    [ForeignKey("TurnoCajaId")]
    [InverseProperty("MovimientosCajas")]
    public virtual TurnosCaja TurnoCaja { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("MovimientosCajas")]
    public virtual Usuario Usuario { get; set; } = null!;
}
