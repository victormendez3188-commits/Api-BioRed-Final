using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("RecuperacionContraseña")]
[Index("TokenHash", Name = "UQ_TokensRecuperacion_Hash", IsUnique = true)]
public partial class RecuperacionContraseña
{
    [Key]
    public long TokenRecuperacionId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string TokenHash { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime FechaExpiracionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaUsoUtc { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string? IpSolicitud { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("RecuperacionContraseñas")]
    public virtual Usuario Usuario { get; set; } = null!;
}
