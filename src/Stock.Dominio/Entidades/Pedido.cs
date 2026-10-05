using Stock.Dominio.Excepciones;

namespace Stock.Dominio.Entidades;

public class Pedido
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int VendedorId { get; set; }
    public Usuario? Vendedor { get; set; }
    public EstadoPedido Estado { get; private set; } = EstadoPedido.Borrador;
    public string? Observaciones { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaConfirmacion { get; private set; }
    public DateTime? FechaPreparacion { get; private set; }
    public DateTime? FechaEntrega { get; private set; }
    public DateTime? FechaCancelacion { get; private set; }
    public string? MotivoCancelacion { get; private set; }
    public List<RenglonPedido> Renglones { get; set; } = [];

    /// <summary>Token de concurrencia: evita, por ejemplo, confirmar dos veces el mismo pedido.</summary>
    public byte[] RowVersion { get; set; } = [];

    public decimal Total => Renglones.Sum(r => r.Subtotal);

    /// <summary>Transiciones permitidas. Todo lo que no está acá se rechaza.</summary>
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> Transiciones = new()
    {
        [EstadoPedido.Borrador] = [EstadoPedido.Confirmado, EstadoPedido.Cancelado],
        [EstadoPedido.Confirmado] = [EstadoPedido.Preparado, EstadoPedido.Cancelado],
        [EstadoPedido.Preparado] = [EstadoPedido.Entregado, EstadoPedido.Cancelado],
        [EstadoPedido.Entregado] = [],
        [EstadoPedido.Cancelado] = []
    };

    public static bool PuedePasar(EstadoPedido desde, EstadoPedido hacia) =>
        Transiciones[desde].Contains(hacia);

    public IReadOnlyList<EstadoPedido> EstadosSiguientes => Transiciones[Estado];

    /// <summary>¿El stock de este pedido ya se descontó? (y por lo tanto hay que devolverlo si se cancela)</summary>
    public bool DescontoStock => Estado is EstadoPedido.Confirmado or EstadoPedido.Preparado;

    private void ValidarTransicion(EstadoPedido hacia)
    {
        if (!PuedePasar(Estado, hacia))
            throw new ReglaDeNegocioException($"Un pedido {Estado} no puede pasar a {hacia}.");
    }

    /// <summary>Reemplaza los renglones. Solo se puede mientras es borrador.</summary>
    public void CambiarRenglones(IEnumerable<RenglonPedido> renglones)
    {
        if (Estado != EstadoPedido.Borrador)
            throw new ReglaDeNegocioException("Solo se pueden modificar pedidos en borrador.");
        var lista = renglones.ToList();
        ValidarRenglones(lista);
        Renglones.Clear();
        Renglones.AddRange(lista);
    }

    public static void ValidarRenglones(IReadOnlyCollection<RenglonPedido> renglones)
    {
        if (renglones.Count == 0)
            throw new ReglaDeNegocioException("El pedido tiene que tener al menos un renglón.");
        if (renglones.Any(r => r.Cantidad <= 0))
            throw new ReglaDeNegocioException("Las cantidades tienen que ser mayores a cero.");
        if (renglones.GroupBy(r => r.ProductoId).Any(g => g.Count() > 1))
            throw new ReglaDeNegocioException("Hay productos repetidos: sumá las cantidades en un solo renglón.");
    }

    /// <summary>
    /// Confirma el pedido y descuenta el stock de cada producto. Si falta stock de alguno,
    /// no toca nada y avisa todos los faltantes juntos. Los productos de los renglones
    /// tienen que venir cargados.
    /// </summary>
    public List<MovimientoStock> Confirmar(int usuarioId, DateTime ahora)
    {
        ValidarTransicion(EstadoPedido.Confirmado);
        ValidarRenglones(Renglones);

        var faltantes = Renglones
            .Where(r => r.Producto!.Stock < r.Cantidad)
            .Select(r => new FaltanteStock(r.ProductoId, r.Producto!.Sku, r.Cantidad, r.Producto.Stock))
            .ToList();
        if (faltantes.Count > 0)
            throw new StockInsuficienteException(faltantes);

        if (Renglones.Any(r => !r.Producto!.Activo))
            throw new ReglaDeNegocioException("El pedido tiene productos dados de baja.");

        var movimientos = Renglones
            .Select(r => r.Producto!.AplicarMovimiento(TipoMovimiento.VentaConfirmada, -r.Cantidad,
                usuarioId, ahora, Id, $"Pedido #{Id}"))
            .ToList();

        Estado = EstadoPedido.Confirmado;
        FechaConfirmacion = ahora;
        return movimientos;
    }

    public void MarcarPreparado(DateTime ahora)
    {
        ValidarTransicion(EstadoPedido.Preparado);
        Estado = EstadoPedido.Preparado;
        FechaPreparacion = ahora;
    }

    public void MarcarEntregado(DateTime ahora)
    {
        ValidarTransicion(EstadoPedido.Entregado);
        Estado = EstadoPedido.Entregado;
        FechaEntrega = ahora;
    }

    /// <summary>
    /// Cancela el pedido. Si ya se había descontado stock, lo devuelve y genera los movimientos.
    /// </summary>
    public List<MovimientoStock> Cancelar(string motivo, int usuarioId, DateTime ahora)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaDeNegocioException("Para cancelar hay que indicar un motivo.");
        ValidarTransicion(EstadoPedido.Cancelado);

        var movimientos = new List<MovimientoStock>();
        if (DescontoStock)
        {
            movimientos = Renglones
                .Select(r => r.Producto!.AplicarMovimiento(TipoMovimiento.CancelacionPedido, r.Cantidad,
                    usuarioId, ahora, Id, $"Cancelación del pedido #{Id}"))
                .ToList();
        }

        Estado = EstadoPedido.Cancelado;
        FechaCancelacion = ahora;
        MotivoCancelacion = motivo.Trim();
        return movimientos;
    }
}
