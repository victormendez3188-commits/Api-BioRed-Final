using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("VentaId", "FechaPagoUtc", Name = "IX_PagosVenta_Venta_Fecha", IsDescending = new[] { false, true })]
public partial class PagosVentum
{
    [Key]
    public long PagoVentaId { get; set; }

    public long VentaId { get; set; }

    public int MetodoPagoId { get; set; }

    public long UsuarioId { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal? MontoRecibido { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Cambio { get; set; }

    [StringLength(120)]
    public string? Referencia { get; set; }

    [Precision(0)]
    public DateTime FechaPagoUtc { get; set; }

    [StringLength(10)]
    public string Estado { get; set; } = null!;

    [InverseProperty("PagoVenta")]
    public virtual ICollection<CxCDetalle> CxCDetalles { get; set; } = new List<CxCDetalle>();

    [ForeignKey("MetodoPagoId")]
    [InverseProperty("PagosVenta")]
    public virtual Tipo_Pago MetodoPago { get; set; } = null!;

    [InverseProperty("PagoVenta")]
    public virtual ICollection<TransaccionesBancaria> TransaccionesBancaria { get; set; } = new List<TransaccionesBancaria>();

    [ForeignKey("UsuarioId")]
    [InverseProperty("PagosVenta")]
    public virtual Usuario Usuario { get; set; } = null!;

    [ForeignKey("VentaId")]
    [InverseProperty("PagosVenta")]
    public virtual FacturaEncabezado Venta { get; set; } = null!;
}
