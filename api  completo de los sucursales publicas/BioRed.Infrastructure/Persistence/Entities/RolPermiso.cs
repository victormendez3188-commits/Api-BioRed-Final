using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("RolId", "PermisoId")]
[Table("RolPermiso")]
public partial class RolPermiso
{
    [Key]
    public int RolId { get; set; }

    [Key]
    public int PermisoId { get; set; }

    [Precision(0)]
    public DateTime FechaAsignacionUtc { get; set; }

    [ForeignKey("PermisoId")]
    [InverseProperty("RolPermisos")]
    public virtual Permiso Permiso { get; set; } = null!;

    [ForeignKey("RolId")]
    [InverseProperty("RolPermisos")]
    public virtual Rol Rol { get; set; } = null!;
}
