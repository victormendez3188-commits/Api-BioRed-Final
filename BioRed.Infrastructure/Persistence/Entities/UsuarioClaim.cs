using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

public partial class UsuarioClaim
{
    [Key]
    public long UsuarioClaimId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(200)]
    public string? TipoClaim { get; set; }

    [StringLength(1000)]
    public string? ValorClaim { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("UsuarioClaims")]
    public virtual Usuario Usuario { get; set; } = null!;
}
