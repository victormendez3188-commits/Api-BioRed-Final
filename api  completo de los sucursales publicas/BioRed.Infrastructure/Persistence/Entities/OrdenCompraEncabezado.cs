using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("OrdenCompraEncabezado")]
[Index("OrdenCompraId", "ProveedorId", "SucursalId", Name = "UQ_OrdenesCompra_Orden_Proveedor_Sucursal", IsUnique = true)]
[Index("SucursalId", "NumeroOrden", Name = "UQ_OrdenesCompra_Sucursal_Numero", IsUnique = true)]
public partial class OrdenCompraEncabezado
{
    [Key]
    public long OrdenCompraId { get; set; }

    public int SucursalId { get; set; }

    public long ProveedorId { get; set; }

    public long UsuarioId { get; set; }

    [StringLength(30)]
    public string NumeroOrden { get; set; } = null!;

    [Precision(0)]
    public DateTime FechaOrdenUtc { get; set; }

    public DateOnly? FechaEsperada { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Descuento { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Impuesto { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Total { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("OrdenCompraEncabezado")]
    public virtual ICollection<CompraEncabezado> CompraEncabezados { get; set; } = new List<CompraEncabezado>();

    [InverseProperty("OrdenCompra")]
    public virtual ICollection<OrdenCompraDetalle> OrdenCompraDetalles { get; set; } = new List<OrdenCompraDetalle>();

    [ForeignKey("ProveedorId")]
    [InverseProperty("OrdenCompraEncabezados")]
    public virtual Proveedore Proveedor { get; set; } = null!;

    [ForeignKey("SucursalId")]
    [InverseProperty("OrdenCompraEncabezados")]
    public virtual Sucursal Sucursal { get; set; } = null!;

    [ForeignKey("UsuarioId")]
    [InverseProperty("OrdenCompraEncabezados")]
    public virtual Usuario Usuario { get; set; } = null!;
}
