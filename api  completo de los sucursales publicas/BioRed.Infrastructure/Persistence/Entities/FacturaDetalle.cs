using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("FacturaDetalle")]
[Index("ProductoId", "VentaId", Name = "IX_VentaDetalles_Producto")]
[Index("VentaDetalleId", "ProductoId", Name = "UQ_VentaDetalles_Detalle_Producto", IsUnique = true)]
[Index("VentaDetalleId", "VentaId", Name = "UQ_VentaDetalles_Detalle_Venta", IsUnique = true)]
public partial class FacturaDetalle
{
    [Key]
    public long VentaDetalleId { get; set; }

    public long VentaId { get; set; }

    public long ProductoId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal PrecioUnitario { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoUnitario { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal DescuentoMonto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal ImpuestoMonto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? TotalLinea { get; set; }

    [InverseProperty("FacturaDetalle")]
    public virtual ICollection<DevolucionDetalle> DevolucionDetalles { get; set; } = new List<DevolucionDetalle>();

    [InverseProperty("FacturaDetalle")]
    public virtual ICollection<FacturaDetalleInventario> FacturaDetalleInventarios { get; set; } = new List<FacturaDetalleInventario>();

    [ForeignKey("ProductoId")]
    [InverseProperty("FacturaDetalles")]
    public virtual Producto Producto { get; set; } = null!;

    [ForeignKey("VentaId")]
    [InverseProperty("FacturaDetalles")]
    public virtual FacturaEncabezado Venta { get; set; } = null!;

    [ForeignKey("VentaDetalleId")]
    [InverseProperty("VentaDetalles")]
    public virtual ICollection<Receta> Receta { get; set; } = new List<Receta>();
}
