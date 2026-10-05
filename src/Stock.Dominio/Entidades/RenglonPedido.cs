namespace Stock.Dominio.Entidades;

public class RenglonPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int Cantidad { get; set; }
    /// <summary>Precio al momento de cargar el renglón: si después cambia el precio de lista, el pedido no cambia.</summary>
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;
}
