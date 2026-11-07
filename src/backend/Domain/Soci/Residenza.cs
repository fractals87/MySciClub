namespace MySciClub.Domain.Soci;

public sealed class Residenza
{
    public string Indirizzo { get; }
    public string? Cap { get; }
    public string CodiceIsoPaese { get; }
    public long? ComuneItalianoId { get; }
    public string? ProvinciaOSuddivisione { get; }

    private Residenza(string indirizzo, string? cap, string codiceIsoPaese, long? comuneItalianoId, string? provinciaOSuddivisione)
    {
        Indirizzo = indirizzo;
        Cap = cap;
        CodiceIsoPaese = codiceIsoPaese;
        ComuneItalianoId = comuneItalianoId;
        ProvinciaOSuddivisione = provinciaOSuddivisione;
    }

    public static Residenza Italiana(string indirizzo, string cap, ComuneItaliano comune)
    {
        ArgumentNullException.ThrowIfNull(comune);
        return new(
            Paese.NormalizeRequired(indirizzo, 200, nameof(indirizzo)),
            Paese.NormalizeRequired(cap, 20, nameof(cap)), "IT", comune.Id, comune.Provincia);
    }

    public static Residenza Estera(string indirizzo, string codiceIsoPaese, string? cap, string? suddivisioneTerritoriale)
    {
        var country = NormalizeCountry(codiceIsoPaese);
        if (country == "IT") throw new ArgumentException("Per l'Italia usare la residenza italiana.", nameof(codiceIsoPaese));
        return new(
            Paese.NormalizeRequired(indirizzo, 200, nameof(indirizzo)),
            NormalizeOptional(cap, 20, nameof(cap)), country, null,
            NormalizeOptional(suddivisioneTerritoriale, 100, nameof(suddivisioneTerritoriale)));
    }

    private static string? NormalizeOptional(string? value, int maxLength, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Paese.NormalizeRequired(value, maxLength, name);

    private static string NormalizeCountry(string value)
    {
        var country = Paese.NormalizeRequired(value, 2, nameof(value)).ToUpperInvariant();
        if (country.Length != 2 || !country.All(char.IsAsciiLetter))
            throw new ArgumentException("Il codice Paese deve essere ISO alpha-2.", nameof(value));
        return country;
    }
}
