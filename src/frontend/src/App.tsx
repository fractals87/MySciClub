import { useEffect, useState, type FormEvent } from 'react'
import { ApiError } from './api/client'
import { securityApi, type SessionState } from './api/security'
import { SociPage } from './features/members/SociPage'
import './App.css'

function App() {
  const [session, setSession] = useState<SessionState | null>(null)
  const [loadingSession, setLoadingSession] = useState(true)
  const [sessionError, setSessionError] = useState<string | null>(null)
  const [sessionExpired, setSessionExpired] = useState(false)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let active = true
    void securityApi.session().then(value => { if (active) setSession(value) })
      .catch(() => { if (active) setSessionError('Impossibile verificare la sessione. Riprova.') })
      .finally(() => { if (active) setLoadingSession(false) })
    const onExpired = () => {
      setSessionExpired(true)
      setSession({ autenticato: false, nomeVisualizzato: null, amministratore: false })
    }
    window.addEventListener('mysciclub:session-expired', onExpired)
    return () => { active = false; window.removeEventListener('mysciclub:session-expired', onExpired) }
  }, [])

  async function login(username: string, password: string) {
    setBusy(true); setSessionError(null)
    try { setSession(await securityApi.login(username, password)); setSessionExpired(false) }
    catch (reason) {
      if (reason instanceof ApiError && reason.status === 423) setSessionError('L’account è temporaneamente bloccato. Riprova più tardi.')
      else if (reason instanceof ApiError && reason.status === 401) setSessionError('Credenziali non valide o account non abilitato.')
      else setSessionError('Accesso non riuscito. Riprova.')
      throw reason
    } finally { setBusy(false) }
  }

  async function logout() {
    setBusy(true); setSessionError(null)
    try { await securityApi.logout(); setSession({ autenticato: false, nomeVisualizzato: null, amministratore: false }); setSessionExpired(false) }
    catch { setSessionError('Non è stato possibile completare l’uscita. Riprova.') }
    finally { setBusy(false) }
  }

  if (loadingSession) return <main className="auth-shell"><p role="status">Verifica della sessione…</p></main>
  if (!session?.autenticato) return <LoginPage onLogin={login} busy={busy} error={sessionError} expired={sessionExpired} onRetrySession={() => {
    setLoadingSession(true); setSessionError(null)
    void securityApi.session().then(setSession).catch(() => setSessionError('Impossibile verificare la sessione. Riprova.')).finally(() => setLoadingSession(false))
  }} />
  if (!session.amministratore) return <main className="auth-shell"><section className="auth-card">
    <h1>Accesso non autorizzato</h1><p>Il tuo account non dispone del ruolo amministrativo richiesto.</p>
    {sessionError && <p role="alert">{sessionError}</p>}<button type="button" disabled={busy} onClick={() => void logout()}>Esci</button>
  </section></main>

  return <><div className="auth-toolbar"><span>{session.nomeVisualizzato ?? 'Amministratore'}</span>
    {sessionError && <span role="alert">{sessionError}</span>}
    <button type="button" disabled={busy} onClick={() => void logout()}>{busy ? 'Uscita…' : 'Esci'}</button>
  </div><SociPage /></>
}

function LoginPage({ onLogin, busy, error, expired, onRetrySession }: {
  onLogin: (username: string, password: string) => Promise<void>; busy: boolean
  error: string | null; expired: boolean; onRetrySession: () => void
}) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    try { await onLogin(username.trim(), password) } catch { /* parent shows the generic error */ }
  }
  return <main className="auth-shell"><section className="auth-card" aria-labelledby="login-title">
    <p className="auth-eyebrow">MYSCICLUB · AREA AMMINISTRATIVA</p>
    <h1 id="login-title">Accedi</h1>
    {expired && <p className="auth-message" role="status">La sessione è scaduta. Accedi di nuovo per continuare.</p>}
    {error && <p className="auth-message auth-error" role="alert">{error}</p>}
    {error?.startsWith('Impossibile verificare') && <button type="button" onClick={onRetrySession}>Riprova</button>}
    <form onSubmit={event => void submit(event)}>
      <label>Email o nome utente<input autoComplete="username" required maxLength={254} value={username} onChange={event => setUsername(event.target.value)} /></label>
      <label>Password<input type="password" autoComplete="current-password" required maxLength={256} value={password} onChange={event => setPassword(event.target.value)} /></label>
      <button type="submit" disabled={busy || !username.trim() || !password}>{busy ? 'Accesso…' : 'Accedi'}</button>
    </form>
  </section></main>
}

export default App
