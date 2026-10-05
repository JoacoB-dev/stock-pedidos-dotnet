import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { Aviso, Cargando, EstadoBadge } from '../componentes/Comunes'
import { api, ErrorApi } from '../lib/api'
import { ACCION_ESTADO, formatoCuit, formatoFecha, formatoMoneda } from '../lib/formato'
import { useSesion } from '../lib/sesion'
import type { EstadoPedido, Pedido } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

const RUTA_ACCION: Partial<Record<EstadoPedido, string>> = {
  Confirmado: 'confirmar', Preparado: 'preparar', Entregado: 'entregar',
}

export function PedidoDetalle() {
  const { id } = useParams()
  const { esAdmin } = useSesion()
  const { datos: pedido, error, cargando, setDatos } = useCarga(() => api<Pedido>(`/pedidos/${id}`), [id])
  const [errorAccion, setErrorAccion] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [cancelando, setCancelando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [enviando, setEnviando] = useState(false)

  if (cargando && !pedido) return <Cargando />
  if (error) return <Aviso>{error}</Aviso>
  if (!pedido) return null

  // Qué botones ve cada rol (la API lo valida igual)
  const puede = (estado: EstadoPedido) => {
    if (estado === 'Preparado' || estado === 'Entregado') return esAdmin
    if (estado === 'Cancelado') return pedido.estado === 'Borrador' || esAdmin
    return true
  }

  async function ejecutar(estado: EstadoPedido, cuerpo?: unknown) {
    const ruta = estado === 'Cancelado' ? 'cancelar' : RUTA_ACCION[estado]
    setEnviando(true)
    setErrorAccion(null)
    setMensaje(null)
    try {
      const actualizado = await api<Pedido>(`/pedidos/${pedido!.id}/${ruta}`, { metodo: 'POST', cuerpo })
      setDatos(actualizado)
      setMensaje(`Pedido ${actualizado.estado.toLowerCase()}.`)
      setCancelando(false)
    } catch (err) {
      const e = err as ErrorApi
      setErrorAccion(e.faltantes?.length
        ? `${e.message} ${e.faltantes.map((f) => `${f.sku}: pedido ${f.pedido}, disponible ${f.disponible}`).join(' · ')}`
        : e.message)
    } finally {
      setEnviando(false)
    }
  }

  const historia: [string, string | null][] = [
    ['Creado', pedido.fechaCreacion], ['Confirmado', pedido.fechaConfirmacion],
    ['Preparado', pedido.fechaPreparacion], ['Entregado', pedido.fechaEntrega], ['Cancelado', pedido.fechaCancelacion],
  ]

  return (
    <>
      <div className="encabezado">
        <h1>Pedido #{pedido.id} <EstadoBadge estado={pedido.estado} /></h1>
        <Link to="/pedidos" className="btn">← Pedidos</Link>
      </div>

      <div className="dos-columnas">
        <section className="tarjeta">
          <h2>Datos</h2>
          <dl className="datos">
            <dt>Cliente</dt><dd>{pedido.cliente} <span className="tenue">({formatoCuit(pedido.clienteCuit)})</span></dd>
            <dt>Vendedor</dt><dd>{pedido.vendedor}</dd>
            <dt>Observaciones</dt><dd>{pedido.observaciones || '—'}</dd>
            {pedido.motivoCancelacion && <><dt>Motivo de cancelación</dt><dd>{pedido.motivoCancelacion}</dd></>}
          </dl>
        </section>
        <section className="tarjeta">
          <h2>Seguimiento</h2>
          <ol className="linea-tiempo">
            {historia.filter(([, f]) => f).map(([nombre, fecha]) => (
              <li key={nombre}><strong>{nombre}</strong> <span className="tenue">{formatoFecha(fecha, true)}</span></li>
            ))}
          </ol>
        </section>
      </div>

      <section className="tarjeta sin-padding">
        <table className="tabla">
          <thead><tr><th>SKU</th><th>Producto</th><th className="num">Cantidad</th><th className="num">Precio</th><th className="num">Subtotal</th>{pedido.estado === 'Borrador' && <th className="num">Stock actual</th>}</tr></thead>
          <tbody>
            {pedido.renglones.map((r) => (
              <tr key={r.productoId}>
                <td className="mono">{r.sku}</td><td>{r.producto}</td>
                <td className="num">{r.cantidad}</td>
                <td className="num">{formatoMoneda(r.precioUnitario)}</td>
                <td className="num">{formatoMoneda(r.subtotal)}</td>
                {pedido.estado === 'Borrador' && <td className={`num ${r.stockDisponible < r.cantidad ? 'texto-alerta' : ''}`}>{r.stockDisponible}</td>}
              </tr>
            ))}
          </tbody>
          <tfoot><tr><td colSpan={4} className="num"><strong>Total</strong></td><td className="num"><strong>{formatoMoneda(pedido.total)}</strong></td>{pedido.estado === 'Borrador' && <td></td>}</tr></tfoot>
        </table>
      </section>

      <Aviso>{errorAccion}</Aviso>
      <Aviso tipo="ok">{mensaje}</Aviso>

      {cancelando ? (
        <form className="tarjeta" onSubmit={(e) => { e.preventDefault(); ejecutar('Cancelado', { motivo }) }}>
          <label>Motivo de la cancelación
            <input value={motivo} onChange={(e) => setMotivo(e.target.value)} required maxLength={300} autoFocus />
          </label>
          {pedido.estado !== 'Borrador' && <p className="tenue">Se van a devolver al stock las unidades de este pedido.</p>}
          <div className="acciones">
            <button type="button" className="btn" onClick={() => setCancelando(false)}>Volver</button>
            <button className="btn btn-peligro" disabled={enviando || !motivo.trim()}>Confirmar cancelación</button>
          </div>
        </form>
      ) : (
        <div className="acciones">
          {pedido.estado === 'Borrador' && <Link className="btn" to={`/pedidos/${pedido.id}/editar`}>Editar</Link>}
          {pedido.estadosSiguientes.filter(puede).map((estado) => estado === 'Cancelado'
            ? <button key={estado} className="btn btn-peligro" onClick={() => setCancelando(true)}>{ACCION_ESTADO[estado]}</button>
            : <button key={estado} className="btn btn-primario" disabled={enviando} onClick={() => ejecutar(estado)}>{ACCION_ESTADO[estado]}</button>)}
        </div>
      )}
    </>
  )
}
