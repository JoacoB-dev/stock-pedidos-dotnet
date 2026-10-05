using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stock.Datos;

/// <summary>
/// Solo la usa `dotnet ef` para generar migraciones sin levantar la API.
/// </summary>
public class DisenoDbContextFactory : IDesignTimeDbContextFactory<StockDbContext>
{
    public StockDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Stock")
            ?? "Server=localhost,1433;Database=stock_dev;User Id=sa;Password=cambiar;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<StockDbContext>().UseSqlServer(cadena).Options;
        return new StockDbContext(options);
    }
}
