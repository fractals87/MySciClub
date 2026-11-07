namespace MySciClub.Infrastructure.Persistence.Entities;

internal sealed class SocioRecord
{
    public long Id { get; set; }
    public long NumeroSocio { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public DateOnly DataNascita { get; set; }
    public long PaeseNascitaId { get; set; }
    public long? ComuneItalianoNascitaId { get; set; }
    public string? ComuneEsteroNascita { get; set; }
    public byte Sesso { get; set; }
    public string CodiceFiscale { get; set; } = string.Empty;
    public byte ProvenienzaCodiceFiscale { get; set; }
    public bool CodiceFiscalePotenzialmenteObsoleto { get; set; }
    public string IndirizzoResidenza { get; set; } = string.Empty;
    public string? CapResidenza { get; set; }
    public long PaeseResidenzaId { get; set; }
    public long? ComuneItalianoResidenzaId { get; set; }
    public string? SuddivisioneEsteraResidenza { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Cellulare { get; set; } = string.Empty;
    public string? PhotoStorageKey { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public CountryRecord PaeseNascita { get; set; } = null!;
    public CountryRecord PaeseResidenza { get; set; } = null!;
    public ItalianTownRecord? ComuneItalianoNascita { get; set; }
    public ItalianTownRecord? ComuneItalianoResidenza { get; set; }
    public ICollection<SocioCitizenshipRecord> Cittadinanze { get; set; } = new List<SocioCitizenshipRecord>();
}
