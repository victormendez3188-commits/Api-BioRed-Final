using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Keyless]
public partial class vw_StockActual
{
    public int SucursalId { get; set; }

    [StringLength(120)]
    public string Sucursal { get; set; } = null!;

    public int AlmacenId { get; set; }

    [StringLength(100)]
    public string Almacen { get; set; } = null!;

    public long ProductoId { get; set; }

    [StringLength(40)]
    public string Sku { get; set; } = null!;

    [StringLength(180)]
    public string Producto { get; set; } = null!;

    public long? LoteId { get; set; }

    [StringLength(80)]
    public string? NumeroLote { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal CantidadReservada { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? CantidadDisponible { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? CantidadDisponibleSucursal { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal StockMinimo { get; set; }

    public bool? BajoMinimo { get; set; }

    [Precision(0)]
    public DateTime FechaActualizacionUtc { get; set; }
}
