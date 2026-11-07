# Ambiente di sviluppo locale

## Backend e database

Il backend usa Entity Framework Core con SQL Server. La connection string viene letta dalla chiave `ConnectionStrings:MySciClub` e deve essere configurata localmente tramite .NET User Secrets; non inserirla in `appsettings*.json`, nel repository o nei log.

Dalla cartella principale del repository, l'identificativo User Secrets è già configurato nel progetto. Per salvare la connection string senza digitare la password nella riga di comando o stamparla nell'output, usare un prompt PowerShell mascherato:

```powershell
$credential = Get-Credential -Message "Credenziali SQL Server per MySciClub"
$password = $credential.GetNetworkCredential().Password
$connectionString = "Server=localhost;Database=MySciClub;User Id=$($credential.UserName);Password=$password;TrustServerCertificate=True"
try {
    dotnet user-secrets set "ConnectionStrings:MySciClub" $connectionString --project src/backend/MySciClub.Api.csproj
}
finally {
    Remove-Variable credential, password, connectionString -ErrorAction SilentlyContinue
}
```

Inserire il nome utente SQL nel campo utente e la password nel campo password del prompt. Il valore è salvato nell'archivio User Secrets dell'utente e non nel repository. Non aggiungere `-Verbose` o trascrivere il valore della connection string.

Il tool EF Core è installato nel manifest locale del repository. Per visualizzarne la versione:

```powershell
dotnet tool restore
dotnet ef --version
```

Il `DbContext` risiede nel progetto Infrastructure; l'API è il progetto di avvio e fornisce la configurazione. Le migrazioni future vanno create esplicitando entrambi i progetti, ad esempio:

```powershell
dotnet ef migrations add <NomeMigrazione> --project src/backend/Infrastructure/MySciClub.Infrastructure.csproj --startup-project src/backend/MySciClub.Api.csproj
```

L'applicazione delle migrazioni al database è un'operazione separata e va eseguita solo quando autorizzata:

```powershell
dotnet ef database update --project src/backend/Infrastructure/MySciClub.Infrastructure.csproj --startup-project src/backend/MySciClub.Api.csproj
```

Il modello contiene al momento solo le tabelle ASP.NET Core Identity predisposte; non è stata creata alcuna migrazione. Le entità funzionali saranno aggiunte nelle fasi successive.

## Avvio locale con HTTPS e proxy same-origin

Il frontend viene servito da Vite su `https://localhost:5173`. Le richieste browser a `/api/...` sono inoltrate dal proxy Vite al backend HTTPS su `https://localhost:7289`; il browser usa quindi una sola origin e non è configurato CORS.

Il controllo del certificato di sviluppo eseguito per questa configurazione non ha trovato un certificato valido (`dotnet dev-certs https --check`). Prima del primo avvio, l'utente deve predisporre e fidare manualmente un certificato HTTPS per `localhost`. Non salvare il certificato o la chiave privata nel repository. Per il certificato di sviluppo .NET, la procedura manuale è:

1. Eseguire `dotnet dev-certs https --trust` in un terminale interattivo e completare l'eventuale dialogo di fiducia del sistema operativo.
2. Scegliere una cartella privata esterna al repository e un percorso PFX locale.
3. Esportare il certificato in quel percorso con `dotnet dev-certs https --export-path <percorso-PFX> --password <password>`; fornire una password robusta e non salvarla nel repository, in uno script condiviso o nella cronologia del terminale.
4. Verificare il certificato con `dotnet dev-certs https --check` e impostare le variabili d'ambiente sotto riportate nella sessione di sviluppo.

La password passata con `--password` può restare nella cronologia della shell. In ambienti in cui questo non è accettabile, esportare il PFX con uno strumento approvato che acquisisca la password senza inserirla nella riga di comando. Usare lo stesso PFX per il backend e per Vite. Questi passaggi sono manuali: non sono stati eseguiti durante la Fase 2A.

La configurazione Vite legge il percorso e la password esclusivamente da variabili d'ambiente del processo:

- `MYSCICLUB_DEV_HTTPS_PFX_PATH`: percorso assoluto del PFX locale.
- `MYSCICLUB_DEV_HTTPS_PFX_PASSWORD`: password del PFX.
- `MYSCICLUB_API_PROXY_TARGET`: destinazione HTTPS del proxy, opzionale; il default è `https://localhost:7289`.

In PowerShell, impostare il percorso e acquisire la password con un prompt mascherato prima di avviare Vite; sostituire il segnaposto con il percorso scelto localmente:

```powershell
$env:MYSCICLUB_DEV_HTTPS_PFX_PATH = '<percorso assoluto del PFX>'
$securePassword = Read-Host 'Password del PFX' -AsSecureString
$env:MYSCICLUB_DEV_HTTPS_PFX_PASSWORD = [System.Net.NetworkCredential]::new('', $securePassword).Password
npm.cmd run dev
Remove-Item Env:MYSCICLUB_DEV_HTTPS_PFX_PASSWORD
Remove-Variable securePassword
```

Rimuovere la variabile della password anche se Vite viene interrotto o termina con errore. Lo script `npm run dev` avvia Node con `--use-system-ca`, così il proxy valida il certificato usando lo store attendibile del sistema. Il requisito Node è `>=22.19.0`.

