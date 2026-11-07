import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import {
  sociApi, type Country, type ItalianTown, type SceltaCodiceFiscale, type SocioDetail,
  type SocioInput, type SocioPage, type SocioSearch, type TesseramentoFilter,
} from '../../api/soci'
import './soci.css'

const initialSearch: SocioSearch = { tesseramento: 0, page: 1, pageSize: 25, sortBy: 0, sortDirection: 0 }

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'La sessione è scaduta. Accedi di nuovo per continuare.'
    if (error.status === 403) return 'Il tuo account non è autorizzato a gestire i soci.'
    if (error.problem.code === 'tesseramento_non_disponibile') return 'Lo stato di tesseramento non è disponibile finché non sarà collegato il modulo acquisti e periodi.'
    if (error.status === 503) return 'Il servizio non è disponibile in questo momento. Riprova più tardi.'
    return error.problem.detail ?? error.problem.title ?? 'La richiesta non è riuscita.'
  }
  return error instanceof Error ? error.message : 'Si è verificato un errore imprevisto.'
}

function useCatalogues() {
  const [countries, setCountries] = useState<Country[]>([])
  const [towns, setTowns] = useState<ItalianTown[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void Promise.all([sociApi.countries(), sociApi.towns()]).then(([countryRows, townRows]) => {
      if (!active) return
      setCountries(countryRows); setTowns(townRows)
    }).catch(reason => { if (active) setError(errorMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])
  return { countries, towns, loading, error }
}

export function SociPage() {
  const catalogues = useCatalogues()
  const [view, setView] = useState<'list' | 'create' | 'detail' | 'edit'>('list')
  const [selected, setSelected] = useState<SocioDetail | null>(null)
  const [search, setSearch] = useState<SocioSearch>(initialSearch)
  const [text, setText] = useState('')
  const [page, setPage] = useState<SocioPage | null>(null)
  const [busy, setBusy] = useState(false)
  const [loadingList, setLoadingList] = useState(false)
  const [tesseramentoUnavailable, setTesseramentoUnavailable] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const loadPage = useCallback(async (query: SocioSearch) => {
    setLoadingList(true); setError(null); setTesseramentoUnavailable(false)
    try { setPage(await sociApi.search(query)) }
    catch (reason) {
      setPage(null)
      if (reason instanceof ApiError && reason.problem.code === 'tesseramento_non_disponibile') setTesseramentoUnavailable(true)
      else setError(errorMessage(reason))
    } finally { setLoadingList(false) }
  }, [])

  // This starts an asynchronous request; state updates happen after the request settles.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { void loadPage(search) }, [loadPage, search])

  async function openSocio(id: number) {
    setBusy(true); setError(null)
    try { setSelected(await sociApi.get(id)); setView('detail') }
    catch (reason) { setError(errorMessage(reason)) }
    finally { setBusy(false) }
  }

  async function exportAll() {
    setBusy(true); setError(null)
    try {
      const result = await sociApi.export(search)
      const url = URL.createObjectURL(result.blob)
      const link = document.createElement('a'); link.href = url
      link.download = result.fileName ?? 'ElencoSoci.xlsx'; link.click(); URL.revokeObjectURL(url)
      setNotice('Esportazione completata.')
    } catch (reason) { setError(errorMessage(reason)) }
    finally { setBusy(false) }
  }

  async function deleteSelected() {
    if (!selected || !window.confirm(`Eliminare il socio ${selected.numeroSocio} ${selected.nome} ${selected.cognome}?`)) return
    setBusy(true); setError(null)
    try { await sociApi.remove(selected.numeroSocio); setSelected(null); setView('list'); setNotice('Socio eliminato.'); await loadPage(search) }
    catch (reason) { setError(errorMessage(reason)) }
    finally { setBusy(false) }
  }

  async function submitForm(input: SocioInput) {
    setBusy(true); setError(null)
    try {
      if (view === 'edit' && selected) {
        setSelected(await sociApi.update(selected.numeroSocio, input)); setView('detail'); setNotice('Scheda aggiornata.')
      } else {
        const created = await sociApi.create(input)
        setSelected(await sociApi.get(created.numeroSocio)); setView('detail'); setNotice(`Socio ${created.numeroSocio} creato.`)
      }
    } catch (reason) { setError(errorMessage(reason)); throw reason }
    finally { setBusy(false) }
  }

  async function uploadPhoto(file: File) {
    if (!selected) return
    setBusy(true); setError(null)
    try { await sociApi.uploadPhoto(selected.numeroSocio, file); setSelected(await sociApi.get(selected.numeroSocio)); setNotice('Fotografia aggiornata.') }
    catch (reason) { setError(errorMessage(reason)) }
    finally { setBusy(false) }
  }

  async function removePhoto() {
    if (!selected) return
    setBusy(true); setError(null)
    try { await sociApi.removePhoto(selected.numeroSocio); setSelected(await sociApi.get(selected.numeroSocio)); setNotice('Fotografia rimossa.') }
    catch (reason) { setError(errorMessage(reason)) }
    finally { setBusy(false) }
  }

  const formSocio = view === 'edit' ? selected : null
  const cataloguesAvailable = !catalogues.loading && catalogues.countries.length > 0

  return <main className="members-app">
    <header className="members-header">
      <div><p className="eyebrow">MYSCICLUB · ANAGRAFICA</p><h1>Gestione soci</h1></div>
      <span className="session-badge">Area amministrativa</span>
    </header>
    {error && <div className="message message-error" role="alert">{error}</div>}
    {notice && <div className="message message-success" role="status">{notice}<button type="button" aria-label="Chiudi messaggio" onClick={() => setNotice(null)}>×</button></div>}
    {catalogues.error && <div className="message message-warning" role="status">Cataloghi geografici non disponibili: {catalogues.error}</div>}
    {!catalogues.loading && !catalogues.error && (!catalogues.countries.length || !catalogues.towns.length) &&
      <div className="message message-warning" role="status">I cataloghi geografici non sono ancora popolati. Non sono stati inseriti dati provvisori.</div>}

    {view === 'list' && <section aria-labelledby="list-title">
      <div className="section-heading"><div><h2 id="list-title">Elenco soci</h2><p>Ricerca e filtri applicati dal server.</p></div>
        <div className="toolbar"><button className="button button-secondary" type="button" disabled={busy || !page} onClick={() => void exportAll()}>Esporta Excel</button>
          <button className="button button-primary" type="button" disabled={!cataloguesAvailable} onClick={() => { setError(null); setView('create') }}>Nuovo socio</button></div>
      </div>
      <form className="search-panel" onSubmit={event => { event.preventDefault(); const next = { ...search, text, page: 1 }; setSearch(next) }}>
        <label className="search-field">Cerca socio<input value={text} onChange={event => setText(event.target.value)} placeholder="Nome, cognome, CF, numero o email" /></label>
        <label>Comune di residenza<select value={search.codiceCatastaleComune ?? ''} onChange={event => setSearch({ ...search, codiceCatastaleComune: event.target.value || undefined, page: 1 })}>
          <option value="">Tutti i comuni</option>{catalogues.towns.map(town => <option key={town.codiceCatastale} value={town.codiceCatastale}>{town.nome} ({town.provincia})</option>)}
        </select></label>
        <label>Stato tesseramento<select value={search.tesseramento} disabled={tesseramentoUnavailable} onChange={event => setSearch({ ...search, tesseramento: Number(event.target.value) as TesseramentoFilter, page: 1 })}>
          <option value={0}>Tutti</option><option value={1}>Tesserato</option><option value={2}>Non tesserato</option>
        </select></label>
        <button className="button button-primary" type="submit" disabled={loadingList}>Cerca</button>
      </form>
      {tesseramentoUnavailable && <div className="message message-warning" role="status">Elenco non disponibile: il sistema non può ancora determinare il tesseramento corrente e non mostrerà uno stato presunto.</div>}
      {loadingList && <p role="status" className="loading">Caricamento soci…</p>}
      {page && <>
        <p className="result-count">{page.totalCount} {page.totalCount === 1 ? 'socio trovato' : 'soci trovati'}</p>
        <div className="table-wrap"><table><caption className="sr-only">Soci. Nessuna fotografia è visualizzata nell'elenco.</caption>
          <thead><tr>
            <SortHeader label="Numero socio" field={2} query={search} onSort={setSearch} />
            <SortHeader label="Cognome" field={0} query={search} onSort={setSearch} />
            <SortHeader label="Nome" field={1} query={search} onSort={setSearch} />
            <SortHeader label="Codice fiscale" field={3} query={search} onSort={setSearch} />
            <SortHeader label="Email" field={4} query={search} onSort={setSearch} /><th>Comune</th><th>Tesseramento</th>
          </tr></thead>
          <tbody>{page.items.map(item => <tr key={item.numeroSocio}>
            <td><button type="button" className="text-link" onClick={() => void openSocio(item.numeroSocio)}>{item.numeroSocio}</button></td>
            <td>{item.cognome}</td><td>{item.nome}</td><td>{item.codiceFiscale}</td><td>{item.email}</td><td>{item.comuneResidenza ?? '—'}</td><td>{item.tesserato ? 'Tesserato' : 'Non tesserato'}</td>
          </tr>)}</tbody>
        </table></div>
        <div className="pagination"><label>Elementi per pagina<select value={search.pageSize} onChange={event => setSearch({ ...search, pageSize: Number(event.target.value) as 25 | 50 | 100, page: 1 })}><option>25</option><option>50</option><option>100</option></select></label>
          <span>Pagina {page.page} · {page.totalCount} risultati</span>
          <button type="button" className="button button-secondary" disabled={page.page <= 1 || loadingList} onClick={() => setSearch({ ...search, page: search.page - 1 })}>Precedente</button>
          <button type="button" className="button button-secondary" disabled={page.page * page.pageSize >= page.totalCount || loadingList} onClick={() => setSearch({ ...search, page: search.page + 1 })}>Successiva</button>
        </div>
      </>}
      {!page && !loadingList && !tesseramentoUnavailable && <p className="empty-state">Nessun risultato da mostrare.</p>}
    </section>}

    {view === 'create' && <SocioForm title="Nuovo socio" countries={catalogues.countries} towns={catalogues.towns} busy={busy} onCancel={() => setView('list')} onSubmit={submitForm} />}
    {view === 'edit' && formSocio && <SocioForm key={formSocio.numeroSocio} title="Modifica socio" initial={formSocio} countries={catalogues.countries} towns={catalogues.towns} busy={busy} onCancel={() => setView('detail')} onSubmit={submitForm} />}
    {view === 'detail' && selected && <SocioCard socio={selected} countries={catalogues.countries} towns={catalogues.towns} busy={busy}
      onBack={() => { setView('list'); void loadPage(search) }} onEdit={() => { setError(null); setView('edit') }} onDelete={() => void deleteSelected()}
      onUpload={uploadPhoto} onRemovePhoto={() => void removePhoto()} />}
  </main>
}

function SortHeader({ label, field, query, onSort }: { label: string; field: SocioSearch['sortBy']; query: SocioSearch; onSort: (query: SocioSearch) => void }) {
  const active = query.sortBy === field
  const direction = active ? query.sortDirection : 0
  return <th aria-sort={active ? direction === 0 ? 'ascending' : 'descending' : 'none'}><button className="sort-button" type="button" onClick={() => onSort({ ...query, sortBy: field, sortDirection: active && direction === 0 ? 1 : 0, page: 1 })}>{label}{active ? direction === 0 ? ' ▲' : ' ▼' : ''}</button></th>
}

function SocioCard({ socio, countries, towns, busy, onBack, onEdit, onDelete, onUpload, onRemovePhoto }: {
  socio: SocioDetail; countries: Country[]; towns: ItalianTown[]; busy: boolean; onBack: () => void; onEdit: () => void; onDelete: () => void
  onUpload: (file: File) => Promise<void>; onRemovePhoto: () => void
}) {
  const [pendingPhoto, setPendingPhoto] = useState<File | null>(null)
  const [photoPreview, setPhotoPreview] = useState<string | null>(null)
  useEffect(() => () => { if (photoPreview) URL.revokeObjectURL(photoPreview) }, [photoPreview])
  const birthplace = socio.codiceCatastaleComuneItalianoNascita ? towns.find(x => x.codiceCatastale === socio.codiceCatastaleComuneItalianoNascita)?.nome : socio.comuneEsteroNascita
  const birthCountry = countries.find(x => x.codiceIsoAlpha2 === socio.codiceIsoPaeseNascita)?.nome ?? socio.codiceIsoPaeseNascita
  const residenceCountry = countries.find(x => x.codiceIsoAlpha2 === socio.codiceIsoPaeseResidenza)?.nome ?? socio.codiceIsoPaeseResidenza
  const residenceTown = socio.codiceCatastaleComuneItalianoResidenza ? towns.find(x => x.codiceCatastale === socio.codiceCatastaleComuneItalianoResidenza) : null
  return <section className="record-view" aria-labelledby="detail-title">
    <div className="section-heading"><div><p className="eyebrow">SCHEDA SOCIO · N. {socio.numeroSocio}</p><h2 id="detail-title">{socio.nome} {socio.cognome}</h2></div>
      <div className="toolbar"><button type="button" className="button button-secondary" onClick={onBack}>Torna all'elenco</button><button type="button" className="button button-primary" onClick={onEdit}>Modifica</button><button type="button" className="button button-danger" disabled={busy} onClick={onDelete}>Elimina socio</button></div>
    </div>
    <div className="detail-grid">
      <section className="card"><h3>Dati anagrafici</h3><dl>
        <Detail label="Nome" value={socio.nome} /><Detail label="Cognome" value={socio.cognome} /><Detail label="Data di nascita" value={socio.dataNascita} />
        <Detail label="Sesso" value={socio.sesso === 0 ? 'M' : 'F'} /><Detail label="Luogo di nascita" value={[birthplace, birthCountry].filter(Boolean).join(', ')} />
      </dl></section>
      <section className="card"><h3>Codice fiscale</h3><dl><Detail label="Codice fiscale" value={socio.codiceFiscale} /><Detail label="Provenienza" value={socio.provenienzaCodiceFiscale === 0 ? 'Calcolato' : 'Manuale'} /></dl>
        {socio.codiceFiscalePotenzialmenteObsoleto && <p className="message message-warning">I dati anagrafici sono cambiati: il codice fiscale potrebbe dover essere aggiornato.</p>}
      </section>
      <section className="card"><h3>Cittadinanze</h3><ul>{socio.cittadinanze.map(c => <li key={c.codiceIsoPaese}>{countries.find(x => x.codiceIsoAlpha2 === c.codiceIsoPaese)?.nome ?? c.codiceIsoPaese}{c.principale ? ' · principale' : ''}</li>)}</ul></section>
      <section className="card"><h3>Residenza</h3><dl><Detail label="Indirizzo" value={socio.indirizzo} /><Detail label="CAP" value={socio.cap} />
        <Detail label="Comune" value={residenceTown?.nome ?? socio.provinciaOSuddivisione} /><Detail label="Provincia" value={residenceTown?.provincia} /><Detail label="Paese" value={residenceCountry} />
        <Detail label="Email" value={socio.email} /><Detail label="Cellulare" value={socio.cellulare} /></dl></section>
      <section className="card photo-card"><h3>Fotografia</h3>
        {socio.fotoDisponibile ? <img className="member-photo" src={sociApi.photoUrl(socio.numeroSocio)} alt={`Fotografia di ${socio.nome} ${socio.cognome}`} /> : <p>Nessuna fotografia caricata.</p>}
        <label>Seleziona fotografia<input type="file" accept="image/jpeg,image/png,image/webp" disabled={busy} onChange={event => {
          const file = event.target.files?.[0] ?? null
          setPendingPhoto(file)
          setPhotoPreview(file ? URL.createObjectURL(file) : null)
          event.currentTarget.value = ''
        }} /></label>
        {photoPreview && <img className="member-photo photo-preview" src={photoPreview} alt="Anteprima della fotografia selezionata" />}
        {pendingPhoto && <button type="button" className="button button-primary" disabled={busy} onClick={() => {
          const file = pendingPhoto
          setPendingPhoto(null); setPhotoPreview(null)
          void onUpload(file)
        }}>Carica fotografia</button>}
        {socio.fotoDisponibile && <button type="button" className="button button-secondary" disabled={busy} onClick={onRemovePhoto}>Rimuovi fotografia</button>}
      </section>
    </div>
  </section>
}

function Detail({ label, value }: { label: string; value?: string | null }) { return <div><dt>{label}</dt><dd>{value || '—'}</dd></div> }

function SocioForm({ title, initial, countries, towns, busy, onCancel, onSubmit }: {
  title: string; initial?: SocioDetail | null; countries: Country[]; towns: ItalianTown[]; busy: boolean
  onCancel: () => void; onSubmit: (input: SocioInput) => Promise<void>
}) {
  const [form, setForm] = useState<SocioInput>(() => initial ? {
    nome: initial.nome, cognome: initial.cognome, dataNascita: initial.dataNascita,
    codiceIsoPaeseNascita: initial.codiceIsoPaeseNascita,
    codiceCatastaleComuneItalianoNascita: initial.codiceCatastaleComuneItalianoNascita, comuneEsteroNascita: initial.comuneEsteroNascita,
    sesso: initial.sesso, cittadinanze: initial.cittadinanze.map(x => ({ ...x })), indirizzoResidenza: initial.indirizzo,
    codiceIsoPaeseResidenza: initial.codiceIsoPaeseResidenza,
    cap: initial.cap, codiceCatastaleComuneItalianoResidenza: initial.codiceCatastaleComuneItalianoResidenza,
    suddivisioneResidenza: initial.codiceCatastaleComuneItalianoResidenza ? undefined : initial.provinciaOSuddivisione,
    email: initial.email, cellulare: initial.cellulare,
    sceltaCodiceFiscale: 0,
  } : {
    nome: '', cognome: '', dataNascita: '', codiceIsoPaeseNascita: '', sesso: 0, cittadinanze: [],
    indirizzoResidenza: '', codiceIsoPaeseResidenza: '', email: '', cellulare: '', sceltaCodiceFiscale: 0,
  })
  const [manualCf, setManualCf] = useState('')
  const [citizenshipCountry, setCitizenshipCountry] = useState('')
  const [localError, setLocalError] = useState<string | null>(null)
  const birthCountry = countries.find(x => x.codiceIsoAlpha2 === form.codiceIsoPaeseNascita)
  const residenceCountry = countries.find(x => x.codiceIsoAlpha2 === form.codiceIsoPaeseResidenza)
  const birthInItaly = birthCountry?.codiceIsoAlpha2 === 'IT'
  const residenceInItaly = residenceCountry?.codiceIsoAlpha2 === 'IT'
  const birthTowns = useMemo(() => birthInItaly ? towns : [], [birthInItaly, towns])
  const residenceTowns = useMemo(() => residenceInItaly ? towns : [], [residenceInItaly, towns])
  const selectedResidenceTown = towns.find(x => x.codiceCatastale === form.codiceCatastaleComuneItalianoResidenza)
  const calculatedToForeign = Boolean(initial?.provenienzaCodiceFiscale === 0 && initial.codiceIsoPaeseNascita === 'IT' && !birthInItaly)
  const manualRequired = !initial && Boolean(birthCountry && !birthInItaly) || calculatedToForeign

  function setField<K extends keyof SocioInput>(key: K, value: SocioInput[K]) { setForm(current => ({ ...current, [key]: value })) }
  function setCountry(key: 'codiceIsoPaeseNascita' | 'codiceIsoPaeseResidenza', value: string) {
    setForm(current => ({ ...current, [key]: value,
      ...(key === 'codiceIsoPaeseNascita' ? { codiceCatastaleComuneItalianoNascita: null, comuneEsteroNascita: '' } : { codiceCatastaleComuneItalianoResidenza: null, suddivisioneResidenza: '' }) }))
  }
  function addCitizenship() {
    const code = citizenshipCountry
    if (!code || form.cittadinanze.some(x => x.codiceIsoPaese === code)) return
    setField('cittadinanze', [...form.cittadinanze, { codiceIsoPaese: code, principale: form.cittadinanze.length === 0 }]); setCitizenshipCountry('')
  }
  function setPrimary(code: string) { setField('cittadinanze', form.cittadinanze.map(c => ({ ...c, principale: c.codiceIsoPaese === code }))) }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setLocalError(null)
    if (!form.cittadinanze.length || form.cittadinanze.filter(x => x.principale).length !== 1) { setLocalError('Indica almeno una cittadinanza e selezionane una principale.'); return }
    if (!form.codiceIsoPaeseNascita || !form.codiceIsoPaeseResidenza) { setLocalError('Seleziona il Paese di nascita e quello di residenza.'); return }
    if (birthInItaly && !form.codiceCatastaleComuneItalianoNascita) { setLocalError('Seleziona il comune italiano di nascita.'); return }
    if (residenceInItaly && !form.codiceCatastaleComuneItalianoResidenza) { setLocalError('Seleziona il comune italiano di residenza.'); return }
    if ((manualRequired || form.sceltaCodiceFiscale === 2 || !initial && !birthInItaly) && !manualCf.trim()) { setLocalError('Inserisci il codice fiscale manuale richiesto.'); return }
    if (calculatedToForeign && form.sceltaCodiceFiscale !== 2) { setLocalError('Per passare da nascita italiana a estera, scegli di inserire un codice fiscale manuale valido.'); return }
    const payload: SocioInput = { ...form,
      codiceFiscaleManuale: form.sceltaCodiceFiscale === 2 || !initial && !birthInItaly ? manualCf.trim() : undefined,
      sceltaCodiceFiscale: !initial ? 0 : form.sceltaCodiceFiscale as SceltaCodiceFiscale,
    }
    try { await onSubmit(payload) } catch { /* parent provides the API error */ }
  }

  return <section className="record-form" aria-labelledby="form-title">
    <div className="section-heading"><div><p className="eyebrow">ANAGRAFICA</p><h2 id="form-title">{title}</h2></div></div>
    {localError && <p className="message message-error" role="alert">{localError}</p>}
    <form onSubmit={event => void submit(event)}>
      <fieldset><legend>Dati anagrafici</legend><div className="form-grid">
        <label>Nome<input required maxLength={100} value={form.nome} onChange={e => setField('nome', e.target.value)} /></label>
        <label>Cognome<input required maxLength={100} value={form.cognome} onChange={e => setField('cognome', e.target.value)} /></label>
        <label>Data di nascita<input type="date" required value={form.dataNascita} onChange={e => setField('dataNascita', e.target.value)} /></label>
        <label>Sesso<select value={form.sesso} onChange={e => setField('sesso', Number(e.target.value) as 0 | 1)}><option value={0}>M</option><option value={1}>F</option></select></label>
        <label>Paese di nascita<select required value={form.codiceIsoPaeseNascita} onChange={e => setCountry('codiceIsoPaeseNascita', e.target.value)}><option value="">Seleziona un Paese</option>{countries.map(c => <option key={c.codiceIsoAlpha2} value={c.codiceIsoAlpha2}>{c.nome}</option>)}</select></label>
        {birthInItaly ? <label>Comune italiano di nascita<select required value={form.codiceCatastaleComuneItalianoNascita ?? ''} onChange={e => setField('codiceCatastaleComuneItalianoNascita', e.target.value || null)}><option value="">Seleziona un comune</option>{birthTowns.map(t => <option key={t.codiceCatastale} value={t.codiceCatastale}>{t.nome} ({t.provincia})</option>)}</select></label>
          : birthCountry && <label>Comune estero di nascita<input maxLength={100} value={form.comuneEsteroNascita ?? ''} onChange={e => setField('comuneEsteroNascita', e.target.value)} /></label>}
      </div></fieldset>
      <fieldset><legend>Codice fiscale</legend>
        {initial && <p>Codice attuale: <strong>{initial.codiceFiscale}</strong> · Provenienza: {initial.provenienzaCodiceFiscale === 0 ? 'calcolato' : 'manuale'}{initial.codiceFiscalePotenzialmenteObsoleto ? ' · potenzialmente obsoleto' : ''}</p>}
        <div className="choice-list">
          <label><input type="radio" name="cfChoice" checked={form.sceltaCodiceFiscale === 0} onChange={() => setField('sceltaCodiceFiscale', 0)} />{initial ? 'Conserva il codice fiscale attuale' : 'Calcola automaticamente se la nascita è in Italia'}</label>
          {birthInItaly && <label><input type="radio" name="cfChoice" checked={form.sceltaCodiceFiscale === 1} onChange={() => setField('sceltaCodiceFiscale', 1)} />Ricalcola il codice fiscale</label>}
          <label><input type="radio" name="cfChoice" checked={form.sceltaCodiceFiscale === 2} onChange={() => setField('sceltaCodiceFiscale', 2)} />Inserisci un codice fiscale manuale</label>
        </div>
        {(form.sceltaCodiceFiscale === 2 || !initial && !birthInItaly) && <label>Codice fiscale manuale<input required minLength={16} maxLength={16} value={manualCf} onChange={e => setManualCf(e.target.value.toUpperCase())} autoCapitalize="characters" /></label>}
        {!initial && birthInItaly && form.sceltaCodiceFiscale !== 2 && <p className="helper">Il codice sarà calcolato al salvataggio usando il comune selezionato.</p>}
        {calculatedToForeign && <p className="message message-warning" role="alert">Il CF calcolato non può essere conservato per una nascita estera: è obbligatorio fornire un CF manuale valido.</p>}
      </fieldset>
      <fieldset><legend>Cittadinanze</legend><div className="citizenship-list">
        {form.cittadinanze.map(c => <div className="citizenship-row" key={c.codiceIsoPaese}><span>{countries.find(x => x.codiceIsoAlpha2 === c.codiceIsoPaese)?.nome ?? c.codiceIsoPaese}</span>
          <label><input type="radio" name="primaryCitizenship" checked={c.principale} onChange={() => setPrimary(c.codiceIsoPaese)} />Principale</label>
          <button className="button button-quiet" type="button" disabled={form.cittadinanze.length <= 1} onClick={() => setField('cittadinanze', form.cittadinanze.filter(x => x.codiceIsoPaese !== c.codiceIsoPaese))}>Rimuovi</button></div>)}
      </div><div className="inline-control"><label>Aggiungi cittadinanza<select value={citizenshipCountry} onChange={e => setCitizenshipCountry(e.target.value)}><option value="">Seleziona un Paese</option>{countries.filter(c => !form.cittadinanze.some(x => x.codiceIsoPaese === c.codiceIsoAlpha2)).map(c => <option key={c.codiceIsoAlpha2} value={c.codiceIsoAlpha2}>{c.nome}</option>)}</select></label><button type="button" className="button button-secondary" disabled={!citizenshipCountry} onClick={addCitizenship}>Aggiungi</button></div></fieldset>
      <fieldset><legend>Residenza e contatti</legend><div className="form-grid">
        <label>Indirizzo<input required maxLength={200} value={form.indirizzoResidenza} onChange={e => setField('indirizzoResidenza', e.target.value)} /></label>
        <label>Paese di residenza<select required value={form.codiceIsoPaeseResidenza} onChange={e => setCountry('codiceIsoPaeseResidenza', e.target.value)}><option value="">Seleziona un Paese</option>{countries.map(c => <option key={c.codiceIsoAlpha2} value={c.codiceIsoAlpha2}>{c.nome}</option>)}</select></label>
        {residenceInItaly ? <><label>Comune<select required value={form.codiceCatastaleComuneItalianoResidenza ?? ''} onChange={e => setField('codiceCatastaleComuneItalianoResidenza', e.target.value || null)}><option value="">Seleziona un comune</option>{residenceTowns.map(t => <option key={t.codiceCatastale} value={t.codiceCatastale}>{t.nome} ({t.provincia})</option>)}</select></label>
          <label>Provincia<input value={selectedResidenceTown?.provincia ?? ''} readOnly aria-readonly="true" /></label></>
          : residenceCountry && <label>Suddivisione territoriale<input maxLength={100} value={form.suddivisioneResidenza ?? ''} onChange={e => setField('suddivisioneResidenza', e.target.value)} /></label>}
        <label>CAP<input maxLength={20} required={Boolean(residenceInItaly)} value={form.cap ?? ''} onChange={e => setField('cap', e.target.value)} /></label>
        <label>Email<input type="email" required maxLength={254} value={form.email} onChange={e => setField('email', e.target.value)} /></label>
        <label>Cellulare<input required maxLength={50} value={form.cellulare} onChange={e => setField('cellulare', e.target.value)} /></label>
      </div></fieldset>
      <div className="form-actions"><button type="button" className="button button-secondary" disabled={busy} onClick={onCancel}>Annulla</button><button type="submit" className="button button-primary" disabled={busy || !countries.length}>{busy ? 'Salvataggio…' : 'Salva socio'}</button></div>
    </form>
  </section>
}
