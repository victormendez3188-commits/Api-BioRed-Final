using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Codigo", Name = "UQ_Estados_Codigo", IsUnique = true)]
[Index("Nombre", Name = "UQ_Estados_Nombre", IsUnique = true)]
public partial class Estado
{
    [Key]
    public int EstadoId { get; set; }

    [StringLength(30)]
    public string Codigo { get; set; } = null!;

    [StringLength(80)]
    public string Nombre { get; set; } = null!;

    [StringLength(200)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; }
}
