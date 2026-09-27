using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

public partial class Receta
{
    [Key]
    public long RecetaId { get; set; }

    public long ClienteId { get; set; }

    [StringLength(80)]
    public string? NumeroReceta { get; set; }

    [StringLength(180)]
    public string MedicoNombre { get; set; } = null!;

    [StringLength(50)]
    public string? MedicoColegiado { get; set; }

    public DateOnly FechaEmision { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    [StringLength(500)]
    public string? ImagenUrl { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    [Precision(0)]
    public DateTime FechaRegistroUtc { get; set; }

    [ForeignKey("ClienteId")]
    [InverseProperty("Receta")]
    public virtual Cliente Cliente { get; set; } = null!;

    [ForeignKey("RecetaId")]
    [InverseProperty("Receta")]
    public virtual ICollection<FacturaDetalle> VentaDetalles { get; set; } = new List<FacturaDetalle>();
}
