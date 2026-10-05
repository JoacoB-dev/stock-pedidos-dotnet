using System.Linq.Expressions;
using Stock.Dominio.Entidades;

namespace Stock.Api.Dtos;

/// <summary>Proyecciones a DTO. Las que son Expression las traduce EF a SQL (solo trae las columnas necesarias).</summary>
public static class Mapeos
{
    public static readonly Expression<Func<Producto, ProductoDto>> AProductoDto = p => new ProductoDto(
        p.Id, p.Sku, p.Nombre, p.CategoriaId, p.Categoria!.Nombre, p.Precio, p.Stock, p.StockMinimo,
        p.Stock <= p.StockMinimo, p.Activo, Convert.ToBase64String(p.RowVersion));

    public static readonly Expression<Func<Cliente, ClienteDto>> AClienteDto = c => new ClienteDto(
        c.Id, c.RazonSocial, c.Cuit, c.Email, c.Telefono, c.Direccion, c.Activo);

    public static readonly Expression<Func<Pedido, PedidoResumenDto>> APedidoResumenDto = p => new PedidoResumenDto(
        p.Id, p.ClienteId, p.Cliente!.RazonSocial, p.Vendedor!.Nombre, p.Estado, p.FechaCreacion,
        p.FechaConfirmacion, p.Renglones.Count, p.Renglones.Sum(r => r.Cantidad * r.PrecioUnitario));

    public static readonly Expression<Func<MovimientoStock, MovimientoDto>> AMovimientoDto = m => new MovimientoDto(
        m.Id, m.Fecha, m.ProductoId, m.Producto!.Sku, m.Producto.Nombre, m.Tipo, m.Cantidad,
        m.StockResultante, m.Usuario!.Nombre, m.PedidoId, m.Motivo);

    /// <summary>Requiere el pedido con Cliente, Vendedor y Renglones.Producto cargados.</summary>
    public static PedidoDto APedidoDto(Pedido p) => new(
        p.Id, p.ClienteId, p.Cliente!.RazonSocial, p.Cliente.Cuit, p.VendedorId, p.Vendedor!.Nombre,
        p.Estado, p.Observaciones, p.FechaCreacion, p.FechaConfirmacion, p.FechaPreparacion,
        p.FechaEntrega, p.FechaCancelacion, p.MotivoCancelacion, p.Total,
        p.Renglones.OrderBy(r => r.Id).Select(r => new RenglonDto(r.ProductoId, r.Producto!.Sku, r.Producto.Nombre,
            r.Cantidad, r.PrecioUnitario, r.Subtotal, r.Producto.Stock)).ToList(),
        p.EstadosSiguientes.ToList());
}
