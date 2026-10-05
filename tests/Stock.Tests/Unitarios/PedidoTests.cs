using Stock.Dominio.Entidades;
using Stock.Dominio.Excepciones;

namespace Stock.Tests.Unitarios;

/// <summary>Reglas del pedido sin base de datos.</summary>
public class PedidoTests
{
    private static readonly DateTime Ahora = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Producto NuevoProducto(int id, string sku, int stock, decimal precio = 100m)
    {
        var p = new Producto(sku, $"Producto {sku}", 1, precio, 2) { Id = id };
        if (stock > 0) p.AplicarMovimiento(TipoMovimiento.Ingreso, stock, 1, Ahora);
        return p;
    }

    private static Pedido PedidoCon(params (Producto Producto, int Cantidad)[] items)
    {
        var pedido = new Pedido { Id = 7, ClienteId = 1, VendedorId = 1, FechaCreacion = Ahora };
        pedido.CambiarRenglones(items.Select(i => new RenglonPedido
        {
            ProductoId = i.Producto.Id, Producto = i.Producto, Cantidad = i.Cantidad, PrecioUnitario = i.Producto.Precio
        }));
        return pedido;
    }

    [Fact]
    public void Confirmar_descuenta_stock_y_genera_un_movimiento_por_renglon()
    {
        var yerba = NuevoProducto(1, "ALM-001", 10);
        var agua = NuevoProducto(2, "BEB-001", 5);
        var pedido = PedidoCon((yerba, 3), (agua, 5));

        var movimientos = pedido.Confirmar(usuarioId: 9, Ahora);

        Assert.Equal(EstadoPedido.Confirmado, pedido.Estado);
        Assert.Equal(Ahora, pedido.FechaConfirmacion);
        Assert.Equal(7, yerba.Stock);
        Assert.Equal(0, agua.Stock);
        Assert.Equal(2, movimientos.Count);
        Assert.All(movimientos, m =>
        {
            Assert.Equal(TipoMovimiento.VentaConfirmada, m.Tipo);
            Assert.True(m.Cantidad < 0);
            Assert.Equal(7, m.PedidoId);
            Assert.Equal(9, m.UsuarioId);
        });
        Assert.Equal(7, movimientos.Single(m => m.ProductoId == 1).StockResultante);
    }

    [Fact]
    public void Confirmar_sin_stock_no_toca_nada_e_informa_todos_los_faltantes()
    {
        var yerba = NuevoProducto(1, "ALM-001", 2);
        var agua = NuevoProducto(2, "BEB-001", 50);
        var harina = NuevoProducto(3, "ALM-006", 0);
        var pedido = PedidoCon((yerba, 3), (agua, 5), (harina, 1));

        var ex = Assert.Throws<StockInsuficienteException>(() => pedido.Confirmar(1, Ahora));

        Assert.Equal(["ALM-001", "ALM-006"], ex.Faltantes.Select(f => f.Sku).Order());
        Assert.Equal(EstadoPedido.Borrador, pedido.Estado);
        Assert.Equal(2, yerba.Stock);
        Assert.Equal(50, agua.Stock); // ni siquiera el que alcanzaba
    }

    [Fact]
    public void Cancelar_un_confirmado_devuelve_el_stock()
    {
        var yerba = NuevoProducto(1, "ALM-001", 10);
        var pedido = PedidoCon((yerba, 4));
        pedido.Confirmar(1, Ahora);

        var movimientos = pedido.Cancelar("  El cliente se arrepintió ", 2, Ahora.AddHours(1));

        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
        Assert.Equal("El cliente se arrepintió", pedido.MotivoCancelacion);
        Assert.Equal(10, yerba.Stock);
        var mov = Assert.Single(movimientos);
        Assert.Equal(TipoMovimiento.CancelacionPedido, mov.Tipo);
        Assert.Equal(4, mov.Cantidad);
    }

    [Fact]
    public void Cancelar_un_borrador_no_genera_movimientos()
    {
        var yerba = NuevoProducto(1, "ALM-001", 10);
        var pedido = PedidoCon((yerba, 4));

        var movimientos = pedido.Cancelar("Duplicado", 1, Ahora);

        Assert.Empty(movimientos);
        Assert.Equal(10, yerba.Stock);
    }

