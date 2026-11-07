# Infrastructure — Gestione Soci

## Cataloghi geografici

`Paesi` and `ComuniItaliani` are mapped as reference tables. This phase does not seed them. Candidate authoritative sources for a separately approved import are the [ISTAT current administrative-unit list](https://www.istat.it/classificazione/codici-dei-comuni-delle-province-e-delle-regioni/) for commune/province names and identifiers, and the [Agenzia delle Entrate ARCOM archive](https://arcom.agenziaentrate.gov.it/CitizenArCom/) for cadastral codes. These sources must be reconciled before import: an administrative municipality is not always itself a cadastral municipality. No synthetic rows or import process are included. Country source/version and the stable-ID policy still require approval.

## Database model

The SQL Server model declares `dbo.SeqNumeroSocio` starting at 1 and incrementing by 1. The sequence is only model metadata here; no database or migration has been created. Gaps are expected. `Soci.Id` is an identity PK; `NumeroSocio` and normalized CF have unique indexes. A SQL Server rowversion protects updates. Status of tessera is deliberately absent from `Soci`.

Writes and audit rows use one scoped `MySciClubDbContext` and one relational transaction. New socio insertion performs `SaveChanges` while that transaction is open so the generated identity is available for its audit entry. Any failure rolls back and clears the change tracker. Audit records are append-only through `IAuditService`; this layer exposes no update/delete audit operation.

## Tesseramento and deletion relations

There is no Tesseramento purchase/period model in the repository. `ICurrentTesseramentoSource` is the isolated integration contract. Its current registration fails explicitly; searches and exports requiring the derived status cannot be served until the future Tesseramenti module provides it. This avoids silently treating every socio as not tesserato. The relation checker currently finds no member activity tables because none exist; it must be extended as each activity module introduces relations and restrictive FKs.

## Photograph storage and recovery

Set `Storage:MemberPhotosRoot` to an absolute directory outside the repository. The fallback is the current user's LocalApplicationData directory. Objects receive opaque random keys under `photos/`; physical paths and keys are internal to Infrastructure. Writes go to a same-directory temporary file and are atomically renamed. Temporary files are removed on ordinary failures.

Replacement stores the new file first, then the Application unit of work commits the new reference and audit. If that unit of work fails, Application attempts to delete the new object and leaves the former database reference intact. A process crash in the gap can leave an unreferenced object. After commit, failure to delete the old object is logged. `IPhotoStorageMaintenance.RemoveOrphansAsync` removes unreferenced files and old temporary files after a caller-selected age; schedule it with a 24-hour minimum age so it cannot race an in-flight replacement. The host must provide that schedule in a later composition/deployment phase.

## Image dependency

`SkiaSharp` 3.119.2 is used for JPEG/PNG/WebP decoding and WebP encoding; its NuGet listing states MIT licensing and it is cross-platform. Linux native assets are included for Linux hosts; the core package supplies Win32 assets. Re-encoding decoded pixels drops source EXIF and other metadata. No Excel package is used; XLSX is written with BCL ZIP and XML APIs.
