using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySciClub.Application.Features.Members;
using MySciClub.Domain.Soci;

namespace MySciClub.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/soci")]
public sealed class SociController(
    CreateSocioUseCase create,
    GetSocioUseCase get,
    UpdateSocioUseCase update,
    SearchSociUseCase search,
    DeleteSocioUseCase delete,
    ManageSocioPhotoUseCases photos,
    ExportSociUseCase export,
    SocioCatalogUseCases catalogs) : ControllerBase
{
    [HttpGet("cataloghi/paesi")]
    [ProducesResponseType<IReadOnlyList<CountryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CountryResponse>>> GetCountries(CancellationToken ct)
    {
        var countries = await catalogs.GetCountriesAsync(ct);
        return Ok(countries.Select(x => new CountryResponse(x.CodiceIsoAlpha2, x.Nome)).ToArray());
    }

    [HttpGet("cataloghi/comuni")]
    [ProducesResponseType<IReadOnlyList<ItalianTownResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ItalianTownResponse>>> GetItalianTowns([FromQuery] string? countryIsoAlpha2, CancellationToken ct)
    {
        var towns = await catalogs.GetItalianTownsAsync(countryIsoAlpha2, ct);
        return Ok(towns.Select(x => new ItalianTownResponse(x.CodiceCatastale, x.Nome, x.Provincia)).ToArray());
    }

    [HttpGet]
    [ProducesResponseType<SocioPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SocioPage>> Search([FromQuery] SocioSearchRequest request, CancellationToken ct)
    {
        var page = await search.ExecuteAsync(await catalogs.ResolveSearchAsync(request.ToApplicationQuery(), ct), ct);
        return Ok(page);
    }

    [HttpGet("{numeroSocio:long}")]
    [ProducesResponseType<SocioDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocioDetailResponse>> Get(long numeroSocio, CancellationToken ct)
    {
        if (numeroSocio <= 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Parametro non valido", Detail = "Il numero socio deve essere maggiore di zero." });
        return Ok(await catalogs.ToPublicDetailAsync(await get.ExecuteAsync(numeroSocio, ct), ct));
    }

    [HttpPost]
    [ProducesResponseType<SocioCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SocioCreatedResponse>> Create([FromBody] SocioWriteRequest request, CancellationToken ct)
    {
        var created = await create.ExecuteAsync(await catalogs.ResolveWriteInputAsync(request.ToPublicInput(), ct), ct);
        var response = new SocioCreatedResponse(created.NumeroSocio);
        return CreatedAtAction(nameof(Get), new { numeroSocio = created.NumeroSocio }, response);
    }

    [HttpPut("{numeroSocio:long}")]
    [ProducesResponseType<SocioDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocioDetailResponse>> Update(long numeroSocio, [FromBody] SocioWriteRequest request, CancellationToken ct)
    {
        if (numeroSocio <= 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Parametro non valido", Detail = "Il numero socio deve essere maggiore di zero." });
        var detail = await update.ExecuteAsync(numeroSocio, await catalogs.ResolveWriteInputAsync(request.ToPublicInput(), ct), ct);
        return Ok(await catalogs.ToPublicDetailAsync(detail, ct));
    }

    [HttpDelete("{numeroSocio:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long numeroSocio, CancellationToken ct)
    {
        if (numeroSocio <= 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Parametro non valido", Detail = "Il numero socio deve essere maggiore di zero." });
        await delete.ExecuteAsync(numeroSocio, ct);
        return NoContent();
    }

    [HttpPut("{numeroSocio:long}/foto")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UploadPhoto(long numeroSocio, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (numeroSocio <= 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Parametro non valido", Detail = "Il numero socio deve essere maggiore di zero." });
        if (file is null || file.Length == 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Fotografia non valida", Detail = "Selezionare una fotografia." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new ProblemDetails { Status = 400, Title = "Fotografia non valida", Detail = "La fotografia supera il limite di 5 MB." });
        await using var stream = file.OpenReadStream();
        await photos.UploadAsync(numeroSocio, new PhotoUpload(Path.GetFileName(file.FileName), file.ContentType, stream), ct);
        return NoContent();
    }

    [HttpGet("{numeroSocio:long}/foto")]
    [Produces("image/webp")]
    public async Task<IActionResult> GetPhoto(long numeroSocio, CancellationToken ct)
    {
        if (numeroSocio <= 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Parametro non valido", Detail = "Il numero socio deve essere maggiore di zero." });
        var detail = await get.ExecuteAsync(numeroSocio, ct);
        if (!detail.FotoDisponibile) return NotFound();
        var photo = await photos.DownloadAsync(numeroSocio, ct);
        return File(photo.Content, photo.ContentType);
    }

    [HttpDelete("{numeroSocio:long}/foto")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemovePhoto(long numeroSocio, CancellationToken ct)
    {
        if (numeroSocio <= 0) return BadRequest(new ProblemDetails { Status = 400, Title = "Parametro non valido", Detail = "Il numero socio deve essere maggiore di zero." });
        await photos.RemoveAsync(numeroSocio, ct);
        return NoContent();
    }

    [HttpGet("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export([FromQuery] SocioSearchRequest request, CancellationToken ct)
    {
        var file = await export.ExecuteAsync(await catalogs.ResolveSearchAsync(request.ToApplicationQuery(), ct), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

public sealed record CountryResponse(string CodiceIsoAlpha2, string Nome);
public sealed record ItalianTownResponse(string CodiceCatastale, string Nome, string Provincia);
public sealed record CitizenshipResponse(string CodiceIsoPaese, bool Principale);
public sealed record SocioCreatedResponse(long NumeroSocio);
public sealed record SocioDetailResponse(
    long NumeroSocio, string Nome, string Cognome, DateOnly DataNascita,
    string CodiceIsoPaeseNascita, string? CodiceCatastaleComuneItalianoNascita, string? ComuneEsteroNascita,
    Sesso Sesso, string CodiceFiscale, ProvenienzaCodiceFiscale ProvenienzaCodiceFiscale,
    bool CodiceFiscalePotenzialmenteObsoleto, IReadOnlyList<CitizenshipResponse> Cittadinanze,
    string Indirizzo, string? Cap, string CodiceIsoPaeseResidenza,
    string? CodiceCatastaleComuneItalianoResidenza, string? ProvinciaOSuddivisione,
    string Email, string Cellulare, bool FotoDisponibile);

public sealed class SocioSearchRequest
{
    [StringLength(254)] public string? Text { get; init; }
    public TesseramentoFilter Tesseramento { get; init; }
    [StringLength(4, MinimumLength = 4)] public string? CodiceCatastaleComune { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public SocioSortField SortBy { get; init; } = SocioSortField.Cognome;
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    public PublicSocioSearch ToApplicationQuery() => new(Text, Tesseramento, CodiceCatastaleComune,
        Page, PageSize, SortBy, SortDirection);
}

public sealed class SocioWriteRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string Nome { get; init; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 1)] public string Cognome { get; init; } = string.Empty;
    public DateOnly DataNascita { get; init; }
    [Required, StringLength(2, MinimumLength = 2)] public string CodiceIsoPaeseNascita { get; init; } = string.Empty;
    [StringLength(4, MinimumLength = 4)] public string? CodiceCatastaleComuneItalianoNascita { get; init; }
    [StringLength(100)] public string? ComuneEsteroNascita { get; init; }
    public Sesso Sesso { get; init; }
    [Required, MinLength(1)] public IReadOnlyList<CittadinanzaRequest> Cittadinanze { get; init; } = [];
    [Required, StringLength(200, MinimumLength = 1)] public string IndirizzoResidenza { get; init; } = string.Empty;
    [Required, StringLength(2, MinimumLength = 2)] public string CodiceIsoPaeseResidenza { get; init; } = string.Empty;
    [StringLength(20)] public string? Cap { get; init; }
    [StringLength(4, MinimumLength = 4)] public string? CodiceCatastaleComuneItalianoResidenza { get; init; }
    [StringLength(100)] public string? SuddivisioneResidenza { get; init; }
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string Cellulare { get; init; } = string.Empty;
    [StringLength(16, MinimumLength = 16)] public string? CodiceFiscaleManuale { get; init; }
    public SceltaCodiceFiscale SceltaCodiceFiscale { get; init; } = SceltaCodiceFiscale.Conserva;

    public PublicSocioWriteInput ToPublicInput() => new(Nome, Cognome, DataNascita, CodiceIsoPaeseNascita,
        CodiceCatastaleComuneItalianoNascita, ComuneEsteroNascita, Sesso,
        Cittadinanze.Select(x => new PublicCitizenship(x.CodiceIsoPaese, x.Principale)).ToArray(),
        IndirizzoResidenza, CodiceIsoPaeseResidenza, Cap, CodiceCatastaleComuneItalianoResidenza,
        SuddivisioneResidenza, Email, Cellulare, CodiceFiscaleManuale, SceltaCodiceFiscale);
}

public sealed record CittadinanzaRequest([Required, StringLength(2, MinimumLength = 2)] string CodiceIsoPaese, bool Principale);
