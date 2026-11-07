const apiBasePath = '/api'
const antiforgeryTokenPath = `${apiBasePath}/security/antiforgery-token`
const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE'])

interface AntiforgeryTokenResponse {
  requestToken: string
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  type?: string
  code?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `API request failed with status ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers(options.headers)

  if (!safeMethods.has(method)) {
    headers.set('X-CSRF-TOKEN', await requestAntiforgeryToken())
  }

  const relativePath = path.replace(/^\/+/, '')
  const response = await fetch(`${apiBasePath}/${relativePath}`, {
    ...options,
    method,
    headers,
    credentials: 'same-origin',
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as ProblemDetails
    if (response.status === 401 && !relativePath.startsWith('security/'))
      window.dispatchEvent(new Event('mysciclub:session-expired'))
    throw new ApiError(response.status, problem)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return await response.json() as T
}

export async function apiDownload(path: string, options: RequestInit = {}): Promise<{ blob: Blob; fileName?: string }> {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers(options.headers)
  if (!safeMethods.has(method)) headers.set('X-CSRF-TOKEN', await requestAntiforgeryToken())
  const relativePath = path.replace(/^\/+/, '')
  const response = await fetch(`${apiBasePath}/${relativePath}`, {
    ...options,
    method,
    headers,
    credentials: 'same-origin',
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as ProblemDetails
    if (response.status === 401 && !relativePath.startsWith('security/'))
      window.dispatchEvent(new Event('mysciclub:session-expired'))
    throw new ApiError(response.status, problem)
  }

  const disposition = response.headers.get('Content-Disposition')
  const fileName = disposition?.match(/filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i)
  return {
    blob: await response.blob(),
    fileName: fileName?.[1] ? decodeURIComponent(fileName[1]) : fileName?.[2],
  }
}

async function requestAntiforgeryToken(): Promise<string> {
  const response = await fetch(antiforgeryTokenPath, {
    method: 'GET',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as ProblemDetails
    throw new ApiError(response.status, problem)
  }

  const payload = await response.json() as AntiforgeryTokenResponse
  if (!payload.requestToken) {
    throw new Error('The API did not return an antiforgery request token.')
  }

  return payload.requestToken
}
