import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { Aviso } from '../componentes/Comunes'
import { useSesion } from '../lib/sesion'

export function Login() {
  const { usuario, ingresar } = useSesion()
  const [email, setEmail] = useState('')
  const [clave, setClave] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (usuario) return <Navigate to="/" replace />

  async function enviar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setError(null)
    try {
      await ingresar(email, clave)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="login">
      <form className="tarjeta login-form" onSubmit={enviar}>
        <div className="marca marca-grande">
          <img src="/icono.svg" alt="" width={36} height={36} />
          <span>Stock y Pedidos</span>
        </div>
        <p className="tenue">Distribuidora mayorista · proyecto de portfolio con datos de ejemplo</p>
        <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" required /></label>
        <label>Contraseña<input type="password" value={clave} onChange={(e) => setClave(e.target.value)} autoComplete="current-password" required /></label>
        <Aviso>{error}</Aviso>
        <button className="btn btn-primario" disabled={enviando}>{enviando ? 'Ingresando…' : 'Ingresar'}</button>
        <div className="demo">
          <strong>Usuarios demo</strong> (contraseña <code>demo1234</code>)
          <div className="botones-demo">
            <button type="button" className="btn btn-chico" onClick={() => { setEmail('admin@demo.local'); setClave('demo1234') }}>admin@demo.local</button>
            <button type="button" className="btn btn-chico" onClick={() => { setEmail('vendedor@demo.local'); setClave('demo1234') }}>vendedor@demo.local</button>
          </div>
        </div>
      </form>
    </div>
  )
}
