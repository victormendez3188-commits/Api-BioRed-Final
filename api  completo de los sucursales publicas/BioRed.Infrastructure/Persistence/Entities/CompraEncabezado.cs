using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("CompraEncabezado")]
[Index("ProveedorId", "FechaDocumento", Name = "IX_Compras_Proveedor_Fecha", IsDescending = new[] { false, true })]
[Index("CompraId", "ProveedorId", Name = "UQ_Compras_Compra_Proveedor", IsUnique = true)]
[Index("ProveedorId", "SerieDocumento", "NumeroDocumento", Name = "UX_Compras_Proveedor_Documento", IsUnique = true)]
public partial class CompraEncabezado
{
    [Key]
    public long CompraId { get; set; }

    public long? OrdenCompraId { get; set; }

    public int SucursalId { get; set; }

    public int AlmacenId { get; set; }

    public long ProveedorId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(30)]
    public string? SerieDocumento { get; set; }

    [StringLength(50)]
    public string NumeroDocumento { get; set; } = null!;

    public DateOnly FechaDocumento { get; set; }

    [Precision(0)]
    public DateTime FechaRecepcionUtc { get; set; }

    public DateOnly? FechaVencimientoPago { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    [StringLength(10)]
    public string CondicionPago { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Descuento { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Impuesto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Total { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal SaldoPendiente { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("AlmacenId, SucursalId")]
    [InverseProperty("CompraEncabezados")]
    public virtual Almacene Almacene { get; set; } = null!;

    [InverseProperty("Compra")]
    public virtual ICollection<CompraDetalle> CompraDetalles { get; set; } = new List<CompraDetalle>();

    [InverseProperty("CompraEncabezado")]
    public virtual ICollection<CxPEncabezado> CxPEncabezados { get; set; } = new List<CxPEncabezado>();

    [InverseProperty("Compra")]
    public virtual ICollection<DevolucionCompraEncabezado> DevolucionCompraEncabezados { get; set; } = new List<DevolucionCompraEncabezado>();

    [ForeignKey("OrdenCompraId, ProveedorId, SucursalId")]
    [InverseProperty("CompraEncabezados")]
    public virtual OrdenCompraEncabezado? OrdenCompraEncabezado { get; set; }

    [InverseProperty("Compra")]
    public virtual ICollection<PagosCompra> PagosCompras { get; set; } = new List<PagosCompra>();

    [ForeignKey("ProveedorId")]
    [InverseProperty("CompraEncabezados")]
    public virtual Proveedore Proveedor { get; set; } = null!;

    [ForeignKey("SucursalId")]
    [InverseProperty("CompraEncabezados")]
    public virtual Sucursal Sucursal { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("CompraEncabezados")]
    public virtual Usuario Usuario { get; set; } = null!;
}
