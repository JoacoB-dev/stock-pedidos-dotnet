import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { Aviso, Cargando } from '../componentes/Comunes'
import { EditorRenglones, type RenglonEnEdicion } from '../componentes/EditorRenglones'
import { api, ErrorApi } from '../lib/api'
import { formatoCuit } from '../lib/formato'
import type { Cliente, Pagina, Pedido, PedidoRequest, Producto } from '../lib/tipos'

/** Alta de un pedido nuevo o edición de un borrador. */
export function PedidoForm() {
  const { id } = useParams()
  const editando = id !== undefined
  const navegar = useNavigate()

  const [clientes, setClientes] = useState<Cliente[]>([])
  const [productos, setProductos] = useState<Producto[]>([])
  const [clienteId, setClienteId] = useState('')
  const [observaciones, setObservaciones] = useState('')
  const [renglones, setRenglones] = useState<RenglonEnEdicion[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    Promise.all([
      api<Pagina<Cliente>>('/clientes?tamanio=100'),
      api<Pagina<Producto>>('/productos?tamanio=100'),
      editando ? api<Pedido>(`/pedidos/${id}`) : Promise.resolve(null),
    ]).then(([c, p, pedido]) => {
      setClientes(c.items)
      setProductos(p.items)
      if (pedido) {
        setClienteId(String(pedido.clienteId))
        setObservaciones(pedido.observaciones ?? '')
        setRenglones(pedido.renglones.map((r) => ({
          productoId: r.productoId, sku: r.sku, nombre: r.producto, cantidad: r.cantidad,
          precioUnitario: r.precioUnitario, stock: r.stockDisponible,
        })))
      }
    }).catch((e: Error) => setError(e.message)).finally(() => setCargando(false))
  }, [id, editando])

  async function guardar(e: FormEvent, confirmar: boolean) {
    e.preventDefault()
    setEnviando(true)
    setError(null)
    const cuerpo: PedidoRequest = {
      clienteId: Number(clienteId),
      observaciones: observaciones.trim() || null,
      renglones: renglones.map((r) => ({ productoId: r.productoId, cantidad: r.cantidad })),
    }
    let pedidoId = Number(id)
    try {
      const pedido = editando
        ? await api<Pedido>(`/pedidos/${id}`, { metodo: 'PUT', cuerpo })
        : await api<Pedido>('/pedidos', { metodo: 'POST', cuerpo })
      pedidoId = pedido.id
      if (confirmar) await api<Pedido>(`/pedidos/${pedido.id}/confirmar`, { metodo: 'POST' })
      navegar(`/pedidos/${pedido.id}`)
    } catch (err) {
      const errApi = err as ErrorApi
      if (errApi.faltantes?.length) {
        setError(`${errApi.message} Faltan: ${errApi.faltantes.map((f) => `${f.sku} (pedido ${f.pedido}, hay ${f.disponible})`).join(', ')}. El pedido quedó guardado como borrador.`)
        if (!editando && pedidoId) navegar(`/pedidos/${pedidoId}/editar`, { replace: true })
      } else {
        setError(errApi.message)
      }
    } finally {
      setEnviando(false)
    }
  }

  if (cargando) return <Cargando />

  return (
    <form onSubmit={(e) => guardar(e, false)}>
      <div className="encabezado"><h1>{editando ? `Editar pedido #${id}` : 'Nuevo pedido'}</h1></div>
      <section className="tarjeta">
        <div className="grilla-form">
          <label>
            Cliente
            <select value={clienteId} onChange={(e) => setClienteId(e.target.value)} required>
              <option value="">Elegí un cliente…</option>
              {clientes.map((c) => <option key={c.id} value={c.id}>{c.razonSocial} — {formatoCuit(c.cuit)}</option>)}
            </select>
          </label>
          <label>
            Observaciones
            <input value={observaciones} maxLength={500} onChange={(e) => setObservaciones(e.target.value)}
              placeholder="Ej.: entregar por la mañana" />
          </label>
        </div>
      </section>

      <section className="tarjeta">
        <h2>Renglones</h2>
        <EditorRenglones productos={productos} renglones={renglones} onCambiar={setRenglones} />
      </section>

      <Aviso>{error}</Aviso>
      <div className="acciones">
        <button type="button" className="btn" onClick={() => navegar(-1)}>Volver</button>
        <button className="btn" disabled={enviando || !clienteId || renglones.length === 0}>Guardar borrador</button>
        <button type="button" className="btn btn-primario" disabled={enviando || !clienteId || renglones.length === 0}
          onClick={(e) => guardar(e, true)}>Guardar y confirmar</button>
      </div>
    </form>
  )
}
