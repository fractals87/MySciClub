using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Domain.Soci;

namespace MySciClub.Application.Features.Members;

public enum SocioErrorCode
{
    DatiNonValidi, SocioNonTrovato, CodiceFiscaleDuplicato, NumeroSocioNonAssegnabile,
    PaeseNonTrovato, ComuneNonTrovato, GeografiaNonCoerente, EliminazioneBloccata,
    NonAutorizzato, ConflittoAggiornamento, ErroreStorage, ErroreElaborazioneFoto, ErroreEsportazione
}

public sealed class SocioApplicationException(SocioErrorCode code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public SocioErrorCode Code { get; } = code;
}

public interface IAdministratorContext
{
    bool IsAdministrator { get; }
    long? AdministratorId { get; }
}

/// <summary>All enlisted repository and audit writes in an operation must commit in one transaction; on rollback the persistence adapter must clear or restore its tracked state.</summary>
public interface IApplicationUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

/// <summary>Repository writes enlist in IApplicationUnitOfWork and never commit independently.</summary>
public interface ISocioRepository
{
    Task<Socio?> GetByNumeroSocioAsync(long numeroSocio, CancellationToken cancellationToken = default);
    /// <summary>Must make the database-generated Socio.Id available before returning, while remaining enlisted in the unit-of-work transaction.</summary>
    Task AddAsync(Socio socio, CancellationToken cancellationToken = default);
    Task UpdateAsync(Socio socio, CancellationToken cancellationToken = default);
    Task DeleteAsync(Socio socio, CancellationToken cancellationToken = default);
    /// <summary>Performs partial, case-insensitive matching (including NumeroSocio formatted as text); derives Tesserato from the current period's tessera purchase. The default Cognome order uses Nome as the second ascending key.</summary>
    Task<SocioPage> SearchAsync(SocioSearchQuery query, CancellationToken cancellationToken = default);
    /// <summary>Same matching, filters, and ordering as SearchAsync, with pagination ignored.</summary>
    Task<IReadOnlyList<SocioExportRow>> SearchAllAsync(SocioSearchQuery query, CancellationToken cancellationToken = default);
}

public interface ISocioCatalogRepository
{
    Task<IReadOnlyList<Paese>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComuneItaliano>> GetItalianTownsAsync(string? countryIsoAlpha2 = null, CancellationToken cancellationToken = default);
    Task<Paese?> GetCountryAsync(long id, CancellationToken cancellationToken = default);
    Task<ComuneItaliano?> GetItalianTownAsync(long id, CancellationToken cancellationToken = default);
    Task<Paese?> GetCountryByIsoAlpha2Async(string isoAlpha2, CancellationToken cancellationToken = default);
    Task<ComuneItaliano?> GetItalianTownByCadastralCodeAsync(string cadastralCode, CancellationToken cancellationToken = default);
}

/// <summary>Implemented with a database sequence; implementations must never use MAX(numero)+1.</summary>
public interface INumeroSocioGenerator
{
    Task<long> GetNextAsync(CancellationToken cancellationToken = default);
}

public interface ISocioRelationChecker
{
    Task<IReadOnlyList<string>> GetDeletionBlockersAsync(long socioId, CancellationToken cancellationToken = default);
}

public interface IPhotoProcessor
{
    /// <summary>Validates the image bytes and returns EXIF-free WebP, max 512x512, no upscaling, quality 80.</summary>
    Task<ProcessedPhoto> ProcessAsync(Stream input, string declaredContentType, CancellationToken cancellationToken = default);
}

public sealed record ProcessedPhoto(Stream Content, string FileName, string ContentType);

public interface IExcelExportService
{
    /// <summary>Creates XLSX with Italian headers, frozen first row, column filters and suitable widths; images are excluded.</summary>
    Task<byte[]> CreateAsync(IReadOnlyList<SocioExportRow> rows, CancellationToken cancellationToken = default);
}

public enum TesseramentoFilter { Qualsiasi, Tesserato, NonTesserato }
public enum SocioSortField { Cognome, Nome, NumeroSocio, CodiceFiscale, Email }
public enum SortDirection { Ascending, Descending }

public sealed record SocioSearchQuery(
    string? Text = null,
    TesseramentoFilter Tesseramento = TesseramentoFilter.Qualsiasi,
    long? ComuneId = null,
    int Page = 1,
    int PageSize = 25,
    SocioSortField SortBy = SocioSortField.Cognome,
    SortDirection SortDirection = SortDirection.Ascending);

public sealed record SocioListItem(
    long NumeroSocio, string Nome, string Cognome, string CodiceFiscale,
    string Email, string? ComuneResidenza, bool Tesserato);
public sealed record SocioPage(IReadOnlyList<SocioListItem> Items, int Page, int PageSize, long TotalCount);

public sealed record PublicCountry(string CodiceIsoAlpha2, string Nome);
public sealed record PublicItalianTown(string CodiceCatastale, string Nome, string Provincia);
public sealed record PublicCitizenship(string CodiceIsoPaese, bool Principale);
public sealed record PublicSocioDetail(
    long NumeroSocio, string Nome, string Cognome, DateOnly DataNascita,
    string CodiceIsoPaeseNascita, string? CodiceCatastaleComuneItalianoNascita, string? ComuneEsteroNascita,
    Sesso Sesso, string CodiceFiscale, ProvenienzaCodiceFiscale ProvenienzaCodiceFiscale,
    bool CodiceFiscalePotenzialmenteObsoleto, IReadOnlyList<PublicCitizenship> Cittadinanze,
    string Indirizzo, string? Cap, string CodiceIsoPaeseResidenza,
    string? CodiceCatastaleComuneItalianoResidenza, string? ProvinciaOSuddivisione,
    string Email, string Cellulare, bool FotoDisponibile);

