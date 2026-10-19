using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("UsuarioId", "RolId")]
[Table("UsuarioRol")]
public partial class UsuarioRol
{
    [Key]
    public long UsuarioId { get; set; }

    [Key]
    public int RolId { get; set; }

    [Precision(0)]
    public DateTime FechaAsignacionUtc { get; set; }

    [ForeignKey("RolId")]
    [InverseProperty("UsuarioRols")]
    public virtual Rol Rol { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("UsuarioRols")]
    public virtual Usuario Usuario { get; set; } = null!;
}
