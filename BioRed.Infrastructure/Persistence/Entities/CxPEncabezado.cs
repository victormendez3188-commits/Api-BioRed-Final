using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CxPEncabezado")]
[Index("Estado", "FechaVencimiento", Name = "IX_CxP_Estado_Vencimiento")]
[Index("CompraId", Name = "UQ_CuentasPorPagar_Compra", IsUnique = true)]
public partial class CxPEncabezado
{
    [Key]
    public long CuentaPorPagarId { get; set; }

    public long CompraId { get; set; }

    public long ProveedorId { get; set; }

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

    [ForeignKey("CompraId, ProveedorId")]
    [InverseProperty("CxPEncabezados")]
    public virtual CompraEncabezado CompraEncabezado { get; set; } = null!;

    [InverseProperty("CuentaPorPagar")]
    public virtual ICollection<CxPDetalle> CxPDetalles { get; set; } = new List<CxPDetalle>();

    [ForeignKey("ProveedorId")]
    [InverseProperty("CxPEncabezados")]
    public virtual Proveedore Proveedor { get; set; } = null!;
}
