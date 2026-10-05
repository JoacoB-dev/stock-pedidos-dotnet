using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Dominio.Entidades;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(StockDbContext db, TimeProvider reloj) : ControllerBase
{
    [HttpGet]
    public async Task<DashboardDto> Obtener(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var inicioMes = new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var stockBajo = await db.Productos.AsNoTracking()
            .Where(p => p.Activo && p.Stock <= p.StockMinimo)
            .OrderBy(p => p.Stock - p.StockMinimo).ThenBy(p => p.Nombre)
            .Select(Mapeos.AProductoDto).Take(20).ToListAsync(ct);

        // El vendedor ve los números de sus pedidos; el administrador, los de todos
        var pedidos = db.Pedidos.AsNoTracking();
        if (!User.EsAdministrador())
        {
            var yo = User.IdUsuario();
            pedidos = pedidos.Where(p => p.VendedorId == yo);
        }

        var porEstado = await pedidos.GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Estado, x => x.Cantidad, ct);
        foreach (var estado in Enum.GetValues<EstadoPedido>()) porEstado.TryAdd(estado, 0);

        var vendidosDelMes = pedidos.Where(p => p.FechaConfirmacion >= inicioMes &&
            (p.Estado == EstadoPedido.Confirmado || p.Estado == EstadoPedido.Preparado || p.Estado == EstadoPedido.Entregado));
        var ventas = await vendidosDelMes.SelectMany(p => p.Renglones).SumAsync(r => (decimal?)(r.Cantidad * r.PrecioUnitario), ct) ?? 0;
        var cantidad = await vendidosDelMes.CountAsync(ct);

        var ultimos = await pedidos.OrderByDescending(p => p.Id).Take(5).Select(Mapeos.APedidoResumenDto).ToListAsync(ct);

        return new DashboardDto(stockBajo, porEstado, ventas, cantidad, ultimos);
    }
}
