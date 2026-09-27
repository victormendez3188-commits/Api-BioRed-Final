using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Codigo", Name = "UQ_Categorias_Codigo", IsUnique = true)]
public partial class Categoria
{
    [Key]
    public int CategoriaId { get; set; }

    public int? CategoriaPadreId { get; set; }

    [StringLength(30)]
    public string Codigo { get; set; } = null!;

    [StringLength(120)]
    public string Nombre { get; set; } = null!;

    [StringLength(300)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("CategoriaPadreId")]
    [InverseProperty("InverseCategoriaPadre")]
    public virtual Categoria? CategoriaPadre { get; set; }

    [InverseProperty("CategoriaPadre")]
    public virtual ICollection<Categoria> InverseCategoriaPadre { get; set; } = new List<Categoria>();

    [InverseProperty("Categoria")]
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
