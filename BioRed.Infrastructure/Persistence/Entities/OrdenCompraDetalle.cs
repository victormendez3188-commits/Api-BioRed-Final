using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("OrdenCompraDetalle")]
public partial class OrdenCompraDetalle
{
    [Key]
    public long OrdenCompraDetalleId { get; set; }

    public long OrdenCompraId { get; set; }

    public long ProductoId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal CantidadRecibida { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoUnitario { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal DescuentoMonto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal ImpuestoMonto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? TotalLinea { get; set; }

    [ForeignKey("OrdenCompraId")]
    [InverseProperty("OrdenCompraDetalles")]
    public virtual OrdenCompraEncabezado OrdenCompra { get; set; } = null!;

    [ForeignKey("ProductoId")]
    [InverseProperty("OrdenCompraDetalles")]
    public virtual Producto Producto { get; set; } = null!;
}
