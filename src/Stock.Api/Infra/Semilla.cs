using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stock.Datos;
using Stock.Dominio.Entidades;

namespace Stock.Api.Infra;

/// <summary>
/// Carga datos de ejemplo (ficticios) para probar la aplicación: usuarios demo, catálogo,
/// clientes y pedidos de los últimos meses para que el dashboard y los reportes tengan algo.
/// Solo corre si la base no tiene usuarios.
/// </summary>
public static class Semilla
{
    public const string ClaveDemo = "demo1234";

    public static async Task UsuariosAsync(StockDbContext db, IPasswordHasher<Usuario> hasher)
    {
        if (await db.Usuarios.AnyAsync()) return;
        db.Usuarios.AddRange(
            NuevoUsuario("admin@demo.local", "Ana Administradora", Rol.Administrador, hasher),
            NuevoUsuario("vendedor@demo.local", "Víctor Vendedor", Rol.Vendedor, hasher));
        await db.SaveChangesAsync();
    }

    public static Usuario NuevoUsuario(string email, string nombre, Rol rol, IPasswordHasher<Usuario> hasher)
    {
        var u = new Usuario { Email = email, Nombre = nombre, Rol = rol };
        u.HashClave = hasher.HashPassword(u, ClaveDemo);
        return u;
    }

