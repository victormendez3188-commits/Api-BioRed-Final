using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("UsuarioId", "SucursalId")]
[Table("UsuarioSucursal")]
public partial class UsuarioSucursal
{
    [Key]
    public long UsuarioId { get; set; }

    [Key]
    public int SucursalId { get; set; }

    public bool EsPredeterminada { get; set; }

    [Precision(0)]
    public DateTime FechaAsignacionUtc { get; set; }

    [ForeignKey("SucursalId")]
    [InverseProperty("UsuarioSucursals")]
    public virtual Sucursal Sucursal { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("UsuarioSucursal")]
    public virtual Usuario Usuario { get; set; } = null!;
}
