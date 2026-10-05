import { useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { Aviso, Cargando, Paginador } from '../componentes/Comunes'
import { api, query } from '../lib/api'
import { formatoMoneda } from '../lib/formato'
import { useSesion } from '../lib/sesion'
import type { Categoria, Pagina, Producto } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

type Panel = { modo: 'nuevo' } | { modo: 'editar'; producto: Producto } | { modo: 'movimiento'; producto: Producto } | null

export function Productos() {
  const { esAdmin } = useSesion()
  const [params, setParams] = useSearchParams()
  const filtros = {
    texto: params.get('texto') ?? '',
    categoriaId: params.get('categoriaId') ?? '',
    soloStockBajo: params.get('soloStockBajo') === 'true',
    incluirInactivos: params.get('incluirInactivos') === 'true',
    orden: params.get('orden') ?? 'nombre',
    pagina: Number(params.get('pagina') ?? 1),
  }
  const clave = params.toString()
  const { datos, error, cargando, recargar } = useCarga(
    () => api<Pagina<Producto>>(`/productos${query({ ...filtros, tamanio: 15 })}`), [clave])
  const { datos: categorias } = useCarga(() => api<Categoria[]>('/categorias'), [])
  const [panel, setPanel] = useState<Panel>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)

  function cambiar(campo: string, valor: string) {
    const nuevos = new URLSearchParams(params)
    if (valor && valor !== 'false') nuevos.set(campo, valor); else nuevos.delete(campo)
    if (campo !== 'pagina') nuevos.delete('pagina')
    setParams(nuevos)
  }

  function listo(texto: string) {
    setPanel(null)
    setMensaje(texto)
    recargar()
  }

  return (
    <>
      <div className="encabezado">
        <h1>Productos</h1>
        {esAdmin && <button className="btn btn-primario" onClick={() => { setMensaje(null); setPanel({ modo: 'nuevo' }) }}>+ Nuevo producto</button>}
      </div>
      <div className="filtros">
        <input placeholder="SKU o nombre" defaultValue={filtros.texto} aria-label="Buscar"
          onKeyDown={(e) => { if (e.key === 'Enter') cambiar('texto', e.currentTarget.value) }}
          onBlur={(e) => { if (e.target.value !== filtros.texto) cambiar('texto', e.target.value) }} />
        <select value={filtros.categoriaId} onChange={(e) => cambiar('categoriaId', e.target.value)} aria-label="Categoría">
          <option value="">Todas las categorías</option>
          {categorias?.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
        </select>
        <select value={filtros.orden} onChange={(e) => cambiar('orden', e.target.value)} aria-label="Orden">
          <option value="nombre">Ordenar por nombre</option>
          <option value="sku">Ordenar por SKU</option>
          <option value="stock">Ordenar por stock</option>
          <option value="precio">Ordenar por precio</option>
        </select>
        <label className="en-linea"><input type="checkbox" checked={filtros.soloStockBajo} onChange={(e) => cambiar('soloStockBajo', String(e.target.checked))} /> Solo stock bajo</label>
        <label className="en-linea"><input type="checkbox" checked={filtros.incluirInactivos} onChange={(e) => cambiar('incluirInactivos', String(e.target.checked))} /> Incluir inactivos</label>
      </div>

      <Aviso tipo="ok">{mensaje}</Aviso>
      {panel && categorias && (
        <section className="tarjeta">
          {panel.modo === 'movimiento'
            ? <FormMovimiento producto={panel.producto} onListo={listo} onCerrar={() => setPanel(null)} />
            : <FormProducto producto={panel.modo === 'editar' ? panel.producto : null} categorias={categorias}
                onListo={listo} onCerrar={() => setPanel(null)} />}
        </section>
      )}

      <Aviso>{error}</Aviso>
      {cargando && !datos ? <Cargando /> : datos && (
        <section className="tarjeta sin-padding">
          <table className="tabla">
            <thead>
              <tr><th>SKU</th><th>Producto</th><th>Categoría</th><th className="num">Precio</th><th className="num">Stock</th><th className="num">Mínimo</th>{esAdmin && <th></th>}</tr>
            </thead>
            <tbody>
              {datos.items.length === 0 && <tr><td colSpan={7} className="centro tenue">No hay productos con esos filtros.</td></tr>}
              {datos.items.map((p) => (
                <tr key={p.id} className={p.activo ? '' : 'inactivo'}>
                  <td className="mono">{p.sku}</td>
                  <td>{p.nombre}{!p.activo && <span className="badge">Inactivo</span>}</td>
                  <td>{p.categoria}</td>
                  <td className="num">{formatoMoneda(p.precio)}</td>
                  <td className="num">
                    <strong className={p.stockBajo ? 'texto-alerta' : ''}>{p.stock}</strong>
                    {p.stockBajo && <span className="badge badge-alerta" title="En o bajo el stock mínimo">bajo</span>}
                  </td>
                  <td className="num">{p.stockMinimo}</td>
                  {esAdmin && (
                    <td className="derecha nowrap">
                      <button className="btn btn-chico" onClick={() => { setMensaje(null); setPanel({ modo: 'movimiento', producto: p }) }}>Ingreso / ajuste</button>{' '}
                      <button className="btn btn-chico" onClick={() => { setMensaje(null); setPanel({ modo: 'editar', producto: p }) }}>Editar</button>
                    </td>
                  )}
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

function FormProducto({ producto, categorias, onListo, onCerrar }: {
  producto: Producto | null; categorias: Categoria[]; onListo: (m: string) => void; onCerrar: () => void
}) {
  const [f, setF] = useState({
    sku: producto?.sku ?? '', nombre: producto?.nombre ?? '', categoriaId: String(producto?.categoriaId ?? ''),
    precio: String(producto?.precio ?? ''), stockMinimo: String(producto?.stockMinimo ?? 0), stockInicial: '0',
    activo: producto?.activo ?? true,
  })
  const [error, setError] = useState<string | null>(null)
  const set = (campo: string, valor: string | boolean) => setF((x) => ({ ...x, [campo]: valor }))

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      if (producto) {
        await api(`/productos/${producto.id}`, { metodo: 'PUT', cuerpo: {
          nombre: f.nombre, categoriaId: Number(f.categoriaId), precio: Number(f.precio),
          stockMinimo: Number(f.stockMinimo), activo: f.activo, rowVersion: producto.rowVersion,
        } })
        onListo(`Producto ${producto.sku} actualizado.`)
      } else {
        await api('/productos', { metodo: 'POST', cuerpo: {
          sku: f.sku.toUpperCase(), nombre: f.nombre, categoriaId: Number(f.categoriaId), precio: Number(f.precio),
          stockMinimo: Number(f.stockMinimo), stockInicial: Number(f.stockInicial),
        } })
        onListo(`Producto ${f.sku.toUpperCase()} creado.`)
      }
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <form onSubmit={enviar}>
      <h2>{producto ? `Editar ${producto.sku}` : 'Nuevo producto'}</h2>
      <div className="grilla-form">
        <label>SKU<input value={f.sku} disabled={!!producto} required onChange={(e) => set('sku', e.target.value)} placeholder="ALM-010" /></label>
        <label>Nombre<input value={f.nombre} required onChange={(e) => set('nombre', e.target.value)} /></label>
        <label>Categoría
          <select value={f.categoriaId} required onChange={(e) => set('categoriaId', e.target.value)}>
            <option value="">Elegí…</option>
            {categorias.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
          </select>
        </label>
        <label>Precio<input type="number" step="0.01" min="0.01" value={f.precio} required onChange={(e) => set('precio', e.target.value)} /></label>
        <label>Stock mínimo<input type="number" min="0" value={f.stockMinimo} required onChange={(e) => set('stockMinimo', e.target.value)} /></label>
        {producto
          ? <label className="en-linea"><input type="checkbox" checked={f.activo} onChange={(e) => set('activo', e.target.checked)} /> Activo</label>
          : <label>Stock inicial<input type="number" min="0" value={f.stockInicial} onChange={(e) => set('stockInicial', e.target.value)} /></label>}
      </div>
      {producto && <p className="tenue chico">El stock no se edita acá: se cambia con ingresos y ajustes, así queda registrado.</p>}
      <Aviso>{error}</Aviso>
      <div className="acciones">
        <button type="button" className="btn" onClick={onCerrar}>Cerrar</button>
        <button className="btn btn-primario">Guardar</button>
      </div>
    </form>
  )
}

function FormMovimiento({ producto, onListo, onCerrar }: {
  producto: Producto; onListo: (m: string) => void; onCerrar: () => void
}) {
  const [tipo, setTipo] = useState<'Ingreso' | 'Ajuste'>('Ingreso')
  const [cantidad, setCantidad] = useState('')
  const [motivo, setMotivo] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      const actualizado = await api<Producto>(`/productos/${producto.id}/movimientos`, {
        metodo: 'POST', cuerpo: { tipo, cantidad: Number(cantidad), motivo: motivo || null },
      })
      onListo(`${producto.sku}: stock actualizado a ${actualizado.stock}.`)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <form onSubmit={enviar}>
      <h2>Movimiento de stock · {producto.sku} <span className="tenue">(stock actual {producto.stock})</span></h2>
      <div className="grilla-form">
        <label>Tipo
          <select value={tipo} onChange={(e) => setTipo(e.target.value as 'Ingreso' | 'Ajuste')}>
            <option value="Ingreso">Ingreso de mercadería</option>
            <option value="Ajuste">Ajuste de inventario (+/-)</option>
          </select>
        </label>
        <label>Cantidad<input type="number" value={cantidad} required onChange={(e) => setCantidad(e.target.value)}
          placeholder={tipo === 'Ajuste' ? 'Ej.: -3' : 'Ej.: 50'} /></label>
        <label>Motivo{tipo === 'Ajuste' ? '' : ' (opcional)'}<input value={motivo} maxLength={300} required={tipo === 'Ajuste'}
          onChange={(e) => setMotivo(e.target.value)} placeholder={tipo === 'Ajuste' ? 'Rotura, diferencia de inventario…' : 'Remito del proveedor…'} /></label>
      </div>
      <Aviso>{error}</Aviso>
      <div className="acciones">
        <button type="button" className="btn" onClick={onCerrar}>Cerrar</button>
        <button className="btn btn-primario">Registrar</button>
      </div>
    </form>
  )
}
