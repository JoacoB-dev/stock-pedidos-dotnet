import { Link, useSearchParams } from 'react-router'
import { Aviso, Cargando, EstadoBadge, Paginador } from '../componentes/Comunes'
import { api, query } from '../lib/api'
import { ESTADOS, formatoFecha, formatoMoneda } from '../lib/formato'
import type { Pagina, PedidoResumen } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

/** Listado de pedidos. Los filtros viven en la URL, así se pueden compartir o volver atrás. */
export function Pedidos() {
  const [params, setParams] = useSearchParams()
  const filtros = {
    estado: params.get('estado') ?? '',
    texto: params.get('texto') ?? '',
    desde: params.get('desde') ?? '',
    hasta: params.get('hasta') ?? '',
    pagina: Number(params.get('pagina') ?? 1),
  }
  const clave = params.toString()
  const { datos, error, cargando } = useCarga(
    () => api<Pagina<PedidoResumen>>(`/pedidos${query({ ...filtros, tamanio: 15 })}`), [clave])

  function cambiar(campo: string, valor: string) {
    const nuevos = new URLSearchParams(params)
    if (valor) nuevos.set(campo, valor); else nuevos.delete(campo)
    if (campo !== 'pagina') nuevos.delete('pagina')
    setParams(nuevos)
  }

  return (
    <>
      <div className="encabezado">
        <h1>Pedidos</h1>
        <Link to="/pedidos/nuevo" className="btn btn-primario">+ Nuevo pedido</Link>
      </div>
      <div className="filtros">
        <input placeholder="N° de pedido o cliente" defaultValue={filtros.texto} aria-label="Buscar"
          onKeyDown={(e) => { if (e.key === 'Enter') cambiar('texto', e.currentTarget.value) }}
          onBlur={(e) => { if (e.target.value !== filtros.texto) cambiar('texto', e.target.value) }} />
        <select value={filtros.estado} onChange={(e) => cambiar('estado', e.target.value)} aria-label="Estado">
          <option value="">Todos los estados</option>
          {ESTADOS.map((e) => <option key={e}>{e}</option>)}
        </select>
        <label className="en-linea">Desde <input type="date" value={filtros.desde} onChange={(e) => cambiar('desde', e.target.value)} /></label>
        <label className="en-linea">Hasta <input type="date" value={filtros.hasta} onChange={(e) => cambiar('hasta', e.target.value)} /></label>
        {clave && <button className="btn btn-chico" onClick={() => setParams({})}>Limpiar</button>}
      </div>

      <Aviso>{error}</Aviso>
      {cargando && !datos ? <Cargando /> : datos && (
        <section className="tarjeta sin-padding">
          <table className="tabla">
            <thead>
              <tr><th>#</th><th>Cliente</th><th>Vendedor</th><th>Creado</th><th>Confirmado</th><th>Estado</th><th className="num">Renglones</th><th className="num">Total</th></tr>
            </thead>
            <tbody>
              {datos.items.length === 0 && <tr><td colSpan={8} className="centro tenue">No hay pedidos con esos filtros.</td></tr>}
              {datos.items.map((p) => (
                <tr key={p.id}>
                  <td><Link to={`/pedidos/${p.id}`}>#{p.id}</Link></td>
                  <td>{p.cliente}</td>
                  <td>{p.vendedor}</td>
                  <td>{formatoFecha(p.fechaCreacion)}</td>
                  <td>{formatoFecha(p.fechaConfirmacion)}</td>
                  <td><EstadoBadge estado={p.estado} /></td>
                  <td className="num">{p.cantidadRenglones}</td>
                  <td className="num">{formatoMoneda(p.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Paginador pagina={datos.numeroPagina} totalPaginas={datos.totalPaginas} total={datos.total}
            onCambiar={(p) => cambiar('pagina', String(p))} />
        </section>
      )}
    </>
  )
}
