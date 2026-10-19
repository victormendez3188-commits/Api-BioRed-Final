using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("FacturaDetalleInventario")]
[Index("VentaDetalleId", "ExistenciaId", Name = "UQ_VentaDetalleExistencias", IsUnique = true)]
public partial class FacturaDetalleInventario
{
    [Key]
    public long VentaDetalleExistenciaId { get; set; }

    public long VentaDetalleId { get; set; }

    public long ProductoId { get; set; }

    public long ExistenciaId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoUnitario { get; set; }

    [ForeignKey("VentaDetalleId, ProductoId")]
    [InverseProperty("FacturaDetalleInventarios")]
    public virtual FacturaDetalle FacturaDetalle { get; set; } = null!;

    [ForeignKey("ExistenciaId, ProductoId")]
    [InverseProperty("FacturaDetalleInventarios")]
    public virtual Inventario Inventario { get; set; } = null!;
}