    [Fact]
    public void Cancelar_exige_motivo()
    {
        var pedido = PedidoCon((NuevoProducto(1, "A", 5), 1));
        Assert.Throws<ReglaDeNegocioException>(() => pedido.Cancelar("  ", 1, Ahora));
    }

    [Theory]
    [InlineData(EstadoPedido.Borrador, EstadoPedido.Confirmado, true)]
    [InlineData(EstadoPedido.Borrador, EstadoPedido.Cancelado, true)]
    [InlineData(EstadoPedido.Borrador, EstadoPedido.Preparado, false)]
    [InlineData(EstadoPedido.Borrador, EstadoPedido.Entregado, false)]
    [InlineData(EstadoPedido.Confirmado, EstadoPedido.Preparado, true)]
    [InlineData(EstadoPedido.Confirmado, EstadoPedido.Entregado, false)]
    [InlineData(EstadoPedido.Preparado, EstadoPedido.Entregado, true)]
    [InlineData(EstadoPedido.Preparado, EstadoPedido.Cancelado, true)]
    [InlineData(EstadoPedido.Entregado, EstadoPedido.Cancelado, false)]
    [InlineData(EstadoPedido.Cancelado, EstadoPedido.Confirmado, false)]
    public void Matriz_de_transiciones(EstadoPedido desde, EstadoPedido hacia, bool permitida) =>
        Assert.Equal(permitida, Pedido.PuedePasar(desde, hacia));

    [Fact]
    public void Ciclo_completo_hasta_entregado_y_despues_no_se_puede_cancelar()
    {
        var pedido = PedidoCon((NuevoProducto(1, "A", 5), 1));
        pedido.Confirmar(1, Ahora);
        pedido.MarcarPreparado(Ahora.AddHours(2));
        pedido.MarcarEntregado(Ahora.AddDays(1));

        Assert.Equal(EstadoPedido.Entregado, pedido.Estado);
        Assert.Empty(pedido.EstadosSiguientes);
        Assert.Throws<ReglaDeNegocioException>(() => pedido.Cancelar("tarde", 1, Ahora));
    }

    [Fact]
    public void No_se_puede_confirmar_dos_veces()
    {
        var p = NuevoProducto(1, "A", 5);
        var pedido = PedidoCon((p, 1));
        pedido.Confirmar(1, Ahora);

        Assert.Throws<ReglaDeNegocioException>(() => pedido.Confirmar(1, Ahora));
        Assert.Equal(4, p.Stock);
    }

    [Fact]
    public void No_se_pueden_cambiar_renglones_de_un_pedido_confirmado()
    {
        var p = NuevoProducto(1, "A", 5);
        var pedido = PedidoCon((p, 1));
        pedido.Confirmar(1, Ahora);

        Assert.Throws<ReglaDeNegocioException>(() =>
            pedido.CambiarRenglones([new RenglonPedido { ProductoId = 1, Producto = p, Cantidad = 2 }]));
    }

    [Fact]
    public void Renglones_invalidos_se_rechazan()
    {
        var p = NuevoProducto(1, "A", 5);
        var pedido = new Pedido();
        Assert.Throws<ReglaDeNegocioException>(() => pedido.CambiarRenglones([]));
        Assert.Throws<ReglaDeNegocioException>(() => pedido.CambiarRenglones(
            [new RenglonPedido { ProductoId = 1, Producto = p, Cantidad = 0 }]));
        Assert.Throws<ReglaDeNegocioException>(() => pedido.CambiarRenglones(
        [
            new RenglonPedido { ProductoId = 1, Producto = p, Cantidad = 1 },
            new RenglonPedido { ProductoId = 1, Producto = p, Cantidad = 2 }
        ]));
    }

    [Fact]
    public void El_total_usa_el_precio_guardado_en_el_renglon()
    {
        var p = NuevoProducto(1, "A", 5, precio: 150m);
        var pedido = PedidoCon((p, 3));
        p.Precio = 999m; // cambia el precio de lista después de cargar el pedido

        Assert.Equal(450m, pedido.Total);
    }
}
