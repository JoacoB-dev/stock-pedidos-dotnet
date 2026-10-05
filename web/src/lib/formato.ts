import type { EstadoPedido, TipoMovimiento } from './tipos'

const moneda = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 2 })
const numero = new Intl.NumberFormat('es-AR')

export const formatoMoneda = (valor: number) => moneda.format(valor)
export const formatoNumero = (valor: number) => numero.format(valor)

export function formatoFecha(iso: string | null | undefined, conHora = false): string {
  if (!iso) return '—'
  const fecha = new Date(iso)
  return conHora
    ? fecha.toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })
    : fecha.toLocaleDateString('es-AR')
}

export const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto',
  'Septiembre', 'Octubre', 'Noviembre', 'Diciembre']

export const nombreMes = (mes: number) => MESES[mes - 1] ?? String(mes)

export const ESTADOS: EstadoPedido[] = ['Borrador', 'Confirmado', 'Preparado', 'Entregado', 'Cancelado']

export const TEXTO_TIPO_MOVIMIENTO: Record<TipoMovimiento, string> = {
  Ingreso: 'Ingreso',
  VentaConfirmada: 'Venta',
  CancelacionPedido: 'Devolución por cancelación',
  Ajuste: 'Ajuste',
}

/** Texto del botón para pasar a cada estado. */
export const ACCION_ESTADO: Record<EstadoPedido, string> = {
  Borrador: 'Volver a borrador',
  Confirmado: 'Confirmar',
  Preparado: 'Marcar preparado',
  Entregado: 'Marcar entregado',
  Cancelado: 'Cancelar pedido',
}

/** CUIT con guiones: 30712345671 → 30-71234567-1 */
export function formatoCuit(cuit: string): string {
  return cuit.length === 11 ? `${cuit.slice(0, 2)}-${cuit.slice(2, 10)}-${cuit.slice(10)}` : cuit
}

/** Suma de los renglones de un pedido en edición. */
export function totalRenglones(renglones: { cantidad: number; precioUnitario: number }[]): number {
  return renglones.reduce((total, r) => total + r.cantidad * r.precioUnitario, 0)
}
