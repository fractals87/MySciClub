# MySciClub -- Specifica funzionale v0.3

**Data:** 2 novembre 2026\
**Stato:** bozza consolidata per revisione\
**Percorso previsto nel repository:** `docs/SPECIFICA_FUNZIONALE.md`

## 1. Scopo e principi

MySciClub è un'applicazione web per la gestione amministrativa e
operativa di uno Sci Club: soci, tesseramenti, catalogo stagionale,
pratiche, pagamenti, contabilità, crediti, corsi, gite, pullman,
comodati e documenti.

Principi: - separazione tra socio e account, beneficiario e responsabile
del pagamento, acquisto e movimento finanziario; - attribuzione delle
operazioni al periodo amministrativo in base alla data effettiva; -
conservazione degli snapshot economici degli acquisti; - configurabilità
dei dati gestionali; - tracciabilità delle operazioni; - possibilità di
operazioni retroattive anche sui periodi chiusi, con conferme e
segnalazioni; - ogni schermata con dati tabellari deve offrire export
Excel `.xlsx`, rispettando i filtri e includendo tutti i risultati
filtrati, non soltanto la pagina corrente.

## 2. Utenti, account e audit

### Ruoli e account

-   Nel MVP esiste il ruolo Amministratore, con permessi uniformi. Il
    modello deve poter supportare futuri ruoli differenziati.
-   Ogni amministratore ha un account personale, collegato a un socio ma
    distinto dall'anagrafica. Non sono previsti account condivisi.
-   I soci ordinari non hanno credenziali.
-   L'account viene creato da un socio esistente e attivato tramite link
    email per impostare la password. È previsto il recupero password via
    email.
-   Gli account si disattivano/riattivano, non si eliminano fisicamente.
    La disattivazione impedisce l'accesso ma non elimina socio e
    storico.
-   Non è possibile disattivare l'ultimo amministratore attivo.
-   Email limitate ad attivazione e recupero password; nessuna
    comunicazione ai soci.
-   L'architettura deve poter supportare in futuro federazione/SSO
    WordPress, non inclusa nel MVP.

### Audit e accessi

Ogni operazione rilevante registra utente, data/ora, tipo operazione,
entità e ID, valori precedenti e successivi ove applicabile. Data
effettiva e timestamp di registrazione sono distinti.

Registro accessi separato per login riusciti/falliti, timestamp, account
e IP. I log non scadono automaticamente; ogni amministratore può
cancellarli manualmente per intervallo, previa conferma con conteggio.
La cancellazione è registrata in un registro tecnico separato.

La schermata audit filtra per date, amministratore, tipo operazione,
entità e ID; mostra il dettaglio old/new e offre export Excel.

### Dashboard e ricerca

Dashboard personalizzabile per amministratore (widget selezionabili,
ordinabili e disposizione salvata). Aree: situazione economica,
pratiche/pagamenti, attività/partecipanti/capienza, alert operativi e
documentali. Gli alert sono informativi e navigano alla schermata
pertinente.

Ricerca globale disponibile ovunque per soci (nome, cognome, CF, numero
socio), pratiche, gite/corsi/eventi e comodati. Risultati raggruppati
per tipo e con navigazione diretta.

## 3. Anagrafica soci

### Dati e codice fiscale

Campi: nome, cognome, data e comune di nascita, indirizzo e comune di
residenza, cittadinanza, sesso M/F, codice fiscale, email, cellulare,
numero socio e fotografia.

Non si mantiene uno storico delle variazioni anagrafiche: il valore
corrente sovrascrive il precedente. L'audit registra la modifica ma non
costituisce uno storico anagrafico consultabile.

Il CF è calcolato automaticamente ma modificabile manualmente
(omocodia/casi particolari); deve essere univoco. Non è prevista
verifica ufficiale Agenzia Entrate. Se cambiano i dati usati per il
calcolo, il sistema segnala che il CF potrebbe dover essere aggiornato;
se era stato corretto manualmente non lo sovrascrive senza scelta
dell'amministratore.

Numero socio numerico, progressivo, univoco, assegnato automaticamente e
mai riutilizzato. Nessun controllo/avviso di duplicazione su nome e
cognome.

