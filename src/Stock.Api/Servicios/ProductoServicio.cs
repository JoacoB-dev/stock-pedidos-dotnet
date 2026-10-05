using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Datos;
using Stock.Dominio.Entidades;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Servicios;

public class ProductoServicio(StockDbContext db, TimeProvider reloj)
{
    public async Task<Producto> CrearAsync(ProductoAltaRequest req, int usuarioId, CancellationToken ct)
    {
        var sku = req.Sku.Trim().ToUpperInvariant();
        if (await db.Productos.AnyAsync(p => p.Sku == sku, ct))
            throw new ReglaDeNegocioException($"Ya existe un producto con SKU {sku}.");
        await ValidarCategoriaAsync(req.CategoriaId, ct);

        var producto = new Producto(sku, req.Nombre.Trim(), req.CategoriaId, req.Precio, req.StockMinimo);
        db.Productos.Add(producto);
        // El stock inicial también queda como movimiento, así la auditoría cierra desde el primer día
        if (req.StockInicial > 0)
            db.Movimientos.Add(producto.AplicarMovimiento(TipoMovimiento.Ingreso, req.StockInicial, usuarioId,
                reloj.GetUtcNow().UtcDateTime, motivo: "Stock inicial"));
        await db.SaveChangesAsync(ct);
        return producto;
    }

    /// <summary>
    /// Edita los datos del producto (no el stock). Usa el RowVersion que mandó el cliente:
    /// si alguien lo cambió desde que se cargó el formulario, responde 409 en vez de pisar.
    /// </summary>
    public async Task EditarAsync(int id, ProductoEdicionRequest req, CancellationToken ct)
    {
        var producto = await db.Productos.FindAsync([id], ct)
            ?? throw new NoEncontradoException($"No existe el producto {id}.");
        await ValidarCategoriaAsync(req.CategoriaId, ct);

        byte[] rowVersion;
        try { rowVersion = Convert.FromBase64String(req.RowVersion); }
        catch (FormatException) { throw new ReglaDeNegocioException("El RowVersion no es válido."); }

        // Le decimos a EF cuál era la versión "original" que vio el usuario
        db.Entry(producto).Property(p => p.RowVersion).OriginalValue = rowVersion;
        producto.Nombre = req.Nombre.Trim();
        producto.CategoriaId = req.CategoriaId;
        producto.Precio = req.Precio;
        producto.StockMinimo = req.StockMinimo;
        producto.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Registra un ingreso o ajuste manual. Reintenta si una venta tocó el mismo producto.</summary>
    public async Task<MovimientoStock> RegistrarMovimientoAsync(int productoId, MovimientoRequest req, int usuarioId,
        CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            db.ChangeTracker.Clear();
            var producto = await db.Productos.FindAsync([productoId], ct)
                ?? throw new NoEncontradoException($"No existe el producto {productoId}.");
            var movimiento = producto.AplicarMovimiento(req.Tipo, req.Cantidad, usuarioId,
                reloj.GetUtcNow().UtcDateTime, motivo: req.Motivo?.Trim());
            db.Movimientos.Add(movimiento);
            try
            {
                // Un solo SaveChanges: el UPDATE del producto y el INSERT del movimiento van en la misma transacción
                await db.SaveChangesAsync(ct);
                return movimiento;
            }
            catch (DbUpdateException ex) when (PedidoServicio.EsReintentable(ex) && intento < PedidoServicio.ReintentosPorConcurrencia) { }
        }
    }

    private async Task ValidarCategoriaAsync(int categoriaId, CancellationToken ct)
    {
        if (!await db.Categorias.AnyAsync(c => c.Id == categoriaId, ct))
            throw new ReglaDeNegocioException($"No existe la categoría {categoriaId}.");
    }
}
