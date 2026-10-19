using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[PrimaryKey("UsuarioId", "ProveedorLogin", "NombreToken")]
public partial class UsuarioToken
{
    [Key]
    public long UsuarioId { get; set; }

    [Key]
    [StringLength(128)]
    public string ProveedorLogin { get; set; } = null!;

    [Key]
    [StringLength(128)]
    public string NombreToken { get; set; } = null!;

    public string? ValorTokenProtegido { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("UsuarioTokens")]
    public virtual Usuario Usuario { get; set; } = null!;
}
