import { useState } from 'react'
import { formatoMoneda, totalRenglones } from '../lib/formato'
import type { Producto } from '../lib/tipos'

export interface RenglonEnEdicion {
  productoId: number
  sku: string
  nombre: string
  cantidad: number
  precioUnitario: number
  stock: number
}

/**
 * Tabla de renglones de un pedido: agregar productos, cambiar cantidades y quitar.
 * Avisa (sin bloquear) cuando la cantidad supera el stock actual: el control real
 * lo hace la API al confirmar.
 */
export function EditorRenglones({ productos, renglones, onCambiar }: {
  productos: Producto[]
  renglones: RenglonEnEdicion[]
  onCambiar: (r: RenglonEnEdicion[]) => void
}) {
  const [productoId, setProductoId] = useState('')
  const [cantidad, setCantidad] = useState(1)

  const disponibles = productos.filter((p) => !renglones.some((r) => r.productoId === p.id))

  function agregar() {
    const p = productos.find((x) => x.id === Number(productoId))
    if (!p || cantidad < 1) return
    onCambiar([...renglones, {
      productoId: p.id, sku: p.sku, nombre: p.nombre, cantidad, precioUnitario: p.precio, stock: p.stock,
    }])
    setProductoId('')
    setCantidad(1)
  }

  function cambiarCantidad(id: number, valor: number) {
    onCambiar(renglones.map((r) => (r.productoId === id ? { ...r, cantidad: valor } : r)))
  }

  return (
    <div>
      <div className="fila-agregar">
        <label>
          Producto
          <select aria-label="Producto a agregar" value={productoId} onChange={(e) => setProductoId(e.target.value)}>
            <option value="">Elegí un producto…</option>
            {disponibles.map((p) => (
              <option key={p.id} value={p.id}>
                {p.sku} · {p.nombre} — {formatoMoneda(p.precio)} (stock {p.stock})
              </option>
            ))}
          </select>
        </label>
        <label>
          Cantidad
          <input aria-label="Cantidad a agregar" type="number" min={1} value={cantidad}
            onChange={(e) => setCantidad(Number(e.target.value))} />
        </label>
        <button type="button" className="btn" onClick={agregar} disabled={!productoId || cantidad < 1}>Agregar</button>
      </div>

      <table className="tabla">
        <thead>
          <tr><th>SKU</th><th>Producto</th><th className="num">Cantidad</th><th className="num">Precio</th><th className="num">Subtotal</th><th></th></tr>
        </thead>
        <tbody>
          {renglones.length === 0 && (
            <tr><td colSpan={6} className="tenue centro">Todavía no agregaste productos.</td></tr>
          )}
          {renglones.map((r) => (
            <tr key={r.productoId}>
              <td className="mono">{r.sku}</td>
              <td>
                {r.nombre}
                {r.cantidad > r.stock && (
                  <div className="texto-alerta">Hay {r.stock} en stock: no se va a poder confirmar así.</div>
                )}
              </td>
              <td className="num">
                <input type="number" min={1} className="input-cantidad" aria-label={`Cantidad de ${r.sku}`}
                  value={r.cantidad} onChange={(e) => cambiarCantidad(r.productoId, Number(e.target.value))} />
              </td>
              <td className="num">{formatoMoneda(r.precioUnitario)}</td>
              <td className="num">{formatoMoneda(r.cantidad * r.precioUnitario)}</td>
              <td>
                <button type="button" className="btn btn-chico btn-peligro" aria-label={`Quitar ${r.sku}`}
                  onClick={() => onCambiar(renglones.filter((x) => x.productoId !== r.productoId))}>Quitar</button>
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={4} className="num"><strong>Total</strong></td>
            <td className="num" data-testid="total"><strong>{formatoMoneda(totalRenglones(renglones))}</strong></td>
            <td></td>
          </tr>
        </tfoot>
      </table>
    </div>
  )
}
