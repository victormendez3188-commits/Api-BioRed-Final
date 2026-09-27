using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("AuditoriaSistema")]
[Index("FechaUtc", "Modulo", "Accion", Name = "IX_Bitacora_Fecha", IsDescending = new[] { true, false, false })]
public partial class AuditoriaSistema
{
    [Key]
    public long BitacoraId { get; set; }

    public long? UsuarioId { get; set; }

    [StringLength(50)]
    public string Modulo { get; set; } = null!;

    [StringLength(30)]
    public string Accion { get; set; } = null!;

    [StringLength(128)]
    public string Entidad { get; set; } = null!;

    [StringLength(100)]
    public string? EntidadId { get; set; }

    public string? DatosAnteriores { get; set; }

    public string? DatosNuevos { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string? DireccionIp { get; set; }

    [StringLength(500)]
    public string? AgenteUsuario { get; set; }

    public Guid? CorrelationId { get; set; }

    [Precision(0)]
    public DateTime FechaUtc { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("AuditoriaSistemas")]
    public virtual Usuario? Usuario { get; set; }
}
