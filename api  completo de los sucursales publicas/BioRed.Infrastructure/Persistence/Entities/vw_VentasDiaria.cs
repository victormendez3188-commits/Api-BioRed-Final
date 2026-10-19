using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Keyless]
public partial class vw_VentasDiaria
{
    public DateOnly? Fecha { get; set; }

    public int SucursalId { get; set; }

    [StringLength(120)]
    public string Sucursal { get; set; } = null!;

    [StringLength(10)]
    public string CanalVenta { get; set; } = null!;

    public long? CantidadVentas { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? Subtotal { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? Descuento { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? Impuesto { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? Total { get; set; }
}
