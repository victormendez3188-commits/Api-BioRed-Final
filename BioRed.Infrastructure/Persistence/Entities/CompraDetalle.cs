using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CompraDetalle")]
[Index("ProductoId", "CompraId", Name = "IX_CompraDetalles_Producto")]
[Index("CompraDetalleId", "CompraId", Name = "UQ_CompraDetalles_Detalle_Compra", IsUnique = true)]
public partial class CompraDetalle
{
    [Key]
    public long CompraDetalleId { get; set; }

    public long CompraId { get; set; }

    public long ProductoId { get; set; }

    public long? LoteId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoUnitario { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal DescuentoMonto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal ImpuestoMonto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? TotalLinea { get; set; }

    [ForeignKey("CompraId")]
    [InverseProperty("CompraDetalles")]
    public virtual CompraEncabezado Compra { get; set; } = null!;

    [InverseProperty("CompraDetalle")]
    public virtual ICollection<DevolucionCompraDetalle> DevolucionCompraDetalles { get; set; } = new List<DevolucionCompraDetalle>();

    [ForeignKey("LoteId, ProductoId")]
    [InverseProperty("CompraDetalles")]
    public virtual Lote? Lote { get; set; }

    [ForeignKey("ProductoId")]
    [InverseProperty("CompraDetalles")]
    public virtual Producto Producto { get; set; } = null!;
}
