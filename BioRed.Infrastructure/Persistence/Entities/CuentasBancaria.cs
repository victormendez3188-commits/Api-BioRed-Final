using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Index("EmpresaId", "NumeroCuentaEnmascarado", Name = "UQ_CuentasBancarias_Empresa_Numero", IsUnique = true)]
public partial class CuentasBancaria
{
    [Key]
    public int CuentaBancariaId { get; set; }

    public int EmpresaId { get; set; }

    [StringLength(120)]
    public string Banco { get; set; } = null!;

    [StringLength(80)]
    public string NumeroCuentaEnmascarado { get; set; } = null!;

    [StringLength(20)]
    public string TipoCuenta { get; set; } = null!;

    [StringLength(3)]
    [Unicode(false)]
    public string Moneda { get; set; } = null!;

    [Column(TypeName = "decimal(19, 4)")]
    public decimal SaldoInicial { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [ForeignKey("EmpresaId")]
    [InverseProperty("CuentasBancaria")]
    public virtual Empresa Empresa { get; set; } = null!;

    [InverseProperty("CuentaBancaria")]
    public virtual ICollection<TransaccionesBancaria> TransaccionesBancaria { get; set; } = new List<TransaccionesBancaria>();
}
