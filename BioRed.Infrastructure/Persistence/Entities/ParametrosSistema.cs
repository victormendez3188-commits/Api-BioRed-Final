using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence.Entities;

[Table("ParametrosSistema")]
[Index("EmpresaId", "Clave", Name = "UQ_Parametros_Empresa_Clave", IsUnique = true)]
public partial class ParametrosSistema
{
    [Key]
    public int ParametroId { get; set; }

    public int EmpresaId { get; set; }

    [StringLength(100)]
    public string Clave { get; set; } = null!;

    public string? Valor { get; set; }

    [StringLength(300)]
    public string? Descripcion { get; set; }

    public bool EsSecreto { get; set; }

    [Precision(0)]
    public DateTime FechaModificacionUtc { get; set; }

    [ForeignKey("EmpresaId")]
    [InverseProperty("ParametrosSistemas")]
    public virtual Empresa Empresa { get; set; } = null!;
}
