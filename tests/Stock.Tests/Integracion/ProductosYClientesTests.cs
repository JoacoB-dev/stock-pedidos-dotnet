using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Stock.Dominio.Entidades;

namespace Stock.Tests.Integracion;

[Collection(ColeccionApi.Nombre)]
public class ProductosYClientesTests(FabricaApi f)
{
    [Fact]
    public async Task Alta_de_producto_registra_el_stock_inicial_como_movimiento()
    {
        var admin = await f.ClienteComoAsync("admin");
        var producto = await Ayudas.CrearProductoAsync(f, admin, stock: 25);
        var id = producto.GetProperty("id").GetInt32();

        Assert.Equal(25, producto.GetProperty("stock").GetInt32());
        var movs = await (await admin.GetAsync($"/api/movimientos?productoId={id}")).LeerAsync();
        var mov = Assert.Single(movs.GetProperty("items").EnumerateArray());
        Assert.Equal("Ingreso", mov.GetProperty("tipo").GetString());
        Assert.Equal(25, mov.GetProperty("stockResultante").GetInt32());
    }

    [Fact]
    public async Task Sku_duplicado_da_422()
    {
        var admin = await f.ClienteComoAsync("admin");
        var p = await Ayudas.CrearProductoAsync(f, admin, stock: 0);
        var resp = await admin.PostAsJsonAsync("/api/productos", new
        {
            sku = p.GetProperty("sku").GetString(), nombre = "Otro", categoriaId = await Ayudas.CategoriaAsync(f),
            precio = 10, stockMinimo = 0, stockInicial = 0
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Listado_filtra_por_stock_bajo_y_pagina()
    {
        var admin = await f.ClienteComoAsync("admin");
        var bajo = await Ayudas.CrearProductoAsync(f, admin, stock: 1, minimo: 5);
        await Ayudas.CrearProductoAsync(f, admin, stock: 50, minimo: 5);

        var json = await (await admin.GetAsync("/api/productos?soloStockBajo=true&tamanio=100")).LeerAsync();
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("id").GetInt32() == bajo.GetProperty("id").GetInt32());
        Assert.All(items, i => Assert.True(i.GetProperty("stockBajo").GetBoolean()));

        var pagina = await (await admin.GetAsync("/api/productos?tamanio=1&pagina=2")).LeerAsync();
        Assert.Single(pagina.GetProperty("items").EnumerateArray());
        Assert.Equal(2, pagina.GetProperty("numeroPagina").GetInt32());
        Assert.True(pagina.GetProperty("totalPaginas").GetInt32() >= 2);
    }

    [Fact]
    public async Task Editar_con_rowversion_vieja_da_409_y_no_pisa_el_cambio_del_otro()
    {
        var admin = await f.ClienteComoAsync("admin");
        var p = await Ayudas.CrearProductoAsync(f, admin, stock: 3, precio: 100);
        var id = p.GetProperty("id").GetInt32();
        var versionVieja = p.GetProperty("rowVersion").GetString();
        var categoria = await Ayudas.CategoriaAsync(f);

        // Dos usuarios abrieron el mismo formulario; el primero guarda
        var primero = await admin.PutAsJsonAsync($"/api/productos/{id}", new
        {
            nombre = "Nombre del primero", categoriaId = categoria, precio = 150, stockMinimo = 2, activo = true, rowVersion = versionVieja
        });
        // El segundo guarda con la versión que había leído antes
        var segundo = await admin.PutAsJsonAsync($"/api/productos/{id}", new
        {
            nombre = "Nombre del segundo", categoriaId = categoria, precio = 999, stockMinimo = 2, activo = true, rowVersion = versionVieja
        });

        Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        var actual = await (await admin.GetAsync($"/api/productos/{id}")).LeerAsync();
        Assert.Equal("Nombre del primero", actual.GetProperty("nombre").GetString());
        Assert.Equal(150m, actual.GetProperty("precio").GetDecimal());
    }

    [Fact]
    public async Task Ajuste_que_deja_stock_negativo_da_422()
    {
        var admin = await f.ClienteComoAsync("admin");
        var id = (await Ayudas.CrearProductoAsync(f, admin, stock: 2)).GetProperty("id").GetInt32();

        var resp = await admin.PostAsJsonAsync($"/api/productos/{id}/movimientos",
            new { tipo = "Ajuste", cantidad = -5, motivo = "Rotura" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        Assert.Equal(2, await Ayudas.StockAsync(f, id));
    }

    [Fact]
    public async Task La_base_rechaza_stock_negativo_aunque_se_saltee_el_codigo()
    {
        var admin = await f.ClienteComoAsync("admin");
        var id = (await Ayudas.CrearProductoAsync(f, admin, stock: 1)).GetProperty("id").GetInt32();

        await using var db = f.NuevoContexto();
        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE Productos SET Stock = -1 WHERE Id = {id}"));
        Assert.Contains("CK_Productos_Stock_NoNegativo", ex.Message);
    }

    [Fact]
    public async Task Cliente_con_cuit_invalido_da_400_y_duplicado_422()
    {
        var vendedor = await f.ClienteComoAsync("vendedor");
        var invalido = await vendedor.PostAsJsonAsync("/api/clientes", new { razonSocial = "X", cuit = "30712345672" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.True((await invalido.LeerAsync()).GetProperty("errors").TryGetProperty("Cuit", out _));

        var cuit = Ayudas.CuitValido();
        var primero = await vendedor.PostAsJsonAsync("/api/clientes", new { razonSocial = "Uno", cuit });
        var repetido = await vendedor.PostAsJsonAsync("/api/clientes", new { razonSocial = "Dos", cuit = $"{cuit[..2]}-{cuit[2..10]}-{cuit[10]}" });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repetido.StatusCode);
    }

    [Fact]
    public async Task Producto_inexistente_da_404_en_formato_problem_details()
    {
        var admin = await f.ClienteComoAsync("admin");
        var resp = await admin.GetAsync("/api/productos/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Swagger_y_openapi_responden()
    {
        var http = f.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task Los_enums_se_guardan_como_texto()
    {
        await using var db = f.NuevoContexto();
        var roles = await db.Database.SqlQuery<string>($"SELECT Rol AS Value FROM Usuarios").ToListAsync();
        Assert.Contains(nameof(Rol.Administrador), roles);
    }
}
