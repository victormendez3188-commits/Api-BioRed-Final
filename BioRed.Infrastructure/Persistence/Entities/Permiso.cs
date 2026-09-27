using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Codigo", Name = "UQ_Permisos_Codigo", IsUnique = true)]
public partial class Permiso
{
    [Key]
    public int PermisoId { get; set; }

    [StringLength(100)]
    public string Codigo { get; set; } = null!;

    [StringLength(50)]
    public string Modulo { get; set; } = null!;

    [StringLength(120)]
    public string Nombre { get; set; } = null!;

    [StringLength(300)]
    public string? Descripcion { get; set; }

    [InverseProperty("Permiso")]
    public virtual ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}
