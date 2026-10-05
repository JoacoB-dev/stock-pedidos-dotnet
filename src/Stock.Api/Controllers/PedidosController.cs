using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Api.Servicios;
using Stock.Datos;
using Stock.Dominio.Entidades;

namespace Stock.Api.Controllers;

/// <summary>
/// Pedidos. Un vendedor ve y opera solo los suyos; un administrador ve todos,
/// marca preparado/entregado y es el único que cancela pedidos que ya descontaron stock.
/// </summary>
[ApiController]
[Route("api/pedidos")]
[Authorize]
public class PedidosController(StockDbContext db, PedidoServicio servicio) : ControllerBase
{
    [HttpGet]
    public Task<Pagina<PedidoResumenDto>> Listar(EstadoPedido? estado, int? clienteId, string? texto,
        DateTime? desde, DateTime? hasta, int pagina = 1, int tamanio = 20, CancellationToken ct = default)
    {
        var q = db.Pedidos.AsNoTracking().AsQueryable();
        if (!User.EsAdministrador())
        {
            var yo = User.IdUsuario();
            q = q.Where(p => p.VendedorId == yo);
        }
        if (estado is not null) q = q.Where(p => p.Estado == estado);
        if (clienteId is not null) q = q.Where(p => p.ClienteId == clienteId);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = int.TryParse(t, out var numero)
                ? q.Where(p => p.Id == numero || p.Cliente!.RazonSocial.Contains(t))
                : q.Where(p => p.Cliente!.RazonSocial.Contains(t));
        }
        if (desde is not null) q = q.Where(p => p.FechaCreacion >= desde);
        if (hasta is not null) q = q.Where(p => p.FechaCreacion < hasta.Value.Date.AddDays(1));

        return q.OrderByDescending(p => p.Id).Select(Mapeos.APedidoResumenDto).PaginarAsync(pagina, tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<PedidoDto> Obtener(int id, CancellationToken ct) =>
        Mapeos.APedidoDto(await servicio.CargarAsync(id, User.IdUsuario(), User.EsAdministrador(), ct));

    /// <summary>Crea un pedido en borrador (todavía no toca el stock).</summary>
    [HttpPost]
    [ProducesResponseType<PedidoDto>(201)]
    public async Task<IActionResult> Crear(PedidoRequest req, CancellationToken ct)
    {
        var pedido = await servicio.CrearAsync(req, User.IdUsuario(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = pedido.Id }, await Obtener(pedido.Id, ct));
    }

    /// <summary>Reemplaza cliente, observaciones y renglones de un borrador.</summary>
    [HttpPut("{id:int}")]
    public async Task<PedidoDto> Modificar(int id, PedidoRequest req, CancellationToken ct)
    {
        await servicio.ModificarAsync(id, req, User.IdUsuario(), User.EsAdministrador(), ct);
        return await Obtener(id, ct);
    }

    /// <summary>Confirma el pedido y descuenta stock. 409 si no alcanza (con el detalle de faltantes).</summary>
    [HttpPost("{id:int}/confirmar")]
    [ProducesResponseType<PedidoDto>(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<PedidoDto> Confirmar(int id, CancellationToken ct)
    {
        await servicio.ConfirmarAsync(id, User.IdUsuario(), User.EsAdministrador(), ct);
        return await Obtener(id, ct);
    }

    [HttpPost("{id:int}/preparar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<PedidoDto> Preparar(int id, CancellationToken ct)
    {
        await servicio.MarcarPreparadoAsync(id, User.IdUsuario(), ct);
        return await Obtener(id, ct);
    }

    [HttpPost("{id:int}/entregar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<PedidoDto> Entregar(int id, CancellationToken ct)
    {
        await servicio.MarcarEntregadoAsync(id, User.IdUsuario(), ct);
        return await Obtener(id, ct);
    }

    /// <summary>Cancela el pedido. Si ya había descontado stock, lo devuelve.</summary>
    [HttpPost("{id:int}/cancelar")]
    public async Task<PedidoDto> Cancelar(int id, CancelarRequest req, CancellationToken ct)
    {
        await servicio.CancelarAsync(id, req.Motivo, User.IdUsuario(), User.EsAdministrador(), ct);
        return await Obtener(id, ct);
    }
}
