using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySciClub.Application.Features.Members;
using MySciClub.Domain.Soci;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence;

public interface ICurrentTesseramentoSource
{
    /// <summary>Returns member functional numbers that bought Tessera Sci Club for the current administrative period.</summary>
    Task<IReadOnlySet<long>> GetCurrentTesseratiAsync(CancellationToken cancellationToken = default);
}

public sealed class TesseramentoUnavailableException() : Exception("Il tesseramento corrente non è disponibile.") { }

internal sealed class TesseramentoSourceNotConfigured : ICurrentTesseramentoSource
{
    public Task<IReadOnlySet<long>> GetCurrentTesseratiAsync(CancellationToken cancellationToken = default) =>
        throw new TesseramentoUnavailableException();
}

internal sealed class SqlSocioCatalogRepository(MySciClubDbContext db) : ISocioCatalogRepository
{
    public async Task<IReadOnlyList<Paese>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        await db.Paesi.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new Paese(x.Id, x.IsoAlpha2, x.Name)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ComuneItaliano>> GetItalianTownsAsync(string? countryIsoAlpha2 = null, CancellationToken cancellationToken = default)
    {
        var query = db.ComuniItaliani.AsNoTracking().Include(x => x.Country).AsQueryable();
        if (countryIsoAlpha2 is not null) query = query.Where(x => x.Country.IsoAlpha2 == countryIsoAlpha2);
        return await query.OrderBy(x => x.Name).Select(x => new ComuneItaliano(x.Id,
            new Paese(x.Country.Id, x.Country.IsoAlpha2, x.Country.Name), x.Name, x.CadastralCode, x.Province))
            .ToListAsync(cancellationToken);
    }

    public async Task<Paese?> GetCountryAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await db.Paesi.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : new Paese(row.Id, row.IsoAlpha2, row.Name);
    }

    public async Task<ComuneItaliano?> GetItalianTownAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await db.ComuniItaliani.AsNoTracking().Include(x => x.Country)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : new ComuneItaliano(row.Id,
            new Paese(row.Country.Id, row.Country.IsoAlpha2, row.Country.Name), row.Name, row.CadastralCode, row.Province);
    }

    public async Task<Paese?> GetCountryByIsoAlpha2Async(string isoAlpha2, CancellationToken cancellationToken = default)
    {
        var row = await db.Paesi.AsNoTracking().SingleOrDefaultAsync(x => x.IsoAlpha2 == isoAlpha2, cancellationToken);
        return row is null ? null : new Paese(row.Id, row.IsoAlpha2, row.Name);
    }

    public async Task<ComuneItaliano?> GetItalianTownByCadastralCodeAsync(string cadastralCode, CancellationToken cancellationToken = default)
    {
        var row = await db.ComuniItaliani.AsNoTracking().Include(x => x.Country)
            .SingleOrDefaultAsync(x => x.CadastralCode == cadastralCode, cancellationToken);
        return row is null ? null : new ComuneItaliano(row.Id,
            new Paese(row.Country.Id, row.Country.IsoAlpha2, row.Country.Name), row.Name, row.CadastralCode, row.Province);
    }
}

internal sealed class SqlSocioRepository(MySciClubDbContext db, ICurrentTesseramentoSource tesseramento) : ISocioRepository
{
    public async Task<Socio?> GetByNumeroSocioAsync(long numeroSocio, CancellationToken cancellationToken = default)
    {
        var row = await db.Soci.Include(x => x.Cittadinanze).SingleOrDefaultAsync(x => x.NumeroSocio == numeroSocio, cancellationToken);
        return row is null ? null : await ToDomainAsync(row, cancellationToken);
    }

    public async Task AddAsync(Socio socio, CancellationToken cancellationToken = default)
    {
        var row = await ToRecordAsync(socio, cancellationToken);
        db.Soci.Add(row);
        try
        {
            // Save inside the outer transaction so SQL Server returns the generated identity before the audit is appended.
            await db.SaveChangesAsync(cancellationToken);
            SetPrivate(socio, nameof(Socio.Id), row.Id);
        }
        catch (DbUpdateException exception) { throw Translate(exception); }
    }

