using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Nombre", Name = "UQ_PrincipiosActivos_Nombre", IsUnique = true)]
public partial class PrincipiosActivo
{
    [Key]
    public int PrincipioActivoId { get; set; }

    [StringLength(150)]
    public string Nombre { get; set; } = null!;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    [InverseProperty("PrincipioActivo")]
    public virtual ICollection<ProductoPrincipiosActivo> ProductoPrincipiosActivos { get; set; } = new List<ProductoPrincipiosActivo>();
}
