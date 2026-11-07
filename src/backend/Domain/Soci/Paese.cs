namespace MySciClub.Domain.Soci;

public sealed class Paese
{
    public long Id { get; }
    public string CodiceIsoAlpha2 { get; }
    public string Nome { get; }

    public Paese(long id, string codiceIsoAlpha2, string nome)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        Id = id;
        CodiceIsoAlpha2 = NormalizeRequired(codiceIsoAlpha2, 2, nameof(codiceIsoAlpha2)).ToUpperInvariant();
        if (CodiceIsoAlpha2.Length != 2 || !CodiceIsoAlpha2.All(char.IsAsciiLetter))
            throw new ArgumentException("Il codice Paese deve essere ISO alpha-2.", nameof(codiceIsoAlpha2));

        Nome = NormalizeRequired(nome, 100, nameof(nome));
    }

    internal static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length == 0)
            throw new ArgumentException("Il valore è obbligatorio.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Il valore supera {maxLength} caratteri.", parameterName);
        return normalized;
    }
}
