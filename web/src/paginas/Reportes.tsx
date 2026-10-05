import { useState } from 'react'
import { Aviso, Cargando } from '../componentes/Comunes'
import { api, query } from '../lib/api'
import { formatoMoneda, formatoNumero, MESES, nombreMes } from '../lib/formato'
import type { ResumenVentasMes, VentaMesProducto } from '../lib/tipos'
import { useCarga } from '../lib/useCarga'

export function Reportes() {
  const hoy = new Date()
  const [anio, setAnio] = useState(hoy.getFullYear())
  const [mes, setMes] = useState(String(hoy.getMonth() + 1))

  // Resumen mensual: stored procedure sp_ResumenVentasMensual (últimos 12 meses)
  const resumen = useCarga(() => api<ResumenVentasMes[]>('/reportes/resumen-mensual'), [])
  // Detalle por producto: vista vw_VentasPorMesProducto
  const detalle = useCarga(() => api<VentaMesProducto[]>(`/reportes/ventas-por-producto${query({ anio, mes })}`), [anio, mes])

  const maximo = Math.max(1, ...(resumen.datos ?? []).map((r) => r.importe))
  const totalDetalle = (detalle.datos ?? []).reduce((t, v) => t + v.importe, 0)

  return (
    <>
      <div className="encabezado"><h1>Reportes de ventas</h1></div>
      <p className="tenue">Cuenta como venta todo pedido confirmado que no se canceló, en el mes en que se confirmó.</p>

      <section className="tarjeta">
        <h2>Resumen mensual <small className="tenue">(últimos 12 meses · stored procedure)</small></h2>
        <Aviso>{resumen.error}</Aviso>
        {resumen.cargando ? <Cargando /> : (
          <table className="tabla">
            <thead><tr><th>Mes</th><th className="num">Pedidos</th><th className="num">Unidades</th><th className="num">Ticket promedio</th><th className="num">Importe</th><th className="col-barra"></th></tr></thead>
            <tbody>
              {resumen.datos?.length === 0 && <tr><td colSpan={6} className="centro tenue">Sin ventas en el período.</td></tr>}
              {resumen.datos?.map((r) => (
                <tr key={`${r.anio}-${r.mes}`}>
                  <td>{nombreMes(r.mes)} {r.anio}</td>
                  <td className="num">{r.pedidos}</td>
                  <td className="num">{formatoNumero(r.unidades)}</td>
                  <td className="num">{formatoMoneda(r.ticketPromedio)}</td>
                  <td className="num"><strong>{formatoMoneda(r.importe)}</strong></td>
                  <td className="col-barra"><div className="barra-valor" style={{ width: `${(r.importe / maximo) * 100}%` }} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="tarjeta">
        <div className="encabezado">
          <h2>Ventas por producto <small className="tenue">(vista)</small></h2>
          <div className="filtros sin-margen">
            <select value={anio} onChange={(e) => setAnio(Number(e.target.value))} aria-label="Año">
              {[0, 1, 2].map((d) => <option key={d}>{hoy.getFullYear() - d}</option>)}
            </select>
            <select value={mes} onChange={(e) => setMes(e.target.value)} aria-label="Mes">
              <option value="">Todo el año</option>
              {MESES.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
            </select>
          </div>
        </div>
        <Aviso>{detalle.error}</Aviso>
        {detalle.cargando ? <Cargando /> : (
          <table className="tabla">
            <thead><tr>{!mes && <th>Mes</th>}<th>SKU</th><th>Producto</th><th>Categoría</th><th className="num">Pedidos</th><th className="num">Unidades</th><th className="num">Importe</th></tr></thead>
            <tbody>
              {detalle.datos?.length === 0 && <tr><td colSpan={7} className="centro tenue">Sin ventas en el período.</td></tr>}
              {detalle.datos?.map((v) => (
                <tr key={`${v.anio}-${v.mes}-${v.productoId}`}>
                  {!mes && <td>{nombreMes(v.mes)}</td>}
                  <td className="mono">{v.sku}</td><td>{v.producto}</td><td>{v.categoria}</td>
                  <td className="num">{v.pedidos}</td><td className="num">{v.unidades}</td>
                  <td className="num">{formatoMoneda(v.importe)}</td>
                </tr>
              ))}
            </tbody>
            {!!detalle.datos?.length && <tfoot><tr><td colSpan={mes ? 5 : 6} className="num"><strong>Total</strong></td><td className="num"><strong>{formatoMoneda(totalDetalle)}</strong></td></tr></tfoot>}
          </table>
        )}
      </section>
    </>
  )
}
