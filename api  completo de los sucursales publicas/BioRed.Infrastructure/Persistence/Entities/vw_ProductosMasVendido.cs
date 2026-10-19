using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Keyless]
public partial class vw_ProductosMasVendido
{
    public long ProductoId { get; set; }

    [StringLength(40)]
    public string Sku { get; set; } = null!;

    [StringLength(180)]
    public string Producto { get; set; } = null!;

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? CantidadVendida { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? TotalVendido { get; set; }

    public long? NumeroVentas { get; set; }
}