### Foto, tesseramento e storico

Foto caricabile da file o scattabile direttamente dal browser previa
autorizzazione. Anteprima, sostituzione e rimozione. Uso anche da
mobile/tablet, compatibilmente con browser e HTTPS. La foto appare
soltanto nella scheda anagrafica, non nelle tabelle o nella ricerca.

Il socio resta sempre in anagrafica: nessun automatismo di archiviazione
o disattivazione. È tesserato nel periodo corrente solo se ha acquistato
la Tessera Sci Club per quel periodo; altrimenti è un socio non
tesserato. Nessuna relazione familiare permanente:
genitore/figlio/accompagnatore si registra nelle singole operazioni.

La scheda mostra dati correnti e storico di tesseramenti, acquisti,
pratiche, corsi, gite, partecipazioni, pagamenti e crediti, distinguendo
socio beneficiario e responsabile del pagamento.

Ricerca per nome, cognome, CF, numero socio ed email. Filtri anche per
tesseramento e comune. Export Excel filtrato. Nessun PDF scheda, nessuna
importazione massiva, nessun CSV.

### Cancellazione e GDPR

Socio con attività/storico: niente cancellazione fisica; eventuale
anonimizzazione solo se compatibile con obblighi di conservazione e
minimizzazione. Socio senza attività o relazioni può essere eliminato
fisicamente. Cancellazioni/anonimizzazioni tracciate. I dati economici
necessari alla ricostruzione dello storico si conservano nei limiti di
legge.

## 4. Periodi amministrativi

Entità `PeriodoAmministrativo` (in UI può essere chiamato stagione): ID,
nome (es. 2026/2027), data inizio/fine, stato, data creazione, soglia
Under/Over. Intervallo semiaperto `DataInizio <= data < DataFine`; date
configurabili, non hardcoded.

Un periodo può essere preparato in anticipo. L'apertura del nuovo
periodo non chiude automaticamente il precedente. La chiusura è
esplicita e guidata. Il periodo chiuso resta modificabile
retroattivamente.

Le operazioni appartengono al periodo determinato dalla data effettiva,
non dal timestamp di inserimento. Data effettiva proposta con oggi ma
modificabile. Le operazioni retroattive ricalcolano saldo finale del
periodo e saldi iniziali/finali di tutti i periodi successivi; niente
movimenti compensativi artificiali.

I conti sono condivisi tra periodi. Ogni periodo ha saldo
iniziale/finale per conto. All'apertura si propone il saldo finale
precedente come saldo iniziale, da confermare o modificare. La modifica
del saldo iniziale ricalcola i saldi successivi senza alterare i
movimenti ed è auditata.

La chiusura riepiloga saldi, pratiche non saldate/parziali, entrate,
uscite, risultato e saldi finali. Si può chiudere anche con pratiche non
saldate, mostrate in evidenza.

Una modifica a periodo chiuso lo marca "Modificato dopo la chiusura",
ricalcola saldi/report e genera alert in dashboard e dettaglio. È
richiesta nuova verifica/presa visione con utente/data/ora; non è una
nuova chiusura e non cancella la chiusura originaria.
Modifica/cancellazione movimento in periodo chiuso richiede conferma
aggiuntiva con periodo, movimento, saldo prima/dopo e impatti sui
periodi successivi.

## 5. Catalogo prodotti e servizi

Catalogo, prezzi, varianti e configurazioni sono specifici del periodo.
È possibile duplicare il catalogo/configurazioni di una stagione
precedente, senza copiare pratiche, acquisti, pagamenti, partecipanti,
iscrizioni effettive o assegnazioni. Copia indipendente.

Prodotti iniziali: Tessera Sci Club, Corsi Adulti, Corsi Bambini,
Assicurazione Sci, Servizio Accompagnatore, Accompagnatore + Corso Sci,
Cena sociale, Comodato divisa, Gita domenicale.

