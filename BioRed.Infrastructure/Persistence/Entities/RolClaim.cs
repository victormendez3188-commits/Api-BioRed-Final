using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

public partial class RolClaim
{
    [Key]
    public long RolClaimId { get; set; }

    public int RolId { get; set; }

    [StringLength(200)]
    public string? TipoClaim { get; set; }

    [StringLength(1000)]
    public string? ValorClaim { get; set; }

    [ForeignKey("RolId")]
    [InverseProperty("RolClaims")]
    public virtual Rol Rol { get; set; } = null!;
}
