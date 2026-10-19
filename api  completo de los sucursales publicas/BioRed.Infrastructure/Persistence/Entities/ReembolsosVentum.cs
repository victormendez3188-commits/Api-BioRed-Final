using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

public partial class ReembolsosVentum
{
    [Key]
    public long ReembolsoVentaId { get; set; }

    public long DevolucionVentaId { get; set; }

    public long TurnoCajaId { get; set; }

    public int MetodoPagoId { get; set; }

    public long UsuarioId { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [StringLength(120)]
    public string? Referencia { get; set; }

    [Precision(0)]
    public DateTime FechaReembolsoUtc { get; set; }

    [StringLength(10)]
    public string Estado { get; set; } = null!;

    [ForeignKey("DevolucionVentaId")]
    [InverseProperty("ReembolsosVenta")]
    public virtual DevolucionEncabezado DevolucionVenta { get; set; } = null!;

    [ForeignKey("MetodoPagoId")]
    [InverseProperty("ReembolsosVenta")]
    public virtual Tipo_Pago MetodoPago { get; set; } = null!;

    [ForeignKey("TurnoCajaId")]
    [InverseProperty("ReembolsosVenta")]
    public virtual TurnosCaja TurnoCaja { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("ReembolsosVenta")]
    public virtual Usuario Usuario { get; set; } = null!;
}
