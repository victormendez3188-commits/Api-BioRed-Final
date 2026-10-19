using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Auditoria_Contrasena")]
public partial class Auditoria_Contrasena
{
    [Key]
    public long AuditoriaContrasenaId { get; set; }

    public long UsuarioId { get; set; }

    public long? CambiadoPorUsuarioId { get; set; }

    [StringLength(30)]
    public string TipoCambio { get; set; } = null!;

    [StringLength(45)]
    [Unicode(false)]
    public string? DireccionIp { get; set; }

    [Precision(0)]
    public DateTime FechaCambioUtc { get; set; }

    [ForeignKey("CambiadoPorUsuarioId")]
    [InverseProperty("Auditoria_ContrasenaCambiadoPorUsuarios")]
    public virtual Usuario? CambiadoPorUsuario { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("Auditoria_ContrasenaUsuarios")]
    public virtual Usuario Usuario { get; set; } = null!;
}
