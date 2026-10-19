using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Rol")]
[Index("Codigo", Name = "UQ_Roles_Codigo", IsUnique = true)]
[Index("Nombre", Name = "UQ_Roles_Nombre", IsUnique = true)]
[Index("NombreNormalizado", Name = "UQ_Roles_NombreNormalizado", IsUnique = true)]
public partial class Rol
{
    [Key]
    public int RolId { get; set; }

    [StringLength(50)]
    public string Codigo { get; set; } = null!;

    [StringLength(100)]
    public string Nombre { get; set; } = null!;

    [StringLength(100)]
    public string NombreNormalizado { get; set; } = null!;

    [StringLength(300)]
    public string? Descripcion { get; set; }

    [StringLength(100)]
    public string SelloConcurrencia { get; set; } = null!;

    public bool EsSistema { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [InverseProperty("Rol")]
    public virtual ICollection<RolClaim> RolClaims { get; set; } = new List<RolClaim>();

    [InverseProperty("Rol")]
    public virtual ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();

    [InverseProperty("Rol")]
    public virtual ICollection<UsuarioRol> UsuarioRols { get; set; } = new List<UsuarioRol>();
}
