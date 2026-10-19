using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Codigo", Name = "UQ_Impuestos_Codigo", IsUnique = true)]
public partial class Impuesto
{
    [Key]
    public int ImpuestoId { get; set; }

    [StringLength(20)]
    public string Codigo { get; set; } = null!;

    [StringLength(80)]
    public string Nombre { get; set; } = null!;

    [Column(TypeName = "decimal(9, 4)")]
    public decimal Porcentaje { get; set; }

    public bool IncluidoEnPrecio { get; set; }

    public bool Activo { get; set; }

    public DateOnly? FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    [InverseProperty("Impuesto")]
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
