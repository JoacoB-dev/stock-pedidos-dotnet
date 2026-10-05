import { useCallback, useEffect, useState } from 'react'

/** Carga datos asíncronos y expone cargando / error / recargar. */
export function useCarga<T>(cargar: () => Promise<T>, dependencias: unknown[]) {
  const [datos, setDatos] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(true)
  const [version, setVersion] = useState(0)

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const fn = useCallback(cargar, dependencias)

  useEffect(() => {
    let vigente = true
    setCargando(true)
    fn()
      .then((d) => { if (vigente) { setDatos(d); setError(null) } })
      .catch((e: Error) => { if (vigente) setError(e.message) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [fn, version])

  const recargar = useCallback(() => setVersion((v) => v + 1), [])
  return { datos, error, cargando, recargar, setDatos }
}