    public static async Task DatosDemoAsync(StockDbContext db, TimeProvider reloj)
    {
        if (await db.Productos.AnyAsync()) return;

        var admin = await db.Usuarios.FirstAsync(u => u.Rol == Rol.Administrador);
        var vendedor = await db.Usuarios.FirstAsync(u => u.Rol == Rol.Vendedor);
        var hoy = reloj.GetUtcNow().UtcDateTime;
        var inicio = hoy.AddMonths(-5);

        var categorias = new[] { "Almacén", "Bebidas", "Limpieza", "Perfumería" }
            .Select(n => new Categoria { Nombre = n }).ToList();
        db.Categorias.AddRange(categorias);
        await db.SaveChangesAsync();
        int Cat(string nombre) => categorias.First(c => c.Nombre == nombre).Id;

        // SKU, nombre, categoría, precio, mínimo, stock inicial
        var catalogo = new (string, string, string, decimal, int, int)[]
        {
            ("ALM-001", "Yerba mate 1 kg (caja x 10)", "Almacén", 42000m, 10, 120),
            ("ALM-002", "Azúcar 1 kg (bulto x 10)", "Almacén", 11500m, 15, 200),
            ("ALM-003", "Fideos secos 500 g (caja x 20)", "Almacén", 19800m, 10, 90),
            ("ALM-004", "Arroz largo fino 1 kg (bulto x 10)", "Almacén", 16200m, 10, 60),
            ("ALM-005", "Aceite girasol 1,5 L (caja x 12)", "Almacén", 38500m, 8, 45),
            ("ALM-006", "Harina 000 1 kg (bulto x 10)", "Almacén", 9800m, 12, 30),
            ("BEB-001", "Agua mineral 2 L (pack x 6)", "Bebidas", 6200m, 30, 300),
            ("BEB-002", "Gaseosa cola 2,25 L (pack x 6)", "Bebidas", 15600m, 25, 180),
            ("BEB-003", "Jugo en polvo (caja x 20)", "Bebidas", 7400m, 10, 40),
            ("BEB-004", "Soda 2 L (pack x 6)", "Bebidas", 5100m, 20, 70),
            ("LIM-001", "Lavandina 1 L (caja x 12)", "Limpieza", 8900m, 15, 110),
            ("LIM-002", "Detergente 750 ml (caja x 12)", "Limpieza", 14300m, 12, 50),
            ("LIM-003", "Jabón en polvo 3 kg (caja x 4)", "Limpieza", 31000m, 6, 20),
            ("PER-001", "Papel higiénico 30 m (bolsón x 48)", "Perfumería", 21500m, 10, 70),
            ("PER-002", "Shampoo 400 ml (caja x 12)", "Perfumería", 27600m, 5, 18),
            ("PER-003", "Jabón de tocador (caja x 48)", "Perfumería", 24900m, 6, 25),
        };
        var productos = new List<Producto>();
        foreach (var (sku, nombre, cat, precio, minimo, stock) in catalogo)
        {
            var p = new Producto(sku, nombre, Cat(cat), precio, minimo);
            db.Productos.Add(p);
            db.Movimientos.Add(p.AplicarMovimiento(TipoMovimiento.Ingreso, stock, admin.Id, inicio, motivo: "Stock inicial"));
            productos.Add(p);
        }

        var clientes = new List<Cliente>
        {
            new() { RazonSocial = "Almacén Don Tito", Cuit = "20287654325", Telefono = "11 4455-0001", Direccion = "Av. Ejemplo 123, Hurlingham" },
            new() { RazonSocial = "Autoservicio La Esquina SRL", Cuit = "30712345671", Email = "compras@laesquina.example", Direccion = "Calle Falsa 456, Morón" },
            new() { RazonSocial = "Supermercado El Sol SA", Cuit = "30709988774", Email = "pedidos@elsol.example", Direccion = "Ruta 7 km 30, Ituzaingó" },
            new() { RazonSocial = "Kiosco 24 h Estación", Cuit = "27332211000", Telefono = "11 4455-0004" },
            new() { RazonSocial = "Minimercado Los Pinos SRL", Cuit = "30715554441", Direccion = "Los Pinos 789, Castelar" },
            new() { RazonSocial = "Distribuidora Barrial SA", Cuit = "30711002231", Email = "admin@barrial.example" },
        };
        db.Clientes.AddRange(clientes);
        await db.SaveChangesAsync();

        // Pedidos de los últimos meses. Se usan los mismos métodos del dominio que usa la API,
        // así el stock y los movimientos quedan coherentes.
        var azar = new Random(2026);
        for (var i = 0; i < 60; i++)
        {
            var fecha = inicio.AddDays(3 + i * 4).AddHours(azar.Next(9, 18));
            if (fecha > hoy) break;
            var pedido = new Pedido
            {
                ClienteId = clientes[azar.Next(clientes.Count)].Id,
                VendedorId = i % 3 == 0 ? admin.Id : vendedor.Id,
                FechaCreacion = fecha
            };
            pedido.CambiarRenglones(productos.OrderBy(_ => azar.Next()).Take(azar.Next(1, 5))
                .Select(p => new RenglonPedido { ProductoId = p.Id, Producto = p, Cantidad = azar.Next(1, 6), PrecioUnitario = p.Precio }));
            db.Pedidos.Add(pedido);
            await db.SaveChangesAsync();

            // Los más recientes quedan en distintos estados para que se vea todo el ciclo
            var antiguedad = (hoy - fecha).TotalDays;
            if (antiguedad < 6 && i % 2 == 0) continue; // queda en borrador
            try
            {
                db.Movimientos.AddRange(pedido.Confirmar(pedido.VendedorId, fecha.AddHours(1)));
            }
            catch (Dominio.Excepciones.StockInsuficienteException)
            {
                continue;
            }
            if (i % 9 == 4)
                db.Movimientos.AddRange(pedido.Cancelar("El cliente dio de baja la compra", admin.Id, fecha.AddDays(1)));
            else if (antiguedad > 12)
            {
                pedido.MarcarPreparado(fecha.AddDays(1));
                pedido.MarcarEntregado(fecha.AddDays(2));
            }
            else if (antiguedad > 6)
                pedido.MarcarPreparado(fecha.AddDays(1));
            await db.SaveChangesAsync();
        }

        // Un par de productos casi agotados para que el dashboard muestre alertas
        foreach (var sku in new[] { "ALM-006", "PER-002", "BEB-003" })
        {
            var p = productos.First(x => x.Sku == sku);
            var bajar = p.Stock - azar.Next(0, p.StockMinimo);
            if (bajar > 0)
                db.Movimientos.Add(p.AplicarMovimiento(TipoMovimiento.Ajuste, -bajar, admin.Id, hoy.AddDays(-1),
                    motivo: "Ajuste por inventario físico"));
        }
        await db.SaveChangesAsync();
    }
}
