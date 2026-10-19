using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("DevolucionCompraDetalle")]
public partial class DevolucionCompraDetalle
{
    [Key]
    public long DevolucionCompraDetalleId { get; set; }

    public long DevolucionCompraId { get; set; }

    public long CompraId { get; set; }

    public long CompraDetalleId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [ForeignKey("CompraDetalleId, CompraId")]
    [InverseProperty("DevolucionCompraDetalles")]
    public virtual CompraDetalle CompraDetalle { get; set; } = null!;

    [ForeignKey("DevolucionCompraId, CompraId")]
    [InverseProperty("DevolucionCompraDetalles")]
    public virtual DevolucionCompraEncabezado DevolucionCompraEncabezado { get; set; } = null!;
}
