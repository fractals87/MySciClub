using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Application.Abstractions.TechnicalLog;
using MySciClub.Application.Features.Members;
using MySciClub.Domain.Soci;
using MySciClub.Infrastructure.Persistence;

namespace MySciClub.Api.Tests;

internal sealed class FakeSocioApiState
{
    public readonly Paese Italy = new(1, "IT", "Italia");
    public readonly Paese Spain = new(2, "ES", "Spagna");
    public ComuneItaliano Rome => new(10, Italy, "Roma", "H501", "RM");
    public Socio? Socio { get; set; }
    public long NextId { get; set; } = 42;
    public SocioSearchQuery? LastQuery { get; set; }
    public string? PhotoKey { get; set; }
    public IReadOnlyList<string> DeletionBlockers { get; set; } = [];
    public bool TesseramentoUnavailable { get; set; }

    public FakeSocioApiState()
    {
        Reset();
    }

    public void Reset()
    {
        var residence = Residenza.Italiana("Via Roma 1", "00100", Rome);
        var data = new DatiAnagraficiSocio("Mario", "Rossi", new DateOnly(1980, 1, 1),
            LuogoNascita.InComuneItaliano("IT", 10), residence, [new Cittadinanza(1, true)],
            Sesso.M, "mario@example.test", "+390000000");
        var seeded = MySciClub.Domain.Soci.Socio.Crea(42, data, codiceCatastaleComune: "H501");
        SetId(seeded, 42);
        Socio = seeded;
        NextId = 43;
        LastQuery = null;
        PhotoKey = null;
        DeletionBlockers = [];
        TesseramentoUnavailable = false;
    }

    internal static void SetId(Socio socio, long id) =>
        typeof(Socio).GetProperty(nameof(Socio.Id))!.SetValue(socio, id);
}

internal sealed class FakeSocioRepository(FakeSocioApiState state) : ISocioRepository
{
    public Task<Socio?> GetByNumeroSocioAsync(long numeroSocio, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.Socio?.NumeroSocio == numeroSocio ? state.Socio : null);
    public Task AddAsync(Socio socio, CancellationToken cancellationToken = default)
    {
        FakeSocioApiState.SetId(socio, state.NextId++);
        state.Socio = socio;
        return Task.CompletedTask;
    }
    public Task UpdateAsync(Socio socio, CancellationToken cancellationToken = default) { state.Socio = socio; return Task.CompletedTask; }
    public Task DeleteAsync(Socio socio, CancellationToken cancellationToken = default) { state.Socio = null; return Task.CompletedTask; }
    public Task<SocioPage> SearchAsync(SocioSearchQuery query, CancellationToken cancellationToken = default)
    {
        if (state.TesseramentoUnavailable) throw new TesseramentoUnavailableException();
        state.LastQuery = query;
        return Task.FromResult(new SocioPage([], query.Page, query.PageSize, 0));
    }
    public Task<IReadOnlyList<SocioExportRow>> SearchAllAsync(SocioSearchQuery query, CancellationToken cancellationToken = default)
    {
        if (state.TesseramentoUnavailable) throw new TesseramentoUnavailableException();
        state.LastQuery = query;
        return Task.FromResult<IReadOnlyList<SocioExportRow>>([]);
    }
}

internal sealed class FakeSocioCatalog(FakeSocioApiState state) : ISocioCatalogRepository
{
    public Task<IReadOnlyList<Paese>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Paese>>([state.Italy, state.Spain]);
    public Task<IReadOnlyList<ComuneItaliano>> GetItalianTownsAsync(string? countryIsoAlpha2 = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ComuneItaliano>>(countryIsoAlpha2 is null or "IT" ? [state.Rome] : []);
    public Task<Paese?> GetCountryAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(id == 1 ? state.Italy : id == 2 ? state.Spain : null);
    public Task<ComuneItaliano?> GetItalianTownAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(id == 10 ? state.Rome : null);
    public Task<Paese?> GetCountryByIsoAlpha2Async(string isoAlpha2, CancellationToken cancellationToken = default) =>
        Task.FromResult(isoAlpha2 == "IT" ? state.Italy : isoAlpha2 == "ES" ? state.Spain : null);
    public Task<ComuneItaliano?> GetItalianTownByCadastralCodeAsync(string cadastralCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(cadastralCode == "H501" ? state.Rome : null);
}

internal sealed class FakeSocioUnitOfWork : IApplicationUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
}

internal sealed class FakeSocioAudit : IAuditService
{
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeSocioNumberGenerator : INumeroSocioGenerator
{
    private long _number = 1000;
    public Task<long> GetNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(_number++);
}

internal sealed class FakeSocioRelations(FakeSocioApiState state) : ISocioRelationChecker
{
    public Task<IReadOnlyList<string>> GetDeletionBlockersAsync(long socioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.DeletionBlockers);
}

internal sealed class FakeSocioPhotoProcessor : IPhotoProcessor
{
    public async Task<ProcessedPhoto> ProcessAsync(Stream input, string declaredContentType, CancellationToken cancellationToken = default)
    {
        var copy = new MemoryStream();
        await input.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        return new ProcessedPhoto(copy, "socio-photo.webp", "image/webp");
    }
}

internal sealed class FakeSocioFileStorage(FakeSocioApiState state) : IFileStorageService
{
    public async Task<string> StoreAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        await using var sink = new MemoryStream();
        await content.CopyToAsync(sink, cancellationToken);
        state.PhotoKey = "opaque-test-key";
        return state.PhotoKey;
    }
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(storageKey == state.PhotoKey ? new MemoryStream([1, 2, 3]) : null);
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (storageKey == state.PhotoKey) state.PhotoKey = null;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSocioExcel : IExcelExportService
{
    public Task<byte[]> CreateAsync(IReadOnlyList<SocioExportRow> rows, CancellationToken cancellationToken = default) =>
        Task.FromResult(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
}

internal sealed class FakeSocioTechnicalLog : ITechnicalLogService
{
    public Task RecordAsync(TechnicalLogEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