In due terminali, dalla root del repository:

```powershell
dotnet run --project src/backend/MySciClub.Api.csproj --launch-profile https
npm.cmd run dev
```

Il backend richiede la chiave `ConnectionStrings:MySciClub` come descritto sopra quando il `DbContext` viene utilizzato. La configurazione DI non apre connessioni SQL; le verifiche della Fase 2A non risolvono il contesto e non richiedono il database.

## Policy Identity

La configurazione Identity richiede email univoche e confermate prima dell'autenticazione. La password deve avere almeno 15 caratteri; non sono imposti requisiti di composizione per maiuscole, minuscole, cifre o simboli. Il lockout è abilitato dopo 5 tentativi falliti consecutivi e dura 15 minuti. Il login passa `lockoutOnFailure: true` a Identity; credenziali errate e account non confermati ricevono una risposta generica, mentre un account bloccato riceve HTTP 423.

## Accesso applicativo

L'API espone questi endpoint:

| Metodo e percorso | Accesso | Scopo |
| --- | --- | --- |
| `GET /api/security/antiforgery-token` | Anonimo | Memorizza il cookie antiforgery HttpOnly e restituisce il request token nel JSON. |
| `POST /api/security/login` | Anonimo, antiforgery obbligatorio | Accetta `username` e `password`, autentica tramite `SignInManager` e crea il cookie Identity. |
| `GET /api/security/session` | Anonimo | Restituisce autenticazione, nome visualizzabile e ruolo amministrativo; l'utente anonimo riceve `autenticato: false`. |
| `POST /api/security/logout` | Autenticato, antiforgery obbligatorio | Invalida il cookie Identity tramite `SignOutAsync`. |

Sequenza client: richiedere il token, inviare il login con `X-CSRF-TOKEN`, poi leggere `/session`. Logout usa un nuovo request token e il cookie same-origin. Il client non conserva credenziali o token in localStorage/sessionStorage. Il cookie `__Host-MySciClub.Auth` resta HttpOnly, Secure Always e SameSite Strict, con durata di 8 ore e sliding expiration. Un 401 durante l'uso delle API protette riporta alla pagina di accesso; un 403 non viene interpretato come scadenza sessione.

Tutte le rotte Gestione Soci richiedono il ruolo `Administrator`; un utente autenticato privo del ruolo può leggere il proprio stato sessione, ma riceve 403 dalle API soci e non accede alla UI amministrativa.

## Identificativi pubblici di Gestione Soci

Il Numero Socio è l'identificativo pubblico stabile. Dettaglio, modifica, eliminazione e fotografie usano `/api/soci/{numeroSocio}`; le fotografie sono esposte sotto `/foto` con `GET`, `PUT` e `DELETE`. I DTO non restituiscono la PK `Socio.Id`. Anche i riferimenti ai cataloghi usano codici ISO paese e codici catastali dei comuni, non le PK geografiche. Le PK restano nei repository e nella persistenza. Le esportazioni non contengono identificativi tecnici.

Non è stato predisposto alcun account amministratore e non esistono credenziali iniziali. Dopo l'autorizzazione separata dello schema Identity, il primo amministratore dovrà essere creato da un operatore autorizzato tramite una procedura di provisioning offline basata su `UserManager`, con password acquisita in modo interattivo e conferma dell'email; la procedura assegnerà il ruolo `Administrator`. Non usare seed automatici, account predefiniti o password nel repository. Registrazione pubblica, gestione account, reset e modifica password non sono implementati.

I test API simulano store e autenticazione Identity e non creano utenti persistenti. La stringa di connessione fittizia presente nella factory di test serve solo alla configurazione DI e non viene utilizzata per aprire connessioni.

## Client API e token antiforgery

Il client condiviso è `src/frontend/src/api/client.ts`. Usa percorsi relativi `/api/...` e `credentials: 'same-origin'`. Per ogni richiesta mutativa recupera prima un request token da `GET /api/security/antiforgery-token`, quindi lo invia nell'header `X-CSRF-TOKEN`. Le richieste GET e gli altri metodi HTTP sicuri non richiedono il token. Il request token è restituito come JSON; il cookie antiforgery associato resta HttpOnly, Secure e SameSite Strict.

Il filtro MVC `AutoValidateAntiforgeryTokenAttribute` protegge i metodi mutativi dei controller. Non protegge automaticamente Minimal API. Quando saranno aggiunte, configurare la relativa pipeline antiforgery (`UseAntiforgery`) e richiedere la validazione sugli endpoint che accettano dati, oppure invocare `IAntiforgery.ValidateRequestAsync` per endpoint che non sono coperti dalla validazione automatica MVC. Non rendere anonimi gli endpoint mutativi per aggirare il filtro.

## Test frontend e backend

Eseguire i test backend dalla root:

```powershell
dotnet test MySciClub.slnx
```

Eseguire build, test e lint frontend dalla cartella `src/frontend`:

```powershell
npm.cmd run build
npm.cmd test
npm.cmd run lint
```

I test d'integrazione API usano TestServer e una connection string fittizia solo per la registrazione DI; non aprono connessioni SQL e non creano database. I test frontend usano Vitest e Testing Library.
