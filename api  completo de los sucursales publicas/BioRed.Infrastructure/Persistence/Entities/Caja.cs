using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("CajaId", "SucursalId", Name = "UQ_Cajas_Caja_Sucursal", IsUnique = true)]
[Index("SucursalId", "Codigo", Name = "UQ_Cajas_Sucursal_Codigo", IsUnique = true)]
public partial class Caja
{
    [Key]
    public int CajaId { get; set; }

    public int SucursalId { get; set; }

    [StringLength(20)]
    public string Codigo { get; set; } = null!;

    [StringLength(100)]
    public string Nombre { get; set; } = null!;

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Caja")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezados { get; set; } = new List<FacturaEncabezado>();

    [ForeignKey("SucursalId")]
    [InverseProperty("Cajas")]
    public virtual Sucursal Sucursal { get; set; } = null!;

    [InverseProperty("Caja")]
    public virtual TurnosCaja? TurnosCaja { get; set; }
}
