using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integracion;

[Collection(ColeccionApi.Nombre)]
public class AuthYPermisosTests(FabricaApi f)
{
    [Fact]
    public async Task Login_correcto_devuelve_token_y_rol()
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "ADMIN@demo.local ", clave = "demo1234" });
        var json = await resp.LeerAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.False(string.IsNullOrEmpty(json.GetProperty("token").GetString()));
        Assert.Equal("Administrador", json.GetProperty("usuario").GetProperty("rol").GetString());
    }

    [Fact]
    public async Task Login_con_clave_incorrecta_da_401_con_problem_details()
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "admin@demo.local", clave = "otra" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Email o contraseña incorrectos.", (await resp.LeerAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_sin_datos_da_400_con_errores_por_campo()
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "", clave = "" });
        var json = await resp.LeerAsync();

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True(json.GetProperty("errors").TryGetProperty("Email", out _));
        Assert.True(json.GetProperty("errors").TryGetProperty("Clave", out _));
    }

    [Theory]
    [InlineData("/api/productos")]
    [InlineData("/api/pedidos")]
    [InlineData("/api/dashboard")]
    public async Task Sin_token_da_401(string ruta)
    {
        var resp = await f.CreateClient().GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("/api/movimientos")]
    [InlineData("/api/reportes/resumen-mensual")]
    [InlineData("/api/reportes/ventas-por-producto")]
    public async Task Un_vendedor_no_ve_auditoria_ni_reportes(string ruta)
    {
        var vendedor = await f.ClienteComoAsync("vendedor");
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.GetAsync(ruta)).StatusCode);
    }

    [Fact]
    public async Task Un_vendedor_no_puede_crear_productos_ni_registrar_ingresos()
    {
        var vendedor = await f.ClienteComoAsync("vendedor");
        var admin = await f.ClienteComoAsync("admin");
        var producto = await Ayudas.CrearProductoAsync(f, admin, stock: 5);
        var id = producto.GetProperty("id").GetInt32();

        var alta = await vendedor.PostAsJsonAsync("/api/productos", new
        {
            sku = Ayudas.SkuUnico(), nombre = "X", categoriaId = await Ayudas.CategoriaAsync(f), precio = 1, stockMinimo = 0, stockInicial = 0
        });
        var ingreso = await vendedor.PostAsJsonAsync($"/api/productos/{id}/movimientos", new { tipo = "Ingreso", cantidad = 10 });

        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ingreso.StatusCode);
        Assert.Equal(5, await Ayudas.StockAsync(f, id));
    }

    [Fact]
    public async Task Yo_devuelve_el_usuario_del_token()
    {
        var vendedor = await f.ClienteComoAsync("vendedor");
        var json = await (await vendedor.GetAsync("/api/auth/yo")).LeerAsync();
        Assert.Equal("vendedor@demo.local", json.GetProperty("email").GetString());
    }
}
