namespace Stock.Dominio.Entidades;

/// <summary>
/// Registro inmutable de cada cambio de stock: permite reconstruir por qué un producto
/// tiene las unidades que tiene (auditoría).
/// </summary>
public class MovimientoStock
{
    public long Id { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public TipoMovimiento Tipo { get; set; }
    /// <summary>Positiva si entra mercadería, negativa si sale.</summary>
    public int Cantidad { get; set; }
    public int StockResultante { get; set; }
    public DateTime Fecha { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public int? PedidoId { get; set; }
    public string? Motivo { get; set; }
}