Varianti per differenze commerciali (Under/Over, Adulto/Bambino,
Pullman/Auto), non per workflow differenti. Prezzo per
prodotto/variante; modifiche valide solo per nuovi acquisti. Ogni
acquisto conserva snapshot di descrizione, prodotto/variante, prezzo
listino, prezzo applicato, categoria e CDC. L'amministratore può
modificare liberamente il prezzo applicato della singola riga; nessuna
motivazione obbligatoria; audit del prima/dopo. Prodotto mai utilizzato
eliminabile; se utilizzato, solo disattivabile.

Categorie e centri di costo sono distinti: una categoria appartiene a un
solo CDC, un CDC aggrega più categorie. La categoria descrive la natura
economica, il CDC raggruppa. Categorie e CDC condivisi tra periodi e
configurabili (crea/modifica/disattiva/riattiva); non eliminabili
fisicamente se usati. Modifica associazione categoria-CDC valida per
nuove operazioni; le esistenti conservano lo snapshot.

### Documenti configurati per prodotto

Ogni prodotto può definire documenti richiesti, obbligatori o opzionali,
con regola di validità (es. periodo amministrativo). Ogni acquisto
genera adempimento associato alla singola riga e al beneficiario.
Documento mancante è solo segnalazione: mai blocco di
acquisto/registrazione/pagamento.

## 6. Tessera e assicurazione

Esiste un solo prodotto Tessera Sci Club, con prezzi Under/Over
configurati sul prodotto. Omaggio consentito: snapshot prezzo listino,
prezzo applicato zero e flag omaggio. Under/Over calcolato sull'età del
beneficiario alla data effettiva di acquisto, con soglia configurabile.
Amministratore può modificare prezzo o omaggiare.

Una sola tessera per socio/periodo. Acquistabile solo per periodo
corrente. Il beneficiario deve avere tessera corrente prima di
acquistare altri prodotti; la regola riguarda il beneficiario, non il
pagante. Nella stessa pratica la tessera già inserita soddisfa il
prerequisito.

La tessera non può essere eliminata se il socio ha altri acquisti nello
stesso periodo, anche in altre pratiche e non pagati: prima rimuovere
gli acquisti dipendenti.

Assicurazione acquistabile una volta per beneficiario/periodo e valida
fino a fine periodo. Nuova stagione richiede nuovo tesseramento e nuova
assicurazione se desiderata.

## 7. Pratiche e acquisti

Una pratica contiene più righe e beneficiari, ma un solo responsabile
del pagamento, che può essere diverso dai beneficiari. Data effettiva
proposta con oggi e modificabile; periodo derivato dalla data.

Ogni riga è un prodotto/variante per un beneficiario. Nessun campo
quantità: due beneficiari significano due righe. Ogni riga conserva
snapshot prodotto, variante, descrizione, prezzo listino/applicato,
categoria, CDC, beneficiario e dati specifici.

Pratica: numero, data effettiva, periodo, pagante, righe, totale,
pagamenti, crediti applicati, residuo e stato calcolato (da
saldare/parzialmente saldata/saldata). Residuo attribuito al pagante.

Pratiche modificabili anche dopo pagamenti e in periodi chiusi, con
audit/conferme: - aggiunta/modifica/eliminazione righe; - cambio
beneficiario (rimuove partecipazioni vecchie e crea nuove, rivalida
regole e pullman; non cambia automaticamente importi/pagamenti); -
cambio pagante; - cambio prodotto/variante (sostituzione completa,
applica regole nuovo prodotto e prezzo corrente, ricalcola totale,
richiede conferma); - cambio data effettiva (riassegna periodo, rivalida
tessera/assicurazione/compatibilità, ricalcola contabilità; non cambia
automaticamente prodotto o prezzo).

Eliminare una riga tratta l'acquisto come mai avvenuto: rimuove
partecipazione, riduce totale e riallinea pagamenti; niente uscita,
rimborso o credito automatico. Quando il totale diminuisce, preserva
prima crediti applicati, poi riduce pagamenti in denaro dal più recente.
Se il nuovo totale è inferiore ai crediti applicati, riduce prima
l'utilizzo dei crediti e poi i pagamenti.

