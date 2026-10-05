// Tipos que devuelve la API (espejo de Stock.Api/Dtos)

export type Rol = 'Vendedor' | 'Administrador'
export type EstadoPedido = 'Borrador' | 'Confirmado' | 'Preparado' | 'Entregado' | 'Cancelado'
export type TipoMovimiento = 'Ingreso' | 'VentaConfirmada' | 'CancelacionPedido' | 'Ajuste'

export interface Usuario { id: number; email: string; nombre: string; rol: Rol }
export interface LoginResponse { token: string; expira: string; usuario: Usuario }

export interface Pagina<T> { items: T[]; total: number; numeroPagina: number; tamanio: number; totalPaginas: number }

export interface Categoria { id: number; nombre: string; cantidadProductos: number }

export interface Producto {
  id: number; sku: string; nombre: string; categoriaId: number; categoria: string
  precio: number; stock: number; stockMinimo: number; stockBajo: boolean; activo: boolean; rowVersion: string
}

export interface Cliente {
  id: number; razonSocial: string; cuit: string; email: string | null
  telefono: string | null; direccion: string | null; activo: boolean
}

export interface Renglon {
  productoId: number; sku: string; producto: string; cantidad: number
  precioUnitario: number; subtotal: number; stockDisponible: number
}

export interface PedidoResumen {
  id: number; clienteId: number; cliente: string; vendedor: string; estado: EstadoPedido
  fechaCreacion: string; fechaConfirmacion: string | null; cantidadRenglones: number; total: number
}

export interface Pedido {
  id: number; clienteId: number; cliente: string; clienteCuit: string; vendedorId: number; vendedor: string
  estado: EstadoPedido; observaciones: string | null
  fechaCreacion: string; fechaConfirmacion: string | null; fechaPreparacion: string | null
  fechaEntrega: string | null; fechaCancelacion: string | null; motivoCancelacion: string | null
  total: number; renglones: Renglon[]; estadosSiguientes: EstadoPedido[]
}

export interface RenglonRequest { productoId: number; cantidad: number }
export interface PedidoRequest { clienteId: number; observaciones: string | null; renglones: RenglonRequest[] }

export interface Movimiento {
  id: number; fecha: string; productoId: number; sku: string; producto: string; tipo: TipoMovimiento
  cantidad: number; stockResultante: number; usuario: string; pedidoId: number | null; motivo: string | null
}

export interface Dashboard {
  stockBajo: Producto[]
  pedidosPorEstado: Record<EstadoPedido, number>
  ventasDelMes: number
  pedidosConfirmadosDelMes: number
  ultimosPedidos: PedidoResumen[]
}

export interface VentaMesProducto {
  anio: number; mes: number; productoId: number; sku: string; producto: string
  categoria: string; unidades: number; importe: number; pedidos: number
}

export interface ResumenVentasMes {
  anio: number; mes: number; pedidos: number; unidades: number; importe: number; ticketPromedio: number
}

export interface Faltante { productoId: number; sku: string; pedido: number; disponible: number }
