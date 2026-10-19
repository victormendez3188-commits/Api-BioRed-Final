using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CodigosBarraProducto")]
[Index("CodigoBarra", Name = "UQ_CodigosBarraProducto_Codigo", IsUnique = true)]
public partial class CodigosBarraProducto
{
    [Key]
    public long CodigoBarraId { get; set; }

    public long ProductoId { get; set; }

    [StringLength(80)]
    public string CodigoBarra { get; set; } = null!;

    public bool EsPrincipal { get; set; }

    public bool Activo { get; set; }

    [ForeignKey("ProductoId")]
    [InverseProperty("CodigosBarraProducto")]
    public virtual Producto Producto { get; set; } = null!;
}
