using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("DevolucionEncabezado")]
[Index("DevolucionVentaId", "VentaId", Name = "UQ_Devoluciones_Devolucion_Venta", IsUnique = true)]
[Index("NumeroDevolucion", Name = "UQ_Devoluciones_Numero", IsUnique = true)]
public partial class DevolucionEncabezado
{
    [Key]
    public long DevolucionVentaId { get; set; }

    public long VentaId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(30)]
    public string NumeroDevolucion { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaDevolucionUtc { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    [StringLength(300)]
    public string Motivo { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Total { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("DevolucionEncabezado")]
    public virtual ICollection<DevolucionDetalle> DevolucionDetalles { get; set; } = new List<DevolucionDetalle>();

    [InverseProperty("DevolucionVenta")]
    public virtual ICollection<ReembolsosVentum> ReembolsosVenta { get; set; } = new List<ReembolsosVentum>();

    [ForeignKey("UsuarioId")]
    [InverseProperty("DevolucionEncabezados")]
    public virtual Usuario Usuario { get; set; } = null!;

    [ForeignKey("VentaId")]
    [InverseProperty("DevolucionEncabezados")]
    public virtual FacturaEncabezado Venta { get; set; } = null!;
}
