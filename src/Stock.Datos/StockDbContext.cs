using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stock.Datos.Consultas;
using Stock.Dominio.Entidades;

namespace Stock.Datos;

public class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<RenglonPedido> Renglones => Set<RenglonPedido>();
    public DbSet<MovimientoStock> Movimientos => Set<MovimientoStock>();

    /// <summary>Vista vw_VentasPorMesProducto (solo lectura).</summary>
    public DbSet<VentaMesProducto> VentasPorMesProducto => Set<VentaMesProducto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Toma todas las clases IEntityTypeConfiguration<> de este proyecto
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Los enums se guardan como texto: la base se entiende sin mirar el código
        configurationBuilder.Properties<Rol>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EstadoPedido>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<TipoMovimiento>().HaveConversion<string>().HaveMaxLength(30);

        // Todas las fechas se guardan en UTC; al leerlas les marcamos Kind = Utc para que
        // el JSON salga con "Z" y el navegador las pase bien a hora local
        configurationBuilder.Properties<DateTime>().HaveConversion<FechaUtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<FechaUtcConverter>();

        // Importes: decimal(18,2) por defecto (también para los tipos que se leen con SqlQuery,
        // como el resultado del stored procedure)
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    /// <summary>
    /// Llama al stored procedure sp_ResumenVentasMensual. Devuelve un renglón por mes
    /// con la cantidad de pedidos, unidades e importe vendido.
    /// </summary>
    public Task<List<ResumenVentasMes>> ResumenVentasMensualAsync(DateTime desde, DateTime hasta,
        CancellationToken ct = default) =>
        Database.SqlQuery<ResumenVentasMes>(
                $"EXEC dbo.sp_ResumenVentasMensual @Desde = {desde}, @Hasta = {hasta}")
            .ToListAsync(ct);
}

internal class FechaUtcConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
