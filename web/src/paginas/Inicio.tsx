import { Link } from 'react-router'
import { Aviso, Cargando, EstadoBadge } from '../componentes/Comunes'
import { api } from '../lib/api'
import { formatoFecha, formatoMoneda } from '../lib/formato'
import { useSesion } from '../lib/sesion'
import type { Dashboard } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

export function Inicio() {
  const { usuario, esAdmin } = useSesion()
  const { datos, error, cargando } = useCarga(() => api<Dashboard>('/dashboard'), [])

  if (cargando && !datos) return <Cargando />
  if (error) return <Aviso>{error}</Aviso>
  if (!datos) return null
  const e = datos.pedidosPorEstado

  return (
    <>
      <div className="encabezado">
        <h1>Hola, {usuario?.nombre.split(' ')[0]}</h1>
        <Link to="/pedidos/nuevo" className="btn btn-primario">+ Nuevo pedido</Link>
      </div>
      {!esAdmin && <p className="tenue">Los números de pedidos son de tus pedidos.</p>}

      <div className="kpis">
        <div className="kpi"><span>Ventas del mes</span><strong>{formatoMoneda(datos.ventasDelMes)}</strong><small>{datos.pedidosConfirmadosDelMes} pedido{datos.pedidosConfirmadosDelMes === 1 ? '' : 's'} confirmado{datos.pedidosConfirmadosDelMes === 1 ? '' : 's'}</small></div>
        <div className="kpi"><span>Borradores</span><strong>{e.Borrador}</strong><small>sin confirmar</small></div>
        <div className="kpi"><span>Para preparar</span><strong>{e.Confirmado}</strong><small>confirmados</small></div>
        <div className="kpi"><span>Para entregar</span><strong>{e.Preparado}</strong><small>preparados</small></div>
        <div className={`kpi ${datos.stockBajo.length ? 'kpi-alerta' : ''}`}><span>Stock bajo</span><strong>{datos.stockBajo.length}</strong><small>productos en o bajo el mínimo</small></div>
      </div>

      <div className="dos-columnas">
        <section className="tarjeta">
          <h2>⚠ Alertas de stock bajo</h2>
          {datos.stockBajo.length === 0 ? <p className="tenue">Todo el catálogo está por encima del mínimo.</p> : (
            <table className="tabla">
              <thead><tr><th>SKU</th><th>Producto</th><th className="num">Stock</th><th className="num">Mínimo</th></tr></thead>
              <tbody>
                {datos.stockBajo.map((p) => (
                  <tr key={p.id}>
                    <td className="mono">{p.sku}</td>
                    <td>{p.nombre}</td>
                    <td className={`num ${p.stock === 0 ? 'texto-alerta' : ''}`}><strong>{p.stock}</strong></td>
                    <td className="num">{p.stockMinimo}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {esAdmin && datos.stockBajo.length > 0 && <Link to="/productos?soloStockBajo=true">Registrar ingresos →</Link>}
        </section>

        <section className="tarjeta">
          <h2>Últimos pedidos</h2>
          <table className="tabla">
            <thead><tr><th>#</th><th>Cliente</th><th>Estado</th><th className="num">Total</th></tr></thead>
            <tbody>
              {datos.ultimosPedidos.map((p) => (
                <tr key={p.id}>
                  <td><Link to={`/pedidos/${p.id}`}>#{p.id}</Link></td>
                  <td>{p.cliente}<div className="tenue chico">{formatoFecha(p.fechaCreacion)}</div></td>
                  <td><EstadoBadge estado={p.estado} /></td>
                  <td className="num">{formatoMoneda(p.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Link to="/pedidos">Ver todos →</Link>
        </section>
      </div>
    </>
  )
}
