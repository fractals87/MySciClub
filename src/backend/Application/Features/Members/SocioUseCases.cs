using System.Text.Json;
using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Application.Abstractions.TechnicalLog;
using MySciClub.Domain.Soci;

namespace MySciClub.Application.Features.Members;

public sealed class CreateSocioUseCase(
    IAdministratorContext administrator, ISocioRepository repository, ISocioCatalogRepository catalog,
    INumeroSocioGenerator numbers, IApplicationUnitOfWork unitOfWork, IAuditService audit)
{
    public async Task<SocioCreated> ExecuteAsync(DatiSocioInput input, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        var (data, cadastralCode) = await ResolveAsync(input, catalog, ct);
        long number;
        try { number = await numbers.GetNextAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new SocioApplicationException(SocioErrorCode.NumeroSocioNonAssegnabile, "Non è stato possibile assegnare il numero socio.", ex); }
        if (number <= 0) throw new SocioApplicationException(SocioErrorCode.NumeroSocioNonAssegnabile, "Il generatore ha restituito un numero socio non valido.");
        Socio socio;
        try { socio = Socio.Crea(number, data, input.CodiceFiscaleManuale, cadastralCode); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw Invalid(ex); }
        return await unitOfWork.ExecuteAsync(async token =>
        {
            try
            {
                await repository.AddAsync(socio, token);
                await audit.RecordAsync(Entry("Create", socio, null, administrator.AdministratorId), token);
                return new SocioCreated(socio.NumeroSocio);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { throw MapPersistence(ex); }
        }, ct);
    }

    internal static async Task<(DatiAnagraficiSocio Data, string? CadastralCode)> ResolveAsync(
        DatiSocioInput input, ISocioCatalogRepository catalog, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var birthCountry = await catalog.GetCountryAsync(input.PaeseNascitaId, ct) ??
            throw new SocioApplicationException(SocioErrorCode.PaeseNonTrovato, "Paese di nascita non trovato.");
        LuogoNascita birth;
        string? cadastral = null;
        if (birthCountry.CodiceIsoAlpha2 == "IT")
        {
            if (input.ComuneItalianoNascitaId is not > 0 || !string.IsNullOrWhiteSpace(input.ComuneEsteroNascita))
                throw new SocioApplicationException(SocioErrorCode.GeografiaNonCoerente, "Per una nascita in Italia selezionare un comune italiano.");
            var town = await catalog.GetItalianTownAsync(input.ComuneItalianoNascitaId.Value, ct) ??
                throw new SocioApplicationException(SocioErrorCode.ComuneNonTrovato, "Comune di nascita non trovato.");
            if (town.PaeseId != birthCountry.Id) throw new SocioApplicationException(SocioErrorCode.GeografiaNonCoerente, "Il comune di nascita non appartiene al Paese selezionato.");
            birth = LuogoNascita.InComuneItaliano(birthCountry.CodiceIsoAlpha2, town.Id);
            cadastral = town.CodiceCatastale;
        }
        else
        {
            if (input.ComuneItalianoNascitaId is not null) throw new SocioApplicationException(SocioErrorCode.GeografiaNonCoerente, "Il comune italiano non è valido per il Paese di nascita selezionato.");
            birth = LuogoNascita.Estero(birthCountry.CodiceIsoAlpha2, input.ComuneEsteroNascita);
        }

        var residenceCountry = await catalog.GetCountryAsync(input.PaeseResidenzaId, ct) ??
            throw new SocioApplicationException(SocioErrorCode.PaeseNonTrovato, "Paese di residenza non trovato.");
        Residenza residence;
        if (residenceCountry.CodiceIsoAlpha2 == "IT")
        {
            if (input.ComuneItalianoResidenzaId is not > 0 || !string.IsNullOrWhiteSpace(input.SuddivisioneResidenza))
                throw new SocioApplicationException(SocioErrorCode.GeografiaNonCoerente, "Per una residenza in Italia selezionare un comune italiano.");
            var town = await catalog.GetItalianTownAsync(input.ComuneItalianoResidenzaId.Value, ct) ??
                throw new SocioApplicationException(SocioErrorCode.ComuneNonTrovato, "Comune di residenza non trovato.");
            if (town.PaeseId != residenceCountry.Id) throw new SocioApplicationException(SocioErrorCode.GeografiaNonCoerente, "Il comune di residenza non appartiene al Paese selezionato.");
            residence = Residenza.Italiana(input.IndirizzoResidenza, input.Cap ?? string.Empty, town);
        }
        else
        {
            if (input.ComuneItalianoResidenzaId is not null) throw new SocioApplicationException(SocioErrorCode.GeografiaNonCoerente, "Il comune italiano non è valido per il Paese di residenza selezionato.");
            residence = Residenza.Estera(input.IndirizzoResidenza, residenceCountry.CodiceIsoAlpha2, input.Cap, input.SuddivisioneResidenza);
        }

        var citizenships = new List<Cittadinanza>();
        foreach (var citizenship in input.Cittadinanze ?? throw new SocioApplicationException(SocioErrorCode.DatiNonValidi, "Elenco cittadinanze obbligatorio."))
        {
            if (await catalog.GetCountryAsync(citizenship.PaeseId, ct) is null)
                throw new SocioApplicationException(SocioErrorCode.PaeseNonTrovato, "Paese della cittadinanza non trovato.");
            citizenships.Add(new Cittadinanza(citizenship.PaeseId, citizenship.Principale));
        }
        try
        {
            var data = new DatiAnagraficiSocio(input.Nome, input.Cognome, input.DataNascita, birth, residence,
                citizenships, input.Sesso, input.Email, input.Cellulare);
            return (data, cadastral);
        }
        catch (ArgumentException ex) { throw Invalid(ex); }
    }

    internal static AuditEntry Entry(string operation, Socio socio, Socio? previous, long? administratorId = null) =>
        new(administratorId, DateTimeOffset.UtcNow, null, operation, "Socio", socio.Id.ToString(),
            previous is null ? null : JsonSerializer.Serialize(new { previous.NumeroSocio, previous.Dati.Nome, previous.Dati.Cognome, previous.CodiceFiscale.Valore }),
            JsonSerializer.Serialize(new { socio.NumeroSocio, socio.Dati.Nome, socio.Dati.Cognome, socio.CodiceFiscale.Valore }));

    internal static SocioApplicationException Invalid(Exception ex) => new(SocioErrorCode.DatiNonValidi, "I dati del socio non sono validi.", ex);
    internal static Exception MapPersistence(Exception ex) => ex is SocioApplicationException ? ex : ex switch
    {
        DuplicateCodiceFiscaleException => new SocioApplicationException(SocioErrorCode.CodiceFiscaleDuplicato, "Il codice fiscale è già associato a un socio.", ex),
        NumeroSocioNonAssegnabileException => new SocioApplicationException(SocioErrorCode.NumeroSocioNonAssegnabile, "Il numero socio non è assegnabile.", ex),
        SocioConcurrencyException => new SocioApplicationException(SocioErrorCode.ConflittoAggiornamento, "Il socio è stato modificato da un'altra operazione.", ex),
        _ => ex
    };
}

/// <summary>Infrastructure adapters translate database uniqueness and concurrency failures into these contracts.</summary>
public sealed class DuplicateCodiceFiscaleException : Exception { }
public sealed class SocioConcurrencyException : Exception { }
public sealed class NumeroSocioNonAssegnabileException : Exception { }

public sealed class GetSocioUseCase(IAdministratorContext administrator, ISocioRepository repository)
{
    public async Task<SocioDetail> ExecuteAsync(long numeroSocio, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        var socio = await repository.GetByNumeroSocioAsync(numeroSocio, ct) ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
        return SocioApplicationRules.ToDetail(socio);
    }
}

public sealed class SocioCatalogUseCases(ISocioCatalogRepository catalog)
{
    public async Task<IReadOnlyList<PublicCountry>> GetCountriesAsync(CancellationToken ct = default) =>
        (await catalog.GetCountriesAsync(ct)).Select(x => new PublicCountry(x.CodiceIsoAlpha2, x.Nome)).ToArray();

    public async Task<IReadOnlyList<PublicItalianTown>> GetItalianTownsAsync(string? countryIsoAlpha2, CancellationToken ct = default) =>
        (await catalog.GetItalianTownsAsync(countryIsoAlpha2, ct))
            .Select(x => new PublicItalianTown(x.CodiceCatastale, x.Nome, x.Provincia)).ToArray();

    public async Task<SocioSearchQuery> ResolveSearchAsync(PublicSocioSearch request, CancellationToken ct = default)
    {
        long? townId = null;
        if (request.CodiceCatastaleComune is not null)
            townId = (await catalog.GetItalianTownByCadastralCodeAsync(request.CodiceCatastaleComune, ct))?.Id ?? 0;
        return new SocioSearchQuery(request.Text, request.Tesseramento, townId,
            request.Page, request.PageSize, request.SortBy, request.SortDirection);
    }

    public async Task<DatiSocioInput> ResolveWriteInputAsync(PublicSocioWriteInput input, CancellationToken ct = default)
    {
        var birthCountryId = (await catalog.GetCountryByIsoAlpha2Async(input.CodiceIsoPaeseNascita, ct))?.Id ?? 0;
        var residenceCountryId = (await catalog.GetCountryByIsoAlpha2Async(input.CodiceIsoPaeseResidenza, ct))?.Id ?? 0;
        long? birthTownId = null;
        if (input.CodiceCatastaleComuneItalianoNascita is not null)
            birthTownId = (await catalog.GetItalianTownByCadastralCodeAsync(input.CodiceCatastaleComuneItalianoNascita, ct))?.Id ?? 0;
        long? residenceTownId = null;
        if (input.CodiceCatastaleComuneItalianoResidenza is not null)
            residenceTownId = (await catalog.GetItalianTownByCadastralCodeAsync(input.CodiceCatastaleComuneItalianoResidenza, ct))?.Id ?? 0;
        var citizenships = new List<CittadinanzaInput>();
        foreach (var citizenship in input.Cittadinanze)
        {
            var countryId = (await catalog.GetCountryByIsoAlpha2Async(citizenship.CodiceIsoPaese, ct))?.Id ?? 0;
            citizenships.Add(new CittadinanzaInput(countryId, citizenship.Principale));
        }
        return new DatiSocioInput(input.Nome, input.Cognome, input.DataNascita, birthCountryId,
            birthTownId, input.ComuneEsteroNascita, input.Sesso, citizenships,
            input.IndirizzoResidenza, residenceCountryId, input.Cap, residenceTownId,
            input.SuddivisioneResidenza, input.Email, input.Cellulare, input.CodiceFiscaleManuale,
            input.SceltaCodiceFiscale);
    }

    public async Task<PublicSocioDetail> ToPublicDetailAsync(SocioDetail detail, CancellationToken ct = default)
    {
        var countries = (await catalog.GetCountriesAsync(ct)).ToDictionary(x => x.Id, x => x.CodiceIsoAlpha2);
        var birthTown = detail.ComuneItalianoNascitaId is long birthId
            ? await catalog.GetItalianTownAsync(birthId, ct) ?? throw new SocioApplicationException(SocioErrorCode.ComuneNonTrovato, "Comune di nascita non trovato.")
            : null;
        var residenceTown = detail.ComuneItalianoResidenzaId is long residenceId
            ? await catalog.GetItalianTownAsync(residenceId, ct) ?? throw new SocioApplicationException(SocioErrorCode.ComuneNonTrovato, "Comune di residenza non trovato.")
            : null;
        try
        {
            return new PublicSocioDetail(detail.NumeroSocio, detail.Nome, detail.Cognome, detail.DataNascita,
                detail.CodiceIsoPaeseNascita, birthTown?.CodiceCatastale, detail.ComuneEsteroNascita,
                detail.Sesso, detail.CodiceFiscale, detail.ProvenienzaCodiceFiscale,
                detail.CodiceFiscalePotenzialmenteObsoleto,
                detail.Cittadinanze.Select(x => new PublicCitizenship(countries[x.PaeseId], x.Principale)).ToArray(),
                detail.Indirizzo, detail.Cap, detail.CodiceIsoPaeseResidenza, residenceTown?.CodiceCatastale,
                detail.ProvinciaOSuddivisione, detail.Email, detail.Cellulare, detail.FotoDisponibile);
        }
        catch (KeyNotFoundException ex)
        {
            throw new SocioApplicationException(SocioErrorCode.PaeseNonTrovato, "I riferimenti geografici della scheda non sono validi.", ex);
        }
    }
}

public sealed class UpdateSocioUseCase(
    IAdministratorContext administrator, ISocioRepository repository, ISocioCatalogRepository catalog,
    IApplicationUnitOfWork unitOfWork, IAuditService audit)
{
    public async Task<SocioDetail> ExecuteAsync(long numeroSocio, DatiSocioInput input, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var socio = await repository.GetByNumeroSocioAsync(numeroSocio, token) ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
            var previous = SocioApplicationRules.ToDetail(socio);
            var (data, cadastral) = await CreateSocioUseCase.ResolveAsync(input, catalog, token);
            try { socio.AggiornaDati(data, input.SceltaCodiceFiscale, input.CodiceFiscaleManuale,
                input.SceltaCodiceFiscale == SceltaCodiceFiscale.Ricalcola ? cadastral : null); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw CreateSocioUseCase.Invalid(ex); }
            try
            {
                await repository.UpdateAsync(socio, token);
                await audit.RecordAsync(new AuditEntry(administrator.AdministratorId, DateTimeOffset.UtcNow, null, "Update", "Socio", socio.Id.ToString(),
                    JsonSerializer.Serialize(previous), JsonSerializer.Serialize(SocioApplicationRules.ToDetail(socio))), token);
                return SocioApplicationRules.ToDetail(socio);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { throw CreateSocioUseCase.MapPersistence(ex); }
        }, ct);
    }
}