Eliminare una pratica elimina righe e pagamenti senza movimenti
compensativi; ripristina ai titolari originari i crediti usati e
cancella i crediti generati dalla pratica; rimuove
partecipazioni/assegnazioni. Se un credito generato è già stato
utilizzato, l'eliminazione dell'origine è bloccata finché l'utilizzo non
è ripristinato o rimborsato. Operazioni atomiche e audit. Conferma con
riepilogo di righe, beneficiari, importi, pagamenti, crediti,
partecipazioni e pullman.

## 8. Pagamenti e contabilità

Pratica e movimento finanziario sono separati. Una pratica può avere più
pagamenti, in date, conti e metodi diversi; i pagamenti sono collegati
alla pratica intera, non alle righe. Stato e residuo sono calcolati.

Metodi iniziali: contanti, POS, assegno, bonifico. Metodo distinto dal
conto (es. conto BNP, metodo bonifico). Metodi configurabili e
disattivabili; non eliminabili se utilizzati.

Conti configurabili con descrizione, tipo cassa/banca e stato.
Creabili/modificabili/disattivabili/riattivabili. Conto mai usato e
senza riferimenti eliminabile; se usato, solo disattivabile.
Disattivazione consentita anche con saldo non zero; non selezionabile
per nuovi movimenti ma conservato nello storico. Conti condivisi tra
periodi.

Un solo conto predefinito tra quelli disponibili. Se disattivato,
occorre sceglierne un altro. È proposto per entrate/uscite indipendenti
ma modificabile.

Entrate indipendenti (sponsor, contributi, donazioni) e uscite
indipendenti sono consentite. Le uscite si registrano solo quando
pagate: niente debiti fornitori, impegni o scadenze future. Campi:
importo, data, conto, metodo, categoria obbligatoria, CDC derivato,
descrizione; evento opzionale. Nessun numero
fattura/riferimento/allegato obbligatorio. Registrazione genera subito
movimento e aggiorna saldo. Modifica/cancellazione consentita anche su
periodo chiuso con audit, ricalcolo e conferma aggiuntiva.

Trasferimento interno: uscita conto origine + entrata conto
destinazione, collegate come un unico trasferimento. Stesso
importo/data; escluso da ricavi/costi. Data/importo modificabili;
cancellazione elimina entrambi senza compensazioni. Ammesso saldo
negativo sul conto sorgente. Descrizione facoltativa. Nessuna gestione
commissioni bancarie nel MVP.

Schermata movimenti unificata (entrate, uscite, trasferimenti), filtri
per periodo/date, tipo, conto, metodo, categoria, CDC, descrizione;
ordinamento per data/importo; dettaglio/modifica/cancellazione.
Riepilogo filtrato: entrate, uscite, risultato economico, trasferimenti
separati, saldi conti. Distinguere risultato economico e liquidità.
Export Excel.

## 9. Crediti

Credito personale del responsabile del pagamento, non del beneficiario.
Annullamento della sola partecipazione a una gita mantenendo acquisto
genera credito al pagante. Il credito è utilizzabile per acquisti futuri
a beneficio di qualsiasi socio, anche parzialmente, non scade e passa
tra stagioni.

In nuova pratica il credito disponibile del pagante è mostrato; uso
facoltativo, deciso dall'amministratore, non automatico. Una pratica può
utilizzare più crediti di origini diverse. Ogni utilizzo conserva
origine, titolare, importo, pratica di destinazione e data. Uso credito
riduce il residuo ma non genera incasso.

Rimborso consentito entro il credito disponibile: registra titolare,
importo, data, conto e metodo; riduce credito e genera uscita
finanziaria. Storico completo di generazione, uso, ripristino e
rimborso.

Eliminazione pratica che ha usato crediti li ripristina ai titolari
originari; eliminazione pratica che li ha generati cancella i crediti.
Se i crediti sono già usati, bloccare eliminazione finché non
ripristinati o rimborsati.

## 10. Gite, corsi, eventi e pullman

### Prodotto ed evento

Il prodotto/servizio acquistabile è distinto dall'evento a calendario.
Una gita può avere più eventi; ogni acquisto seleziona un evento. Un
corso è un prodotto con più lezioni. Adesioni inserite solo dagli
amministratori; nessun self-service.

Scheda gita senza campi accessori di ritrovo, orari o note. Dati: data,
destinazione, prodotto, partecipanti, pullman e capienza.

