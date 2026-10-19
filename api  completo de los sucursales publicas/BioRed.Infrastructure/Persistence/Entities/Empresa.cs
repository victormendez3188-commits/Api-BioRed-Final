using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("Empresa")]
[Index("Nit", Name = "UQ_Empresas_Nit", IsUnique = true)]
public partial class Empresa
{
    [Key]
    public int EmpresaId { get; set; }

    [StringLength(200)]
    public string RazonSocial { get; set; } = null!;

    [StringLength(150)]
    public string NombreComercial { get; set; } = null!;

    [StringLength(20)]
    public string Nit { get; set; } = null!;

    [StringLength(300)]
    public string? Direccion { get; set; }

    [StringLength(25)]
    public string? Telefono { get; set; }

    [StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(3)]
    [Unicode(false)]
    public string Moneda { get; set; } = null!;

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    public bool Activo { get; set; }

    [Precision(0)]
    public DateTime FechaCreacionUtc { get; set; }

    [Precision(0)]
    public DateTime? FechaModificacionUtc { get; set; }

    public byte[] VersionFila { get; set; } = null!;

    [InverseProperty("Empresa")]
    public virtual ICollection<CuentasBancaria> CuentasBancaria { get; set; } = new List<CuentasBancaria>();

    [InverseProperty("Empresa")]
    public virtual ICollection<ParametrosSistema> ParametrosSistemas { get; set; } = new List<ParametrosSistema>();

    [InverseProperty("Empresa")]
    public virtual Sucursal? Sucursal { get; set; }
}
