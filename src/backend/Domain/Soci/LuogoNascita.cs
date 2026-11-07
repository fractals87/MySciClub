namespace MySciClub.Domain.Soci;

public sealed class LuogoNascita
{
    public string CodiceIsoPaese { get; }
    public long? ComuneItalianoId { get; }
    public string? ComuneEstero { get; }
    public bool InItalia => CodiceIsoPaese == "IT";

    private LuogoNascita(string codiceIsoPaese, long? comuneItalianoId, string? comuneEstero)
    {
        CodiceIsoPaese = codiceIsoPaese;
        ComuneItalianoId = comuneItalianoId;
        ComuneEstero = comuneEstero;
    }

    public static LuogoNascita InComuneItaliano(string codiceIsoPaese, long comuneItalianoId)
    {
        var country = NormalizeCountry(codiceIsoPaese);
        if (country != "IT") throw new ArgumentException("Il comune italiano richiede il Paese IT.", nameof(codiceIsoPaese));
        if (comuneItalianoId <= 0) throw new ArgumentOutOfRangeException(nameof(comuneItalianoId));
        return new(country, comuneItalianoId, null);
    }

    public static LuogoNascita Estero(string codiceIsoPaese, string? comune)
    {
        var country = NormalizeCountry(codiceIsoPaese);
        if (country == "IT") throw new ArgumentException("Per l'Italia va selezionato un comune italiano.", nameof(codiceIsoPaese));
        var normalizedCity = string.IsNullOrWhiteSpace(comune) ? null : Paese.NormalizeRequired(comune, 100, nameof(comune));
        return new(country, null, normalizedCity);
    }

    private static string NormalizeCountry(string value)
    {
        var country = Paese.NormalizeRequired(value, 2, nameof(value)).ToUpperInvariant();
        if (country.Length != 2 || !country.All(char.IsAsciiLetter))
            throw new ArgumentException("Il codice Paese deve essere ISO alpha-2.", nameof(value));
        return country;
    }
}
