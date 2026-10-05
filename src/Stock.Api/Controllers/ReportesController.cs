using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Datos.Consultas;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Roles = Roles.Administrador)]
public class ReportesController(StockDbContext db, TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Ventas por mes y producto. Lee la vista vw_VentasPorMesProducto, mapeada en EF como
    /// entidad sin clave, así que se puede filtrar y ordenar con LINQ.
    /// </summary>
    [HttpGet("ventas-por-producto")]
    public Task<List<VentaMesProducto>> VentasPorProducto(int? anio, int? mes, string? categoria,
        CancellationToken ct)
    {
        anio ??= reloj.GetUtcNow().Year;
        var q = db.VentasPorMesProducto.Where(v => v.Anio == anio);
        if (mes is not null) q = q.Where(v => v.Mes == mes);
        if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(v => v.Categoria == categoria);
        return q.OrderBy(v => v.Anio).ThenBy(v => v.Mes).ThenByDescending(v => v.Importe).ToListAsync(ct);
    }

    /// <summary>
    /// Resumen mensual (pedidos, unidades, importe, ticket promedio) entre dos fechas.
    /// Lo calcula el stored procedure sp_ResumenVentasMensual. Por defecto, los últimos 12 meses.
    /// </summary>
    [HttpGet("resumen-mensual")]
    public Task<List<ResumenVentasMes>> ResumenMensual(DateTime? desde, DateTime? hasta, CancellationToken ct)
    {
        var hoy = reloj.GetUtcNow().UtcDateTime.Date;
        var fin = (hasta?.Date ?? hoy).AddDays(1);
        var inicio = desde?.Date ?? new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-11);
        if (fin <= inicio) throw new ReglaDeNegocioException("La fecha hasta no puede ser anterior a la fecha desde.");
        return db.ResumenVentasMensualAsync(inicio, fin, ct);
    }
}
