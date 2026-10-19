using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Tipo_Pago")]
[Index("Codigo", Name = "UQ_MetodosPago_Codigo", IsUnique = true)]
[Index("Nombre", Name = "UQ_MetodosPago_Nombre", IsUnique = true)]
public partial class Tipo_Pago
{
    [Key]
    public int MetodoPagoId { get; set; }

    [StringLength(30)]
    public string Codigo { get; set; } = null!;

    [StringLength(80)]
    public string Nombre { get; set; } = null!;

    public bool RequiereReferencia { get; set; }

    public bool PermiteCambio { get; set; }

    public bool Activo { get; set; }

    [InverseProperty("MetodoPago")]
    public virtual ICollection<MovimientosCaja> MovimientosCajas { get; set; } = new List<MovimientosCaja>();

    [InverseProperty("MetodoPago")]
    public virtual ICollection<PagosCompra> PagosCompras { get; set; } = new List<PagosCompra>();

    [InverseProperty("MetodoPago")]
    public virtual ICollection<PagosVentum> PagosVenta { get; set; } = new List<PagosVentum>();

    [InverseProperty("MetodoPago")]
    public virtual ICollection<ReembolsosVentum> ReembolsosVenta { get; set; } = new List<ReembolsosVentum>();
}
