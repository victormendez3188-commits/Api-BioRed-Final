using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Kardex")]
public partial class Kardex
{
    [Key]
    public long MovimientoId { get; set; }

    [StringLength(30)]
    public string TipoMovimiento { get; set; } = null!;

    public int? AlmacenOrigenId { get; set; }

    public int? AlmacenDestinoId { get; set; }

    [StringLength(30)]
    public string? ReferenciaTipo { get; set; }

    public long? ReferenciaId { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    [StringLength(500)]
    public string? Observaciones { get; set; }

    public long UsuarioId { get; set; }

    [Precision(0)]
    public DateTime FechaMovimientoUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaAplicacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("AlmacenDestinoId")]
    [InverseProperty("KardexAlmacenDestinos")]
    public virtual Almacene? AlmacenDestino { get; set; }

    [ForeignKey("AlmacenOrigenId")]
    [InverseProperty("KardexAlmacenOrigens")]
    public virtual Almacene? AlmacenOrigen { get; set; }

    [InverseProperty("Movimiento")]
    public virtual ICollection<KardexDetalle> KardexDetalles { get; set; } = new List<KardexDetalle>();

    [ForeignKey("UsuarioId")]
    [InverseProperty("Kardices")]
    public virtual Usuario Usuario { get; set; } = null!;
}
