# Istruzioni del progetto MySciClub

## Contesto e architettura

MySciClub è un'applicazione gestionale. Il backend è una Web API ASP.NET Core in C# su .NET 10; il frontend è realizzato con React, TypeScript e Vite. Il repository contiene inoltre una solution .NET, test e documentazione.

Mantieni una architettura a livelli e separa le responsabilità: ogni componente deve restare nel livello a cui appartiene. Mantieni le dipendenze orientate verso le astrazioni e non introdurre accoppiamenti tra interfaccia utente, API, logica applicativa e persistenza.

## Convenzioni e qualità

- Segui le convenzioni già adottate nel file e nel progetto interessati. In generale, usa PascalCase per tipi e membri pubblici C#, camelCase per variabili e funzioni TypeScript e nomi descrittivi per componenti React.
- Preferisci implementazioni semplici, leggibili, tipizzate e coerenti con gli strumenti già presenti. Evita duplicazioni e modifiche non pertinenti.
- Non introdurre pacchetti o dipendenze senza una motivazione concreta legata al requisito; esplicita l'impatto e valuta prima gli strumenti già disponibili.
- Non implementare funzionalità o modifiche che non siano richieste dall'utente.

## Sicurezza, errori e logging

- Valida gli input ai confini del sistema e applica il principio del minimo privilegio. Non esporre segreti, dati personali, stack trace o dettagli interni nelle risposte verso il client.
- Gestisci gli errori in modo coerente e prevedibile; non nascondere eccezioni o errori con fallback silenziosi.
- Usa il logging strutturato già disponibile, con contesto utile e senza registrare credenziali, token o dati sensibili. Evita log ridondanti.

## Test e documentazione

- Quando una modifica funzionale è richiesta, aggiungi o aggiorna test pertinenti nei progetti di test esistenti e verifica il comportamento interessato con gli strumenti già configurati. Non aggiungere framework di test o dipendenze senza motivazione.
- Aggiorna la documentazione esistente quando una modifica cambia contratti, configurazione o procedure d'uso. Mantienila concisa e coerente con il comportamento effettivo.
- Distingui chiaramente le verifiche eseguite da quelle non eseguite e segnala eventuali limiti.

## Coordinamento e ambito

- Prima di una modifica che attraversa più livelli (per esempio frontend, API e persistenza), descrivi al coordinatore/agente principale il piano, i contratti coinvolti e le responsabilità dei livelli; attendi il coordinamento prima di modificare i file interessati.
- Gli agenti specialistici operano solo nella propria specializzazione. Prima di toccare file condivisi o appartenenti a un altro livello devono coordinarsi con l'agente principale.
- QA e reviewer privilegiano analisi e verifica indipendente; non modificano file a meno che l'agente principale non richieda esplicitamente un intervento mirato.
