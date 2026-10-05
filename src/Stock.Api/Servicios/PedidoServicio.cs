using System.Data;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Datos;
using Stock.Dominio.Entidades;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Servicios;

/// <summary>
/// Casos de uso de pedidos. Las reglas (qué transición vale, cuánto stock hay) están en el
/// dominio; acá se cargan los datos, se guarda en una transacción y se maneja la concurrencia.
/// </summary>
public class PedidoServicio(StockDbContext db, TimeProvider reloj, ILogger<PedidoServicio> log)
{
    /// <summary>Cuántas veces se reintenta una operación si otro usuario tocó el mismo stock.</summary>
    public const int ReintentosPorConcurrencia = 3;

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    public async Task<Pedido> CrearAsync(PedidoRequest req, int vendedorId, CancellationToken ct)
    {
        await ValidarClienteAsync(req.ClienteId, ct);
        var pedido = new Pedido
        {
            ClienteId = req.ClienteId,
            VendedorId = vendedorId,
            Observaciones = req.Observaciones?.Trim(),
            FechaCreacion = Ahora
        };
        pedido.CambiarRenglones(await ArmarRenglonesAsync(req.Renglones, ct));
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
        return pedido;
    }

    public async Task ModificarAsync(int id, PedidoRequest req, int usuarioId, bool esAdmin, CancellationToken ct)
    {
        var pedido = await CargarAsync(id, usuarioId, esAdmin, ct);
        await ValidarClienteAsync(req.ClienteId, ct);
        pedido.ClienteId = req.ClienteId;
        pedido.Observaciones = req.Observaciones?.Trim();
        pedido.CambiarRenglones(await ArmarRenglonesAsync(req.Renglones, ct));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Confirma el pedido y descuenta el stock de forma atómica.
    /// Cómo se evita vender dos veces la misma unidad: Producto y Pedido tienen una columna
    /// rowversion. El UPDATE que genera EF incluye "WHERE RowVersion = @valorLeido"; si otra
    /// operación cambió la fila entre medio, no se actualiza nada, EF lanza
    /// DbUpdateConcurrencyException, se deshace la transacción entera y se reintenta leyendo
    /// los valores nuevos. En el reintento puede pasar que ya no alcance el stock (409) o que
    /// el pedido ya esté confirmado (422).
    /// </summary>
    public Task ConfirmarAsync(int id, int usuarioId, bool esAdmin, CancellationToken ct) =>
        ConReintentosAsync(id, usuarioId, esAdmin, ct, pedido =>
        {
            db.Movimientos.AddRange(pedido.Confirmar(usuarioId, Ahora));
        });

    public Task CancelarAsync(int id, string motivo, int usuarioId, bool esAdmin, CancellationToken ct) =>
        ConReintentosAsync(id, usuarioId, esAdmin, ct, pedido =>
        {
            // Un vendedor puede descartar un borrador; un pedido que ya movió stock lo cancela un administrador
            if (pedido.Estado != EstadoPedido.Borrador && !esAdmin)
                throw new ReglaDeNegocioException("Solo un administrador puede cancelar un pedido ya confirmado.");
            db.Movimientos.AddRange(pedido.Cancelar(motivo, usuarioId, Ahora));
        });

    public Task MarcarPreparadoAsync(int id, int usuarioId, CancellationToken ct) =>
        ConReintentosAsync(id, usuarioId, true, ct, pedido => pedido.MarcarPreparado(Ahora));

    public Task MarcarEntregadoAsync(int id, int usuarioId, CancellationToken ct) =>
        ConReintentosAsync(id, usuarioId, true, ct, pedido => pedido.MarcarEntregado(Ahora));

    /// <summary>
    /// Carga el pedido, aplica la operación y guarda todo en una transacción.
    /// Si hubo un conflicto de concurrencia, descarta lo hecho y vuelve a empezar desde la lectura.
    /// </summary>
    private async Task ConReintentosAsync(int id, int usuarioId, bool esAdmin, CancellationToken ct,
        Action<Pedido> operacion)
    {
        for (var intento = 1; ; intento++)
        {
            // Sin esto, EF devolvería las entidades viejas que ya tiene en memoria
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                var pedido = await CargarAsync(id, usuarioId, esAdmin, ct);
                operacion(pedido);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (EsReintentable(ex) && intento < ReintentosPorConcurrencia)
            {
                await tx.RollbackAsync(ct);
                log.LogInformation("Conflicto de concurrencia en el pedido {Id}, reintento {Intento}", id, intento);
            }
            catch (DbUpdateException ex) when (EsReintentable(ex))
            {
                throw new ConflictoDeConcurrenciaException(
                    "Hay mucha actividad sobre estos productos; volvé a intentar en unos segundos.");
            }
        }
    }

    /// <summary>
    /// Conflicto de rowversion, o deadlock de SQL Server (error 1205): en los dos casos la
    /// transacción se deshizo entera y se puede volver a intentar sin riesgo.
    /// </summary>
    internal static bool EsReintentable(DbUpdateException ex) =>
        ex is DbUpdateConcurrencyException ||
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 1205 };

    /// <summary>Trae el pedido con todo lo necesario. Un vendedor solo ve sus propios pedidos.</summary>
    public async Task<Pedido> CargarAsync(int id, int usuarioId, bool esAdmin, CancellationToken ct)
    {
        var pedido = await db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Vendedor)
            .Include(p => p.Renglones).ThenInclude(r => r.Producto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (pedido is null || (!esAdmin && pedido.VendedorId != usuarioId))
            throw new NoEncontradoException($"No existe el pedido {id}.");
        return pedido;
    }

    private async Task ValidarClienteAsync(int clienteId, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteId, ct)
            ?? throw new ReglaDeNegocioException($"No existe el cliente {clienteId}.");
        if (!cliente.Activo)
            throw new ReglaDeNegocioException($"El cliente {cliente.RazonSocial} está dado de baja.");
    }

    /// <summary>Arma los renglones tomando el precio de lista actual de cada producto.</summary>
    private async Task<List<RenglonPedido>> ArmarRenglonesAsync(List<RenglonRequest> pedidos, CancellationToken ct)
    {
        var ids = pedidos.Select(r => r.ProductoId).Distinct().ToList();
        var productos = await db.Productos.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var inexistentes = ids.Where(i => !productos.ContainsKey(i)).ToList();
        if (inexistentes.Count > 0)
            throw new ReglaDeNegocioException($"No existen los productos: {string.Join(", ", inexistentes)}.");
        var inactivos = productos.Values.Where(p => !p.Activo).Select(p => p.Sku).ToList();
        if (inactivos.Count > 0)
            throw new ReglaDeNegocioException($"Productos dados de baja: {string.Join(", ", inactivos)}.");

        return pedidos.Select(r => new RenglonPedido
        {
            ProductoId = r.ProductoId,
            Producto = productos[r.ProductoId],
            Cantidad = r.Cantidad,
            PrecioUnitario = productos[r.ProductoId].Precio
        }).ToList();
    }
}
