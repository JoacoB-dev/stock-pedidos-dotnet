import { useState } from 'react'
import { Link } from 'react-router'
import { Aviso, Cargando, Paginador } from '../componentes/Comunes'
import { api, query } from '../lib/api'
import { formatoFecha, TEXTO_TIPO_MOVIMIENTO } from '../lib/formato'
import type { Movimiento, Pagina, Producto, TipoMovimiento } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

/** Auditoría: cada cambio de stock con usuario, fecha, motivo y stock resultante. */
export function Movimientos() {
  const [productoId, setProductoId] = useState('')
  const [tipo, setTipo] = useState('')
  const [pagina, setPagina] = useState(1)
  const { datos: productos } = useCarga(() => api<Pagina<Producto>>('/productos?tamanio=100&incluirInactivos=true'), [])
  const { datos, error, cargando } = useCarga(
    () => api<Pagina<Movimiento>>(`/movimientos${query({ productoId, tipo, pagina, tamanio: 20 })}`), [productoId, tipo, pagina])

  return (
    <>
      <div className="encabezado"><h1>Movimientos de stock</h1></div>
      <div className="filtros">
        <select value={productoId} onChange={(e) => { setPagina(1); setProductoId(e.target.value) }} aria-label="Producto">
          <option value="">Todos los productos</option>
          {productos?.items.map((p) => <option key={p.id} value={p.id}>{p.sku} · {p.nombre}</option>)}
        </select>
        <select value={tipo} onChange={(e) => { setPagina(1); setTipo(e.target.value) }} aria-label="Tipo">
          <option value="">Todos los tipos</option>
          {(Object.keys(TEXTO_TIPO_MOVIMIENTO) as TipoMovimiento[]).map((t) => <option key={t} value={t}>{TEXTO_TIPO_MOVIMIENTO[t]}</option>)}
        </select>
      </div>
      <Aviso>{error}</Aviso>
      {cargando && !datos ? <Cargando /> : datos && (
        <section className="tarjeta sin-padding">
          <table className="tabla">
            <thead><tr><th>Fecha</th><th>Producto</th><th>Tipo</th><th className="num">Cantidad</th><th className="num">Stock resultante</th><th>Usuario</th><th>Motivo</th></tr></thead>
            <tbody>
              {datos.items.map((m) => (
                <tr key={m.id}>
                  <td className="nowrap">{formatoFecha(m.fecha, true)}</td>
                  <td><span className="mono">{m.sku}</span> {m.producto}</td>
                  <td>{TEXTO_TIPO_MOVIMIENTO[m.tipo]}</td>
                  <td className={`num ${m.cantidad < 0 ? 'texto-alerta' : 'texto-ok'}`}>{m.cantidad > 0 ? `+${m.cantidad}` : m.cantidad}</td>
                  <td className="num">{m.stockResultante}</td>
                  <td>{m.usuario}</td>
                  <td>{m.pedidoId ? <Link to={`/pedidos/${m.pedidoId}`}>{m.motivo}</Link> : (m.motivo ?? '—')}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Paginador pagina={datos.numeroPagina} totalPaginas={datos.totalPaginas} total={datos.total} onCambiar={setPagina} />
        </section>
      )}
    </>
  )
}
