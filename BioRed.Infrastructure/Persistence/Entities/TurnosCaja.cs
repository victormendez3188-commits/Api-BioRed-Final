using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("TurnosCaja")]
[Index("TurnoCajaId", "CajaId", Name = "UQ_TurnosCaja_Turno_Caja", IsUnique = true)]
public partial class TurnosCaja
{
    [Key]
    public long TurnoCajaId { get; set; }

    public int CajaId { get; set; }

    public long UsuarioAperturaId { get; set; }

    public long? UsuarioCierreId { get; set; }

    [Precision(0)]
    public DateTime FechaAperturaUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaCierreUtc { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal MontoInicial { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? MontoEsperado { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? MontoReal { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? Diferencia { get; set; }

    [StringLength(10)]
    public string Estado { get; set; } = null!;

    [StringLength(500)]
    public string? Observaciones { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("CajaId")]
    [InverseProperty("TurnosCaja")]
    public virtual Caja Caja { get; set; } = null!;

    [InverseProperty("TurnosCaja")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezados { get; set; } = new List<FacturaEncabezado>();

    [InverseProperty("TurnoCaja")]
    public virtual ICollection<MovimientosCaja> MovimientosCajas { get; set; } = new List<MovimientosCaja>();

    [InverseProperty("TurnoCaja")]
    public virtual ICollection<ReembolsosVentum> ReembolsosVenta { get; set; } = new List<ReembolsosVentum>();

    [ForeignKey("UsuarioAperturaId")]
    [InverseProperty("TurnosCajaUsuarioAperturas")]
    public virtual Usuario UsuarioApertura { get; set; } = null!;

    [ForeignKey("UsuarioCierreId")]
    [InverseProperty("TurnosCajaUsuarioCierres")]
    public virtual Usuario? UsuarioCierre { get; set; }
}
