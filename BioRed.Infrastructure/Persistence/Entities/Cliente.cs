using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

public partial class Cliente
{
    [Key]
    public long ClienteId { get; set; }

    [StringLength(1)]
    [Unicode(false)]
    public string TipoPersona { get; set; } = null!;

    [StringLength(20)]
    public string? Nit { get; set; }

    [StringLength(20)]
    public string? Dpi { get; set; }

    [StringLength(120)]
    public string Nombres { get; set; } = null!;

    [StringLength(120)]
    public string? Apellidos { get; set; }

    [StringLength(200)]
    public string? RazonSocial { get; set; }

    [StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(25)]
    public string? Telefono { get; set; }

    [StringLength(300)]
    public string? Direccion { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal LimiteCredito { get; set; }

    public short DiasCredito { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Cliente")]
    public virtual ICollection<CxCEncabezado> CxCEncabezados { get; set; } = new List<CxCEncabezado>();

    [InverseProperty("Cliente")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezados { get; set; } = new List<FacturaEncabezado>();

    [InverseProperty("Cliente")]
    public virtual ICollection<Receta> Receta { get; set; } = new List<Receta>();
}
