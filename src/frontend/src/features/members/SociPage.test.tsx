import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { SociPage } from './SociPage'

describe('SociPage', () => {
  const fetchMock = vi.fn<typeof fetch>()
  const createObjectUrlDescriptor = Object.getOwnPropertyDescriptor(URL, 'createObjectURL')
  const revokeObjectUrlDescriptor = Object.getOwnPropertyDescriptor(URL, 'revokeObjectURL')

  beforeEach(() => {
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
    if (createObjectUrlDescriptor) Object.defineProperty(URL, 'createObjectURL', createObjectUrlDescriptor)
    else delete (URL as { createObjectURL?: typeof URL.createObjectURL }).createObjectURL
    if (revokeObjectUrlDescriptor) Object.defineProperty(URL, 'revokeObjectURL', revokeObjectUrlDescriptor)
    else delete (URL as { revokeObjectURL?: typeof URL.revokeObjectURL }).revokeObjectURL
  })

  it('shows a server-paged list, empty geography warning, and no photos in the table', async () => {
    fetchMock.mockImplementation(async input => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
      if (url.includes('/soci?')) {
        const query = new URL(url, 'https://localhost').searchParams
        return Response.json({
        items: [{ numeroSocio: 123, nome: 'Ada', cognome: 'Lovelace', codiceFiscale: 'LVL...', email: 'ada@example.test', comuneResidenza: null, tesserato: false }],
        page: Number(query.get('page')), pageSize: Number(query.get('pageSize')), totalCount: 60,
      })
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)

    expect(await screen.findByRole('button', { name: '123' })).toBeInTheDocument()
    expect(screen.getByText('60 soci trovati')).toBeInTheDocument()
    expect(screen.getByText(/cataloghi geografici non sono ancora popolati/i)).toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/soci?tesseramento=0&page=1&pageSize=25&sortBy=0&sortDirection=0', expect.objectContaining({ credentials: 'same-origin' }))

    fireEvent.click(screen.getByRole('button', { name: /Cognome/ }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('sortDirection=1'))).toBe(true))
    fireEvent.click(screen.getByRole('button', { name: 'Successiva' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('page=2'))).toBe(true))
  })

  it('reports unavailable tesseramento instead of displaying a presumed status', async () => {
    fetchMock.mockImplementation(async input => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
      if (url.includes('/soci?')) return Response.json({ status: 503, title: 'Tesseramento non disponibile', detail: 'Unavailable', code: 'tesseramento_non_disponibile' }, { status: 503 })
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)

    expect(await screen.findByText(/Elenco non disponibile: il sistema non può ancora determinare il tesseramento corrente/i)).toBeInTheDocument()
    expect(screen.getByLabelText('Stato tesseramento')).toBeDisabled()
    expect(screen.queryAllByRole('row')).toHaveLength(0)
  })

  it('explains that the administrator session has expired after an API 401', async () => {
    fetchMock.mockImplementation(async input => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
      if (url.includes('/soci?')) return Response.json({ status: 401, title: 'Autenticazione richiesta' }, { status: 401 })
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent('La sessione è scaduta. Accedi di nuovo per continuare.')
  })

  it('creates a member using the supplied country and town catalogs', async () => {
    const countries = [{ codiceIsoAlpha2: 'IT', nome: 'Italia' }]
    const towns = [{ codiceCatastale: 'H501', nome: 'Roma', provincia: 'RM' }]
    const detail = {
      numeroSocio: 1000, nome: 'Ada', cognome: 'Lovelace', dataNascita: '1980-01-01',
      codiceIsoPaeseNascita: 'IT', codiceCatastaleComuneItalianoNascita: 'H501', sesso: 1, codiceFiscale: 'LVL...',
      provenienzaCodiceFiscale: 0, codiceFiscalePotenzialmenteObsoleto: false,
      cittadinanze: [{ codiceIsoPaese: 'IT', principale: true }], indirizzo: 'Via Roma 1', cap: '00100',
      codiceIsoPaeseResidenza: 'IT', codiceCatastaleComuneItalianoResidenza: 'H501', email: 'ada@example.test',
      cellulare: '+390000000', fotoDisponibile: false,
    }
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi')) return Response.json(countries)
      if (url.endsWith('/soci/cataloghi/comuni')) return Response.json(towns)
      if (url.includes('/soci?')) return Response.json({ items: [], page: 1, pageSize: 25, totalCount: 0 })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf' })
      if (url === '/api/soci' && init?.method === 'POST') return Response.json({ numeroSocio: 1000 }, { status: 201 })
      if (url === '/api/soci/1000') return Response.json(detail)
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Nuovo socio' }))
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Ada' } })
    fireEvent.change(screen.getByLabelText('Cognome'), { target: { value: 'Lovelace' } })
    fireEvent.change(screen.getByLabelText('Data di nascita'), { target: { value: '1980-01-01' } })
    fireEvent.change(screen.getByLabelText('Paese di nascita'), { target: { value: 'IT' } })
    fireEvent.change(screen.getByLabelText('Comune italiano di nascita'), { target: { value: 'H501' } })
    fireEvent.change(screen.getByLabelText('Indirizzo'), { target: { value: 'Via Roma 1' } })
    fireEvent.change(screen.getByLabelText('Paese di residenza'), { target: { value: 'IT' } })
    fireEvent.change(screen.getByLabelText('Comune', { selector: 'select' }), { target: { value: 'H501' } })
    fireEvent.change(screen.getByLabelText('CAP'), { target: { value: '00100' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ada@example.test' } })
    fireEvent.change(screen.getByLabelText('Cellulare'), { target: { value: '+390000000' } })
    fireEvent.change(screen.getByLabelText('Aggiungi cittadinanza'), { target: { value: 'IT' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aggiungi' }))
    fireEvent.click(screen.getByRole('button', { name: 'Salva socio' }))

    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument()
    const postCall = fetchMock.mock.calls.find(([url, init]) => url === '/api/soci' && init?.method === 'POST')
    expect(postCall).toBeDefined()
    const payload = JSON.parse(String(postCall?.[1]?.body)) as { codiceCatastaleComuneItalianoNascita: string; codiceFiscaleManuale?: string }
    expect(payload.codiceCatastaleComuneItalianoNascita).toBe('H501')
    expect(payload.codiceFiscaleManuale).toBeUndefined()
  })

  it('keeps the list usable and shows a clear message for authorization, validation, missing and conflict errors', async () => {
    const failures = [
      [400, 'Dati non validi', 'Correggi i campi indicati.'],
      [403, 'Accesso negato', 'Il tuo account non è autorizzato a gestire i soci.'],
      [404, 'Risorsa non trovata', 'Il socio richiesto non esiste.'],
      [409, 'Operazione in conflitto', 'Il codice fiscale è già associato a un socio.'],
    ] as const

    for (const [status, title, message] of failures) {
      cleanup()
      fetchMock.mockImplementation(async input => {
        const url = String(input)
        if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
        if (url.includes('/soci?')) return Response.json({ status, title, detail: message }, { status })
        throw new Error(`Unexpected request: ${url}`)
      })

      render(<SociPage />)
      expect(await screen.findByRole('alert')).toHaveTextContent(message)
      expect(screen.getByRole('heading', { name: 'Elenco soci' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Nuovo socio' })).toBeDisabled()
    }
  })

  it('combines text and municipality filters and applies them when searching', async () => {
    fetchMock.mockImplementation(async input => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi')) return Response.json([{ codiceIsoAlpha2: 'IT', nome: 'Italia' }])
      if (url.endsWith('/soci/cataloghi/comuni')) return Response.json([{ codiceCatastale: 'H501', nome: 'Roma', provincia: 'RM' }])
      if (url.includes('/soci?')) return Response.json({ items: [], page: 1, pageSize: 25, totalCount: 0 })
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    await screen.findByText('0 soci trovati')
    fireEvent.change(screen.getByLabelText('Cerca socio'), { target: { value: 'Ada Lovelace' } })
    fireEvent.change(screen.getByLabelText('Comune di residenza'), { target: { value: 'H501' } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Cerca' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Cerca' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => {
      const query = new URL(String(url), 'https://localhost').searchParams
      return query.get('text') === 'Ada Lovelace' && query.get('codiceCatastaleComune') === 'H501'
    }), JSON.stringify(fetchMock.mock.calls.map(([url]) => String(url)))).toBe(true))
    expect(await screen.findByText('0 soci trovati')).toBeInTheDocument()
  })

  it('opens the detail, edits an existing member, preserves the current CF and warns when it may be outdated', async () => {
    const detail = {
      numeroSocio: 1000, nome: 'Ada', cognome: 'Lovelace', dataNascita: '1980-01-01',
      codiceIsoPaeseNascita: 'IT', codiceCatastaleComuneItalianoNascita: 'H501', sesso: 1, codiceFiscale: 'LVL...',
      provenienzaCodiceFiscale: 1, codiceFiscalePotenzialmenteObsoleto: false,
      cittadinanze: [{ codiceIsoPaese: 'IT', principale: true }], indirizzo: 'Via Roma 1', cap: '00100',
      codiceIsoPaeseResidenza: 'IT', codiceCatastaleComuneItalianoResidenza: 'H501', email: 'ada@example.test',
      cellulare: '+390000000', fotoDisponibile: false,
    }
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi')) return Response.json([{ codiceIsoAlpha2: 'IT', nome: 'Italia' }])
      if (url.endsWith('/soci/cataloghi/comuni')) return Response.json([{ codiceCatastale: 'H501', nome: 'Roma', provincia: 'RM' }])
      if (url.includes('/soci?')) return Response.json({ items: [{ ...detail, comuneResidenza: 'Roma', tesserato: false }], page: 1, pageSize: 25, totalCount: 1 })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf' })
      if (url === '/api/soci/1000' && init?.method === 'PUT') return Response.json({ ...detail, cognome: 'Byron', codiceFiscalePotenzialmenteObsoleto: true })
      if (url === '/api/soci/1000') return Response.json(detail)
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    fireEvent.click(await screen.findByRole('button', { name: '1000' }))
    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Modifica' }))
    fireEvent.change(screen.getByLabelText('Cognome'), { target: { value: 'Byron' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salva socio' }))

    expect(await screen.findByRole('heading', { name: 'Ada Byron' })).toBeInTheDocument()
    expect(screen.getByText(/il codice fiscale potrebbe dover essere aggiornato/i)).toBeInTheDocument()
    const update = fetchMock.mock.calls.find(([url, init]) => url === '/api/soci/1000' && init?.method === 'PUT')
    expect(update).toBeDefined()
    expect(JSON.parse(String(update?.[1]?.body))).toMatchObject({ sceltaCodiceFiscale: 0 })
  })

  it('validates foreign birth and residence, requires a manual CF, and records multiple citizenships with one primary', async () => {
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi')) return Response.json([
        { codiceIsoAlpha2: 'IT', nome: 'Italia' },
        { codiceIsoAlpha2: 'FR', nome: 'Francia' },
        { codiceIsoAlpha2: 'ES', nome: 'Spagna' },
      ])
      if (url.endsWith('/soci/cataloghi/comuni')) return Response.json([{ codiceCatastale: 'H501', nome: 'Roma', provincia: 'RM' }])
      if (url.includes('/soci?')) return Response.json({ items: [], page: 1, pageSize: 25, totalCount: 0 })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf' })
      if (url === '/api/soci' && init?.method === 'POST') return Response.json({ numeroSocio: 1001 }, { status: 201 })
      if (url === '/api/soci/1001') return Response.json({ numeroSocio: 1001, nome: 'Ada', cognome: 'Lovelace', dataNascita: '1980-01-01', codiceIsoPaeseNascita: 'FR', comuneEsteroNascita: 'Lione', sesso: 1, codiceFiscale: 'RSSMRA80A01Z110X', provenienzaCodiceFiscale: 1, codiceFiscalePotenzialmenteObsoleto: false, cittadinanze: [{ codiceIsoPaese: 'FR', principale: true }, { codiceIsoPaese: 'ES', principale: false }], indirizzo: 'Rue de Paris 1', codiceIsoPaeseResidenza: 'FR', provinciaOSuddivisione: 'Auvergne-Rhône-Alpes', email: 'ada@example.test', cellulare: '+330000000', fotoDisponibile: false })
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Nuovo socio' }))
    fireEvent.change(screen.getByLabelText('Nome'), { target: { value: 'Ada' } })
    fireEvent.change(screen.getByLabelText('Cognome'), { target: { value: 'Lovelace' } })
    fireEvent.change(screen.getByLabelText('Data di nascita'), { target: { value: '1980-01-01' } })
    fireEvent.change(screen.getByLabelText('Paese di nascita'), { target: { value: 'FR' } })
    fireEvent.change(screen.getByLabelText('Comune estero di nascita'), { target: { value: 'Lione' } })
    fireEvent.change(screen.getByLabelText('Codice fiscale manuale'), { target: { value: 'RSSMRA80A01Z110X' } })
    fireEvent.change(screen.getByLabelText('Aggiungi cittadinanza'), { target: { value: 'FR' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aggiungi' }))
    fireEvent.change(screen.getByLabelText('Aggiungi cittadinanza'), { target: { value: 'ES' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aggiungi' }))
    fireEvent.change(screen.getByLabelText('Indirizzo'), { target: { value: 'Rue de Paris 1' } })
    fireEvent.change(screen.getByLabelText('Paese di residenza'), { target: { value: 'FR' } })
    fireEvent.change(screen.getByLabelText('Suddivisione territoriale'), { target: { value: 'Auvergne-Rhône-Alpes' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ada@example.test' } })
    fireEvent.change(screen.getByLabelText('Cellulare'), { target: { value: '+330000000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salva socio' }))

    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument()
    const create = fetchMock.mock.calls.find(([url, init]) => url === '/api/soci' && init?.method === 'POST')
    const payload = JSON.parse(String(create?.[1]?.body)) as { comuneEsteroNascita: string; codiceFiscaleManuale: string; cittadinanze: { codiceIsoPaese: string; principale: boolean }[] }
    expect(payload.comuneEsteroNascita).toBe('Lione')
    expect(payload.codiceFiscaleManuale).toBe('RSSMRA80A01Z110X')
    expect(payload.cittadinanze).toEqual([{ codiceIsoPaese: 'FR', principale: true }, { codiceIsoPaese: 'ES', principale: false }])
  })

  it('exports the current filtered list as XLSX and reports download errors', async () => {
    const createObjectUrl = vi.fn(() => 'blob:members')
    const revokeObjectUrl = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createObjectUrl })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revokeObjectUrl })
    const anchorClick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    fetchMock.mockImplementation(async input => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi') || url.endsWith('/soci/cataloghi/comuni')) return Response.json([])
      if (url.includes('/soci/export?')) return new Response(new Blob(['xlsx']), { headers: { 'Content-Disposition': 'attachment; filename="Soci.xlsx"' } })
      if (url.includes('/soci?')) return Response.json({ items: [], page: 1, pageSize: 25, totalCount: 0 })
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    await screen.findByText('0 soci trovati')
    fireEvent.click(screen.getByRole('button', { name: 'Esporta Excel' }))
    expect(await screen.findByText('Esportazione completata.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/soci/export?'))).toBe(true)
    expect(createObjectUrl).toHaveBeenCalledOnce()
    expect(anchorClick).toHaveBeenCalledOnce()
    expect(revokeObjectUrl).toHaveBeenCalledOnce()
    anchorClick.mockRestore()
  })

  it('previews, uploads, and removes a photograph from the member detail', async () => {
    let photoAvailable = true
    const detail = {
      numeroSocio: 1000, nome: 'Ada', cognome: 'Lovelace', dataNascita: '1980-01-01',
      codiceIsoPaeseNascita: 'IT', codiceCatastaleComuneItalianoNascita: 'H501', sesso: 1, codiceFiscale: 'LVL...',
      provenienzaCodiceFiscale: 0, codiceFiscalePotenzialmenteObsoleto: false,
      cittadinanze: [{ codiceIsoPaese: 'IT', principale: true }], indirizzo: 'Via Roma 1', cap: '00100',
      codiceIsoPaeseResidenza: 'IT', codiceCatastaleComuneItalianoResidenza: 'H501', email: 'ada@example.test',
      cellulare: '+390000000',
    }
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:preview') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi')) return Response.json([{ codiceIsoAlpha2: 'IT', nome: 'Italia' }])
      if (url.endsWith('/soci/cataloghi/comuni')) return Response.json([{ codiceCatastale: 'H501', nome: 'Roma', provincia: 'RM' }])
      if (url.includes('/soci?')) return Response.json({ items: [{ ...detail, comuneResidenza: 'Roma', tesserato: false }], page: 1, pageSize: 25, totalCount: 1 })
      if (url === '/api/soci/1000' && init?.method !== 'PUT') return Response.json({ ...detail, fotoDisponibile: photoAvailable })
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf' })
      if (url === '/api/soci/1000/foto' && init?.method === 'PUT') {
        photoAvailable = true
        return new Response(null, { status: 204 })
      }
      if (url === '/api/soci/1000/foto' && init?.method === 'DELETE') {
        photoAvailable = false
        return new Response(null, { status: 204 })
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    fireEvent.click(await screen.findByRole('button', { name: '1000' }))
    await screen.findByRole('heading', { name: 'Ada Lovelace' })
    const file = new File(['image'], 'portrait.jpg', { type: 'image/jpeg' })
    fireEvent.change(screen.getByLabelText('Seleziona fotografia'), { target: { files: [file] } })
    expect(screen.getByRole('img', { name: 'Anteprima della fotografia selezionata' })).toHaveAttribute('src', 'blob:preview')
    fireEvent.click(screen.getByRole('button', { name: 'Carica fotografia' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Fotografia aggiornata.')
    expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/soci/1000/foto' && init?.method === 'PUT')).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Rimuovi fotografia' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Fotografia rimossa.')
    expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/soci/1000/foto' && init?.method === 'DELETE')).toBe(true)
  })

  it('asks before deletion, keeps the detail after cancellation and explains when relations block removal', async () => {
    const detail = {
      numeroSocio: 1000, nome: 'Ada', cognome: 'Lovelace', dataNascita: '1980-01-01',
      codiceIsoPaeseNascita: 'IT', codiceCatastaleComuneItalianoNascita: 'H501', sesso: 1, codiceFiscale: 'LVL...',
      provenienzaCodiceFiscale: 0, codiceFiscalePotenzialmenteObsoleto: false,
      cittadinanze: [{ codiceIsoPaese: 'IT', principale: true }], indirizzo: 'Via Roma 1', cap: '00100',
      codiceIsoPaeseResidenza: 'IT', codiceCatastaleComuneItalianoResidenza: 'H501', email: 'ada@example.test',
      cellulare: '+390000000', fotoDisponibile: false,
    }
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true)
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/soci/cataloghi/paesi')) return Response.json([{ codiceIsoAlpha2: 'IT', nome: 'Italia' }])
      if (url.endsWith('/soci/cataloghi/comuni')) return Response.json([{ codiceCatastale: 'H501', nome: 'Roma', provincia: 'RM' }])
      if (url.includes('/soci?')) return Response.json({ items: [{ ...detail, comuneResidenza: 'Roma', tesserato: false }], page: 1, pageSize: 25, totalCount: 1 })
      if (url === '/api/soci/1000' && init?.method !== 'DELETE') return Response.json(detail)
      if (url === '/api/security/antiforgery-token') return Response.json({ requestToken: 'csrf' })
      if (url === '/api/soci/1000' && init?.method === 'DELETE') return Response.json({ status: 409, title: 'Operazione in conflitto', detail: 'Il socio ha relazioni che impediscono l’eliminazione.' }, { status: 409 })
      throw new Error(`Unexpected request: ${url}`)
    })

    render(<SociPage />)
    fireEvent.click(await screen.findByRole('button', { name: '1000' }))
    await screen.findByRole('heading', { name: 'Ada Lovelace' })
    fireEvent.click(screen.getByRole('button', { name: 'Elimina socio' }))
    expect(screen.getByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/soci/1000' && init?.method === 'DELETE')).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Elimina socio' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Il socio ha relazioni che impediscono l’eliminazione.')
    expect(screen.getByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument()
  })
})
