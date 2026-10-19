using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("PagosCompra")]
[Index("CompraId", "FechaPagoUtc", Name = "IX_PagosCompra_Compra_Fecha", IsDescending = new[] { false, true })]
public partial class PagosCompra
{
    [Key]
    public long PagoCompraId { get; set; }

    public long CompraId { get; set; }

    public int MetodoPagoId { get; set; }

    public long UsuarioId { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [StringLength(120)]
    public string? Referencia { get; set; }

    [Precision(0)]
    public DateTime FechaPagoUtc { get; set; }

    [StringLength(10)]
    public string Estado { get; set; } = null!;

    [StringLength(300)]
    public string? Observaciones { get; set; }

    [ForeignKey("CompraId")]
    [InverseProperty("PagosCompras")]
    public virtual CompraEncabezado Compra { get; set; } = null!;

    [InverseProperty("PagoCompra")]
    public virtual ICollection<CxPDetalle> CxPDetalles { get; set; } = new List<CxPDetalle>();

    [ForeignKey("MetodoPagoId")]
    [InverseProperty("PagosCompras")]
    public virtual Tipo_Pago MetodoPago { get; set; } = null!;

    [InverseProperty("PagoCompra")]
    public virtual ICollection<TransaccionesBancaria> TransaccionesBancaria { get; set; } = new List<TransaccionesBancaria>();

    [ForeignKey("UsuarioId")]
    [InverseProperty("PagosCompras")]
    public virtual Usuario Usuario { get; set; } = null!;
}
