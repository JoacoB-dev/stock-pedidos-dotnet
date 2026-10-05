using Stock.Dominio.Excepciones;

namespace Stock.Dominio.Entidades;

public class Producto
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
    public decimal Precio { get; set; }
    /// <summary>Unidades disponibles. Solo cambia a través de <see cref="AplicarMovimiento"/>.</summary>
    public int Stock { get; private set; }
    public int StockMinimo { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Token de concurrencia (rowversion de SQL Server). Si dos operaciones leen el mismo
    /// producto y las dos intentan guardar, la segunda falla en vez de pisar el stock.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    public bool StockBajo => Stock <= StockMinimo;

    public Producto() { }

    public Producto(string sku, string nombre, int categoriaId, decimal precio, int stockMinimo)
    {
        Sku = sku;
        Nombre = nombre;
        CategoriaId = categoriaId;
        Precio = precio;
        StockMinimo = stockMinimo;
    }

    /// <summary>
    /// Suma o resta unidades y devuelve el movimiento que lo registra.
    /// Nunca deja el stock en negativo.
    /// </summary>
    public MovimientoStock AplicarMovimiento(TipoMovimiento tipo, int cantidad, int usuarioId,
        DateTime fecha, int? pedidoId = null, string? motivo = null)
    {
        if (cantidad == 0)
            throw new ReglaDeNegocioException("La cantidad del movimiento no puede ser cero.");

        // Validamos el signo según el tipo para que un movimiento no "mienta"
        var debeSerPositivo = tipo is TipoMovimiento.Ingreso or TipoMovimiento.CancelacionPedido;
        var debeSerNegativo = tipo is TipoMovimiento.VentaConfirmada;
        if (debeSerPositivo && cantidad < 0)
            throw new ReglaDeNegocioException($"Un movimiento de tipo {tipo} debe sumar unidades.");
        if (debeSerNegativo && cantidad > 0)
            throw new ReglaDeNegocioException($"Un movimiento de tipo {tipo} debe restar unidades.");

        if (Stock + cantidad < 0)
            throw new ReglaDeNegocioException(
                $"El producto {Sku} tiene {Stock} unidades; no se pueden restar {-cantidad}.");

        Stock += cantidad;
        return new MovimientoStock
        {
            ProductoId = Id,
            Producto = this,
            Tipo = tipo,
            Cantidad = cantidad,
            StockResultante = Stock,
            Fecha = fecha,
            UsuarioId = usuarioId,
            PedidoId = pedidoId,
            Motivo = motivo
        };
    }
}
