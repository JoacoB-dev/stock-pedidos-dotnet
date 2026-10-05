using Stock.Dominio.Entidades;

namespace Stock.Api.Dtos;

// ---------- Auth ----------
public record LoginRequest(string Email, string Clave);
public record LoginResponse(string Token, DateTime Expira, UsuarioDto Usuario);
public record UsuarioDto(int Id, string Email, string Nombre, Rol Rol);

// ---------- Categorías ----------
public record CategoriaDto(int Id, string Nombre, int CantidadProductos);
public record CategoriaRequest(string Nombre);

// ---------- Productos ----------
public record ProductoDto(
    int Id, string Sku, string Nombre, int CategoriaId, string Categoria, decimal Precio,
    int Stock, int StockMinimo, bool StockBajo, bool Activo,
    /// <summary>Token de concurrencia en base64: hay que devolverlo al editar.</summary>
    string RowVersion);

public record ProductoAltaRequest(string Sku, string Nombre, int CategoriaId, decimal Precio,
    int StockMinimo, int StockInicial);

public record ProductoEdicionRequest(string Nombre, int CategoriaId, decimal Precio, int StockMinimo,
    bool Activo, string RowVersion);

/// <summary>Movimiento manual: solo Ingreso o Ajuste (las ventas y cancelaciones los genera el sistema).</summary>
public record MovimientoRequest(TipoMovimiento Tipo, int Cantidad, string? Motivo);

public record MovimientoDto(long Id, DateTime Fecha, int ProductoId, string Sku, string Producto,
    TipoMovimiento Tipo, int Cantidad, int StockResultante, string Usuario, int? PedidoId, string? Motivo);

// ---------- Clientes ----------
public record ClienteDto(int Id, string RazonSocial, string Cuit, string? Email, string? Telefono,
    string? Direccion, bool Activo);

public record ClienteRequest(string RazonSocial, string Cuit, string? Email, string? Telefono,
    string? Direccion, bool Activo = true);

// ---------- Pedidos ----------
public record RenglonRequest(int ProductoId, int Cantidad);
public record PedidoRequest(int ClienteId, string? Observaciones, List<RenglonRequest> Renglones);
public record CancelarRequest(string Motivo);

public record RenglonDto(int ProductoId, string Sku, string Producto, int Cantidad, decimal PrecioUnitario,
    decimal Subtotal, int StockDisponible);

public record PedidoResumenDto(int Id, int ClienteId, string Cliente, string Vendedor, EstadoPedido Estado,
    DateTime FechaCreacion, DateTime? FechaConfirmacion, int CantidadRenglones, decimal Total);

public record PedidoDto(
    int Id, int ClienteId, string Cliente, string ClienteCuit, int VendedorId, string Vendedor,
    EstadoPedido Estado, string? Observaciones,
    DateTime FechaCreacion, DateTime? FechaConfirmacion, DateTime? FechaPreparacion,
    DateTime? FechaEntrega, DateTime? FechaCancelacion, string? MotivoCancelacion,
    decimal Total, List<RenglonDto> Renglones, List<EstadoPedido> EstadosSiguientes);

// ---------- Dashboard y reportes ----------
public record DashboardDto(
    List<ProductoDto> StockBajo,
    Dictionary<EstadoPedido, int> PedidosPorEstado,
    decimal VentasDelMes,
    int PedidosConfirmadosDelMes,
    List<PedidoResumenDto> UltimosPedidos);