public sealed class SearchSociUseCase(IAdministratorContext administrator, ISocioRepository repository)
{
    public async Task<SocioPage> ExecuteAsync(SocioSearchQuery query, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        SocioApplicationRules.ValidateQuery(query);
        return await repository.SearchAsync(query with { Text = string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim() }, ct);
    }
}

public sealed class DeleteSocioUseCase(
    IAdministratorContext administrator, ISocioRepository repository, ISocioRelationChecker relations,
    IApplicationUnitOfWork unitOfWork, IAuditService audit)
{
    public Task ExecuteAsync(long numeroSocio, CancellationToken ct = default) => unitOfWork.ExecuteAsync(async token =>
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        var socio = await repository.GetByNumeroSocioAsync(numeroSocio, token) ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
        var blockers = await relations.GetDeletionBlockersAsync(socio.Id, token);
        if (blockers.Count != 0) throw new SocioApplicationException(SocioErrorCode.EliminazioneBloccata, "Il socio ha relazioni che impediscono l'eliminazione.");
        await repository.DeleteAsync(socio, token);
        await audit.RecordAsync(new AuditEntry(administrator.AdministratorId, DateTimeOffset.UtcNow, null, "Delete", "Socio", socio.Id.ToString(),
            JsonSerializer.Serialize(new { socio.NumeroSocio, socio.Dati.Nome, socio.Dati.Cognome }), null), token);
        return true;
    }, ct);
}

