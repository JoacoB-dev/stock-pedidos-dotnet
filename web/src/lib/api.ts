import type { Faltante } from './tipos'

const CLAVE_SESION = 'stock.sesion'

/** Error de la API con el contenido del ProblemDetails ya interpretado. */
export class ErrorApi extends Error {
  readonly estado: number
  readonly errores: Record<string, string[]>
  readonly faltantes: Faltante[]

  constructor(estado: number, mensaje: string, errores: Record<string, string[]> = {}, faltantes: Faltante[] = []) {
    super(mensaje)
    this.estado = estado
    this.errores = errores
    this.faltantes = faltantes
  }
}

/** Arma un ErrorApi a partir de la respuesta (ProblemDetails o ValidationProblemDetails). */
export async function errorDesdeRespuesta(resp: Response): Promise<ErrorApi> {
  let cuerpo: { title?: string; detail?: string; errors?: Record<string, string[]>; faltantes?: Faltante[] } = {}
  try {
    cuerpo = await resp.json()
  } catch {
    // La respuesta no era JSON: nos quedamos con el código
  }
  const errores = cuerpo.errors ?? {}
  const primerError = Object.values(errores).flat()[0]
  const mensaje = cuerpo.detail ?? primerError ?? cuerpo.title ?? `Error ${resp.status}`
  return new ErrorApi(resp.status, mensaje, errores, cuerpo.faltantes ?? [])
}

export function tokenGuardado(): string | null {
  try {
    const crudo = localStorage.getItem(CLAVE_SESION)
    return crudo ? (JSON.parse(crudo).token as string) : null
  } catch {
    return null
  }
}

/** Se llama cuando la API responde 401: la app escucha este evento y vuelve al login. */
export const EVENTO_SESION_VENCIDA = 'stock:sesion-vencida'

export async function api<T>(ruta: string, opciones: { metodo?: string; cuerpo?: unknown } = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = tokenGuardado()
  if (token) headers.Authorization = `Bearer ${token}`
  if (opciones.cuerpo !== undefined) headers['Content-Type'] = 'application/json'

  const resp = await fetch(`/api${ruta}`, {
    method: opciones.metodo ?? 'GET',
    headers,
    body: opciones.cuerpo !== undefined ? JSON.stringify(opciones.cuerpo) : undefined,
  })

  if (resp.status === 401 && token) window.dispatchEvent(new Event(EVENTO_SESION_VENCIDA))
  if (!resp.ok) throw await errorDesdeRespuesta(resp)
  if (resp.status === 204) return undefined as T
  return (await resp.json()) as T
}

/** Convierte un objeto de filtros en query string, salteando vacíos. */
export function query(filtros: Record<string, string | number | boolean | null | undefined>): string {
  const params = new URLSearchParams()
  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor === undefined || valor === null || valor === '' || valor === false) continue
    params.set(clave, String(valor))
  }
  const texto = params.toString()
  return texto ? `?${texto}` : ''
}

export { CLAVE_SESION }
