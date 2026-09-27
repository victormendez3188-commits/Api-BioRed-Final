using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Nombre", Name = "UQ_Marcas_Nombre", IsUnique = true)]
public partial class Marca
{
    [Key]
    public int MarcaId { get; set; }

    [StringLength(120)]
    public string Nombre { get; set; } = null!;

    [StringLength(300)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Marca")]
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
