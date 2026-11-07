import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiRequest } from './client'

describe('apiRequest', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('uses a relative API URL and same-origin credentials for GET without an antiforgery token', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))

    await apiRequest<void>('/members', { method: 'GET' })

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/members')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'GET',
      credentials: 'same-origin',
    })
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).has('X-CSRF-TOKEN')).toBe(false)
  })

  it('gets a request token and sends it in the header for a mutating request', async () => {
    fetchMock
      .mockResolvedValueOnce(Response.json({ requestToken: 'request-token' }))
      .mockResolvedValueOnce(Response.json({ id: 123 }))

    const result = await apiRequest<{ id: number }>('members', {
      method: 'POST',
      body: JSON.stringify({ name: 'Ada' }),
      headers: { 'Content-Type': 'application/json' },
    })

    expect(result).toEqual({ id: 123 })
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/security/antiforgery-token')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'GET',
      credentials: 'same-origin',
    })
    expect(fetchMock.mock.calls[1][0]).toBe('/api/members')
    expect(fetchMock.mock.calls[1][1]).toMatchObject({
      method: 'POST',
      credentials: 'same-origin',
      body: JSON.stringify({ name: 'Ada' }),
    })
    expect(new Headers(fetchMock.mock.calls[1][1]?.headers).get('X-CSRF-TOKEN')).toBe('request-token')
  })
})
