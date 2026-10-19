using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Codigo", Name = "UQ_UnidadesMedida_Codigo", IsUnique = true)]
[Index("Nombre", Name = "UQ_UnidadesMedida_Nombre", IsUnique = true)]
public partial class UnidadesMedidum
{
    [Key]
    public int UnidadMedidaId { get; set; }

    [StringLength(15)]
    public string Codigo { get; set; } = null!;

    [StringLength(80)]
    public string Nombre { get; set; } = null!;

    public bool PermiteDecimales { get; set; }

    public bool Activo { get; set; }

    [InverseProperty("UnidadMedida")]
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
