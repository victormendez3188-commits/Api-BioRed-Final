using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Soundex_Cache")]
[Index("TipoEntidad", "EntidadId", Name = "UQ_SoundexCache_Entidad", IsUnique = true)]
public partial class Soundex_Cache
{
    [Key]
    public long SoundexCacheId { get; set; }

    [StringLength(30)]
    public string TipoEntidad { get; set; } = null!;

    [StringLength(100)]
    public string EntidadId { get; set; } = null!;

    [StringLength(250)]
    public string NombreOriginal { get; set; } = null!;

    [StringLength(10)]
    public string CodigoSoundex { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaActualizacionUtc { get; set; }
}
