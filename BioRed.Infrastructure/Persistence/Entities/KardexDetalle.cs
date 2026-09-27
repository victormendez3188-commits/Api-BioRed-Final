using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("KardexDetalle")]
public partial class KardexDetalle
{
    [Key]
    public long MovimientoDetalleId { get; set; }

    public long MovimientoId { get; set; }

    public long ProductoId { get; set; }

    public long? LoteId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoUnitario { get; set; }

    [ForeignKey("LoteId, ProductoId")]
    [InverseProperty("KardexDetalles")]
    public virtual Lote? Lote { get; set; }

    [ForeignKey("MovimientoId")]
    [InverseProperty("KardexDetalles")]
    public virtual Kardex Movimiento { get; set; } = null!;

    [ForeignKey("ProductoId")]
    [InverseProperty("KardexDetalles")]
    public virtual Producto Producto { get; set; } = null!;
}
