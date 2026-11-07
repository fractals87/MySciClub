import { apiDownload, apiRequest } from './client'

export type Sesso = 0 | 1
export type SceltaCodiceFiscale = 0 | 1 | 2
export type TesseramentoFilter = 0 | 1 | 2
export type SortField = 0 | 1 | 2 | 3 | 4
export type SortDirection = 0 | 1

export interface Country { codiceIsoAlpha2: string; nome: string }
export interface ItalianTown { codiceCatastale: string; nome: string; provincia: string }
export interface Cittadinanza { codiceIsoPaese: string; principale: boolean }
export interface SocioListItem {
  numeroSocio: number; nome: string; cognome: string; codiceFiscale: string
  email: string; comuneResidenza?: string | null; tesserato: boolean
}
export interface SocioPage {
  items: SocioListItem[]; page: number; pageSize: number; totalCount: number
}
export interface SocioDetail {
  numeroSocio: number; nome: string; cognome: string; dataNascita: string
  codiceIsoPaeseNascita: string; codiceCatastaleComuneItalianoNascita?: string | null; comuneEsteroNascita?: string | null
  sesso: Sesso; codiceFiscale: string; provenienzaCodiceFiscale: 0 | 1
  codiceFiscalePotenzialmenteObsoleto: boolean; cittadinanze: Cittadinanza[]
  indirizzo: string; cap?: string | null; codiceIsoPaeseResidenza: string
  codiceCatastaleComuneItalianoResidenza?: string | null; provinciaOSuddivisione?: string | null
  email: string; cellulare: string; fotoDisponibile: boolean
}
export interface SocioInput {
  nome: string; cognome: string; dataNascita: string; codiceIsoPaeseNascita: string
  codiceCatastaleComuneItalianoNascita?: string | null; comuneEsteroNascita?: string | null; sesso: Sesso
  cittadinanze: Cittadinanza[]; indirizzoResidenza: string; codiceIsoPaeseResidenza: string
  cap?: string | null; codiceCatastaleComuneItalianoResidenza?: string | null; suddivisioneResidenza?: string | null
  email: string; cellulare: string; codiceFiscaleManuale?: string | null; sceltaCodiceFiscale: SceltaCodiceFiscale
}
export interface SocioSearch {
  text?: string; tesseramento: TesseramentoFilter; codiceCatastaleComune?: string; page: number; pageSize: 25 | 50 | 100
  sortBy: SortField; sortDirection: SortDirection
}
export interface SocioCreated { numeroSocio: number }

function queryString(query: SocioSearch): string {
  const params = new URLSearchParams()
  if (query.text?.trim()) params.set('text', query.text.trim())
  params.set('tesseramento', String(query.tesseramento))
  if (query.codiceCatastaleComune) params.set('codiceCatastaleComune', query.codiceCatastaleComune)
  params.set('page', String(query.page))
  params.set('pageSize', String(query.pageSize))
  params.set('sortBy', String(query.sortBy))
  params.set('sortDirection', String(query.sortDirection))
  return params.toString()
}

export const sociApi = {
  countries: () => apiRequest<Country[]>('/soci/cataloghi/paesi'),
  towns: (countryIsoAlpha2?: string) => apiRequest<ItalianTown[]>(`/soci/cataloghi/comuni${countryIsoAlpha2 ? `?countryIsoAlpha2=${countryIsoAlpha2}` : ''}`),
  search: (query: SocioSearch) => apiRequest<SocioPage>(`/soci?${queryString(query)}`),
  get: (numeroSocio: number) => apiRequest<SocioDetail>(`/soci/${numeroSocio}`),
  create: (input: SocioInput) => apiRequest<SocioCreated>('/soci', { method: 'POST', body: JSON.stringify(input), headers: { 'Content-Type': 'application/json' } }),
  update: (numeroSocio: number, input: SocioInput) => apiRequest<SocioDetail>(`/soci/${numeroSocio}`, { method: 'PUT', body: JSON.stringify(input), headers: { 'Content-Type': 'application/json' } }),
  remove: (numeroSocio: number) => apiRequest<void>(`/soci/${numeroSocio}`, { method: 'DELETE' }),
  uploadPhoto: (numeroSocio: number, file: File) => {
    const data = new FormData(); data.append('file', file)
    return apiRequest<void>(`/soci/${numeroSocio}/foto`, { method: 'PUT', body: data })
  },
  removePhoto: (numeroSocio: number) => apiRequest<void>(`/soci/${numeroSocio}/foto`, { method: 'DELETE' }),
  photoUrl: (numeroSocio: number) => `/api/soci/${numeroSocio}/foto`,
  export: (query: SocioSearch) => apiDownload(`/soci/export?${queryString(query)}`),
}
