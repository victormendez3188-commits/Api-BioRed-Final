using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("DevolucionCompraEncabezado")]
[Index("DevolucionCompraId", "CompraId", Name = "UQ_DevolucionesCompra_Devolucion_Compra", IsUnique = true)]
[Index("NumeroDevolucion", Name = "UQ_DevolucionesCompra_Numero", IsUnique = true)]
public partial class DevolucionCompraEncabezado
{
    [Key]
    public long DevolucionCompraId { get; set; }

    public long CompraId { get; set; }

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

    [ForeignKey("CompraId")]
    [InverseProperty("DevolucionCompraEncabezados")]
    public virtual CompraEncabezado Compra { get; set; } = null!;

    [InverseProperty("DevolucionCompraEncabezado")]
    public virtual ICollection<DevolucionCompraDetalle> DevolucionCompraDetalles { get; set; } = new List<DevolucionCompraDetalle>();

    [ForeignKey("UsuarioId")]
    [InverseProperty("DevolucionCompraEncabezados")]
    public virtual Usuario Usuario { get; set; } = null!;
}