### Gite

Annullamento gita mantiene storico, iscrizioni, pratiche e pagamenti, ma
genera un credito per ogni acquisto interessato, intestato al
responsabile del pagamento. Richiede conferma con numero
partecipanti/acquisti, totale crediti e titolari. Non modifica
automaticamente assegnazioni pullman.

Gita annullata riattivabile: storna i crediti generati, ma solo se tutti
sono ancora interamente disponibili. Se uno è stato utilizzato anche
parzialmente o rimborsato, riattivazione bloccata fino alla risoluzione.
Conferma e audit.

Annullamento di singolo evento/lezione di corso è una casistica separata
da specificare: non applicare regole gita.

Data e destinazione modificabili dopo iscrizioni; nessuna modifica a
prezzi, importi, pagamenti o snapshot. Audit.

### Pullman

Ogni evento può avere uno o più pullman, ciascuno con
identificativo/descrizione e capienza. Gita domenicale parte con un
pullman predefinito; corsi bambini inizialmente quattro pullman. Corsi
adulti seguono lo stesso modello.

Ogni partecipante è assegnato a un pullman. Per corsi l'assegnazione è
per l'intero corso, uguale per tutte le date. Nessuna lista d'attesa.
Capienza superata: avviso visivo ma inserimento consentito.
Sovraccapienza calcolata dalle assegnazioni.

Capienza, numero pullman e assegnazione partecipante modificabili in
qualsiasi momento. Mantenere assegnazioni esistenti quando possibile;
pullman con partecipanti non rimovibile prima della riassegnazione
manuale. Nessuno spostamento automatico. Ricalcolare capienza/alert.
Nessun effetto su pratiche/pagamenti. Audit generale sufficiente,
nessuno storico dedicato.

### Corsi: calendario e prezzi

La configurazione del prodotto corso è la fonte centrale del calendario
stagionale. Durante la stagione: - aggiungere data: tutti gli iscritti
esistenti al prodotto vengono associati alla nuova lezione; - modificare
data: aggiornare tutte le partecipazioni; - rimuovere data: eliminare le
partecipazioni relative per tutti gli iscritti, senza credito e senza
modificare pratiche/importi/pagamenti; - modificare prezzo: vale solo
per nuovi acquisti; precedenti invariati.

Assegnazioni pullman esistenti mantenute; nuove partecipazioni ereditano
l'assegnazione del corso. Ricalcolare capienze/alert.

### Livello

Il livello appartiene al beneficiario nella specifica partecipazione,
non al prodotto corso. Livelli iniziali configurabili: Giallo-base,
Verde-secondo, Blu-terzo, Rosso-quarto, Nero-quinto. Amministratore
assegna e può modificare in qualsiasi momento; nessun impatto economico.
In iscrizione a nuovo corso mostrare, se esistente, il livello del corso
della stagione precedente come riferimento; non assegnarlo
automaticamente.

### Accompagnatori

Servizio Accompagnatore e Accompagnatore + Corso Sci richiedono il
minore accompagnato. Accompagnatore + Corso Sci comprende corso bambino
e partecipazione dell'accompagnatore agli eventi. Genitore può
accompagnare più figli: una sola partecipazione effettiva e un solo
posto per evento/giorno; ogni relazione genitore-figlio è registrata.
Proporre il pullman assegnato ai figli ma consentire scelta diversa: non
è un vincolo rigido.

### Presenze e stampa

Rilevazione presenze esclusa dal MVP. Prevista stampa/esportazione PDF
elenco partecipanti per pullman; layout/template da definire
successivamente. Excel resta previsto per ogni tabella.

## 11. Documentazione

La gestione documentale è configurata per prodotto, non è un archivio
generico nella scheda socio. Per ogni prodotto si definiscono documenti
obbligatori/opzionali e validità. L'acquisto crea adempimento sulla
singola riga e beneficiario.

Stati: da ricevere; ricevuto ma non caricato; caricato. Un solo file per
adempimento. Consultazione/download da scheda socio tramite acquisti,
pratica e schermata documentale. File sostituibile/eliminabile con
audit. Nessun limite funzionale configurabile di formato o dimensione;
restano necessari controlli tecnici di sicurezza e affidabilità.

