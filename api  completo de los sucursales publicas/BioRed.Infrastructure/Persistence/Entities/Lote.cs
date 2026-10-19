using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("LoteId", "ProductoId", Name = "UQ_Lotes_Lote_Producto", IsUnique = true)]
[Index("ProductoId", "NumeroLote", Name = "UQ_Lotes_Producto_Numero", IsUnique = true)]
public partial class Lote
{
    [Key]
    public long LoteId { get; set; }

    public long ProductoId { get; set; }

    [StringLength(80)]
    public string NumeroLote { get; set; } = null!;

    public DateOnly? FechaFabricacion { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoUnitario { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Lote")]
    public virtual ICollection<CompraDetalle> CompraDetalles { get; set; } = new List<CompraDetalle>();

    [InverseProperty("Lote")]
    public virtual ICollection<Inventario> Inventarios { get; set; } = new List<Inventario>();

    [InverseProperty("Lote")]
    public virtual ICollection<KardexDetalle> KardexDetalles { get; set; } = new List<KardexDetalle>();

    [ForeignKey("ProductoId")]
    [InverseProperty("Lotes")]
    public virtual Producto Producto { get; set; } = null!;
}
