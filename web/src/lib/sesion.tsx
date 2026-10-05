import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, CLAVE_SESION, EVENTO_SESION_VENCIDA } from './api'
import type { LoginResponse, Usuario } from './tipos'

interface Sesion {
  usuario: Usuario | null
  esAdmin: boolean
  ingresar: (email: string, clave: string) => Promise<void>
  salir: () => void
}

const ContextoSesion = createContext<Sesion | null>(null)

function leerSesion(): Usuario | null {
  try {
    const crudo = localStorage.getItem(CLAVE_SESION)
    if (!crudo) return null
    const datos = JSON.parse(crudo) as LoginResponse
    // Si el token ya venció, ni lo intentamos
    return new Date(datos.expira) > new Date() ? datos.usuario : null
  } catch {
    return null
  }
}

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(leerSesion)

  const salir = useCallback(() => {
    try { localStorage.removeItem(CLAVE_SESION) } catch { /* sin almacenamiento */ }
    setUsuario(null)
  }, [])

  const ingresar = useCallback(async (email: string, clave: string) => {
    const resp = await api<LoginResponse>('/auth/login', { metodo: 'POST', cuerpo: { email, clave } })
    try { localStorage.setItem(CLAVE_SESION, JSON.stringify(resp)) } catch { /* sin almacenamiento */ }
    setUsuario(resp.usuario)
  }, [])

  useEffect(() => {
    window.addEventListener(EVENTO_SESION_VENCIDA, salir)
    return () => window.removeEventListener(EVENTO_SESION_VENCIDA, salir)
  }, [salir])

  const valor = useMemo(
    () => ({ usuario, esAdmin: usuario?.rol === 'Administrador', ingresar, salir }),
    [usuario, ingresar, salir],
  )
  return <ContextoSesion.Provider value={valor}>{children}</ContextoSesion.Provider>
}

export function useSesion(): Sesion {
  const sesion = useContext(ContextoSesion)
  if (!sesion) throw new Error('useSesion tiene que usarse dentro de ProveedorSesion')
  return sesion
}
