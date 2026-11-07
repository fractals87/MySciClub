using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Application.Abstractions.TechnicalLog;
using MySciClub.Application.Features.Members;
using MySciClub.Domain.Soci;
using Xunit;

namespace MySciClub.Application.Tests;

public sealed class SocioUseCasesTests
{
    [Fact]
    public async Task Create_assigns_sequence_resolves_catalog_and_audits()
    {
        var f = new Fixture();
        var result = await new CreateSocioUseCase(f.Admin, f.Repository, f.Catalog, f.Numbers, f.Unit, f.Audit).ExecuteAsync(f.ItalianInput());
        Assert.Equal(71, result.NumeroSocio);
        Assert.Equal(1, f.Numbers.Calls);
        Assert.Equal("H501", f.Repository.LastAdded!.Dati.LuogoNascita.InItalia ? "H501" : null);
        Assert.Single(f.Audit.Entries);
        Assert.Equal("Create", f.Audit.Entries[0].Operation);
    }

    [Fact]
    public async Task Create_foreign_birth_requires_and_preserves_manual_cf()
    {
        var f = new Fixture();
        var input = f.ItalianInput() with { PaeseNascitaId = 2, ComuneItalianoNascitaId = null, ComuneEsteroNascita = "Lione", CodiceFiscaleManuale = "RSSMRA85C01H501D" };
        var result = await new CreateSocioUseCase(f.Admin, f.Repository, f.Catalog, f.Numbers, f.Unit, f.Audit).ExecuteAsync(input);
        Assert.Equal(71, result.NumeroSocio);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, f.Repository.LastAdded!.ProvenienzaCodiceFiscale);
    }

    [Fact]
    public async Task Create_rejects_inconsistent_geography_before_allocating_number()
    {
        var f = new Fixture();
        var input = f.ItalianInput() with { PaeseNascitaId = 2 };
        var error = await Assert.ThrowsAsync<SocioApplicationException>(() => new CreateSocioUseCase(f.Admin, f.Repository, f.Catalog, f.Numbers, f.Unit, f.Audit).ExecuteAsync(input));
        Assert.Equal(SocioErrorCode.GeografiaNonCoerente, error.Code);
        Assert.Equal(0, f.Numbers.Calls);
        Assert.Empty(f.Audit.Entries);
    }

    [Fact]
    public async Task Search_preserves_filters_and_validates_page_size()
    {
        var f = new Fixture();
        var query = new SocioSearchQuery("  ross  ", TesseramentoFilter.Tesserato, 5, 2, 50, SocioSortField.Nome, SortDirection.Descending);
        await new SearchSociUseCase(f.Admin, f.Repository).ExecuteAsync(query);
        Assert.Equal("ross", f.Repository.LastQuery!.Text);
        Assert.Equal(TesseramentoFilter.Tesserato, f.Repository.LastQuery.Tesseramento);
        Assert.Equal(50, f.Repository.LastQuery.PageSize);
        var error = await Assert.ThrowsAsync<SocioApplicationException>(() => new SearchSociUseCase(f.Admin, f.Repository).ExecuteAsync(query with { PageSize = 30 }));
        Assert.Equal(SocioErrorCode.DatiNonValidi, error.Code);
    }

    [Fact]
    public async Task Delete_is_blocked_when_related_records_exist()
    {
        var f = new Fixture(); f.Repository.Seed(f.CreateExisting()); f.Relations.Blockers = ["Tessera"];
        var error = await Assert.ThrowsAsync<SocioApplicationException>(() => new DeleteSocioUseCase(f.Admin, f.Repository, f.Relations, f.Unit, f.Audit).ExecuteAsync(70));
        Assert.Equal(SocioErrorCode.EliminazioneBloccata, error.Code);
        Assert.False(f.Repository.Deleted);
        Assert.Empty(f.Audit.Entries);
    }

    [Fact]
    public async Task Delete_without_relations_is_physical_and_audited()
    {
        var f = new Fixture(); f.Repository.Seed(f.CreateExisting());
        await new DeleteSocioUseCase(f.Admin, f.Repository, f.Relations, f.Unit, f.Audit).ExecuteAsync(70);
        Assert.True(f.Repository.Deleted);
        Assert.Equal("Delete", Assert.Single(f.Audit.Entries).Operation);
    }

    [Fact]
    public async Task Update_keeps_manual_cf_when_conserving_and_flags_possible_obsolescence()
    {
        var f = new Fixture();
        var socio = f.CreateExisting("RSSMRA85C01H501D"); f.Repository.Seed(socio);
        var input = f.ItalianInput() with { Cognome = "Rossi Bianchi", SceltaCodiceFiscale = SceltaCodiceFiscale.Conserva };
        var result = await new UpdateSocioUseCase(f.Admin, f.Repository, f.Catalog, f.Unit, f.Audit).ExecuteAsync(70, input);
        Assert.Equal("RSSMRA85C01H501D", result.CodiceFiscale);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, result.ProvenienzaCodiceFiscale);
        Assert.True(result.CodiceFiscalePotenzialmenteObsoleto);
        Assert.Equal("Update", Assert.Single(f.Audit.Entries).Operation);
    }

    [Fact]
    public async Task Update_rejects_calculated_cf_on_Italy_to_foreign_change_without_persisting()
    {
        var f = new Fixture(); var socio = f.CreateExisting(); f.Repository.Seed(socio);
        var input = f.ItalianInput() with { PaeseNascitaId = 2, ComuneItalianoNascitaId = null, ComuneEsteroNascita = "Lione", SceltaCodiceFiscale = SceltaCodiceFiscale.Conserva };
        var error = await Assert.ThrowsAsync<SocioApplicationException>(() => new UpdateSocioUseCase(f.Admin, f.Repository, f.Catalog, f.Unit, f.Audit).ExecuteAsync(70, input));
        Assert.Equal(SocioErrorCode.DatiNonValidi, error.Code);
        Assert.Equal("IT", f.Repository.Get(10)!.Dati.LuogoNascita.CodiceIsoPaese);
        Assert.Equal(0, f.Repository.UpdateCalls);
        Assert.Empty(f.Audit.Entries);
    }

    [Fact]
    public async Task Update_allows_Italy_to_foreign_only_with_explicit_valid_manual_cf()
    {
        var f = new Fixture(); f.Repository.Seed(f.CreateExisting());
        var input = f.ItalianInput() with { PaeseNascitaId = 2, ComuneItalianoNascitaId = null, ComuneEsteroNascita = "Lione", CodiceFiscaleManuale = "RSSMRA85C01H501D", SceltaCodiceFiscale = SceltaCodiceFiscale.ImpostaManuale };
        var result = await new UpdateSocioUseCase(f.Admin, f.Repository, f.Catalog, f.Unit, f.Audit).ExecuteAsync(70, input);
        Assert.Equal("FR", result.CodiceIsoPaeseNascita);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, result.ProvenienzaCodiceFiscale);
    }

    [Fact]
    public async Task Failed_photo_storage_does_not_change_existing_photo_reference()
    {
        var f = new Fixture(); var socio = f.CreateExisting(); socio.ImpostaChiaveFotografia("old-key"); f.Repository.Seed(socio); f.Storage.FailStore = true;
        var useCase = f.PhotoUseCase(new PassthroughProcessor());
        await Assert.ThrowsAsync<SocioApplicationException>(() => useCase.UploadAsync(70, new PhotoUpload("photo.png", "image/png", new MemoryStream([1, 2, 3]))));
        Assert.Equal("old-key", f.Repository.Get(10)!.ChiaveFotografia);
        Assert.Empty(f.Audit.Entries);
    }

    [Fact]
    public async Task Photo_replacement_commits_new_reference_then_removes_old_object()
    {
        var f = new Fixture(); var socio = f.CreateExisting(); socio.ImpostaChiaveFotografia("old-key"); f.Repository.Seed(socio);
        await f.PhotoUseCase(new PassthroughProcessor()).UploadAsync(70, new PhotoUpload("photo.jpg", "image/jpeg", new MemoryStream([1, 2])));
        Assert.Equal("new-key", f.Repository.Get(10)!.ChiaveFotografia);
        Assert.Contains("old-key", f.Storage.Deleted);
        Assert.Equal("PhotoReplace", Assert.Single(f.Audit.Entries).Operation);
    }

    [Fact]
    public async Task Photo_removal_clears_reference_and_audits()
    {
        var f = new Fixture(); var socio = f.CreateExisting(); socio.ImpostaChiaveFotografia("old-key"); f.Repository.Seed(socio);
        await f.PhotoUseCase(new PassthroughProcessor()).RemoveAsync(70);
        Assert.Null(f.Repository.Get(10)!.ChiaveFotografia);
        Assert.Contains("old-key", f.Storage.Deleted);
        Assert.Equal("PhotoRemove", Assert.Single(f.Audit.Entries).Operation);
    }

    [Fact]
    public async Task Export_uses_all_rows_and_records_audit()
    {
        var f = new Fixture(); f.Repository.ExportRows = [new(71, "Mario", "Rossi", new(1985, 3, 1), "Roma", Sesso.M, "RSSMRA85C01H501D", "Italiana", "Via Roma", "00100", "Roma", "RM", "Italia", "m@example.it", "123", true)];
        var result = await new ExportSociUseCase(f.Admin, f.Repository, new FakeExcel(), f.Unit, f.Audit).ExecuteAsync(new SocioSearchQuery("mario", TesseramentoFilter.Tesserato));
        Assert.Single(f.Repository.ExportRows);
        Assert.StartsWith("ElencoSoci_", result.FileName);
        Assert.Equal("Export", Assert.Single(f.Audit.Entries).Operation);
    }

    [Fact]
    public async Task Non_admin_cannot_access_member_use_cases()
    {
        var f = new Fixture(); f.Admin.IsAdministrator = false;
        var error = await Assert.ThrowsAsync<SocioApplicationException>(() => new GetSocioUseCase(f.Admin, f.Repository).ExecuteAsync(10));
        Assert.Equal(SocioErrorCode.NonAutorizzato, error.Code);
    }

    private sealed class Fixture
    {
        public readonly FakeAdmin Admin = new(); public readonly FakeRepository Repository = new(); public readonly FakeCatalog Catalog = new();
        public readonly FakeNumber Numbers = new(); public readonly FakeUnit Unit = new(); public readonly FakeAudit Audit = new(); public readonly FakeRelations Relations = new(); public readonly FakeStorage Storage = new();
        public Fixture() { Catalog.Countries[1] = new(1, "IT", "Italia"); Catalog.Countries[2] = new(2, "FR", "Francia"); Catalog.Towns[5] = new(5, Catalog.Countries[1], "Roma", "H501", "RM"); }
        public DatiSocioInput ItalianInput() => new("Mario", "Rossi", new(1985, 3, 1), 1, 5, null, Sesso.M,
            [new(1, true)], "Via Roma 1", 1, "00100", 5, null, "mario@example.it", "3331234567");
        public Socio CreateExisting(string? manualCf = null)
        {
            var socio = Socio.Crea(70, new DatiAnagraficiSocio("Mario", "Rossi", new(1985, 3, 1), LuogoNascita.InComuneItaliano("IT", 5),
                Residenza.Italiana("Via Roma 1", "00100", Catalog.Towns[5]), [new(1, true)], Sesso.M, "mario@example.it", "3331234567"), manualCf, "H501");
            typeof(Socio).GetProperty(nameof(Socio.Id))!.SetValue(socio, 10L);
            return socio;
        }
        public ManageSocioPhotoUseCases PhotoUseCase(IPhotoProcessor processor) => new(Admin, Repository, processor, Storage, Unit, Audit, new FakeLog());
    }
    private sealed class FakeAdmin : IAdministratorContext { public bool IsAdministrator { get; set; } = true; public long? AdministratorId => 7; }
    private sealed class FakeNumber : INumeroSocioGenerator { public int Calls; public Task<long> GetNextAsync(CancellationToken ct = default) { Calls++; return Task.FromResult(71L); } }
    private sealed class FakeCatalog : ISocioCatalogRepository
    {
        public readonly Dictionary<long, Paese> Countries = []; public readonly Dictionary<long, ComuneItaliano> Towns = [];
        public Task<IReadOnlyList<Paese>> GetCountriesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Paese>>(Countries.Values.ToArray());
        public Task<IReadOnlyList<ComuneItaliano>> GetItalianTownsAsync(string? countryIsoAlpha2 = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ComuneItaliano>>(Towns.Values.Where(x => countryIsoAlpha2 is null || Countries[x.PaeseId].CodiceIsoAlpha2 == countryIsoAlpha2).ToArray());
        public Task<Paese?> GetCountryAsync(long id, CancellationToken ct = default) => Task.FromResult(Countries.GetValueOrDefault(id));
        public Task<ComuneItaliano?> GetItalianTownAsync(long id, CancellationToken ct = default) => Task.FromResult(Towns.GetValueOrDefault(id));
        public Task<Paese?> GetCountryByIsoAlpha2Async(string isoAlpha2, CancellationToken ct = default) => Task.FromResult(Countries.Values.SingleOrDefault(x => x.CodiceIsoAlpha2 == isoAlpha2));
        public Task<ComuneItaliano?> GetItalianTownByCadastralCodeAsync(string cadastralCode, CancellationToken ct = default) => Task.FromResult(Towns.Values.SingleOrDefault(x => x.CodiceCatastale == cadastralCode));
    }
    private sealed class FakeRepository : ISocioRepository
    {
        private readonly Dictionary<long, Socio> _items = []; public Socio? LastAdded; public SocioSearchQuery? LastQuery; public bool Deleted; public int UpdateCalls; public IReadOnlyList<SocioExportRow> ExportRows = [];
        public void Seed(Socio socio) => _items[socio.Id] = socio;
        public Socio? Get(long id) => _items.GetValueOrDefault(id);
        public Task<Socio?> GetByNumeroSocioAsync(long numeroSocio, CancellationToken ct = default) => Task.FromResult(_items.Values.SingleOrDefault(x => x.NumeroSocio == numeroSocio));
        public Task AddAsync(Socio socio, CancellationToken ct = default) { LastAdded = socio; _items[socio.Id] = socio; return Task.CompletedTask; }
        public Task UpdateAsync(Socio socio, CancellationToken ct = default) { UpdateCalls++; return Task.CompletedTask; }
        public Task DeleteAsync(Socio socio, CancellationToken ct = default) { Deleted = true; _items.Remove(socio.Id); return Task.CompletedTask; }
        public Task<SocioPage> SearchAsync(SocioSearchQuery query, CancellationToken ct = default) { LastQuery = query; return Task.FromResult(new SocioPage([], query.Page, query.PageSize, 0)); }
        public Task<IReadOnlyList<SocioExportRow>> SearchAllAsync(SocioSearchQuery query, CancellationToken ct = default) { LastQuery = query; return Task.FromResult(ExportRows); }
    }
    private sealed class FakeUnit : IApplicationUnitOfWork { public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => await operation(cancellationToken); }
    private sealed class FakeAudit : IAuditService { public readonly List<AuditEntry> Entries = []; public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default) { Entries.Add(entry); return Task.CompletedTask; } }
    private sealed class FakeRelations : ISocioRelationChecker { public IReadOnlyList<string> Blockers = []; public Task<IReadOnlyList<string>> GetDeletionBlockersAsync(long id, CancellationToken ct = default) => Task.FromResult(Blockers); }
    private sealed class FakeStorage : IFileStorageService
    {
        public bool FailStore; public List<string> Deleted = [];
        public Task<string> StoreAsync(Stream content, string fileName, string contentType, CancellationToken ct = default) => FailStore ? Task.FromException<string>(new IOException()) : Task.FromResult("new-key");
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream?>(new MemoryStream());
        public Task DeleteAsync(string key, CancellationToken ct = default) { Deleted.Add(key); return Task.CompletedTask; }
    }
    private sealed class FakeLog : ITechnicalLogService { public Task RecordAsync(TechnicalLogEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class PassthroughProcessor : IPhotoProcessor { public Task<ProcessedPhoto> ProcessAsync(Stream input, string type, CancellationToken ct = default) => Task.FromResult(new ProcessedPhoto(new MemoryStream([1]), "photo.webp", "image/webp")); }
    private sealed class FakeExcel : IExcelExportService { public Task<byte[]> CreateAsync(IReadOnlyList<SocioExportRow> rows, CancellationToken ct = default) => Task.FromResult(new byte[] { 1, 2 }); }
}
