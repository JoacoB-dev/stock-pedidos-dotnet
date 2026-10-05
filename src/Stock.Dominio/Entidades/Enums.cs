namespace Stock.Dominio.Entidades;

public enum Rol
{
    Vendedor = 1,
    Administrador = 2
}

/// <summary>Ciclo de vida: Borrador → Confirmado → Preparado → Entregado, o Cancelado.</summary>
public enum EstadoPedido
{
    Borrador = 1,
    Confirmado = 2,
    Preparado = 3,
    Entregado = 4,
    Cancelado = 5
}

public enum TipoMovimiento
{
    /// <summary>Entrada de mercadería (compra a proveedor).</summary>
    Ingreso = 1,
    /// <summary>Salida por confirmar un pedido.</summary>
    VentaConfirmada = 2,
    /// <summary>Devolución al stock por cancelar un pedido confirmado.</summary>
    CancelacionPedido = 3,
    /// <summary>Corrección manual (inventario físico, rotura, etc.). Puede ser positiva o negativa.</summary>
    Ajuste = 4
}
