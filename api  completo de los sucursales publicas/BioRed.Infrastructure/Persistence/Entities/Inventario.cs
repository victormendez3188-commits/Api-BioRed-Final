using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Inventario")]
[Index("ProductoId", "AlmacenId", Name = "IX_Existencias_Producto")]
[Index("ExistenciaId", "ProductoId", Name = "UQ_Existencias_Existencia_Producto", IsUnique = true)]
public partial class Inventario
{
    [Key]
    public long ExistenciaId { get; set; }

    public int AlmacenId { get; set; }

    public long ProductoId { get; set; }

    public long? LoteId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal CantidadReservada { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? CantidadDisponible { get; set; }

    [Precision(0)]
    public DateTime FechaActualizacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("AlmacenId")]
    [InverseProperty("Inventarios")]
    public virtual Almacene Almacen { get; set; } = null!;

    [InverseProperty("Inventario")]
    public virtual ICollection<FacturaDetalleInventario> FacturaDetalleInventarios { get; set; } = new List<FacturaDetalleInventario>();

    [ForeignKey("LoteId, ProductoId")]
    [InverseProperty("Inventarios")]
    public virtual Lote? Lote { get; set; }

    [ForeignKey("ProductoId")]
    [InverseProperty("Inventarios")]
    public virtual Producto Producto { get; set; } = null!;
}