public sealed class ManageSocioPhotoUseCases(
    IAdministratorContext administrator, ISocioRepository repository, IPhotoProcessor processor,
    IFileStorageService storage, IApplicationUnitOfWork unitOfWork, IAuditService audit, ITechnicalLogService technicalLog)
{
    public async Task UploadAsync(long numeroSocio, PhotoUpload photo, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        SocioApplicationRules.ValidatePhoto(photo);
        await using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await photo.Content.ReadAsync(buffer, ct)) > 0)
        {
            if (bounded.Length + read > 5 * 1024 * 1024) throw new SocioApplicationException(SocioErrorCode.ErroreElaborazioneFoto, "La fotografia supera il limite di 5 MB.");
            await bounded.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        bounded.Position = 0;
        ProcessedPhoto converted;
        try { converted = await processor.ProcessAsync(bounded, photo.ContentType, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new SocioApplicationException(SocioErrorCode.ErroreElaborazioneFoto, "Impossibile elaborare la fotografia.", ex); }
        if (converted.ContentType != "image/webp") { await converted.Content.DisposeAsync(); throw new SocioApplicationException(SocioErrorCode.ErroreElaborazioneFoto, "Il processore deve restituire una fotografia WebP."); }
        string newKey;
        try { await using var content = converted.Content; newKey = await storage.StoreAsync(content, "socio-photo.webp", converted.ContentType, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new SocioApplicationException(SocioErrorCode.ErroreStorage, "Impossibile salvare la fotografia.", ex); }
        string? oldKey = null;
        try
        {
            oldKey = await unitOfWork.ExecuteAsync(async token =>
            {
                var socio = await repository.GetByNumeroSocioAsync(numeroSocio, token) ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
                var previous = socio.ChiaveFotografia;
                socio.ImpostaChiaveFotografia(newKey);
                await repository.UpdateAsync(socio, token);
                await audit.RecordAsync(new AuditEntry(administrator.AdministratorId, DateTimeOffset.UtcNow, null, previous is null ? "PhotoUpload" : "PhotoReplace", "Socio", socio.Id.ToString(), previous, newKey), token);
                return previous;
            }, ct);
        }
        catch
        {
            await TryDeleteAsync(newKey, numeroSocio, ct);
            throw;
        }
        if (oldKey is not null) await TryDeleteAsync(oldKey, numeroSocio, ct);
    }

    public async Task RemoveAsync(long numeroSocio, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        var oldKey = await unitOfWork.ExecuteAsync(async token =>
        {
            var socio = await repository.GetByNumeroSocioAsync(numeroSocio, token) ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
            var previous = socio.ChiaveFotografia;
            if (previous is null) return null;
            socio.ImpostaChiaveFotografia(null);
            await repository.UpdateAsync(socio, token);
            await audit.RecordAsync(new AuditEntry(administrator.AdministratorId, DateTimeOffset.UtcNow, null, "PhotoRemove", "Socio", socio.Id.ToString(), previous, null), token);
            return previous;
        }, ct);
        if (oldKey is not null) await TryDeleteAsync(oldKey, numeroSocio, ct);
    }

    public async Task<PhotoDownload> DownloadAsync(long numeroSocio, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        var socio = await repository.GetByNumeroSocioAsync(numeroSocio, ct) ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
        if (socio.ChiaveFotografia is null) throw new SocioApplicationException(SocioErrorCode.ErroreStorage, "Il socio non ha una fotografia.");
        try { return new PhotoDownload(await storage.OpenReadAsync(socio.ChiaveFotografia, ct) ?? throw new IOException(), "image/webp"); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new SocioApplicationException(SocioErrorCode.ErroreStorage, "Fotografia non disponibile.", ex); }
    }

    private async Task TryDeleteAsync(string key, long numeroSocio, CancellationToken ct)
    {
        try { await storage.DeleteAsync(key, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await technicalLog.RecordAsync(new TechnicalLogEntry(DateTimeOffset.UtcNow, "Error", "SocioPhotoOrphanCleanupFailed", $"Cleanup failed for socio {numeroSocio}.", null), ct);
        }
    }
}

public sealed class ExportSociUseCase(
    IAdministratorContext administrator, ISocioRepository repository, IExcelExportService excel,
    IApplicationUnitOfWork unitOfWork, IAuditService audit)
{
    public async Task<ExportFile> ExecuteAsync(SocioSearchQuery query, CancellationToken ct = default)
    {
        SocioApplicationRules.RequireAdministrator(administrator);
        SocioApplicationRules.ValidateQuery(query);
        var rows = await repository.SearchAllAsync(query, ct);
        try
        {
            var bytes = await excel.CreateAsync(rows, ct);
            await unitOfWork.ExecuteAsync(async token =>
            {
                await audit.RecordAsync(new AuditEntry(administrator.AdministratorId, DateTimeOffset.UtcNow, null, "Export", "Socio", "*",
                    null, JsonSerializer.Serialize(new { query.Text, query.Tesseramento, query.ComuneId, query.SortBy, query.SortDirection, Count = rows.Count })), token);
                return true;
            }, ct);
            var stamp = DateTimeOffset.UtcNow;
            return new ExportFile($"ElencoSoci_{stamp:yyyyMMdd_HHmm}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes);
        }
        catch (SocioApplicationException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new SocioApplicationException(SocioErrorCode.ErroreEsportazione, "Impossibile esportare l'elenco soci.", ex); }
    }
}
