using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integracion;

[Collection(ColeccionApi.Nombre)]
public class ReportesTests(FabricaApi f)
{
    /// <summary>
    /// Usa fechas de 2024 (ninguna otra prueba escribe en ese año) moviendo el reloj de la app.
    /// Verifica la vista y el stored procedure con números calculados a mano.
    /// </summary>
    [Fact]
    public async Task Vista_y_stored_procedure_calculan_las_ventas_por_mes()
    {
        var admin = await f.ClienteComoAsync("admin");
        var a = await Ayudas.CrearProductoAsync(f, admin, stock: 100, precio: 100);
        var b = await Ayudas.CrearProductoAsync(f, admin, stock: 100, precio: 50);
        int idA = a.GetProperty("id").GetInt32(), idB = b.GetProperty("id").GetInt32();
        var clienteId = await Ayudas.CrearClienteAsync(admin);

        try
        {
            async Task<int> PedidoConfirmadoEl(DateTime fecha, params (int, int)[] renglones)
            {
                f.Reloj.Fijo = new DateTimeOffset(fecha, TimeSpan.Zero);
                var id = await Ayudas.CrearPedidoAsync(admin, clienteId, renglones);
                Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/pedidos/{id}/confirmar", null)).StatusCode);
                return id;
            }

            // Marzo 2024: dos pedidos (2xA + 1xB = 250) y (1xA = 100)
            await PedidoConfirmadoEl(new DateTime(2024, 3, 5), (idA, 2), (idB, 1));
            await PedidoConfirmadoEl(new DateTime(2024, 3, 20), (idA, 1));
            // Abril 2024: uno confirmado (4xB = 200) y uno cancelado (no cuenta)
            await PedidoConfirmadoEl(new DateTime(2024, 4, 2), (idB, 4));
            var cancelado = await PedidoConfirmadoEl(new DateTime(2024, 4, 3), (idA, 10));
            await admin.PostAsJsonAsync($"/api/pedidos/{cancelado}/cancelar", new { motivo = "prueba" });
            // Un borrador tampoco cuenta
            await Ayudas.CrearPedidoAsync(admin, clienteId, (idA, 50));
        }
        finally
        {
            f.Reloj.Fijo = null;
        }

        // Stored procedure
        var resumen = await (await admin.GetAsync("/api/reportes/resumen-mensual?desde=2024-01-01&hasta=2024-12-31")).LeerAsync();
        var meses = resumen.EnumerateArray().ToList();
        Assert.Equal(2, meses.Count);
        var marzo = meses[0];
        Assert.Equal(3, marzo.GetProperty("mes").GetInt32());
        Assert.Equal(2, marzo.GetProperty("pedidos").GetInt32());
        Assert.Equal(4, marzo.GetProperty("unidades").GetInt32());
        Assert.Equal(350m, marzo.GetProperty("importe").GetDecimal());
        Assert.Equal(175m, marzo.GetProperty("ticketPromedio").GetDecimal());
        var abril = meses[1];
        Assert.Equal(1, abril.GetProperty("pedidos").GetInt32());
        Assert.Equal(200m, abril.GetProperty("importe").GetDecimal());

        // Vista
        var vista = await (await admin.GetAsync("/api/reportes/ventas-por-producto?anio=2024&mes=3")).LeerAsync();
        var filas = vista.EnumerateArray().ToList();
        var filaA = filas.Single(x => x.GetProperty("productoId").GetInt32() == idA);
        Assert.Equal(3, filaA.GetProperty("unidades").GetInt32());
        Assert.Equal(300m, filaA.GetProperty("importe").GetDecimal());
        Assert.Equal(2, filaA.GetProperty("pedidos").GetInt32());
        Assert.Equal("Pruebas", filaA.GetProperty("categoria").GetString());
    }

    [Fact]
    public async Task Rango_de_fechas_invertido_da_422()
    {
        var admin = await f.ClienteComoAsync("admin");
        var resp = await admin.GetAsync("/api/reportes/resumen-mensual?desde=2024-05-01&hasta=2024-01-01");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Dashboard_muestra_productos_con_stock_bajo()
    {
        var admin = await f.ClienteComoAsync("admin");
        var p = await Ayudas.CrearProductoAsync(f, admin, stock: 0, minimo: 3);

        var json = await (await admin.GetAsync("/api/dashboard")).LeerAsync();
        Assert.Contains(json.GetProperty("stockBajo").EnumerateArray(),
            x => x.GetProperty("id").GetInt32() == p.GetProperty("id").GetInt32());
        Assert.True(json.GetProperty("pedidosPorEstado").TryGetProperty("Borrador", out _));
    }
}
