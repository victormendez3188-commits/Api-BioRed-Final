using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("DevolucionDetalle")]
public partial class DevolucionDetalle
{
    [Key]
    public long DevolucionVentaDetalleId { get; set; }

    public long DevolucionVentaId { get; set; }

    public long VentaId { get; set; }

    public long VentaDetalleId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [ForeignKey("DevolucionVentaId, VentaId")]
    [InverseProperty("DevolucionDetalles")]
    public virtual DevolucionEncabezado DevolucionEncabezado { get; set; } = null!;

    [ForeignKey("VentaDetalleId, VentaId")]
    [InverseProperty("DevolucionDetalles")]
    public virtual FacturaDetalle FacturaDetalle { get; set; } = null!;
}
