using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("ProductoId", "PrincipioActivoId")]
public partial class ProductoPrincipiosActivo
{
    [Key]
    public long ProductoId { get; set; }

    [Key]
    public int PrincipioActivoId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Cantidad { get; set; }

    [StringLength(30)]
    public string? Unidad { get; set; }

    [ForeignKey("PrincipioActivoId")]
    [InverseProperty("ProductoPrincipiosActivos")]
    public virtual PrincipiosActivo PrincipioActivo { get; set; } = null!;

    [ForeignKey("ProductoId")]
    [InverseProperty("ProductoPrincipiosActivos")]
    public virtual Producto Producto { get; set; } = null!;
}
