using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Stock.Dominio.Entidades;

namespace Stock.Tests.Integracion;

[Collection(ColeccionApi.Nombre)]
public class PedidosTests(FabricaApi f)
{
    [Fact]
    public async Task Crear_un_borrador_no_toca_el_stock_y_guarda_el_precio()
    {
        var admin = await f.ClienteComoAsync("admin");
        var vendedor = await f.ClienteComoAsync("vendedor");
        var p = await Ayudas.CrearProductoAsync(f, admin, stock: 10, precio: 250);
        var productoId = p.GetProperty("id").GetInt32();
        var clienteId = await Ayudas.CrearClienteAsync(vendedor);

        var pedidoId = await Ayudas.CrearPedidoAsync(vendedor, clienteId, (productoId, 4));
        var pedido = await (await vendedor.GetAsync($"/api/pedidos/{pedidoId}")).LeerAsync();

        Assert.Equal("Borrador", pedido.GetProperty("estado").GetString());
        Assert.Equal(1000m, pedido.GetProperty("total").GetDecimal());
        Assert.Equal(10, await Ayudas.StockAsync(f, productoId));
    }

    [Fact]
    public async Task Ciclo_completo_confirmar_preparar_entregar()
    {
        var admin = await f.ClienteComoAsync("admin");
        var vendedor = await f.ClienteComoAsync("vendedor");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 10)).GetProperty("id").GetInt32();
        var pedidoId = await Ayudas.CrearPedidoAsync(vendedor, await Ayudas.CrearClienteAsync(vendedor), (productoId, 6));

        var confirmar = await vendedor.PostAsync($"/api/pedidos/{pedidoId}/confirmar", null);
        Assert.Equal(HttpStatusCode.OK, confirmar.StatusCode);
        Assert.Equal(4, await Ayudas.StockAsync(f, productoId));

        // Preparar y entregar es tarea del administrador
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.PostAsync($"/api/pedidos/{pedidoId}/preparar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/pedidos/{pedidoId}/preparar", null)).StatusCode);
        var entregado = await (await admin.PostAsync($"/api/pedidos/{pedidoId}/entregar", null)).LeerAsync();

        Assert.Equal("Entregado", entregado.GetProperty("estado").GetString());
        Assert.Equal(0, entregado.GetProperty("estadosSiguientes").GetArrayLength());

        // Un entregado ya no se cancela
        var cancelar = await admin.PostAsJsonAsync($"/api/pedidos/{pedidoId}/cancelar", new { motivo = "tarde" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelar.StatusCode);
    }

    [Fact]
    public async Task Confirmar_sin_stock_da_409_con_faltantes_y_no_descuenta_nada()
    {
        var admin = await f.ClienteComoAsync("admin");
        var conStock = (await Ayudas.CrearProductoAsync(f, admin, stock: 50)).GetProperty("id").GetInt32();
        var sinStock = (await Ayudas.CrearProductoAsync(f, admin, stock: 2)).GetProperty("id").GetInt32();
        var pedidoId = await Ayudas.CrearPedidoAsync(admin, await Ayudas.CrearClienteAsync(admin), (conStock, 5), (sinStock, 3));

        var resp = await admin.PostAsync($"/api/pedidos/{pedidoId}/confirmar", null);
        var json = await resp.LeerAsync();

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var faltante = Assert.Single(json.GetProperty("faltantes").EnumerateArray());
        Assert.Equal(sinStock, faltante.GetProperty("productoId").GetInt32());
        Assert.Equal(2, faltante.GetProperty("disponible").GetInt32());
        Assert.Equal(50, await Ayudas.StockAsync(f, conStock));
        Assert.Equal(2, await Ayudas.StockAsync(f, sinStock));
    }

    [Fact]
    public async Task Cancelar_un_confirmado_devuelve_el_stock_y_deja_movimientos_auditables()
    {
        var admin = await f.ClienteComoAsync("admin");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 20)).GetProperty("id").GetInt32();
        var pedidoId = await Ayudas.CrearPedidoAsync(admin, await Ayudas.CrearClienteAsync(admin), (productoId, 8));
        await admin.PostAsync($"/api/pedidos/{pedidoId}/confirmar", null);
        Assert.Equal(12, await Ayudas.StockAsync(f, productoId));

        var resp = await admin.PostAsJsonAsync($"/api/pedidos/{pedidoId}/cancelar", new { motivo = "Sin crédito" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(20, await Ayudas.StockAsync(f, productoId));
        var movs = await (await admin.GetAsync($"/api/movimientos?pedidoId={pedidoId}")).LeerAsync();
        var tipos = movs.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("tipo").GetString()).ToList();
        Assert.Equal(["CancelacionPedido", "VentaConfirmada"], tipos); // más nuevo primero
    }

    [Fact]
    public async Task Un_vendedor_no_puede_cancelar_un_confirmado_pero_si_su_borrador()
    {
        var admin = await f.ClienteComoAsync("admin");
        var vendedor = await f.ClienteComoAsync("vendedor");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 20)).GetProperty("id").GetInt32();
        var clienteId = await Ayudas.CrearClienteAsync(vendedor);
        var confirmado = await Ayudas.CrearPedidoAsync(vendedor, clienteId, (productoId, 1));
        var borrador = await Ayudas.CrearPedidoAsync(vendedor, clienteId, (productoId, 1));
        await vendedor.PostAsync($"/api/pedidos/{confirmado}/confirmar", null);

        var r1 = await vendedor.PostAsJsonAsync($"/api/pedidos/{confirmado}/cancelar", new { motivo = "x" });
        var r2 = await vendedor.PostAsJsonAsync($"/api/pedidos/{borrador}/cancelar", new { motivo = "Duplicado" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Fact]
    public async Task Un_vendedor_no_ve_pedidos_de_otro()
    {
        var admin = await f.ClienteComoAsync("admin");
        var vendedor = await f.ClienteComoAsync("vendedor");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 5)).GetProperty("id").GetInt32();
        var delAdmin = await Ayudas.CrearPedidoAsync(admin, await Ayudas.CrearClienteAsync(admin), (productoId, 1));

        Assert.Equal(HttpStatusCode.NotFound, (await vendedor.GetAsync($"/api/pedidos/{delAdmin}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vendedor.PostAsync($"/api/pedidos/{delAdmin}/confirmar", null)).StatusCode);
        var lista = await (await vendedor.GetAsync("/api/pedidos?tamanio=100")).LeerAsync();
        Assert.DoesNotContain(lista.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetInt32() == delAdmin);
    }

    [Fact]
    public async Task Modificar_un_borrador_reemplaza_los_renglones_y_un_confirmado_no_se_modifica()
    {
        var admin = await f.ClienteComoAsync("admin");
        var a = (await Ayudas.CrearProductoAsync(f, admin, stock: 10, precio: 100)).GetProperty("id").GetInt32();
        var b = (await Ayudas.CrearProductoAsync(f, admin, stock: 10, precio: 10)).GetProperty("id").GetInt32();
        var clienteId = await Ayudas.CrearClienteAsync(admin);
        var pedidoId = await Ayudas.CrearPedidoAsync(admin, clienteId, (a, 1), (b, 1));

        // Mismo producto con otra cantidad + uno quitado: prueba el índice único (PedidoId, ProductoId)
        var resp = await admin.PutAsJsonAsync($"/api/pedidos/{pedidoId}", new
        {
            clienteId, observaciones = "cambiado", renglones = new[] { new { productoId = a, cantidad = 3 } }
        });
        var json = await resp.LeerAsync();
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(300m, json.GetProperty("total").GetDecimal());
        Assert.Equal(1, json.GetProperty("renglones").GetArrayLength());

        await admin.PostAsync($"/api/pedidos/{pedidoId}/confirmar", null);
        var otra = await admin.PutAsJsonAsync($"/api/pedidos/{pedidoId}", new
        {
            clienteId, observaciones = "", renglones = new[] { new { productoId = a, cantidad = 1 } }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otra.StatusCode);
    }

    [Fact]
    public async Task Pedido_con_producto_inexistente_o_renglones_repetidos_se_rechaza()
    {
        var admin = await f.ClienteComoAsync("admin");
        var clienteId = await Ayudas.CrearClienteAsync(admin);
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 5)).GetProperty("id").GetInt32();

        var inexistente = await admin.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId, renglones = new[] { new { productoId = 999999, cantidad = 1 } }
        });
        var repetido = await admin.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId, renglones = new[] { new { productoId, cantidad = 1 }, new { productoId, cantidad = 2 } }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
    }

    /// <summary>
    /// La prueba importante: muchos pedidos compiten por las mismas unidades al mismo tiempo.
    /// Con 10 unidades y 8 pedidos de 3, tienen que confirmarse exactamente 3, el resto tiene
    /// que fallar, el stock no puede quedar negativo y los movimientos tienen que cerrar.
    /// </summary>
    [Fact]
    public async Task Confirmaciones_simultaneas_no_venden_mas_de_lo_que_hay()
    {
        var admin = await f.ClienteComoAsync("admin");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 10)).GetProperty("id").GetInt32();
        var clienteId = await Ayudas.CrearClienteAsync(admin);
        var pedidos = new List<int>();
        for (var i = 0; i < 8; i++)
            pedidos.Add(await Ayudas.CrearPedidoAsync(admin, clienteId, (productoId, 3)));

        // Cada pedido con su propio HttpClient, todos a la vez
        var clientes = await Task.WhenAll(pedidos.Select(_ => f.ClienteComoAsync("admin")));
        using var largada = new ManualResetEventSlim(false);
        var tareas = pedidos.Select((id, i) => Task.Run(async () =>
        {
            largada.Wait();
            return await clientes[i].PostAsync($"/api/pedidos/{id}/confirmar", null);
        })).ToList();
        largada.Set();
        var respuestas = await Task.WhenAll(tareas);

        var ok = respuestas.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, ok);
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await Ayudas.StockAsync(f, productoId));

        await using var db = f.NuevoContexto();
        var sumaMovimientos = await db.Movimientos.Where(m => m.ProductoId == productoId).SumAsync(m => m.Cantidad);
        Assert.Equal(1, sumaMovimientos); // 10 de ingreso - 3 x 3 vendidos
        var confirmados = await db.Pedidos.CountAsync(p => pedidos.Contains(p.Id) && p.Estado == EstadoPedido.Confirmado);
        Assert.Equal(3, confirmados);
    }

    [Fact]
    public async Task Confirmar_dos_veces_el_mismo_pedido_a_la_vez_descuenta_una_sola_vez()
    {
        var admin = await f.ClienteComoAsync("admin");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 10)).GetProperty("id").GetInt32();
        var pedidoId = await Ayudas.CrearPedidoAsync(admin, await Ayudas.CrearClienteAsync(admin), (productoId, 2));
        var c1 = await f.ClienteComoAsync("admin");
        var c2 = await f.ClienteComoAsync("admin");

        var respuestas = await Task.WhenAll(
            Task.Run(() => c1.PostAsync($"/api/pedidos/{pedidoId}/confirmar", null)),
            Task.Run(() => c2.PostAsync($"/api/pedidos/{pedidoId}/confirmar", null)));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict);
        Assert.Equal(8, await Ayudas.StockAsync(f, productoId));
    }

    [Fact]
    public async Task Listado_de_pedidos_filtra_por_estado()
    {
        var admin = await f.ClienteComoAsync("admin");
        var productoId = (await Ayudas.CrearProductoAsync(f, admin, stock: 10)).GetProperty("id").GetInt32();
        var pedidoId = await Ayudas.CrearPedidoAsync(admin, await Ayudas.CrearClienteAsync(admin), (productoId, 1));
        await admin.PostAsJsonAsync($"/api/pedidos/{pedidoId}/cancelar", new { motivo = "prueba" });

        var json = await (await admin.GetAsync("/api/pedidos?estado=Cancelado&tamanio=100")).LeerAsync();
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, p => p.GetProperty("id").GetInt32() == pedidoId);
        Assert.All(items, p => Assert.Equal("Cancelado", p.GetProperty("estado").GetString()));
    }
}
