using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("ProveedorLogin", "ClaveProveedor")]
public partial class UsuarioLogin
{
    [Key]
    [StringLength(128)]
    public string ProveedorLogin { get; set; } = null!;

    [Key]
    [StringLength(256)]
    public string ClaveProveedor { get; set; } = null!;

    [StringLength(256)]
    public string? NombreProveedor { get; set; }

    public long UsuarioId { get; set; }

    [Precision(0)]
    public DateTime FechaVinculacionUtc { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("UsuarioLogins")]
    public virtual Usuario Usuario { get; set; } = null!;
}
