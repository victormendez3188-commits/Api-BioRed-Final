using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("AlmacenId", "SucursalId", Name = "UQ_Almacenes_Almacen_Sucursal", IsUnique = true)]
[Index("SucursalId", "Codigo", Name = "UQ_Almacenes_Sucursal_Codigo", IsUnique = true)]
public partial class Almacene
{
    [Key]
    public int AlmacenId { get; set; }

    public int SucursalId { get; set; }

    [StringLength(20)]
    public string Codigo { get; set; } = null!;

    [StringLength(100)]
    public string Nombre { get; set; } = null!;

    public bool EsPrincipal { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Almacene")]
    public virtual ICollection<CompraEncabezado> CompraEncabezados { get; set; } = new List<CompraEncabezado>();

    [InverseProperty("Almacene")]
    public virtual ICollection<FacturaEncabezado> FacturaEncabezados { get; set; } = new List<FacturaEncabezado>();

    [InverseProperty("Almacen")]
    public virtual ICollection<Inventario> Inventarios { get; set; } = new List<Inventario>();

    [InverseProperty("AlmacenDestino")]
    public virtual ICollection<Kardex> KardexAlmacenDestinos { get; set; } = new List<Kardex>();

    [InverseProperty("AlmacenOrigen")]
    public virtual ICollection<Kardex> KardexAlmacenOrigens { get; set; } = new List<Kardex>();

    [ForeignKey("SucursalId")]
    [InverseProperty("Almacene")]
    public virtual Sucursal Sucursal { get; set; } = null!;
}
