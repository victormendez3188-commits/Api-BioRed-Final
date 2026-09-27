using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("CategoriaId", "Activo", Name = "IX_Productos_Categoria")]
[Index("Nombre", Name = "IX_Productos_Nombre")]
[Index("Sku", Name = "UQ_Productos_Sku", IsUnique = true)]
public partial class Producto
{
    [Key]
    public long ProductoId { get; set; }

    public int CategoriaId { get; set; }

    public int? MarcaId { get; set; }

    public int? LaboratorioId { get; set; }

    public int UnidadMedidaId { get; set; }

    public int? ImpuestoId { get; set; }

    [StringLength(40)]
    public string Sku { get; set; } = null!;

    [StringLength(180)]
    public string Nombre { get; set; } = null!;

    [StringLength(180)]
    public string? NombreGenerico { get; set; }

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [StringLength(120)]
    public string? Presentacion { get; set; }

    [StringLength(80)]
    public string? Concentracion { get; set; }

    [StringLength(80)]
    public string? RegistroSanitario { get; set; }

    public bool RequiereReceta { get; set; }

    public bool EsControlado { get; set; }

    public bool ControlaLote { get; set; }

    public bool ControlaVencimiento { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal StockMinimo { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? StockMaximo { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal CostoReferencia { get; set; }

    [StringLength(500)]
    public string? ImagenUrl { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("CategoriaId")]
    [InverseProperty("Productos")]
    public virtual Categoria Categoria { get; set; } = null!;

    [InverseProperty("Producto")]
    public virtual CodigosBarraProducto? CodigosBarraProducto { get; set; }

    [InverseProperty("Producto")]
    public virtual ICollection<CompraDetalle> CompraDetalles { get; set; } = new List<CompraDetalle>();

    [InverseProperty("Producto")]
    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();

    [ForeignKey("ImpuestoId")]
    [InverseProperty("Productos")]
    public virtual Impuesto? Impuesto { get; set; }

    [InverseProperty("Producto")]
    public virtual ICollection<Inventario> Inventarios { get; set; } = new List<Inventario>();

    [InverseProperty("Producto")]
    public virtual ICollection<KardexDetalle> KardexDetalles { get; set; } = new List<KardexDetalle>();

    [ForeignKey("LaboratorioId")]
    [InverseProperty("Productos")]
    public virtual Laboratorio? Laboratorio { get; set; }

    [InverseProperty("Producto")]
    public virtual ICollection<Lote> Lotes { get; set; } = new List<Lote>();

    [ForeignKey("MarcaId")]
    [InverseProperty("Productos")]
    public virtual Marca? Marca { get; set; }

    [InverseProperty("Producto")]
    public virtual ICollection<OrdenCompraDetalle> OrdenCompraDetalles { get; set; } = new List<OrdenCompraDetalle>();

    [InverseProperty("Producto")]
    public virtual ICollection<PreciosProducto> PreciosProductos { get; set; } = new List<PreciosProducto>();

    [InverseProperty("Producto")]
    public virtual ICollection<ProductoPrincipiosActivo> ProductoPrincipiosActivos { get; set; } = new List<ProductoPrincipiosActivo>();

    [InverseProperty("Producto")]
    public virtual ProveedorProducto? ProveedorProducto { get; set; }

    [ForeignKey("UnidadMedidaId")]
    [InverseProperty("Productos")]
    public virtual UnidadesMedidum UnidadMedida { get; set; } = null!;
}
