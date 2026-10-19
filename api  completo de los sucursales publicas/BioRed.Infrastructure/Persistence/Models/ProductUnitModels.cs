namespace BioRed.Infrastructure.Persistence.Models;

/// <summary>
/// Configuración opcional para vender un producto en unidades individuales.
/// La existencia de Inventario.CantidadActual para estos productos se interpreta
/// como unidades base (no como blísteres).
/// </summary>
public sealed class ProductoVentaUnidad
{
    public string CodProducto { get; set; } = string.Empty;
    public bool PermiteVentaUnidad { get; set; }
    public int UnidadesPorPresentacion { get; set; }
    public decimal PrecioPorUnidad { get; set; }
    public string UnidadBase { get; set; } = "unidad";
    public bool Activo { get; set; }
}
