import { NavLink, Outlet } from 'react-router'
import { useSesion } from '../lib/sesion'

export function Layout() {
  const { usuario, esAdmin, salir } = useSesion()
  return (
    <div className="app">
      <header className="barra">
        <div className="marca">
          <img src="/icono.svg" alt="" width={26} height={26} />
          <span>Stock y Pedidos</span>
        </div>
        <nav>
          <NavLink to="/" end>Inicio</NavLink>
          <NavLink to="/pedidos">Pedidos</NavLink>
          <NavLink to="/productos">Productos</NavLink>
          <NavLink to="/clientes">Clientes</NavLink>
          {esAdmin && <NavLink to="/movimientos">Movimientos</NavLink>}
          {esAdmin && <NavLink to="/reportes">Reportes</NavLink>}
        </nav>
        <div className="usuario">
          <span>{usuario?.nombre} <small className="tenue">({usuario?.rol})</small></span>
          <button className="btn btn-chico" onClick={salir}>Salir</button>
        </div>
      </header>
      <main className="contenido">
        <Outlet />
      </main>
    </div>
  )
}
