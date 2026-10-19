using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("IntentosSesion")]
[Index("UsuarioId", "FechaIntentoUtc", Name = "IX_IntentosSesion_Usuario_Fecha", IsDescending = new[] { false, true })]
public partial class IntentosSesion
{
    [Key]
    public long IntentoSesionId { get; set; }

    public long? UsuarioId { get; set; }

    [StringLength(150)]
    public string NombreUsuarioIntentado { get; set; } = null!;

    public bool Exitoso { get; set; }

    [StringLength(100)]
    public string? MotivoFallo { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string? DireccionIp { get; set; }

    [StringLength(500)]
    public string? AgenteUsuario { get; set; }

    [Precision(0)]
    public DateTime FechaIntentoUtc { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("IntentosSesions")]
    public virtual Usuario? Usuario { get; set; }
}
