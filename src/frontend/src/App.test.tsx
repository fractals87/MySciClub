import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App authentication flow', () => {
  const fetchMock = vi.fn<typeof fetch>()
  let authenticated = false
  let isAdministrator = true

  beforeEach(() => {
    authenticated = false
    isAdministrator = true
    fetchMock.mockReset()
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url === '/api/security/session') return Response.json({ autenticato: authenticated, nomeVisualizzato: authenticated ? 'admin' : null, amministratore: authenticated && isAdministrator })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf-test' })
      if (url === '/api/security/login' && init?.method === 'POST') {
        const credentials = JSON.parse(String(init.body)) as { username: string; password: string }
        if (credentials.password !== 'correct') return Response.json({ status: 401, title: 'Accesso non riuscito', detail: 'Credenziali non valide o account non abilitato.' }, { status: 401 })
        authenticated = true
        return Response.json({ autenticato: true, nomeVisualizzato: 'admin', amministratore: isAdministrator })
      }
      if (url === '/api/security/logout' && init?.method === 'POST') {
        authenticated = false
        return new Response(null, { status: 204 })
      }
      if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
      if (url.includes('/soci?')) return Response.json({ items: [], page: 1, pageSize: 25, totalCount: 0 })
      throw new Error(`Unexpected request: ${url}`)
    })
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => { cleanup(); vi.unstubAllGlobals() })

  it('shows the login form and uses antiforgery for successful login', async () => {
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Accedi' })).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Email o nome utente'), { target: { value: 'admin@example.test' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct' } })
    fireEvent.click(screen.getByRole('button', { name: 'Accedi' }))

    expect(await screen.findByRole('heading', { name: 'Gestione soci' })).toBeInTheDocument()
    const session = fetchMock.mock.calls.find(([url]) => url === '/api/security/session')
    const tokenIndex = fetchMock.mock.calls.findIndex(([url]) => url === '/api/security/antiforgery-token')
    const login = fetchMock.mock.calls.find(([url, init]) => url === '/api/security/login' && init?.method === 'POST')
    expect(session).toBeDefined()
    expect(fetchMock.mock.calls.filter(([url]) => url === '/api/security/session')).toHaveLength(1)
    expect(login).toBeDefined()
    expect(tokenIndex).toBeGreaterThan(fetchMock.mock.calls.indexOf(session!))
    expect(tokenIndex).toBeLessThan(fetchMock.mock.calls.indexOf(login!))
    expect(new Headers(login?.[1]?.headers).get('X-CSRF-TOKEN')).toBe('csrf-test')
    expect(login?.[1]).toMatchObject({ credentials: 'same-origin' })
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url) === '/api/soci?tesseramento=0&page=1&pageSize=25&sortBy=0&sortDirection=0')).toBe(true))
  })

  it('shows generic credential errors and keeps the login form available', async () => {
    render(<App />)
    await screen.findByRole('heading', { name: 'Accedi' })
    fireEvent.change(screen.getByLabelText('Email o nome utente'), { target: { value: 'unknown@example.test' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByRole('button', { name: 'Accedi' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Credenziali non valide o account non abilitato.')
    expect(screen.getByRole('heading', { name: 'Accedi' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Gestione soci' })).not.toBeInTheDocument()
  })

  it('explains a lockout without exposing account details', async () => {
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url === '/api/security/session') return Response.json({ autenticato: false, nomeVisualizzato: null, amministratore: false })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf-test' })
      if (url === '/api/security/login' && init?.method === 'POST') return Response.json({ status: 423, title: 'Account bloccato' }, { status: 423 })
      throw new Error(`Unexpected request: ${url}`)
    })
    render(<App />)
    await screen.findByRole('heading', { name: 'Accedi' })
    fireEvent.change(screen.getByLabelText('Email o nome utente'), { target: { value: 'admin' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByRole('button', { name: 'Accedi' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('L’account è temporaneamente bloccato. Riprova più tardi.')
  })

  it('prevents duplicate login submits while a request is pending', async () => {
    let finishLogin: ((response: Response) => void) | undefined
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url === '/api/security/session') return Response.json({ autenticato: false, nomeVisualizzato: null, amministratore: false })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf-test' })
      if (url === '/api/security/login' && init?.method === 'POST') return await new Promise<Response>(resolve => { finishLogin = resolve })
      throw new Error(`Unexpected request: ${url}`)
    })
    render(<App />)
    await screen.findByRole('heading', { name: 'Accedi' })
    fireEvent.change(screen.getByLabelText('Email o nome utente'), { target: { value: 'admin' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct' } })
    fireEvent.click(screen.getByRole('button', { name: 'Accedi' }))

    expect(await screen.findByRole('button', { name: 'Accesso…' })).toBeDisabled()
    expect(fetchMock.mock.calls.filter(([url]) => url === '/api/security/login')).toHaveLength(1)
    authenticated = true
    finishLogin?.(Response.json({ autenticato: true, nomeVisualizzato: 'admin', amministratore: true }))
    expect(await screen.findByRole('heading', { name: 'Gestione soci' })).toBeInTheDocument()
  })

  it('restores an administrator session and logs out with a fresh antiforgery token', async () => {
    authenticated = true
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Gestione soci' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Esci' }))

    expect(await screen.findByRole('heading', { name: 'Accedi' })).toBeInTheDocument()
    const logout = fetchMock.mock.calls.find(([url, init]) => url === '/api/security/logout' && init?.method === 'POST')
    expect(logout).toBeDefined()
    expect(new Headers(logout?.[1]?.headers).get('X-CSRF-TOKEN')).toBe('csrf-test')
  })

  it('does not render member management for a signed-in non-administrator', async () => {
    authenticated = true
    isAdministrator = false
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Accesso non autorizzato' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Gestione soci' })).not.toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/soci?'))).toBe(false)
  })

  it('returns to login when a protected API request reports an expired session', async () => {
    authenticated = true
    fetchMock.mockImplementation(async input => {
      const url = String(input)
      if (url === '/api/security/session') return Response.json({ autenticato: true, nomeVisualizzato: 'admin', amministratore: true })
      if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
      if (url.includes('/soci?')) return Response.json({ status: 401, title: 'Autenticazione richiesta' }, { status: 401 })
      throw new Error(`Unexpected request: ${url}`)
    })
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Accedi' })).toBeInTheDocument()
    expect(await screen.findByRole('status')).toHaveTextContent('La sessione è scaduta. Accedi di nuovo per continuare.')
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/soci?'))).toBe(true))
  })
})