Certificato medico Corso Bambini obbligatorio: - adempimento per ogni
riga e bambino; - due bambini = due righe e due certificati; - validità
per Periodo Amministrativo, senza scadenza autonoma; - nuovo acquisto in
nuova stagione richiede nuovo certificato; - mancanza non blocca
acquisto/registrazione: solo segnalazione.

Schermata controllo con filtri periodo, prodotto, beneficiario, tipo
documento e stato. Evidenziare documenti obbligatori mancanti/non
caricati e consentire apertura socio/acquisto. Dashboard con widget
numero adempimenti incompleti e link al controllo.

## 12. Comodati divise

Prodotto Comodato divisa con cauzione indicativa 50 euro. Ogni comodato
registra socio, data consegna, taglia tuta, taglia pantaloni, cauzione,
pratica e stato. Completo indivisibile, nessun reso parziale; uno stesso
socio può avere più comodati aperti.

Restituzione completa e rimborso contestuali. Alla conferma proporre
uscita finanziaria con conto Cassa, metodo Contanti e importo cauzione
predefiniti; amministratore può cambiare importo, conto e metodo.
Rimborso collegato al comodato, non ripetibile. Stato restituito solo a
rimborso confermato. Rimborso cauzione non è normale costo del club.

Comodati non restituiti restano aperti oltre la stagione, senza
movimenti automatici. Possibile chiusura esplicita come non
restituito/cauzione trattenuta: cauzione già incassata riclassificata
come ricavo senza nuovo incasso.

Schermata centralizzata con priorità aperti, ricerca socio e filtri
periodo/socio/stato; mostra taglie, consegna, cauzione, restituzione e
rimborso. Nessuno storico dettagliato necessario nella scheda socio.

## 13. Report, export e seed

Report per periodo, prodotto, categoria, CDC, operazione e beneficiario.
Distinguere beneficiario e responsabile pagamento, risultato economico e
liquidità, crediti, stati pratiche, documenti mancanti, partecipanti e
capienze.

Regola export: ogni tabella offre `.xlsx`, rispetta i filtri e comprende
tutti i risultati filtrati anche oltre la pagina corrente. No CSV. No
PDF scheda socio. PDF elenchi partecipanti previsto con template da
definire.

Seed: 1. strutturale: categorie, CDC, relazioni, metodi pagamento, tipi
conto e configurazioni base; versionato/idempotente e non sovrascrive
personalizzazioni; 2. demo/test: dati fittizi per soci, periodi,
prodotti, pratiche, pagamenti, movimenti, crediti, attività, pullman e
comodati; solo Development/Test, mai produzione automatica.

EF Core migrations gestiscono lo schema; seed gestisce dati iniziali.
Distribuzione controllata.

## 14. Esclusioni MVP

-   account per soci ordinari e portale self-service;
-   ruoli differenziati;
-   SSO WordPress effettivo;
-   email massive/newsletter/comunicazioni soci;
-   rilevazione presenze;
-   importazione massiva soci;
-   CSV;
-   PDF scheda socio;
-   debiti fornitori, impegni e scadenze future;
-   commissioni bancarie;
-   regole di annullamento singolo evento corso (da definire
    separatamente);
-   template PDF elenchi partecipanti (da definire).

## 15. Punti da dettagliare

Prima dell'implementazione delle relative funzioni vanno precisati: -
stati e transizioni del PeriodoAmministrativo; - trattamento contabile
della cauzione trattenuta; - annullamento di singola lezione/evento di
corso; - template PDF partecipanti; - formati file effettivamente
accettati e controlli di sicurezza, pur senza limiti funzionali di
formato/dimensione; - regole di conservazione/anonimizzazione da
validare legalmente; - dettaglio report e formule; - casi limite quando
si modifica la composizione di un corso con iscritti già presenti.

Questi punti sono aperti e non vanno considerati decisioni già
approvate.

------------------------------------------------------------------------

**Nota:** questa specifica consolida le decisioni funzionali, ma non
autorizza la creazione del database, l'applicazione di migrazioni o
modifiche all'ambiente SQL Server.
