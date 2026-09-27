using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Keyless]
public partial class vw_CuentasPorPagar
{
    public long CuentaPorPagarId { get; set; }

    public long CompraId { get; set; }

    public int SucursalId { get; set; }

    public long ProveedorId { get; set; }

    [StringLength(200)]
    public string Proveedor { get; set; } = null!;

    [StringLength(20)]
    public string Nit { get; set; } = null!;

    public DateOnly FechaEmision { get; set; }

    public DateOnly FechaVencimiento { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal MontoOriginal { get; set; }

    [Column(TypeName = "decimal(19, 4)")]
    public decimal Saldo { get; set; }

    [StringLength(15)]
    public string Estado { get; set; } = null!;

    public bool? EstaVencida { get; set; }
}
