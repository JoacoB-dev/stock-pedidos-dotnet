using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Dominio.Entidades;

namespace Stock.Api.Controllers;

/// <summary>Auditoría de stock: cada entrada y salida, quién la hizo y por qué.</summary>
[ApiController]
[Route("api/movimientos")]
[Authorize(Roles = Roles.Administrador)]
public class MovimientosController(StockDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<Pagina<MovimientoDto>> Listar(int? productoId, TipoMovimiento? tipo, int? pedidoId,
        DateTime? desde, DateTime? hasta, int pagina = 1, int tamanio = 20, CancellationToken ct = default)
    {
        var q = db.Movimientos.AsQueryable();
        if (productoId is not null) q = q.Where(m => m.ProductoId == productoId);
        if (tipo is not null) q = q.Where(m => m.Tipo == tipo);
        if (pedidoId is not null) q = q.Where(m => m.PedidoId == pedidoId);
        if (desde is not null) q = q.Where(m => m.Fecha >= desde);
        if (hasta is not null) q = q.Where(m => m.Fecha < hasta.Value.Date.AddDays(1));
        return q.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Select(Mapeos.AMovimientoDto).PaginarAsync(pagina, tamanio, ct);
    }
}
