using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("TokensRenovacion")]
[Index("UsuarioId", "FechaExpiracionUtc", Name = "IX_TokensRenovacion_Usuario_Expiracion")]
[Index("TokenHash", Name = "UQ_TokensRenovacion_Hash", IsUnique = true)]
public partial class TokensRenovacion
{
    [Key]
    public long TokenRenovacionId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string TokenHash { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime FechaExpiracionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaRevocacionUtc { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string? IpCreacion { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string? IpRevocacion { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string? ReemplazadoPorHash { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("TokensRenovacions")]
    public virtual Usuario Usuario { get; set; } = null!;
}
