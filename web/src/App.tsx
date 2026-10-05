import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { Layout } from './componentes/Layout'
import { useSesion } from './lib/sesion'
import { Clientes } from './paginas/Clientes'
import { Inicio } from './paginas/Inicio'
import { Login } from './paginas/Login'
import { Movimientos } from './paginas/Movimientos'
import { PedidoDetalle } from './paginas/PedidoDetalle'
import { PedidoForm } from './paginas/PedidoForm'
import { Pedidos } from './paginas/Pedidos'
import { Productos } from './paginas/Productos'
import { Reportes } from './paginas/Reportes'

function Privada({ children, soloAdmin = false }: { children: ReactNode; soloAdmin?: boolean }) {
  const { usuario, esAdmin } = useSesion()
  if (!usuario) return <Navigate to="/ingresar" replace />
  if (soloAdmin && !esAdmin) return <Navigate to="/" replace />
  return children
}

export function App() {
  return (
    <Routes>
      <Route path="/ingresar" element={<Login />} />
      <Route element={<Privada><Layout /></Privada>}>
        <Route index element={<Inicio />} />
        <Route path="pedidos" element={<Pedidos />} />
        <Route path="pedidos/nuevo" element={<PedidoForm />} />
        <Route path="pedidos/:id" element={<PedidoDetalle />} />
        <Route path="pedidos/:id/editar" element={<PedidoForm />} />
        <Route path="productos" element={<Productos />} />
        <Route path="clientes" element={<Clientes />} />
        <Route path="movimientos" element={<Privada soloAdmin><Movimientos /></Privada>} />
        <Route path="reportes" element={<Privada soloAdmin><Reportes /></Privada>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
