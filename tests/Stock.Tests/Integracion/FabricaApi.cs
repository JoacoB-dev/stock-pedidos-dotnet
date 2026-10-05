using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Dominio.Entidades;

namespace Stock.Tests.Integracion;

/// <summary>Reloj que las pruebas pueden mover (para los reportes por mes).</summary>
public class RelojDePrueba : TimeProvider
{
    public DateTimeOffset? Fijo { get; set; }
    public override DateTimeOffset GetUtcNow() => Fijo ?? DateTimeOffset.UtcNow;
}

/// <summary>
/// Levanta la API completa en memoria contra una base SQL Server real (stock_test).
/// La base se borra y se recrea con las migraciones una vez por corrida.
/// </summary>
public class FabricaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static string CadenaDeConexion =>
        Environment.GetEnvironmentVariable("STOCK_TEST_BD")
        ?? "Server=localhost,1433;Database=stock_test;User Id=sa;Password=Cambiar_Esta_Clave_2026;TrustServerCertificate=True";

    public RelojDePrueba Reloj { get; } = new();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Stock", CadenaDeConexion);
        builder.UseSetting("Jwt:Clave", "clave-de-pruebas-de-integracion-de-32-caracteres-o-mas");
        builder.UseSetting("Bd:MigrarAlIniciar", "false");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Reloj);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await Semilla.UsuariosAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>());
        db.Categorias.Add(new Categoria { Nombre = "Pruebas" });
        await db.SaveChangesAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>Contexto de EF para mirar la base directamente desde una prueba.</summary>
    public StockDbContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<StockDbContext>().UseSqlServer(CadenaDeConexion).Options;
        return new StockDbContext(options);
    }

    public async Task<HttpClient> ClienteComoAsync(string rol)
    {
        var http = CreateClient();
        var email = rol == "admin" ? "admin@demo.local" : "vendedor@demo.local";
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, clave = Semilla.ClaveDemo });
        resp.EnsureSuccessStatusCode();
        var login = await resp.Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());
        return http;
    }
}

[CollectionDefinition(Nombre)]
public class ColeccionApi : ICollectionFixture<FabricaApi>
{
    public const string Nombre = "API contra SQL Server";
}

/// <summary>Atajos para armar datos de prueba a través de la API.</summary>
public static class Ayudas
{
    private static int _contador;

    public static string SkuUnico() => $"T-{Interlocked.Increment(ref _contador):D4}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    /// <summary>Genera un CUIT válido al azar (prefijo 30, dígito verificador calculado).</summary>
    public static string CuitValido()
    {
        int[] pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        while (true)
        {
            var baseCuit = "30" + Random.Shared.Next(10_000_000, 99_999_999);
            var suma = baseCuit.Select((c, i) => (c - '0') * pesos[i]).Sum();
            var resto = 11 - suma % 11;
            if (resto == 10) continue;
            return baseCuit + (resto == 11 ? 0 : resto);
        }
    }

    public static async Task<JsonElement> LeerAsync(this HttpResponseMessage resp)
    {
        var texto = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static async Task<int> CategoriaAsync(FabricaApi f)
    {
        await using var db = f.NuevoContexto();
        return (await db.Categorias.FirstAsync(c => c.Nombre == "Pruebas")).Id;
    }

    public static async Task<JsonElement> CrearProductoAsync(FabricaApi f, HttpClient admin, int stock,
        decimal precio = 1000m, int minimo = 2)
    {
        var resp = await admin.PostAsJsonAsync("/api/productos", new
        {
            sku = SkuUnico(), nombre = "Producto de prueba", categoriaId = await CategoriaAsync(f),
            precio, stockMinimo = minimo, stockInicial = stock
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        return await resp.LeerAsync();
    }

    public static async Task<int> CrearClienteAsync(HttpClient http)
    {
        var resp = await http.PostAsJsonAsync("/api/clientes", new
        {
            razonSocial = $"Cliente de prueba {Guid.NewGuid():N}"[..30], cuit = CuitValido()
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        return (await resp.LeerAsync()).GetProperty("id").GetInt32();
    }

    public static async Task<int> CrearPedidoAsync(HttpClient http, int clienteId, params (int ProductoId, int Cantidad)[] renglones)
    {
        var resp = await http.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId,
            observaciones = "Pedido de prueba",
            renglones = renglones.Select(r => new { productoId = r.ProductoId, cantidad = r.Cantidad })
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        return (await resp.LeerAsync()).GetProperty("id").GetInt32();
    }

    public static async Task<int> StockAsync(FabricaApi f, int productoId)
    {
        await using var db = f.NuevoContexto();
        return await db.Productos.Where(p => p.Id == productoId).Select(p => p.Stock).SingleAsync();
    }
}