public sealed record PublicSocioSearch(
    string? Text, TesseramentoFilter Tesseramento, string? CodiceCatastaleComune,
    int Page, int PageSize, SocioSortField SortBy, SortDirection SortDirection);

public sealed record PublicSocioWriteInput(
    string Nome, string Cognome, DateOnly DataNascita, string CodiceIsoPaeseNascita,
    string? CodiceCatastaleComuneItalianoNascita, string? ComuneEsteroNascita, Sesso Sesso,
    IReadOnlyList<PublicCitizenship> Cittadinanze, string IndirizzoResidenza,
    string CodiceIsoPaeseResidenza, string? Cap, string? CodiceCatastaleComuneItalianoResidenza,
    string? SuddivisioneResidenza, string Email, string Cellulare,
    string? CodiceFiscaleManuale, SceltaCodiceFiscale SceltaCodiceFiscale);

public sealed record SocioExportRow(
    long NumeroSocio, string Nome, string Cognome, DateOnly DataNascita, string LuogoNascita,
    Sesso Sesso, string CodiceFiscale, string Cittadinanza, string Indirizzo, string? Cap,
    string? Comune, string? Provincia, string PaeseResidenza, string Email, string Cellulare,
    bool Tesserato);

public sealed record SocioDetail(
    long NumeroSocio, string Nome, string Cognome, DateOnly DataNascita,
    string CodiceIsoPaeseNascita, long? ComuneItalianoNascitaId, string? ComuneEsteroNascita,
    Sesso Sesso, string CodiceFiscale, ProvenienzaCodiceFiscale ProvenienzaCodiceFiscale,
    bool CodiceFiscalePotenzialmenteObsoleto, IReadOnlyList<Cittadinanza> Cittadinanze,
    string Indirizzo, string? Cap, string CodiceIsoPaeseResidenza,
    long? ComuneItalianoResidenzaId, string? ProvinciaOSuddivisione, string Email,
    string Cellulare, bool FotoDisponibile);

public sealed record CittadinanzaInput(long PaeseId, bool Principale);
public sealed record DatiSocioInput(
    string Nome, string Cognome, DateOnly DataNascita, long PaeseNascitaId,
    long? ComuneItalianoNascitaId, string? ComuneEsteroNascita, Sesso Sesso,
    IReadOnlyList<CittadinanzaInput> Cittadinanze, string IndirizzoResidenza,
    long PaeseResidenzaId, string? Cap, long? ComuneItalianoResidenzaId,
    string? SuddivisioneResidenza, string Email, string Cellulare,
    string? CodiceFiscaleManuale = null, SceltaCodiceFiscale SceltaCodiceFiscale = SceltaCodiceFiscale.Conserva);

public sealed record SocioCreated(long NumeroSocio);
public sealed record PhotoUpload(string FileName, string ContentType, Stream Content);
public sealed record PhotoDownload(Stream Content, string ContentType);
public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

internal static class SocioApplicationRules
{
    public static void RequireAdministrator(IAdministratorContext context)
    {
        if (!context.IsAdministrator) throw new SocioApplicationException(SocioErrorCode.NonAutorizzato, "Operazione riservata agli amministratori.");
    }

    public static void ValidateQuery(SocioSearchQuery query)
    {
        if (query.Page < 1 || query.PageSize is not (25 or 50 or 100) ||
            !Enum.IsDefined(query.Tesseramento) || !Enum.IsDefined(query.SortBy) || !Enum.IsDefined(query.SortDirection) ||
            query.ComuneId is <= 0)
            throw new SocioApplicationException(SocioErrorCode.DatiNonValidi, "Parametri di ricerca non validi.");
    }

    public static SocioDetail ToDetail(Socio s) => new(s.NumeroSocio, s.Dati.Nome, s.Dati.Cognome,
        s.Dati.DataNascita, s.Dati.LuogoNascita.CodiceIsoPaese, s.Dati.LuogoNascita.ComuneItalianoId,
        s.Dati.LuogoNascita.ComuneEstero, s.Dati.Sesso, s.CodiceFiscale.Valore, s.ProvenienzaCodiceFiscale,
        s.CodiceFiscalePotenzialmenteObsoleto, s.Dati.Cittadinanze, s.Dati.Residenza.Indirizzo,
        s.Dati.Residenza.Cap, s.Dati.Residenza.CodiceIsoPaese, s.Dati.Residenza.ComuneItalianoId,
        s.Dati.Residenza.ProvinciaOSuddivisione, s.Dati.Email, s.Dati.Cellulare, s.ChiaveFotografia is not null);

    public static void ValidatePhoto(PhotoUpload photo)
    {
        if (photo.Content is null || !photo.Content.CanRead ||
            photo.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new SocioApplicationException(SocioErrorCode.ErroreElaborazioneFoto, "Formato o contenuto della fotografia non valido.");
        if (photo.Content.CanSeek && photo.Content.Length > 5 * 1024 * 1024)
            throw new SocioApplicationException(SocioErrorCode.ErroreElaborazioneFoto, "La fotografia supera il limite di 5 MB.");
        if (string.IsNullOrWhiteSpace(photo.FileName))
            throw new SocioApplicationException(SocioErrorCode.ErroreElaborazioneFoto, "Nome file fotografia non valido.");
    }
}