    public async Task UpdateAsync(Socio socio, CancellationToken cancellationToken = default)
    {
        var row = await db.Soci.Include(x => x.Cittadinanze).SingleOrDefaultAsync(x => x.Id == socio.Id, cancellationToken)
            ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
        await ApplyAsync(row, socio, cancellationToken);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new SocioApplicationException(SocioErrorCode.ConflittoAggiornamento, "Il socio è stato modificato da un'altra operazione.", exception); }
        catch (DbUpdateException exception) { throw Translate(exception); }
    }

    public async Task DeleteAsync(Socio socio, CancellationToken cancellationToken = default)
    {
        var row = await db.Soci.SingleOrDefaultAsync(x => x.Id == socio.Id, cancellationToken)
            ?? throw new SocioApplicationException(SocioErrorCode.SocioNonTrovato, "Socio non trovato.");
        db.Soci.Remove(row);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new SocioApplicationException(SocioErrorCode.ConflittoAggiornamento, "Il socio è stato modificato da un'altra operazione.", exception); }
        catch (DbUpdateException exception) { throw Translate(exception); }
    }

    public async Task<SocioPage> SearchAsync(SocioSearchQuery query, CancellationToken cancellationToken = default)
    {
        var source = await FilteredAsync(query, cancellationToken);
        var total = await source.LongCountAsync(cancellationToken);
        var ordered = Order(source, query);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue) return new SocioPage(Array.Empty<SocioListItem>(), query.Page, query.PageSize, total);
        var rows = await ordered.Skip((int)offset).Take(query.PageSize)
            .Select(x => new
            {
                x.NumeroSocio, x.Nome, x.Cognome, x.CodiceFiscale, x.Email,
                Comune = x.ComuneItalianoResidenza == null ? null : x.ComuneItalianoResidenza.Name
            }).ToListAsync(cancellationToken);
        var statuses = await GetStatusesAsync(rows.Select(x => x.NumeroSocio), cancellationToken);
        return new SocioPage(rows.Select(x => new SocioListItem(x.NumeroSocio, x.Nome, x.Cognome,
            x.CodiceFiscale, x.Email, x.Comune, statuses.Contains(x.NumeroSocio))).ToArray(), query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<SocioExportRow>> SearchAllAsync(SocioSearchQuery query, CancellationToken cancellationToken = default)
    {
        var source = await FilteredAsync(query, cancellationToken);
        var rows = await Order(source, query).Select(x => new
        {
            x.NumeroSocio, x.Nome, x.Cognome, x.DataNascita, x.Sesso, x.CodiceFiscale,
            x.PaeseNascita.IsoAlpha2, x.ComuneEsteroNascita,
            BirthTown = x.ComuneItalianoNascita == null ? null : x.ComuneItalianoNascita.Name,
            x.IndirizzoResidenza, x.CapResidenza,
            ResidenceTown = x.ComuneItalianoResidenza == null ? null : x.ComuneItalianoResidenza.Name,
            Province = x.ComuneItalianoResidenza == null ? x.SuddivisioneEsteraResidenza : x.ComuneItalianoResidenza.Province,
            x.PaeseResidenza.Name, x.Email, x.Cellulare,
            CitizenshipNames = x.Cittadinanze.Select(c => c.Country.Name)
        }).ToListAsync(cancellationToken);
        var statuses = await GetStatusesAsync(rows.Select(x => x.NumeroSocio), cancellationToken);
        return rows.Select(x => new SocioExportRow(x.NumeroSocio, x.Nome, x.Cognome, x.DataNascita,
            x.BirthTown ?? x.ComuneEsteroNascita ?? x.IsoAlpha2, (Sesso)x.Sesso, x.CodiceFiscale,
            string.Join(", ", x.CitizenshipNames), x.IndirizzoResidenza, x.CapResidenza, x.ResidenceTown,
            x.Province, x.Name, x.Email, x.Cellulare, statuses.Contains(x.NumeroSocio))).ToArray();
    }

    private async Task<IQueryable<SocioRecord>> FilteredAsync(SocioSearchQuery query, CancellationToken ct)
    {
        IQueryable<SocioRecord> source = db.Soci.AsNoTracking().Include(x => x.Cittadinanze);
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var term = query.Text.Trim().ToLowerInvariant();
            source = source.Where(x => x.Nome.ToLower().Contains(term) || x.Cognome.ToLower().Contains(term) ||
                x.CodiceFiscale.ToLower().Contains(term) || x.Email.ToLower().Contains(term) || x.NumeroSocio.ToString().Contains(term));
        }
        if (query.ComuneId is not null) source = source.Where(x => x.ComuneItalianoResidenzaId == query.ComuneId);
        if (query.Tesseramento != TesseramentoFilter.Qualsiasi)
        {
            var current = await tesseramento.GetCurrentTesseratiAsync(ct);
            var currentNumbers = current.ToArray();
            source = query.Tesseramento == TesseramentoFilter.Tesserato
                ? source.Where(x => currentNumbers.Contains(x.NumeroSocio))
                : source.Where(x => !currentNumbers.Contains(x.NumeroSocio));
        }
        return source;
    }

    private async Task<IReadOnlySet<long>> GetStatusesAsync(IEnumerable<long> memberNumbers, CancellationToken ct)
    {
        var all = await tesseramento.GetCurrentTesseratiAsync(ct);
        var wanted = memberNumbers.ToHashSet();
        return all.Where(wanted.Contains).ToHashSet();
    }

    private static IOrderedQueryable<SocioRecord> Order(IQueryable<SocioRecord> source, SocioSearchQuery query)
    {
        var descending = query.SortDirection == SortDirection.Descending;
        return query.SortBy switch
        {
            SocioSortField.Nome => descending ? source.OrderByDescending(x => x.Nome).ThenByDescending(x => x.Cognome) : source.OrderBy(x => x.Nome).ThenBy(x => x.Cognome),
            SocioSortField.NumeroSocio => descending ? source.OrderByDescending(x => x.NumeroSocio) : source.OrderBy(x => x.NumeroSocio),
            SocioSortField.CodiceFiscale => descending ? source.OrderByDescending(x => x.CodiceFiscale) : source.OrderBy(x => x.CodiceFiscale),
            SocioSortField.Email => descending ? source.OrderByDescending(x => x.Email) : source.OrderBy(x => x.Email),
            _ => descending ? source.OrderByDescending(x => x.Cognome).ThenByDescending(x => x.Nome) : source.OrderBy(x => x.Cognome).ThenBy(x => x.Nome)
        };
    }

    private async Task<Socio> ToDomainAsync(SocioRecord row, CancellationToken ct)
    {
        var birthCountry = await db.Paesi.AsNoTracking().SingleAsync(x => x.Id == row.PaeseNascitaId, ct);
        var residenceCountry = await db.Paesi.AsNoTracking().SingleAsync(x => x.Id == row.PaeseResidenzaId, ct);
        LuogoNascita birth;
        string? cadastral = null;
        if (row.ComuneItalianoNascitaId is long birthTownId)
        {
            var town = await db.ComuniItaliani.AsNoTracking().Include(x => x.Country).SingleAsync(x => x.Id == birthTownId, ct);
            birth = LuogoNascita.InComuneItaliano(birthCountry.IsoAlpha2, town.Id);
            cadastral = town.CadastralCode;
        }
        else birth = LuogoNascita.Estero(birthCountry.IsoAlpha2, row.ComuneEsteroNascita);
        Residenza residence;
        if (row.ComuneItalianoResidenzaId is long residenceTownId)
        {
            var town = await db.ComuniItaliani.AsNoTracking().Include(x => x.Country).SingleAsync(x => x.Id == residenceTownId, ct);
            residence = Residenza.Italiana(row.IndirizzoResidenza, row.CapResidenza ?? string.Empty,
                new ComuneItaliano(town.Id, new Paese(town.Country.Id, town.Country.IsoAlpha2, town.Country.Name), town.Name, town.CadastralCode, town.Province));
        }
        else residence = Residenza.Estera(row.IndirizzoResidenza, residenceCountry.IsoAlpha2, row.CapResidenza, row.SuddivisioneEsteraResidenza);
        var citizenships = row.Cittadinanze.Select(x => new Cittadinanza(x.CountryId, x.Principale)).ToArray();
        var data = new DatiAnagraficiSocio(row.Nome, row.Cognome, row.DataNascita, birth, residence, citizenships,
            (Sesso)row.Sesso, row.Email, row.Cellulare);
        var socio = Socio.Crea(row.NumeroSocio, data, row.CodiceFiscale, cadastral);
        SetPrivate(socio, nameof(Socio.Id), row.Id);
        SetPrivate(socio, nameof(Socio.ProvenienzaCodiceFiscale), (ProvenienzaCodiceFiscale)row.ProvenienzaCodiceFiscale);
        SetPrivate(socio, nameof(Socio.CodiceFiscalePotenzialmenteObsoleto), row.CodiceFiscalePotenzialmenteObsoleto);
        socio.ImpostaChiaveFotografia(row.PhotoStorageKey);
        return socio;
    }

    private async Task<SocioRecord> ToRecordAsync(Socio socio, CancellationToken ct)
    {
        var record = new SocioRecord();
        await ApplyAsync(record, socio, ct);
        return record;
    }

    private async Task ApplyAsync(SocioRecord row, Socio socio, CancellationToken cancellationToken)
    {
        var d = socio.Dati;
        row.NumeroSocio = socio.NumeroSocio;
        row.Nome = d.Nome; row.Cognome = d.Cognome; row.DataNascita = d.DataNascita;
        var codes = new[] { d.LuogoNascita.CodiceIsoPaese, d.Residenza.CodiceIsoPaese };
        var countryIds = await db.Paesi.AsNoTracking().Where(x => codes.Contains(x.IsoAlpha2))
            .ToDictionaryAsync(x => x.IsoAlpha2, x => x.Id, cancellationToken);
        row.PaeseNascitaId = countryIds[d.LuogoNascita.CodiceIsoPaese];
        row.ComuneItalianoNascitaId = d.LuogoNascita.ComuneItalianoId;
        row.ComuneEsteroNascita = d.LuogoNascita.ComuneEstero;
        row.Sesso = (byte)d.Sesso; row.CodiceFiscale = socio.CodiceFiscale.Valore;
        row.ProvenienzaCodiceFiscale = (byte)socio.ProvenienzaCodiceFiscale;
        row.CodiceFiscalePotenzialmenteObsoleto = socio.CodiceFiscalePotenzialmenteObsoleto;
        row.IndirizzoResidenza = d.Residenza.Indirizzo; row.CapResidenza = d.Residenza.Cap;
        row.PaeseResidenzaId = countryIds[d.Residenza.CodiceIsoPaese];
        row.ComuneItalianoResidenzaId = d.Residenza.ComuneItalianoId;
        row.SuddivisioneEsteraResidenza = d.Residenza.ComuneItalianoId is null ? d.Residenza.ProvinciaOSuddivisione : null;
        row.Email = d.Email; row.Cellulare = d.Cellulare; row.PhotoStorageKey = socio.ChiaveFotografia;
        var incoming = d.Cittadinanze.ToDictionary(x => x.PaeseId, x => x.Principale);
        foreach (var existing in row.Cittadinanze.ToArray())
        {
            if (!incoming.Remove(existing.CountryId, out var principale)) row.Cittadinanze.Remove(existing);
            else existing.Principale = principale;
        }
        foreach (var citizenship in incoming)
            row.Cittadinanze.Add(new SocioCitizenshipRecord
            {
                CountryId = citizenship.Key, Principale = citizenship.Value, Socio = row
            });
    }

    private static void SetPrivate<T>(Socio socio, string propertyName, T value) =>
        typeof(Socio).GetProperty(propertyName)!.SetValue(socio, value);

    private static Exception Translate(DbUpdateException exception)
    {
        var sql = exception.GetBaseException() as SqlException;
        if (sql is { Number: 2601 or 2627 })
        {
            if (sql.Message.Contains("CodiceFiscale", StringComparison.OrdinalIgnoreCase))
                return new SocioApplicationException(SocioErrorCode.CodiceFiscaleDuplicato, "Il codice fiscale è già associato a un socio.", exception);
            if (sql.Message.Contains("NumeroSocio", StringComparison.OrdinalIgnoreCase))
                return new SocioApplicationException(SocioErrorCode.NumeroSocioNonAssegnabile, "Il numero socio non è assegnabile.", exception);
        }
        return exception;
    }
}
