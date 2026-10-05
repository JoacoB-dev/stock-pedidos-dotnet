import { useState, type FormEvent } from 'react'
import { Aviso, Cargando, Paginador } from '../componentes/Comunes'
import { api, query } from '../lib/api'
import { formatoCuit } from '../lib/formato'
import type { Cliente, Pagina } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

export function Clientes() {
  const [texto, setTexto] = useState('')
  const [pagina, setPagina] = useState(1)
  const { datos, error, cargando, recargar } = useCarga(
    () => api<Pagina<Cliente>>(`/clientes${query({ texto, pagina, tamanio: 15, incluirInactivos: true })}`), [texto, pagina])
  const [editando, setEditando] = useState<Cliente | 'nuevo' | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)

  return (
    <>
      <div className="encabezado">
        <h1>Clientes</h1>
        <button className="btn btn-primario" onClick={() => { setMensaje(null); setEditando('nuevo') }}>+ Nuevo cliente</button>
      </div>
      <div className="filtros">
        <input placeholder="Razón social o CUIT" aria-label="Buscar"
          onKeyDown={(e) => { if (e.key === 'Enter') { setPagina(1); setTexto(e.currentTarget.value) } }}
          onBlur={(e) => { setPagina(1); setTexto(e.target.value) }} />
      </div>
      <Aviso tipo="ok">{mensaje}</Aviso>
      {editando && (
        <section className="tarjeta">
          <FormCliente cliente={editando === 'nuevo' ? null : editando} onCerrar={() => setEditando(null)}
            onListo={(m) => { setEditando(null); setMensaje(m); recargar() }} />
        </section>
      )}
      <Aviso>{error}</Aviso>
      {cargando && !datos ? <Cargando /> : datos && (
        <section className="tarjeta sin-padding">
          <table className="tabla">
            <thead><tr><th>Razón social</th><th>CUIT</th><th>Email</th><th>Teléfono</th><th>Dirección</th><th></th></tr></thead>
            <tbody>
              {datos.items.map((c) => (
                <tr key={c.id} className={c.activo ? '' : 'inactivo'}>
                  <td>{c.razonSocial}{!c.activo && <span className="badge">Inactivo</span>}</td>
                  <td className="mono">{formatoCuit(c.cuit)}</td>
                  <td>{c.email ?? '—'}</td>
                  <td>{c.telefono ?? '—'}</td>
                  <td>{c.direccion ?? '—'}</td>
                  <td className="derecha"><button className="btn btn-chico" onClick={() => { setMensaje(null); setEditando(c) }}>Editar</button></td>
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

function FormCliente({ cliente, onListo, onCerrar }: { cliente: Cliente | null; onListo: (m: string) => void; onCerrar: () => void }) {
  const [f, setF] = useState({
    razonSocial: cliente?.razonSocial ?? '', cuit: cliente?.cuit ?? '', email: cliente?.email ?? '',
    telefono: cliente?.telefono ?? '', direccion: cliente?.direccion ?? '', activo: cliente?.activo ?? true,
  })
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [error, setError] = useState<string | null>(null)
  const set = (campo: string, valor: string | boolean) => setF((x) => ({ ...x, [campo]: valor }))

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setErrores({})
    try {
      if (cliente) await api(`/clientes/${cliente.id}`, { metodo: 'PUT', cuerpo: f })
      else await api('/clientes', { metodo: 'POST', cuerpo: f })
      onListo(`Cliente ${f.razonSocial} guardado.`)
    } catch (err) {
      const e = err as { message: string; errores?: Record<string, string[]> }
      setErrores(e.errores ?? {})
      setError(e.message)
    }
  }
  const errorDe = (campo: string) => errores[campo]?.[0] && <small className="texto-alerta">{errores[campo][0]}</small>

  return (
    <form onSubmit={enviar}>
      <h2>{cliente ? `Editar ${cliente.razonSocial}` : 'Nuevo cliente'}</h2>
      <div className="grilla-form">
        <label>Razón social<input value={f.razonSocial} required onChange={(e) => set('razonSocial', e.target.value)} />{errorDe('RazonSocial')}</label>
        <label>CUIT<input value={f.cuit} required placeholder="30-71234567-1" onChange={(e) => set('cuit', e.target.value)} />{errorDe('Cuit')}</label>
        <label>Email<input type="email" value={f.email} onChange={(e) => set('email', e.target.value)} />{errorDe('Email')}</label>
        <label>Teléfono<input value={f.telefono} onChange={(e) => set('telefono', e.target.value)} /></label>
        <label>Dirección<input value={f.direccion} onChange={(e) => set('direccion', e.target.value)} /></label>
        <label className="en-linea"><input type="checkbox" checked={f.activo} onChange={(e) => set('activo', e.target.checked)} /> Activo</label>
      </div>
      <Aviso>{error}</Aviso>
      <div className="acciones">
        <button type="button" className="btn" onClick={onCerrar}>Cerrar</button>
        <button className="btn btn-primario">Guardar</button>
      </div>
    </form>
  )
}
