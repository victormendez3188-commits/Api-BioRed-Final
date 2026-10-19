using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("PreciosProducto")]
[Index("SucursalId", "ProductoId", "TipoPrecio", "Activo", "FechaInicioUtc", Name = "IX_PreciosProducto_Vigencia")]
public partial class PreciosProducto
{
    [Key]
    public long PrecioProductoId { get; set; }

    public int SucursalId { get; set; }

    public long ProductoId { get; set; }

    [StringLength(20)]
    public string TipoPrecio { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Precio { get; set; }

    [Precision(0)]
    public DateTime FechaInicioUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaFinUtc { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("ProductoId")]
    [InverseProperty("PreciosProductos")]
    public virtual Producto Producto { get; set; } = null!;

    [ForeignKey("SucursalId")]
    [InverseProperty("PreciosProductos")]
    public virtual Sucursal Sucursal { get; set; } = null!;
}
