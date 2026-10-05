using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Api.Servicios;
using Stock.Datos;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController(StockDbContext db, ProductoServicio servicio) : ControllerBase
{
    /// <summary>Listado con filtros y paginado.</summary>
    /// <param name="texto">Busca en SKU y nombre.</param>
    /// <param name="categoriaId">Filtra por categoría.</param>
    /// <param name="soloStockBajo">Solo productos con stock menor o igual al mínimo.</param>
    /// <param name="incluirInactivos">Por defecto solo trae los activos.</param>
    /// <param name="orden">nombre | sku | stock | precio</param>
    [HttpGet]
    public Task<Pagina<ProductoDto>> Listar(string? texto, int? categoriaId, bool soloStockBajo = false,
        bool incluirInactivos = false, string orden = "nombre", int pagina = 1, int tamanio = 20,
        CancellationToken ct = default)
    {
        var q = db.Productos.AsNoTracking().AsQueryable();
        if (!incluirInactivos) q = q.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(p => p.Nombre.Contains(t) || p.Sku.Contains(t));
        }
        if (categoriaId is not null) q = q.Where(p => p.CategoriaId == categoriaId);
        if (soloStockBajo) q = q.Where(p => p.Stock <= p.StockMinimo);

        q = orden switch
        {
            "sku" => q.OrderBy(p => p.Sku),
            "stock" => q.OrderBy(p => p.Stock).ThenBy(p => p.Nombre),
            "precio" => q.OrderBy(p => p.Precio).ThenBy(p => p.Nombre),
            _ => q.OrderBy(p => p.Nombre)
        };
        return q.Select(Mapeos.AProductoDto).PaginarAsync(pagina, tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ProductoDto> Obtener(int id, CancellationToken ct) =>
        await db.Productos.AsNoTracking().Where(p => p.Id == id).Select(Mapeos.AProductoDto).FirstOrDefaultAsync(ct)
        ?? throw new NoEncontradoException($"No existe el producto {id}.");

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    [ProducesResponseType<ProductoDto>(201)]
    public async Task<IActionResult> Crear(ProductoAltaRequest req, CancellationToken ct)
    {
        var producto = await servicio.CrearAsync(req, User.IdUsuario(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = producto.Id }, await Obtener(producto.Id, ct));
    }

    /// <summary>Edita nombre, categoría, precio, mínimo y estado. El stock se cambia con movimientos.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    [ProducesResponseType<ProductoDto>(200)]
    [ProducesResponseType(409)]
    public async Task<ProductoDto> Editar(int id, ProductoEdicionRequest req, CancellationToken ct)
    {
        await servicio.EditarAsync(id, req, ct);
        return await Obtener(id, ct);
    }

    /// <summary>Registra un ingreso de mercadería o un ajuste de inventario.</summary>
    [HttpPost("{id:int}/movimientos")]
    [Authorize(Roles = Roles.Administrador)]
    [ProducesResponseType<ProductoDto>(200)]
    public async Task<ProductoDto> RegistrarMovimiento(int id, MovimientoRequest req, CancellationToken ct)
    {
        await servicio.RegistrarMovimientoAsync(id, req, User.IdUsuario(), ct);
        return await Obtener(id, ct);
    }
}
