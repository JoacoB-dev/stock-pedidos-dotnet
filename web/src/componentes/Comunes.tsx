import type { EstadoPedido } from '../lib/tipos'

export function EstadoBadge({ estado }: { estado: EstadoPedido }) {
  return <span className={`badge estado-${estado.toLowerCase()}`}>{estado}</span>
}

export function Aviso({ tipo = 'error', children }: { tipo?: 'error' | 'ok' | 'info'; children: React.ReactNode }) {
  if (!children) return null
  return <div className={`aviso aviso-${tipo}`} role={tipo === 'error' ? 'alert' : 'status'}>{children}</div>
}

export function Cargando() {
  return <p className="tenue">Cargando…</p>
}

export function Paginador({ pagina, totalPaginas, total, onCambiar }: {
  pagina: number; totalPaginas: number; total: number; onCambiar: (p: number) => void
}) {
  return (
    <div className="paginador">
      <span className="tenue">{total} resultado{total === 1 ? '' : 's'}</span>
      <div>
        <button className="btn btn-chico" disabled={pagina <= 1} onClick={() => onCambiar(pagina - 1)}>← Anterior</button>
        <span className="tenue"> Página {pagina} de {Math.max(totalPaginas, 1)} </span>
        <button className="btn btn-chico" disabled={pagina >= totalPaginas} onClick={() => onCambiar(pagina + 1)}>Siguiente →</button>
      </div>
    </div>
  )
}
