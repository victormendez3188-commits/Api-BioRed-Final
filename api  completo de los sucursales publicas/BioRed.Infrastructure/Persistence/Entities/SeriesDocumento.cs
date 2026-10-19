using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("SeriesDocumento")]
[Index("SucursalId", "TipoDocumento", "Serie", Name = "UQ_Series_Sucursal_Tipo_Serie", IsUnique = true)]
public partial class SeriesDocumento
{
    [Key]
    public int SerieDocumentoId { get; set; }

    public int SucursalId { get; set; }

    [StringLength(30)]
    public string TipoDocumento { get; set; } = null!;

    [StringLength(30)]
    public string Serie { get; set; } = null!;

    public long UltimoNumero { get; set; }

    public byte LongitudNumero { get; set; }

    [StringLength(20)]
    public string? Prefijo { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("SucursalId")]
    [InverseProperty("SeriesDocumentos")]
    public virtual Sucursal Sucursal { get; set; } = null!;
}
