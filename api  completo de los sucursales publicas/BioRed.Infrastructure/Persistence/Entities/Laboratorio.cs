using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Nombre", Name = "UQ_Laboratorios_Nombre", IsUnique = true)]
public partial class Laboratorio
{
    [Key]
    public int LaboratorioId { get; set; }

    [StringLength(150)]
    public string Nombre { get; set; } = null!;

    [StringLength(100)]
    public string? PaisOrigen { get; set; }

    [StringLength(25)]
    public string? Telefono { get; set; }

    [StringLength(250)]
    public string? SitioWeb { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Laboratorio")]
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
