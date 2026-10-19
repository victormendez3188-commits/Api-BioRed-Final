using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CxCEncabezado")]
[Index("Estado", "FechaVencimiento", Name = "IX_CxC_Estado_Vencimiento")]
[Index("VentaId", Name = "UQ_CuentasPorCobrar_Venta", IsUnique = true)]
public partial class CxCEncabezado
{
    [Key]
    public long CuentaPorCobrarId { get; set; }

    public long VentaId { get; set; }

    public long ClienteId { get; set; }

    public DateOnly FechaEmision { get; set; }

    public DateOnly FechaVencimiento { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal MontoOriginal { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Saldo { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("ClienteId")]
    [InverseProperty("CxCEncabezados")]
    public virtual Cliente Cliente { get; set; } = null!;

    [InverseProperty("CuentaPorCobrar")]
    public virtual ICollection<CxCDetalle> CxCDetalles { get; set; } = new List<CxCDetalle>();

    [ForeignKey("VentaId")]
    [InverseProperty("CxCEncabezado")]
    public virtual FacturaEncabezado Venta { get; set; } = null!;
}
