using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Dominio;
using Stock.Dominio.Entidades;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesController(StockDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<Pagina<ClienteDto>> Listar(string? texto, bool incluirInactivos = false, int pagina = 1,
        int tamanio = 20, CancellationToken ct = default)
    {
        var q = db.Clientes.AsNoTracking().AsQueryable();
        if (!incluirInactivos) q = q.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            var soloDigitos = Cuit.Normalizar(t);
            q = q.Where(c => c.RazonSocial.Contains(t) || (soloDigitos != "" && c.Cuit.Contains(soloDigitos)));
        }
        return q.OrderBy(c => c.RazonSocial).Select(Mapeos.AClienteDto).PaginarAsync(pagina, tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ClienteDto> Obtener(int id, CancellationToken ct) =>
        await db.Clientes.AsNoTracking().Where(c => c.Id == id).Select(Mapeos.AClienteDto).FirstOrDefaultAsync(ct)
        ?? throw new NoEncontradoException($"No existe el cliente {id}.");

    [HttpPost]
    [ProducesResponseType<ClienteDto>(201)]
    public async Task<IActionResult> Crear(ClienteRequest req, CancellationToken ct)
    {
        var cliente = new Cliente();
        await Aplicar(cliente, req, ct);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = cliente.Id }, await Obtener(cliente.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ClienteDto> Editar(int id, ClienteRequest req, CancellationToken ct)
    {
        var cliente = await db.Clientes.FindAsync([id], ct) ?? throw new NoEncontradoException($"No existe el cliente {id}.");
        await Aplicar(cliente, req, ct);
        await db.SaveChangesAsync(ct);
        return await Obtener(id, ct);
    }

    private async Task Aplicar(Cliente cliente, ClienteRequest req, CancellationToken ct)
    {
        var cuit = Cuit.Normalizar(req.Cuit);
        if (await db.Clientes.AnyAsync(c => c.Cuit == cuit && c.Id != cliente.Id, ct))
            throw new ReglaDeNegocioException($"Ya hay un cliente con CUIT {cuit}.");
        cliente.RazonSocial = req.RazonSocial.Trim();
        cliente.Cuit = cuit;
        cliente.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        cliente.Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        cliente.Direccion = string.IsNullOrWhiteSpace(req.Direccion) ? null : req.Direccion.Trim();
        cliente.Activo = req.Activo;
    }
}
