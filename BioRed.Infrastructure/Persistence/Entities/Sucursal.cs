using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Sucursal")]
[Index("EmpresaId", "Codigo", Name = "UQ_Sucursales_Empresa_Codigo", IsUnique = true)]
public partial class Sucursal
{
    [Key]
    public int SucursalId { get; set; }

    public int EmpresaId { get; set; }

    [StringLength(20)]
    public string Codigo { get; set; } = null!;

    [StringLength(120)]
    public string Nombre { get; set; } = null!;

    [StringLength(300)]
    public string Direccion { get; set; } = null!;

    [StringLength(25)]
    public string? Telefono { get; set; }

    [StringLength(150)]
    public string? Correo { get; set; }

    public bool EsPrincipal { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Sucursal")]
    public virtual Almacene? Almacene { get; set; }

    [InverseProperty("Sucursal")]
    public virtual ICollection<Caja> Cajas { get; set; } = new List<Caja>();

    [InverseProperty("Sucursal")]
    public virtual ICollection<CompraEncabezado> CompraEncabezados { get; set; } = new List<CompraEncabezado>();

    [ForeignKey("EmpresaId")]
    [InverseProperty("Sucursal")]
    public virtual Empresa Empresa { get; set; } = null!;

    [InverseProperty("Sucursal")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezados { get; set; } = new List<FacturaEncabezado>();

    [InverseProperty("Sucursal")]
    public virtual ICollection<OrdenCompraEncabezado> OrdenCompraEncabezados { get; set; } = new List<OrdenCompraEncabezado>();

    [InverseProperty("Sucursal")]
    public virtual ICollection<PreciosProducto> PreciosProductos { get; set; } = new List<PreciosProducto>();

    [InverseProperty("Sucursal")]
    public virtual ICollection<SeriesDocumento> SeriesDocumentos { get; set; } = new List<SeriesDocumento>();

    [InverseProperty("Sucursal")]
    public virtual ICollection<UsuarioSucursal> UsuarioSucursals { get; set; } = new List<UsuarioSucursal>();
}
