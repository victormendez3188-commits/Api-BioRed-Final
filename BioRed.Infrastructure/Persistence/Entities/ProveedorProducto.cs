using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("ProveedorId", "ProductoId")]
public partial class ProveedorProducto
{
    [Key]
    public long ProveedorId { get; set; }

    [Key]
    public long ProductoId { get; set; }

    [StringLength(80)]
    public string? CodigoProveedor { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? CostoUltimo { get; set; }

    public short? DiasEntrega { get; set; }

    public bool EsPreferido { get; set; }

    public bool Activo { get; set; }

    [ForeignKey("ProductoId")]
    [InverseProperty("ProveedorProducto")]
    public virtual Producto Producto { get; set; } = null!;

    [ForeignKey("ProveedorId")]
    [InverseProperty("ProveedorProductos")]
    public virtual Proveedore Proveedor { get; set; } = null!;
}
