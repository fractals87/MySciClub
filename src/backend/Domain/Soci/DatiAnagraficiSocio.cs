namespace MySciClub.Domain.Soci;

public sealed class DatiAnagraficiSocio
{
    public string Nome { get; }
    public string Cognome { get; }
    public DateOnly DataNascita { get; }
    public LuogoNascita LuogoNascita { get; }
    public Residenza Residenza { get; }
    public IReadOnlyList<Cittadinanza> Cittadinanze { get; }
    public Sesso Sesso { get; }
    public string Email { get; }
    public string Cellulare { get; }

    public DatiAnagraficiSocio(
        string nome,
        string cognome,
        DateOnly dataNascita,
        LuogoNascita luogoNascita,
        Residenza residenza,
        IEnumerable<Cittadinanza> cittadinanze,
        Sesso sesso,
        string email,
        string cellulare)
    {
        Nome = Paese.NormalizeRequired(nome, 100, nameof(nome));
        Cognome = Paese.NormalizeRequired(cognome, 100, nameof(cognome));
        DataNascita = dataNascita;
        LuogoNascita = luogoNascita ?? throw new ArgumentNullException(nameof(luogoNascita));
        Residenza = residenza ?? throw new ArgumentNullException(nameof(residenza));
        if (!Enum.IsDefined(sesso)) throw new ArgumentOutOfRangeException(nameof(sesso));
        Sesso = sesso;

        ArgumentNullException.ThrowIfNull(cittadinanze);
        var citizenships = cittadinanze.ToArray();
        if (citizenships.Length == 0) throw new ArgumentException("È richiesta almeno una cittadinanza.", nameof(cittadinanze));
        if (citizenships.Count(c => c.Principale) != 1)
            throw new ArgumentException("Deve essere indicata esattamente una cittadinanza principale.", nameof(cittadinanze));
        if (citizenships.Select(c => c.PaeseId).Distinct().Count() != citizenships.Length)
            throw new ArgumentException("La stessa cittadinanza non può essere ripetuta.", nameof(cittadinanze));
        Cittadinanze = Array.AsReadOnly(citizenships);

        Email = NormalizeEmail(email);
        Cellulare = Paese.NormalizeRequired(cellulare, 50, nameof(cellulare));
    }

    private static string NormalizeEmail(string value)
    {
        var normalized = Paese.NormalizeRequired(value, 254, nameof(value)).ToLowerInvariant();
        try
        {
            var address = new System.Net.Mail.MailAddress(normalized);
            if (!string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("L'indirizzo email non è valido.", nameof(value));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("L'indirizzo email non è valido.", nameof(value), exception);
        }
        return normalized;
    }
}
