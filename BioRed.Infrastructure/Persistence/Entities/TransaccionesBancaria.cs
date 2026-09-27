using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("CuentaBancariaId", "FechaTransaccionUtc", Name = "IX_TransaccionesBancarias_Cuenta_Fecha", IsDescending = new[] { false, true })]
public partial class TransaccionesBancaria
{
    [Key]
    public long TransaccionBancariaId { get; set; }

    public int CuentaBancariaId { get; set; }

    public long? PagoVentaId { get; set; }

    public long? PagoCompraId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(15)]
    public string TipoTransaccion { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Monto { get; set; }

    [StringLength(120)]
    public string? Referencia { get; set; }

    [StringLength(300)]
    public string? Descripcion { get; set; }

    [Precision(0)]
    public DateTime FechaTransaccionUtc { get; set; }

    [StringLength(10)]
    public string Estado { get; set; } = null!;

    [ForeignKey("CuentaBancariaId")]
    [InverseProperty("TransaccionesBancaria")]
    public virtual CuentasBancaria CuentaBancaria { get; set; } = null!;

    [ForeignKey("PagoCompraId")]
    [InverseProperty("TransaccionesBancaria")]
    public virtual PagosCompra? PagoCompra { get; set; }

    [ForeignKey("PagoVentaId")]
    [InverseProperty("TransaccionesBancaria")]
    public virtual PagosVentum? PagoVenta { get; set; }

    [ForeignKey("UsuarioId")]
    [InverseProperty("TransaccionesBancaria")]
    public virtual Usuario Usuario { get; set; } = null!;
}
