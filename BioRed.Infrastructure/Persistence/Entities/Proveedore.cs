using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("Nit", Name = "UQ_Proveedores_Nit", IsUnique = true)]
public partial class Proveedore
{
    [Key]
    public long ProveedorId { get; set; }

    [StringLength(20)]
    public string Nit { get; set; } = null!;

    [StringLength(200)]
    public string RazonSocial { get; set; } = null!;

    [StringLength(150)]
    public string? NombreComercial { get; set; }

    [StringLength(150)]
    public string? ContactoNombre { get; set; }

    [StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(25)]
    public string? Telefono { get; set; }

    [StringLength(300)]
    public string? Direccion { get; set; }

    public short DiasCredito { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Proveedor")]
    public virtual ICollection<CompraEncabezado> CompraEncabezados { get; set; } = new List<CompraEncabezado>();

    [InverseProperty("Proveedor")]
    public virtual ICollection<CxPEncabezado> CxPEncabezados { get; set; } = new List<CxPEncabezado>();

    [InverseProperty("Proveedor")]
    public virtual ICollection<OrdenCompraEncabezado> OrdenCompraEncabezados { get; set; } = new List<OrdenCompraEncabezado>();

    [InverseProperty("Proveedor")]
    public virtual ICollection<ProveedorProducto> ProveedorProductos { get; set; } = new List<ProveedorProducto>();
}
