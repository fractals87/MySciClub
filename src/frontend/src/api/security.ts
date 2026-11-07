import { apiRequest } from './client'

export interface SessionState {
  autenticato: boolean
  nomeVisualizzato: string | null
  amministratore: boolean
}

export const securityApi = {
  session: () => apiRequest<SessionState>('/security/session'),
  login: (username: string, password: string) => apiRequest<SessionState>('/security/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  }),
  logout: () => apiRequest<void>('/security/logout', { method: 'POST' }),
}
