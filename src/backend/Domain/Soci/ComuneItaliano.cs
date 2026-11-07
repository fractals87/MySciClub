namespace MySciClub.Domain.Soci;

public sealed class ComuneItaliano
{
    public long Id { get; }
    public long PaeseId { get; }
    public string Nome { get; }
    public string CodiceCatastale { get; }
    public string Provincia { get; }

    public ComuneItaliano(long id, Paese paese, string nome, string codiceCatastale, string provincia)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentNullException.ThrowIfNull(paese);
        if (paese.CodiceIsoAlpha2 != "IT") throw new ArgumentException("Il comune italiano deve appartenere all'Italia.", nameof(paese));
        Id = id;
        PaeseId = paese.Id;
        Nome = Paese.NormalizeRequired(nome, 100, nameof(nome));
        CodiceCatastale = Paese.NormalizeRequired(codiceCatastale, 4, nameof(codiceCatastale)).ToUpperInvariant();
        if (CodiceCatastale.Length != 4 || !char.IsAsciiLetter(CodiceCatastale[0]) || !CodiceCatastale.Skip(1).All(char.IsAsciiDigit))
            throw new ArgumentException("Il codice catastale deve contenere una lettera e tre caratteri alfanumerici.", nameof(codiceCatastale));
        Provincia = Paese.NormalizeRequired(provincia, 100, nameof(provincia));
    }
}
