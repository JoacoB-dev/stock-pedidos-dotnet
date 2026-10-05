using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Dominio.Entidades;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController(StockDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<CategoriaDto>> Listar(CancellationToken ct) =>
        db.Categorias.OrderBy(c => c.Nombre)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Productos.Count))
            .ToListAsync(ct);

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    [ProducesResponseType<CategoriaDto>(201)]
    public async Task<IActionResult> Crear(CategoriaRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre, ct))
            throw new ReglaDeNegocioException($"Ya existe la categoría {nombre}.");
        var categoria = new Categoria { Nombre = nombre };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(ct);
        return Created($"/api/categorias/{categoria.Id}", new CategoriaDto(categoria.Id, categoria.Nombre, 0));
    }
}
